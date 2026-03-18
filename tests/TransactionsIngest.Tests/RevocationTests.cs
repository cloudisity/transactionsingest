using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Models;
using TransactionsIngest.Services;
using Xunit;

namespace TransactionsIngest.Tests;

public class RevocationTests : IDisposable
{
    private readonly TransactionsIngest.Data.TransactionsDbContext _db;
    private readonly StubTransactionFetcher _fetcher;
    private readonly FakeClock _clock;
    private readonly IngestionService _service;

    public RevocationTests()
    {
        _db = TestDbHelper.CreateInMemoryContext();
        _fetcher = new StubTransactionFetcher();
        _clock = new FakeClock(); 
        _service = TestDbHelper.CreateService(_db, _fetcher, _clock);
    }

    public void Dispose() => _db.Dispose();

    private static TransactionDto MakeDto(int id, DateTime? timestamp = null) => new()
    {
        TransactionId = id,
        CardNumber = "4111111111111111",
        LocationCode = "STO-01",
        ProductName = "Widget",
        Amount = 9.99m,
        Timestamp = timestamp ?? new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task MissingTransaction_WithinWindow_IsRevoked()
    {
        _fetcher.Transactions.AddRange(new[] { MakeDto(5001), MakeDto(5002) });
        await _service.IngestTransactionsAsync();

        _fetcher.Transactions.Clear();
        _fetcher.Transactions.Add(MakeDto(5001));
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.IngestTransactionsAsync();

        var revoked = await _db.Transactions.SingleAsync(t => t.TransactionId == 5002);
        Assert.Equal(TransactionStatus.Revoked, revoked.Status);
    }

    [Fact]
    public async Task Revocation_CreatesAuditEntry()
    {
        _fetcher.Transactions.Add(MakeDto(5501));
        await _service.IngestTransactionsAsync();

        _fetcher.Transactions.Clear();
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.IngestTransactionsAsync();

        var audit = await _db.Auditlogs
            .SingleAsync(a => a.TransactionId == 5501 && a.ChangeType == ChangeType.Revoked);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task AlreadyRevoked_NotRevokedAgain()
    {
        _fetcher.Transactions.Add(MakeDto(5601));
        await _service.IngestTransactionsAsync();

        _fetcher.Transactions.Clear();
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.IngestTransactionsAsync();
        await _service.IngestTransactionsAsync();

        var revocations = await _db.Auditlogs
            .Where(a => a.TransactionId == 5601 && a.ChangeType == ChangeType.Revoked)
            .ToListAsync();

        Assert.Single(revocations);
    }

    [Fact]
    public async Task RevokedTransaction_Reappears_IsReactivated()
    {
        _fetcher.Transactions.Add(MakeDto(6001));
        await _service.IngestTransactionsAsync();

        _fetcher.Transactions.Clear();
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.IngestTransactionsAsync();

        var txn = await _db.Transactions.SingleAsync(t => t.TransactionId == 6001);
        Assert.Equal(TransactionStatus.Revoked, txn.Status);

        _fetcher.Transactions.Add(MakeDto(6001));
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.IngestTransactionsAsync();

        txn = await _db.Transactions.SingleAsync(t => t.TransactionId == 6001);
        Assert.Equal(TransactionStatus.Active, txn.Status);
    }

    [Fact]
    public async Task Reactivation_CreatesAuditEntry()
    {
        _fetcher.Transactions.Add(MakeDto(6101));
        await _service.IngestTransactionsAsync();

        _fetcher.Transactions.Clear();
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.IngestTransactionsAsync();

        _fetcher.Transactions.Add(MakeDto(6101));
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.IngestTransactionsAsync();

        var statusAudit = await _db.Auditlogs
            .Where(a => a.TransactionId == 6101
                        && a.ChangeType == ChangeType.Updated
                        && a.FieldName == "Status")
            .ToListAsync();

        Assert.Single(statusAudit);
        Assert.Equal("Revoked", statusAudit[0].OldValue);
        Assert.Equal("Active", statusAudit[0].NewValue);
    }

    [Fact]
    public async Task Transaction_OutsideWindow_IsNotRevoked()
    {
        var oldTimestamp = new DateTime(2026, 2, 26, 8, 0, 0, DateTimeKind.Utc);
        _fetcher.Transactions.Add(MakeDto(7001, oldTimestamp));
        await _service.IngestTransactionsAsync();

        _fetcher.Transactions.Clear();
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.IngestTransactionsAsync();

        var txn = await _db.Transactions.SingleAsync(t => t.TransactionId == 7001);
        Assert.NotEqual(TransactionStatus.Revoked, txn.Status);
    }
}