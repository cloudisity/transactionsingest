using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TransactionsIngest.Configuration;
using TransactionsIngest.Data;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var settings = new IngestionSettings();
configuration.GetSection(IngestionSettings.SectionName).Bind(settings);

Console.WriteLine($"Mock feed enabled: {settings.UseMockFeed}");
Console.WriteLine($"Connection string: {settings.ConnectionString}");
Console.WriteLine($"Look-back window: {settings.LookBackHours}h");

var options = new DbContextOptionsBuilder<TransactionsDbContext>()
    .UseSqlite(settings.ConnectionString)
    .Options;

using var db = new TransactionsDbContext(options);
await db.Database.EnsureCreatedAsync();

Console.WriteLine("Database ready.");