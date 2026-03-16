using System.Text.Json.Serialization;

namespace TransactionsIngest.Models;

public class TransactionDto
{
    [JsonPropertyName("transactionId")]
    public int TransactionId { get; set;}

    [JsonPropertyName("cardNumber")]
    public string CardNumber { get; set; } = string.Empty;

    [JsonPropertyName("locationCode")]
    public string LocationCode { get; set; } = string.Empty;

    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}