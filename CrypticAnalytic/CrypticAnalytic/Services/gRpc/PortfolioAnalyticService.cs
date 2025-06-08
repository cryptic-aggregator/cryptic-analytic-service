using Cryptic.PortfolioAnalytic.Models.Requests;
using Cryptic.PortfolioAnalytic.Models.Responses;
using Grpc.Core;

namespace CrypticAnalytic.Services.gRpc;

public class
    PortfolioAnalyticService : Cryptic.PortfolioAnalytic.Rpc.PortfolioAnalyticService.PortfolioAnalyticServiceBase
{
    public PortfolioAnalyticService(PortfolioCorrelationService portfolioCorrelationService)
    {
        _portfolioCorrelationService = portfolioCorrelationService;
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

    private readonly PortfolioCorrelationService _portfolioCorrelationService;
}