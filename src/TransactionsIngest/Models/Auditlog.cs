using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TransactionsIngest.Models;

public enum ChangeType
{
    Created,
    Updated,
    Revoked,
    Finalized
}

public class Auditlog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]

    public int Id { get; set; }

    public int TransactionId { get; set; }

    public ChangeType ChangeType { get; set; }

    [MaxLength(50)]
    public string? FieldName { get; set; }

    [MaxLength(500)] 
    public string? OldValue { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? NewValue { get; set; } = string.Empty;

    public DateTime TimestampUTC { get; set; }
}