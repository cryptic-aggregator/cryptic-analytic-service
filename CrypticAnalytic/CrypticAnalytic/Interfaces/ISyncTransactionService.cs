using Cryptic.BlockchainInteraction.Models.Responses;

namespace CrypticAnalytic.Interfaces;

public interface ISyncTransactionService
{
    Task<GetWalletTransactionsResponse> GetTransactionsAsync(
        string address,
        string chain,
        long sinceTs,
        CancellationToken ct);
}