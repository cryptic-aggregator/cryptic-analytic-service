using System.Globalization;
using Cryptic.BlockchainInteraction.Models.Requests;
using Cryptic.BlockchainInteraction.Models.Responses;
using Cryptic.BlockchainInteraction.Rpc;
using CrypticAnalytic.Database.Repos;
using CrypticAnalytic.Database.Tables;
using CrypticAnalytic.Enums;
using CrypticAnalytic.Interfaces;
using Grpc.Core;

namespace CrypticAnalytic.Services.Processors;

public class TokenPriceProcessor : ITokenPriceProcessor
{
    private readonly ILogger<TokenPriceProcessor> _logger;
    private readonly DimTokenRepo _tokenRepo;
    private readonly FactTokenPriceRepo _priceRepo;
    private readonly TokenPriceService.TokenPriceServiceClient _grpcClient;
    
    private const int MaxConcurrentGrpc = 8;
    
    private const int MaxRowsPerInsert = 10000;

    public TokenPriceProcessor(
        ILogger<TokenPriceProcessor> logger,
        DimTokenRepo tokenRepo,
        FactTokenPriceRepo priceRepo,
        TokenPriceService.TokenPriceServiceClient grpcClient)
    {
        _logger = logger;
        _tokenRepo = tokenRepo;
        _priceRepo = priceRepo;
        _grpcClient = grpcClient;
    }

    public async Task ProcessAllTokensAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TokenPriceProcessor: починаємо синхронізацію {Time}.",
            DateTimeOffset.UtcNow);
        
        var allTokens = await _tokenRepo.GetAllAsync();
        if (allTokens == null || allTokens.Count == 0)
        {
            _logger.LogInformation("TokenPriceProcessor: dim_token порожня, пропускаємо.");
            return;
        }

        int[] allTokenIds = allTokens.Select(t => t.TokenId).ToArray();

        var lastPriceDatesDict = await _priceRepo.GetLastPriceDatesAsync(allTokenIds);

        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        using var semaphore = new SemaphoreSlim(MaxConcurrentGrpc);

        var allNewPrices = new List<FactTokenPriceTable>();

        var tasks = new List<Task>();

        foreach (var token in allTokens)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var localToken = token;

            var task = Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    int tokenId = localToken.TokenId;
                    string symbol = localToken.Symbol;
                    
                    long lastTs = lastPriceDatesDict[tokenId];

                    long fromTs;
                    if (lastTs <= 0)
                    {
                        const long OneYearInSeconds = 365L * 24 * 3600;
                        fromTs = nowUnix - OneYearInSeconds;
                    }
                    else
                    {
                        fromTs = lastTs + 1;
                    }

                    var request = new GetPriceHistoryRequest
                    {
                        Symbol = symbol,
                        FromTs = fromTs,
                        ToTs = nowUnix
                    };

                    GetPriceHistoryResponse response;
                    try
                    {
                        response = await _grpcClient.GetPriceHistoryAsync(
                            request,
                            cancellationToken: cancellationToken
                        );
                    }
                    catch (RpcException rex)
                    {
                        _logger.LogError(rex,
                            "TokenPriceProcessor: GetPriceHistory не вдався для Symbol={Symbol}.",
                            symbol);
                        return;
                    }

                    if (response?.Prices == null || response.Prices.Count == 0)
                    {
                        _logger.LogInformation(
                            "TokenPriceProcessor: нових цін для {Symbol} (ост. ts={LastTs}) немає.",
                            symbol, lastTs);
                        return;
                    }
                    
                    var localList = new List<FactTokenPriceTable>();
                    foreach (var pt in response.Prices)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        if (!decimal.TryParse(
                                pt.Price,
                                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                                CultureInfo.InvariantCulture,
                                out decimal parsedPrice))
                        {
                            _logger.LogWarning(
                                "TokenPriceProcessor: Не вдалося розпарсити price '{PriceStr}' для {Symbol}, ts={Ts}.",
                                pt.Price, symbol, pt.Ts);
                            continue;
                        }

                        var fact = new FactTokenPriceTable
                        {
                            TokenId = tokenId,
                            Price = parsedPrice,
                            PriceDate = pt.Ts,
                            Currency = CurrencyType.USD
                        };

                        localList.Add(fact);
                    }
                    
                    lock (allNewPrices)
                    {
                        allNewPrices.AddRange(localList);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            tasks.Add(task);
        }
        
        await Task.WhenAll(tasks);
        
        if (allNewPrices.Count == 0)
        {
            _logger.LogInformation(
                "TokenPriceProcessor: жодної нової точки для вставки не знайдено.");
            _logger.LogInformation(
                "TokenPriceProcessor: завершено процес синхронізації.");
            return;
        }
        
        int totalCount = allNewPrices.Count;
        int offset = 0;
        int chunkNumber = 0;

        while (offset < totalCount)
        {
            int take = Math.Min(MaxRowsPerInsert, totalCount - offset);
            var chunk = allNewPrices.Skip(offset).Take(take).ToList();

            try
            {
                await _priceRepo.CreateBatchAsync(chunk);
                _logger.LogInformation(
                    "TokenPriceProcessor: батчова вставка {InsertedCount} рядків у fact_token_price (чанк #{Chunk}).",
                    chunk.Count, ++chunkNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "TokenPriceProcessor: помилка батчової вставки в fact_token_price (чанк #{Chunk}).",
                    chunkNumber);
            }

            offset += take;
        }

        _logger.LogInformation(
            "TokenPriceProcessor: всього вставлено {TotalCount} нових рядків у fact_token_price.",
            allNewPrices.Count);

        _logger.LogInformation("TokenPriceProcessor: завершено процес синхронізації.");
    }
}