using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

var services = new ServiceCollection();

services.AddSingleton(settings);
services.AddSingleton<IClock, SystemClock>();

services.AddDbContext<TransactionsDbContext>(options =>
    options.UseSqlite(settings.ConnectionString));

services.AddSingleton<ITransactionFetcher, MockTransactionFetcher>();
services.AddTransient<IngestionService>();

services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

await using var serviceProvider = services.BuildServiceProvider();

using (var scope = serviceProvider.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
    await db.Database.EnsureCreatedAsync();
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    var ingestion = serviceProvider.GetRequiredService<IngestionService>();
    await ingestion.IngestTransactionsAsync(cts.Token);
    return 0;
}
catch (OperationCanceledException)
{
    Console.WriteLine("Ingestion cancelled.");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Fatal error: {ex.Message}");
    return 1;
}