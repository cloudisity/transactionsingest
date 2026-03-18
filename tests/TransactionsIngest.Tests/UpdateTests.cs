using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Models;
using TransactionsIngest.Services;
using Xunit;

namespace TransactionsIngest.Tests;

public class UpdateTests : IDisposable
{
    private readonly TransactionsIngest.Data.TransactionsDbContext _db;
    private readonly StubTransactionFetcher _fetcher;
    private readonly FakeClock _clock;
    private readonly IngestionService _service;

    public UpdateTests()
    {
        _db = TestDbHelper.CreateInMemoryContext();
        _fetcher = new StubTransactionFetcher();
        _clock = new FakeClock();
        _service = TestDbHelper.CreateService(_db, _fetcher, _clock);
    }

    public void Dispose() => _db.Dispose();

    private static TransactionDto MakeDto(
        int id,
        decimal amount = 19.99m,
        string product = "Widget",
        string location = "STO-01",
        string card = "4111111111111111") => new()
    {
        TransactionId = id,
        CardNumber = card,
        LocationCode = location,
        ProductName = product,
        Amount = amount,
        Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task AmountChange_IsDetectedAndRecorded()
    {
        _fetcher.Transactions.Add(MakeDto(2001, amount: 10.00m));
        await _service.IngestTransactionsAsync();

        _fetcher.Transactions.Clear();
        _fetcher.Transactions.Add(MakeDto(2001, amount: 15.00m));
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.IngestTransactionsAsync();

        var txn = await _db.Transactions.SingleAsync(t => t.TransactionId == 2001);
        Assert.Equal(15.00m, txn.Amount);

        var update = await _db.Auditlogs
            .SingleAsync(a => a.TransactionId == 2001 && a.ChangeType == ChangeType.Updated);
        Assert.Equal("Amount", update.FieldName);
        Assert.Equal("10.00", update.OldValue);
        Assert.Equal("15.00", update.NewValue);
    }

    [Fact]
    public async Task ProductNameChange_IsDetectedAndRecorded()
    {
        _fetcher.Transactions.Add(MakeDto(3001, product: "Old Name"));
        await _service.IngestTransactionsAsync();

        _fetcher.Transactions.Clear();
        _fetcher.Transactions.Add(MakeDto(3001, product: "New Name"));
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.IngestTransactionsAsync();

        var txn = await _db.Transactions.SingleAsync(t => t.TransactionId == 3001);
        Assert.Equal("New Name", txn.ProductName);

        var update = await _db.Auditlogs
            .SingleAsync(a => a.TransactionId == 3001 && a.ChangeType == ChangeType.Updated);
        Assert.Equal("ProductName", update.FieldName);
        Assert.Equal("Old Name", update.OldValue);
        Assert.Equal("New Name", update.NewValue);
    }

    [Fact]
    public async Task LocationCodeChange_IsDetectedAndRecorded()
    {
        _fetcher.Transactions.Add(MakeDto(3501, location: "STO-01"));
        await _service.IngestTransactionsAsync();

        _fetcher.Transactions.Clear();
        _fetcher.Transactions.Add(MakeDto(3501, location: "STO-99"));
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.IngestTransactionsAsync();

        var update = await _db.Auditlogs
            .SingleAsync(a => a.TransactionId == 3501 && a.ChangeType == ChangeType.Updated);
        Assert.Equal("LocationCode", update.FieldName);
        Assert.Equal("STO-01", update.OldValue);
        Assert.Equal("STO-99", update.NewValue);
    }

    [Fact]
    public async Task MultipleFieldChanges_AllRecorded()
    {
        _fetcher.Transactions.Add(MakeDto(4001, amount: 10.00m, product: "Alpha"));
        await _service.IngestTransactionsAsync();

        _fetcher.Transactions.Clear();
        _fetcher.Transactions.Add(MakeDto(4001, amount: 20.00m, product: "Beta"));
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.IngestTransactionsAsync();

        var updates = await _db.Auditlogs
            .Where(a => a.TransactionId == 4001 && a.ChangeType == ChangeType.Updated)
            .ToListAsync();

        Assert.Equal(2, updates.Count);
        Assert.Contains(updates, u => u.FieldName == "Amount");
        Assert.Contains(updates, u => u.FieldName == "ProductName");
    }

    [Fact]
    public async Task NoChanges_NoSpuriousAuditEntries()
    {
        _fetcher.Transactions.Add(MakeDto(5001));
        await _service.IngestTransactionsAsync();

        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.IngestTransactionsAsync();

        var audits = await _db.Auditlogs
            .Where(a => a.TransactionId == 5001)
            .ToListAsync();

        Assert.Single(audits); 
        Assert.Equal(ChangeType.Created, audits[0].ChangeType);
    }
}