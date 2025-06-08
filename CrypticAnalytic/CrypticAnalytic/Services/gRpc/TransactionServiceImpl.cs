using Cryptic.BlockchainInteraction.Rpc;
using Cryptic.PortfolioAnalytic.Models.Requests;
using Cryptic.PortfolioAnalytic.Models.Responses;
using Cryptic.PortfolioAnalytic.Rpc;
using CrypticAnalytic.Database.Repos;
using Grpc.Core;

namespace CrypticAnalytic.Services.gRpc;

public class TransactionServiceImpl : AnalyticTransactionService.AnalyticTransactionServiceBase
{
    private readonly FactTransactionRepo _factTransactionRepo;
    private readonly ILogger<TransactionServiceImpl> _logger;

    public TransactionServiceImpl(
        FactTransactionRepo factTransactionRepo,
        ILogger<TransactionServiceImpl> logger)
    {
        _factTransactionRepo = factTransactionRepo;
        _logger = logger;
    }

    public override async Task<GetWalletTransactionsResponse>
        GetWalletTransactions(GetWalletTransactionsRequest request, ServerCallContext context)
    {
        var walletIds = request.Wallets.Select(w => w.WalletId).ToArray();
        if (walletIds.Length == 0)
        {
            return new GetWalletTransactionsResponse
            {
                Total = 0,
                Page = request.Page <= 0 ? 1 : request.Page,
                PerPage = request.PerPage <= 0 ? 10 : request.PerPage
            };
        }

        int page = request.Page <= 0 ? 1 : request.Page;
        int perPage = request.PerPage <= 0 ? 10 : request.PerPage;
        int offset = (page - 1) * perPage;

        int? txTypeFilter = request.TransactionType switch
        {
            TransactionTypeFilter.Incoming => 0,
            TransactionTypeFilter.Outgoing => 1,
            _ => null
        };

        long? tsFrom = null, tsTo = null;
        if (request.DateRange != null)
        {
            if (request.DateRange.From != null)
                tsFrom = request.DateRange.From;
            if (request.DateRange.To != null)
                tsTo = request.DateRange.To;
        }

        var (records, total) = await _factTransactionRepo
            .GetPagedWithTokenInfoAsync(
                walletIds,
                txTypeFilter,
                tsFrom,
                tsTo,
                request.Search,
                offset,
                perPage);

        var response = new GetWalletTransactionsResponse
        {
            Total = total,
            Page = page,
            PerPage = perPage
        };

        foreach (var rec in records)
        {
            var tsProto = rec.Ts;

            var tokenInfo = new TokenInfo
            {
                TokenId = rec.TokenId,
                Symbol = rec.Symbol,
                Name = rec.Name,
                LogoUri = rec.LogoUri,
                LastPrice = rec.LastPrice.ToString()
            };

            response.Transactions.Add(new Transaction
            {
                TransactionId = rec.TransactionId,
                WalletId = rec.WalletId,
                Token = tokenInfo,
                TransactionHash = rec.TransactionHash,
                FromAddress = rec.FromAddress,
                ToAddress = rec.ToAddress,
                Amount = rec.Amount.ToString(),
                Timestamp = tsProto,
                TransactionType = rec.TransactionType,
                Chain = rec.Chain,
                Fee = "0.0001",
                Status = 1
            });
        }

        return response;
    }
}