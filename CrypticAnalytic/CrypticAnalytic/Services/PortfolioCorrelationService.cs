using CrypticAnalytic.Database.Repos;
using CrypticAnalytic.Models;

namespace CrypticAnalytic.Services;

public class PortfolioCorrelationService
{
    private readonly DimTokenRepo _tokenRepo;
    private readonly FactTransactionRepo _factTxRepo;
    private readonly FactTokenPriceRepo _priceRepo;

    public PortfolioCorrelationService(
        DimTokenRepo tokenRepo,
        FactTransactionRepo factTxRepo,
        FactTokenPriceRepo priceRepo)
    {
        _tokenRepo = tokenRepo;
        _factTxRepo = factTxRepo;
        _priceRepo = priceRepo;
    }

    /// <summary>
    /// Формує часовий ряд довжиною pointsCount (за замовчуванням 12), де кожна точка містить:
    /// - Unix-мітку часу Ts
    /// - абсолютну вартість portfoliovalue
    /// - абсолютну ціну reference token у USD
    /// - процентні зміни від початку інтервалу для кожного (portfolio та token)
    /// 
    /// Вхід:
    ///   walletIds   – список WalletId (DimWalletTable.WalletId)
    ///   tokenSymbol – символ reference-токена (наприклад, "ETH")
    ///   fromTs      – Unix-мітка початку інтервалу
    ///   toTs        – Unix-мітка кінця інтервалу
    ///   pointsCount – кількість точок (наприклад, 12)
    ///   currency    – код валюти у fact_token_price (за замовчуванням USD = 0)
    /// 
    /// Повертає список PortfolioTokenPointDto завдовжки pointsCount.
    /// </summary>
    public async Task<List<PortfolioTokenPointDto>>
        GetPortfolioVsTokenPointsAsync(
            List<int> walletIds,
            string tokenSymbol,
            long fromTs,
            long toTs,
            int pointsCount = 12,
            int currency = (int)CrypticAnalytic.Enums.CurrencyType.USD,
            CancellationToken ct = default)
    {
        if (walletIds == null || walletIds.Count == 0)
            throw new ArgumentException("WalletIds list is empty");

        if (string.IsNullOrWhiteSpace(tokenSymbol))
            throw new ArgumentException("TokenSymbol is required");

        if (fromTs < 0 || toTs <= fromTs)
            throw new ArgumentException("Invalid time interval");

        if (pointsCount < 2)
            throw new ArgumentException("pointsCount must be at least 2");
        
        var tokenRecord = await _tokenRepo.GetBySymbolAsync(tokenSymbol);
        if (tokenRecord == null)
            throw new ArgumentException($"Token with symbol '{tokenSymbol}' not found");

        int referenceTokenId = tokenRecord.TokenId;
        
        double totalSpan = (double)toTs - fromTs;
        double step = totalSpan / (pointsCount - 1);

        var samplePoints = new long[pointsCount];
        for (int i = 0; i < pointsCount; i++)
        {
            samplePoints[i] = fromTs + (long)Math.Round(step * i);
        }
        
        var portfolioSeries = new decimal[pointsCount];
        var tokenSeries = new decimal[pointsCount];

        for (int idx = 0; idx < pointsCount; idx++)
        {
            ct.ThrowIfCancellationRequested();
            long snapshotTs = samplePoints[idx];

            decimal refPrice = await _priceRepo.GetLastPriceAtAsync(
                referenceTokenId,
                currency,
                snapshotTs
            );
            tokenSeries[idx] = refPrice;
            
            decimal portfolioValue = 0m;
            
            foreach (var walletId in walletIds)
            {
                var balances = await _factTxRepo.GetAllTokenBalancesAtAsync(walletId, snapshotTs);
                if (balances == null || balances.Count == 0)
                    continue;

                foreach (var kvp in balances)
                {
                    int tId = kvp.Key;
                    decimal bal = kvp.Value;
                    if (bal <= 0m)
                        continue;

                    decimal price = await _priceRepo.GetLastPriceAtAsync(tId, currency, snapshotTs);
                    portfolioValue += bal * price;
                }
            }

            portfolioSeries[idx] = portfolioValue;
        }
        
        decimal basePortfolio = portfolioSeries[0];
        decimal baseToken = tokenSeries[0];
        
        var result = new List<PortfolioTokenPointDto>(pointsCount);
        for (int i = 0; i < pointsCount; i++)
        {
            var ts = samplePoints[i];
            var pv = portfolioSeries[i];
            var tp = tokenSeries[i];

            decimal portPct = 0m;
            if (basePortfolio != 0m)
            {
                portPct = Math.Round((pv - basePortfolio) / basePortfolio * 100m, 4);
            }

            decimal tokPct = 0m;
            if (baseToken != 0m)
            {
                tokPct = Math.Round((tp - baseToken) / baseToken * 100m, 4);
            }

            result.Add(new PortfolioTokenPointDto
            {
                Ts = ts,
                PortfolioValue = pv,
                TokenPrice = tp,
                PortfolioChangePct = portPct,
                TokenChangePct = tokPct
            });
        }

        return result;
    }
}