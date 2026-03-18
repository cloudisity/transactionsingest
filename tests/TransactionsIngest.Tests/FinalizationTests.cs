using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Models;
using TransactionsIngest.Services;
using Xunit;

namespace TransactionsIngest.Tests;

public class FinalizationTests : IDisposable
{
    private readonly TransactionsIngest.Data.TransactionsDbContext _db;
    private readonly StubTransactionFetcher _fetcher;
    private readonly FakeClock _clock;
    private readonly IngestionService _service;

    public FinalizationTests()
    {
        _db = TestDbHelper.CreateInMemoryContext();
        _fetcher = new StubTransactionFetcher();
        _clock = new FakeClock(); 
        _service = TestDbHelper.CreateService(_db, _fetcher, _clock);
    }

    public void Dispose() => _db.Dispose();

    private static TransactionDto MakeDto(int id, DateTime? timestamp = null, decimal amount = 9.99m) => new()
    {
        TransactionId = id,
        CardNumber = "4111111111111111",
        LocationCode = "STO-01",
        ProductName = "Widget",
        Amount = amount,
        Timestamp = timestamp ?? new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task OldTransaction_IsFinalized()
    {
        var oldTimestamp = new DateTime(2026, 2, 26, 5, 0, 0, DateTimeKind.Utc);
        _fetcher.Transactions.Add(MakeDto(8001, oldTimestamp));
        await _service.IngestTransactionsAsync();

        var txn = await _db.Transactions.SingleAsync(t => t.TransactionId == 8001);
        Assert.Equal(TransactionStatus.Finalized, txn.Status);
    }

    [Fact]
    public async Task Finalization_CreatesAuditEntry()
    {
        var oldTimestamp = new DateTime(2026, 2, 26, 5, 0, 0, DateTimeKind.Utc);
        _fetcher.Transactions.Add(MakeDto(8101, oldTimestamp));
        await _service.IngestTransactionsAsync();

        var audit = await _db.Auditlogs
            .SingleAsync(a => a.TransactionId == 8101 && a.ChangeType == ChangeType.Finalized);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task FinalizedTransaction_CannotBeUpdated()
    {
        var oldTimestamp = new DateTime(2026, 2, 26, 5, 0, 0, DateTimeKind.Utc);
        _fetcher.Transactions.Add(MakeDto(9001, oldTimestamp, amount: 9.99m));
        await _service.IngestTransactionsAsync();

        var txn = await _db.Transactions.SingleAsync(t => t.TransactionId == 9001);
        Assert.Equal(TransactionStatus.Finalized, txn.Status);

        _fetcher.Transactions.Clear();
        _fetcher.Transactions.Add(MakeDto(9001, oldTimestamp, amount: 50.00m));
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.IngestTransactionsAsync();

        txn = await _db.Transactions.SingleAsync(t => t.TransactionId == 9001);
        Assert.Equal(9.99m, txn.Amount); 
        Assert.Equal(TransactionStatus.Finalized, txn.Status);
    }

    [Fact]
    public async Task FinalizedTransaction_NoUpdateAuditEntries()
    {
        var oldTimestamp = new DateTime(2026, 2, 26, 5, 0, 0, DateTimeKind.Utc);
        _fetcher.Transactions.Add(MakeDto(9101, oldTimestamp, amount: 9.99m));
        await _service.IngestTransactionsAsync();

        _fetcher.Transactions.Clear();
        _fetcher.Transactions.Add(MakeDto(9101, oldTimestamp, amount: 50.00m));
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.IngestTransactionsAsync();

        var updates = await _db.Auditlogs
            .Where(a => a.TransactionId == 9101 && a.ChangeType == ChangeType.Updated)
            .ToListAsync();

        Assert.Empty(updates);
    }

    [Fact]
    public async Task TransactionWithinWindow_IsNotFinalized()
    {
        var recentTimestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc);
        _fetcher.Transactions.Add(MakeDto(9201, recentTimestamp));
        await _service.IngestTransactionsAsync();

        var txn = await _db.Transactions.SingleAsync(t => t.TransactionId == 9201);
        Assert.Equal(TransactionStatus.Active, txn.Status);
    }
}