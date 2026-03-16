using TransactionsIngest.Models;

namespace TransactionsIngest.Services;

public interface ITransactionFetcher
{
    Task<IReadOnlyList<TransactionDto>> FetchTransactionsAsync(CancellationToken ct = default);
}