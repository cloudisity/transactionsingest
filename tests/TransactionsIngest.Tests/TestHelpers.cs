using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionsIngest.Configuration;
using TransactionsIngest.Data;
using TransactionsIngest.Models;
using TransactionsIngest.Services;

namespace TransactionsIngest.Tests;

public class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 2, 27, 12, 0, 0, DateTimeKind.Utc);
}

public class StubTransactionFetcher : ITransactionFetcher
{
    public List<TransactionDto> Transactions { get; set; } = new();

    public Task<IReadOnlyList<TransactionDto>> FetchTransactionsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TransactionDto>>(Transactions.AsReadOnly());
}

public static class TestDbHelper
{
    public static TransactionsDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<TransactionsDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var context = new TransactionsDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    public static IngestionService CreateService(
        TransactionsDbContext db,
        StubTransactionFetcher fetcher,
        FakeClock clock,
        IngestionSettings? settings = null)
    {
        return new IngestionService(
            db,
            fetcher,
            settings ?? new IngestionSettings(),
            NullLogger<IngestionService>.Instance,
            clock);
    }
}