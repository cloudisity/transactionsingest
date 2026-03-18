using System.Text.Json;
using Microsoft.Extensions.Logging;
using TransactionsIngest.Configuration;
using TransactionsIngest.Models;

namespace TransactionsIngest.Services;

public class HttpTransactionFetcher : ITransactionFetcher
{
    private readonly HttpClient _httpClient;
    private readonly IngestionSettings _settings;
    private readonly ILogger<HttpTransactionFetcher> _logger;

    public HttpTransactionFetcher(
        HttpClient httpClient,
        IngestionSettings settings,
        ILogger<HttpTransactionFetcher> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TransactionDto>> FetchTransactionsAsync(CancellationToken ct = default)
    {
        var url = $"{_settings.ApiBaseUrl.TrimEnd('/')}{_settings.ApiPath}";
        _logger.LogInformation("Fetching transactions from {Url}", url);

        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var transactions = JsonSerializer.Deserialize<List<TransactionDto>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        _logger.LogInformation("Fetched {Count} transactions from API", transactions?.Count ?? 0);
        return transactions?.AsReadOnly() ?? (IReadOnlyList<TransactionDto>)Array.Empty<TransactionDto>();
    }
}