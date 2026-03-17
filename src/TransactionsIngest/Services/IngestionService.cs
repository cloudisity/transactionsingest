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

            foreach (var dto in snapshot)
            {
                await UpsertTransactionAsync(dto, now, ct);
            }
            
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

        // TODO: update detection
        _logger.LogDebug("TransactionId {Id} already exists, skipping for now", dto.TransactionId);
    }
}
