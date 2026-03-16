using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Data;

var options = new DbContextOptionsBuilder<TransactionsDbContext>()
    .UseSqlite("Data Source=transactions.db")
    .Options;

using var db = new TransactionsDbContext(options);
await db.Database.EnsureCreatedAsync();

Console.WriteLine("Database created successfully");
Console.WriteLine($"Tables: Transactions, Auditlogs");
