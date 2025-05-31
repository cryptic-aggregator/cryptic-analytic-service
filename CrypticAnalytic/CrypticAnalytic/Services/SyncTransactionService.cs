using Cryptic.BlockchainInteraction.Models.Requests;
using Cryptic.BlockchainInteraction.Models.Responses;
using Cryptic.BlockchainInteraction.Rpc;
using CrypticAnalytic.Interfaces;

namespace CrypticAnalytic.Services;

public class SyncTransactionService : ISyncTransactionService
{
    private readonly TransactionService.TransactionServiceClient _grpcClient;

    public SyncTransactionService(TransactionService.TransactionServiceClient grpcClient)
    {
        _grpcClient = grpcClient;
    }

    public async Task<GetWalletTransactionsResponse> GetTransactionsAsync(
        string address,
        string chain,
        long sinceTs,
        CancellationToken ct)
    {
        var req = new GetWalletTransactionsRequest
        {
            Address = address,
            Chain = chain,
            SinceTs = sinceTs
        };
        return await _grpcClient.GetWalletTransactionsAsync(req, cancellationToken: ct);
    }
}