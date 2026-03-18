using System.Text.Json;
using Microsoft.Extensions.Logging;
using TransactionsIngest.Configuration;
using TransactionsIngest.Models;

namespace TransactionsIngest.Services;

public class MockTransactionFetcher : ITransactionFetcher
{
    private readonly IngestionSettings _settings;
    private readonly ILogger<MockTransactionFetcher> _logger;

    public MockTransactionFetcher(IngestionSettings settings, ILogger<MockTransactionFetcher> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TransactionDto>> FetchTransactionsAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(AppContext.BaseDirectory, _settings.MockFeedPath);
        _logger.LogInformation("Reading mock feed from {Path}", path);

        if (!File.Exists(path))
        {
            _logger.LogError("Mock feed file not found at {Path}; returning empty snapshot", path);
            return Array.Empty<TransactionDto>();
        }

        var json = await File.ReadAllTextAsync(path, ct);
        var transactions = JsonSerializer.Deserialize<IReadOnlyList<TransactionDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        _logger.LogInformation("Loaded {Count} transactions from mock feed", transactions?.Count ?? 0);
        return transactions ?? (IReadOnlyList<TransactionDto>)Array.Empty<TransactionDto>();
    }
}