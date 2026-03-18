using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Models;
using TransactionsIngest.Services;
using Xunit;

namespace TransactionsIngest.Tests;

public class InsertTests : IDisposable
{
    private readonly TransactionsIngest.Data.TransactionsDbContext _db;
    private readonly StubTransactionFetcher _fetcher;
    private readonly FakeClock _clock;
    private readonly IngestionService _service;

    public InsertTests()
    {
        _db = TestDbHelper.CreateInMemoryContext();
        _fetcher = new StubTransactionFetcher();
        _clock = new FakeClock();
        _service = TestDbHelper.CreateService(_db, _fetcher, _clock);
    }

    public void Dispose() => _db.Dispose();

    private static TransactionDto MakeDto(int id, decimal amount = 19.99m, string product = "Widget") => new()
    {
        TransactionId = id,
        CardNumber = "4111111111111111",
        LocationCode = "STO-01",
        ProductName = product,
        Amount = amount,
        Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task NewTransaction_IsInserted()
    {
        _fetcher.Transactions.Add(MakeDto(1001));

        await _service.IngestTransactionsAsync();

        var txn = await _db.Transactions.SingleAsync(t => t.TransactionId == 1001);
        Assert.Equal(TransactionStatus.Active, txn.Status);
        Assert.Equal(19.99m, txn.Amount);
        Assert.Equal("Widget", txn.ProductName);
        Assert.Equal("1111", txn.CardNumberLast4);
    }

    [Fact]
    public async Task NewTransaction_CreatesAuditEntry()
    {
        _fetcher.Transactions.Add(MakeDto(1002));

        await _service.IngestTransactionsAsync();

        var audit = await _db.Auditlogs.SingleAsync(a => a.TransactionId == 1002);
        Assert.Equal(ChangeType.Created, audit.ChangeType);
        Assert.Null(audit.FieldName);
    }

    [Fact]
    public async Task MultipleTransactions_AllInserted()
    {
        _fetcher.Transactions.AddRange(new[] { MakeDto(2001), MakeDto(2002), MakeDto(2003) });

        await _service.IngestTransactionsAsync();

        var count = await _db.Transactions.CountAsync();
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task CardNumber_IsHashedNotStoredRaw()
    {
        _fetcher.Transactions.Add(MakeDto(3001));

        await _service.IngestTransactionsAsync();

        var txn = await _db.Transactions.SingleAsync(t => t.TransactionId == 3001);
        Assert.NotEqual("4111111111111111", txn.CardNumberHash);
        Assert.Equal(64, txn.CardNumberHash.Length);
        Assert.Equal("1111", txn.CardNumberLast4);
    }

    [Fact]
    public async Task RepeatedRun_SameData_NoDuplicates()
    {
        _fetcher.Transactions.Add(MakeDto(4001));

        await _service.IngestTransactionsAsync();
        await _service.IngestTransactionsAsync();
        await _service.IngestTransactionsAsync();

        var count = await _db.Transactions.CountAsync(t => t.TransactionId == 4001);
        Assert.Equal(1, count);
    }
}