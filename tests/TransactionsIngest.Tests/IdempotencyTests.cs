using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Models;
using TransactionsIngest.Services;
using Xunit;

namespace TransactionsIngest.Tests;

public class IdempotencyTests : IDisposable
{
    private readonly TransactionsIngest.Data.TransactionsDbContext _db;
    private readonly StubTransactionFetcher _fetcher;
    private readonly FakeClock _clock;
    private readonly IngestionService _service;

    public IdempotencyTests()
    {
        _db = TestDbHelper.CreateInMemoryContext();
        _fetcher = new StubTransactionFetcher();
        _clock = new FakeClock();
        _service = TestDbHelper.CreateService(_db, _fetcher, _clock);
    }

    public void Dispose() => _db.Dispose();

    private static TransactionDto MakeDto(int id) => new()
    {
        TransactionId = id,
        CardNumber = "4111111111111111",
        LocationCode = "STO-01",
        ProductName = "Widget",
        Amount = 9.99m,
        Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task RepeatedRuns_SameData_NoDuplicateRows()
    {
        _fetcher.Transactions.Add(MakeDto(10001));

        await _service.IngestTransactionsAsync();
        await _service.IngestTransactionsAsync();
        await _service.IngestTransactionsAsync();

        var count = await _db.Transactions.CountAsync(t => t.TransactionId == 10001);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task RepeatedRuns_SameData_NoSpuriousAuditEntries()
    {
        _fetcher.Transactions.Add(MakeDto(10101));

        await _service.IngestTransactionsAsync();
        await _service.IngestTransactionsAsync();
        await _service.IngestTransactionsAsync();

        var audits = await _db.Auditlogs
            .Where(a => a.TransactionId == 10101)
            .ToListAsync();

        Assert.Single(audits);
        Assert.Equal(ChangeType.Created, audits[0].ChangeType);
    }

    [Fact]
    public async Task RepeatedRuns_NoDuplicateRevocations()
    {
        _fetcher.Transactions.Add(MakeDto(11001));
        await _service.IngestTransactionsAsync();

        _fetcher.Transactions.Clear();
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.IngestTransactionsAsync();
        await _service.IngestTransactionsAsync();

        var revocations = await _db.Auditlogs
            .Where(a => a.TransactionId == 11001 && a.ChangeType == ChangeType.Revoked)
            .ToListAsync();

        Assert.Single(revocations);
    }

    [Fact]
    public async Task FullCycle_Insert_Update_Revoke_AllIdempotent()
    {
        _fetcher.Transactions.Add(MakeDto(12001));
        await _service.IngestTransactionsAsync();

        _fetcher.Transactions.Clear();
        var updated = MakeDto(12001);
        updated.Amount = 20.00m;
        _fetcher.Transactions.Add(updated);
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.IngestTransactionsAsync();

        await _service.IngestTransactionsAsync();

        _fetcher.Transactions.Clear();
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _service.IngestTransactionsAsync();

        await _service.IngestTransactionsAsync();

        var audits = await _db.Auditlogs
            .Where(a => a.TransactionId == 12001)
            .OrderBy(a => a.TimestampUTC)
            .ThenBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(3, audits.Count);
        Assert.Equal(ChangeType.Created, audits[0].ChangeType);
        Assert.Equal(ChangeType.Updated, audits[1].ChangeType);
        Assert.Equal(ChangeType.Revoked, audits[2].ChangeType);
    }
}