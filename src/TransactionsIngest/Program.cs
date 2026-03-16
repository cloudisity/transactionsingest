using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TransactionsIngest.Configuration;
using TransactionsIngest.Data;
using TransactionsIngest.Services;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var settings = new IngestionSettings();
configuration.GetSection(IngestionSettings.SectionName).Bind(settings);

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

var options = new DbContextOptionsBuilder<TransactionsDbContext>()
    .UseSqlite(settings.ConnectionString)
    .Options;

using var db = new TransactionsDbContext(options);
await db.Database.EnsureCreatedAsync();

var fetcher = new MockTransactionFetcher(settings, loggerFactory.CreateLogger<MockTransactionFetcher>());
var snapshot = await fetcher.FetchTransactionsAsync();

Console.WriteLine($"Fetched {snapshot.Count} transactions:");
foreach (var txn in snapshot)
{
    Console.WriteLine($"  #{txn.TransactionId} - {txn.ProductName} - ${txn.Amount}");
}