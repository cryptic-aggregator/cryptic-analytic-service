using Cryptic.PortfolioAnalytic.Models.Requests;
using Cryptic.PortfolioAnalytic.Models.Responses;
using Grpc.Core;

namespace CrypticAnalytic.Services.gRpc;

public class PortfolioAnalyticService : Cryptic.PortfolioAnalytic.Rpc.PortfolioAnalyticService.PortfolioAnalyticServiceBase
{
    public override Task<CalculateWalletResponse> GetAssetAllocations(CalculateWalletRequest request, ServerCallContext context)
    {
        var response = new CalculateWalletResponse();
        
        if (!double.TryParse(request.WalletResponse.TotalPortfolioValueUSDT, out var totalPortfolioValue) || totalPortfolioValue == 0)
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
}