using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Models;

namespace TransactionsIngest.Data;

public class TransactionsDbContext : DbContext
{
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<Auditlog> Auditlogs { get; set; }

    public TransactionsDbContext(DbContextOptions<TransactionsDbContext> options)
        : base(options) { }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasIndex(t => t.Status);
            entity.HasIndex(t => t.TransactionTime);
        });

        modelBuilder.Entity<Auditlog>(entity =>
        {
            entity.HasIndex(a => a.TransactionId);
            entity.HasIndex(a => a.TimestampUTC);
        });
    }
}