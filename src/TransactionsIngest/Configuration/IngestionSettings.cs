namespace TransactionsIngest.Configuration;

public class IngestionSettings
{
    public const string SectionName = "Ingestion";

    public string ApiBaseUrl { get; set; } = string.Empty;
    public string ApiPath { get; set; } = "/api/transactions/last24h";
    public bool UseMockFeed { get; set; } = false;
    public string MockFeedPath { get; set; } = "mock-data/transactions.json";
    public string ConnectionString { get; set; } = "Data Source=transactions.db";
    public int LookBackHours { get; set; } = 24;
}