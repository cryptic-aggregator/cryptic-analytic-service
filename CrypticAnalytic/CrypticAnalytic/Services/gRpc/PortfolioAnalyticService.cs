using Cryptic.Base.V1.Models.Responses;
using Cryptic.PortfolioAnalytic.Models.Requests;
using Cryptic.PortfolioAnalytic.Models.Responses;
using CrypticAnalytic.Database.Repos;
using Grpc.Core;

namespace CrypticAnalytic.Services.gRpc;

public class
    PortfolioAnalyticService : Cryptic.PortfolioAnalytic.Rpc.PortfolioAnalyticService.PortfolioAnalyticServiceBase
{
    public PortfolioAnalyticService(PortfolioCorrelationService portfolioCorrelationService, DimTokenRepo tokenRepo,
        FactTokenPriceRepo factToken, FactTransactionRepo txRepo)
    {
        _portfolioCorrelationService = portfolioCorrelationService;
        _tokenRepo = tokenRepo;
        _factToken = factToken;
        _txRepo = txRepo;
    }

    public override Task<CalculateWalletResponse> GetAssetAllocations(CalculateWalletRequest request,
        ServerCallContext context)
    {
        var response = new CalculateWalletResponse();

        if (!double.TryParse(request.WalletResponse.TotalPortfolioValueUSDT, out var totalPortfolioValue) ||
            totalPortfolioValue == 0)
        {
            return Task.FromResult(response);
        }

        foreach (var coin in request.WalletResponse.Coins)
        {
            if (double.TryParse(coin.CurrentValue, out var coinValue))
            {
                double percentage = coinValue / totalPortfolioValue * 100;

                var coinResult = new WalletCoinResult
                {
                    Symbol = coin.Symbol,
                    Image = coin.Image,
                    DollarValue = coinValue.ToString("F2"),
                    Percentage = percentage.ToString("F2")
                };

                response.WalletCoins.Add(coinResult);
            }
        }

        return Task.FromResult(response);
    }

    public override async Task<GetPortfolioCorrelationResponse>
        GetPortfolioVsTokenPoints(
            GetPortfolioVsTokenPointsRequest request,
            ServerCallContext context)
    {
        var walletIds = request.WalletIds.ToList();
        var tokenSymbol = request.TokenSymbol;
        var fromTs = request.FromTs;
        var toTs = request.ToTs;
        var pointsCount = request.PointsCount > 0
            ? request.PointsCount
            : 12;

        var dtoPoints = await _portfolioCorrelationService
            .GetPortfolioVsTokenPointsAsync(
                walletIds,
                tokenSymbol,
                fromTs,
                toTs,
                pointsCount,
                currency: (int)CrypticAnalytic.Enums.CurrencyType.USD,
                ct: context.CancellationToken);

        var response = new GetPortfolioCorrelationResponse();
        foreach (var p in dtoPoints)
        {
            response.Points.Add(new PortfolioCorrelationPoint
            {
                Ts = p.Ts,
                PortfolioValue = (double)p.PortfolioValue,
                TokenPrice = (double)p.TokenPrice,
                PortfolioChangePct = (double)p.PortfolioChangePct,
                TokenChangePct = (double)p.TokenChangePct
            });
        }

        return response;
    }

    public override async Task<GetPortfolioPnlPointsResponse> GetPortfolioPnlPoints(
        GetPortfolioPnlPointsRequest request,
        ServerCallContext context)
    {
        var resp = new GetPortfolioPnlPointsResponse
        {
            Result = new TaskResponse { Success = true }
        };

        if (request.WalletIds.Count == 0 || request.PointsCount < 2)
            return resp;

        var fromTs = request.FromTs;
        var toTs = request.ToTs;
        var count = request.PointsCount;
        double span = toTs - fromTs;
        double step = span / (count - 1);

        var sampleTs = Enumerable
            .Range(0, count)
            .Select(i => fromTs + (long)Math.Round(step * i))
            .ToArray();

        var series = new decimal[count];
        for (int i = 0; i < count; i++)
        {
            long ts = sampleTs[i];
            decimal value = 0m;

            foreach (var wid in request.WalletIds)
            {
                var balances = await _txRepo.GetAllTokenBalancesAtAsync(wid, ts);
                foreach (var kv in balances)
                {
                    int tokenId = kv.Key;
                    decimal bal = kv.Value;
                    if (bal <= 0) continue;
                    var price = await _factToken.GetLastPriceAtAsync(tokenId, currency: 1, ts);
                    value += bal * price;
                }
            }

            series[i] = value;
        }

        decimal baseVal = series[0];
        for (int i = 0; i < count; i++)
        {
            decimal delta = series[i] - baseVal;
            resp.Points.Add(new PnlPoint
            {
                Ts = sampleTs[i],
                Profit = delta > 0 ? (double)delta : 0,
                Loss = delta < 0 ? (double)(-delta) : 0
            });
        }

        return resp;
    }

    public override async Task<GetPortfolioBalancePointsResponse> GetPortfolioBalancePoints(
        GetPortfolioBalancePointsRequest request,
        ServerCallContext context)
    {
        var resp = new GetPortfolioBalancePointsResponse
        {
            Result = new TaskResponse { Success = true }
        };
        if (request.WalletIds.Count == 0 || request.PointsCount < 2 || request.FromTs >= request.ToTs)
            return resp;

        double span = request.ToTs - request.FromTs;
        double step = span / (request.PointsCount - 1);
        var sampleTs = Enumerable.Range(0, request.PointsCount)
            .Select(i => request.FromTs + (long)Math.Round(step * i))
            .ToArray();

        for (int i = 0; i < sampleTs.Length; i++)
        {
            long ts = sampleTs[i];
            decimal total = 0m;

            foreach (var wid in request.WalletIds)
            {
                var balances = await _txRepo.GetAllTokenBalancesAtAsync(wid, ts);
                foreach (var kv in balances)
                {
                    var price = await _factToken.GetLastPriceAtAsync(kv.Key, currency: 1, ts);
                    total += kv.Value * price;
                }
            }

            resp.Points.Add(new BalancePoint
            {
                Ts = ts,
                Balance = (double)total
            });
        }

        return resp;
    }

    private readonly PortfolioCorrelationService _portfolioCorrelationService;
    private readonly DimTokenRepo _tokenRepo;
    private readonly FactTokenPriceRepo _factToken;
    private readonly FactTransactionRepo _txRepo;
}