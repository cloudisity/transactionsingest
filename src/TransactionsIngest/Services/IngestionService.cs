using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionsIngest.Configuration;
using TransactionsIngest.Data;
using TransactionsIngest.Models;

namespace TransactionsIngest.Services;

public class IngestionService
{
    private readonly TransactionsDbContext _db;
    private readonly ITransactionFetcher _fetcher;
    private readonly IngestionSettings _settings;
    private readonly ILogger<IngestionService> _logger;
    private readonly IClock _clock;

    public IngestionService(
        TransactionsDbContext db,
        ITransactionFetcher fetcher,
        IngestionSettings settings,
        ILogger<IngestionService> logger,
        IClock clock)
    {
        _db = db;
        _fetcher = fetcher;
        _settings = settings;
        _logger = logger;
        _clock = clock;
    }
    
    public async Task IngestTransactionsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("=== Ingestion run starting at {UtcNow} ===", _clock.UtcNow);

        await using var dbTransaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var snapshot = await _fetcher.FetchTransactionsAsync(ct);
            _logger.LogInformation("Received {Count} transactions in snapshot", snapshot.Count);

            var now = _clock.UtcNow;
            var cutoff = now.AddHours(-_settings.LookBackHours);
            var snapshotIds = new HashSet<int>(snapshot.Select(s => s.TransactionId));

            foreach (var dto in snapshot)
            {
                await UpsertTransactionAsync(dto, now, ct);
            }

            await RevokeAbsentTransactionsAsync(snapshotIds, cutoff, now, ct);
            
            await _db.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);
            
            _logger.LogInformation("=== Ingestion run completed successfully ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ingestion run failed at {UtcNow}; rolling back transaction", _clock.UtcNow);
            await dbTransaction.RollbackAsync(ct);
            throw;
        }
    }

    private async Task UpsertTransactionAsync(TransactionDto dto, DateTime now, CancellationToken ct)
        {
            var existing = await _db.Transactions
                .FirstOrDefaultAsync(t => t.TransactionId == dto.TransactionId, ct);

            if (existing is null)
            {
                var entity = new Transaction
                {
                    TransactionId = dto.TransactionId,
                    CardNumberHash = CardNumberHelper.Hash(dto.CardNumber),
                    CardNumberLast4 = CardNumberHelper.Last4(dto.CardNumber),
                    LocationCode = dto.LocationCode,
                    ProductName = dto.ProductName,
                    Amount = dto.Amount,
                    Timestamp = DateTime.SpecifyKind(dto.Timestamp, DateTimeKind.Utc),
                    Status = TransactionStatus.Active,
                    CreatedAtUTC = now,
                    UpdatedAtUTC = now
                };

                _db.Transactions.Add(entity);

                _db.Auditlogs.Add(new Auditlog
                {
                    TransactionId = dto.TransactionId,
                    ChangeType = ChangeType.Created,
                    TimestampUTC = now
                });

                _logger.LogInformation("Inserted TransactionId {Id}", dto.TransactionId);
                return;
            }

            if (existing.Status == TransactionStatus.Finalized)
            {
                _logger.LogDebug("Skipping finalized TransactionId {Id}", dto.TransactionId);
                return;
            }

        var changes = DetectChanges(existing, dto);

        if (existing.Status == TransactionStatus.Revoked)
        {
            changes.Add(("Status", existing.Status.ToString(), TransactionStatus.Active.ToString()));
            existing.Status = TransactionStatus.Active;
        }

        if (changes.Count == 0)
        {
            _logger.LogDebug("No changes for TransactionId {Id}", dto.TransactionId);
            return;
        }

            existing.CardNumberHash = CardNumberHelper.Hash(dto.CardNumber);
            existing.CardNumberLast4 = CardNumberHelper.Last4(dto.CardNumber);
            existing.LocationCode = dto.LocationCode;
            existing.ProductName = dto.ProductName;
            existing.Amount = dto.Amount;
            existing.Timestamp = DateTime.SpecifyKind(dto.Timestamp, DateTimeKind.Utc);
            existing.UpdatedAtUTC = now;

            foreach (var (field, oldVal, newVal) in changes)
            {
                _db.Auditlogs.Add(new Auditlog
                {
                    TransactionId = dto.TransactionId,
                    ChangeType = ChangeType.Updated,
                    FieldName = field,
                    OldValue = oldVal,
                    NewValue = newVal,
                    TimestampUTC = now
                });
            }

            _logger.LogInformation("Updated TransactionId {Id}: {Fields}",
                dto.TransactionId,
                string.Join(", ", changes.Select(c => c.Field)));
        }
        private async Task RevokeAbsentTransactionsAsync(
        HashSet<int> snapshotIds, DateTime cutoff, DateTime now, CancellationToken ct)
    {
        var candidates = await _db.Transactions
            .Where(t => t.Status == TransactionStatus.Active
                        && t.Timestamp >= cutoff)
            .ToListAsync(ct);

        foreach (var txn in candidates)
        {
            if (snapshotIds.Contains(txn.TransactionId))
                continue;

            txn.Status = TransactionStatus.Revoked;
            txn.UpdatedAtUTC = now;

            _db.Auditlogs.Add(new Auditlog
            {
                TransactionId = txn.TransactionId,
                ChangeType = ChangeType.Revoked,
                TimestampUTC = now
            });

            _logger.LogInformation("Revoked TransactionId {Id}", txn.TransactionId);
        }
    }
    private static List<(string Field, string OldValue, string NewValue)> DetectChanges(
        Transaction existing, TransactionDto dto)
    {
        var changes = new List<(string, string, string)>();

        var newHash = CardNumberHelper.Hash(dto.CardNumber);
        if (existing.CardNumberHash != newHash)
            changes.Add(("CardNumber", $"****{existing.CardNumberLast4}", $"****{CardNumberHelper.Last4(dto.CardNumber)}"));

        if (existing.LocationCode != dto.LocationCode)
            changes.Add(("LocationCode", existing.LocationCode, dto.LocationCode));

        if (existing.ProductName != dto.ProductName)
            changes.Add(("ProductName", existing.ProductName, dto.ProductName));

        if (existing.Amount != dto.Amount)
            changes.Add(("Amount", existing.Amount.ToString("F2"), dto.Amount.ToString("F2")));

        var dtoTime = DateTime.SpecifyKind(dto.Timestamp, DateTimeKind.Utc);
        if (existing.Timestamp != dtoTime)
            changes.Add(("Timestamp", existing.Timestamp.ToString("O"), dtoTime.ToString("O")));

        return changes;
    }
}
