using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TransactionsIngest.Models;

public enum TransactionStatus
{
    Active,
    Revoked,
    Finalized
}

public class Transaction
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int TransactionId { get; set; }

    [MaxLength(64)]
    public string CardNumberHash { get; set; } = string.Empty;

    [MaxLength(4)]
    public string CardNumberLast4 { get; set; } = string.Empty;

    [MaxLength(19)]
    public string LocationCode { get; set; } = string.Empty;

    [MaxLength(19)]
    public string ProductName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Amount { get; set; }

    public DateTime TransactionTime { get; set; }

    public TransactionStatus Status { get; set; }

    public DateTime CreatedAtUTC { get; set; }

    public DateTime UpdatedAtUTC { get; set; }
}