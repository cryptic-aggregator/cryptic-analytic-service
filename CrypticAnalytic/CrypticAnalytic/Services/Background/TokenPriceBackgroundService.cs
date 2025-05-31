using CrypticAnalytic.Interfaces;

namespace CrypticAnalytic.Services.Background;

public class TokenPriceBackgroundService : BackgroundService
{
    private readonly ILogger<TokenPriceBackgroundService> _logger;
    private readonly IServiceProvider _provider;

    private const int IntervalMinutes = 15;

    public TokenPriceBackgroundService(
        ILogger<TokenPriceBackgroundService> logger,
        IServiceProvider processor)
    {
        _logger = logger;
        _provider = processor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TokenPriceBackgroundService запущено.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _provider.CreateScope();
                var processor = scope.ServiceProvider
                    .GetRequiredService<ITokenPriceProcessor>();

                await processor.ProcessAllTokensAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TokenPriceBackgroundService: неочікувана помилка під час ProcessAllTokensAsync.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(IntervalMinutes), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("TokenPriceBackgroundService завершується.");
    }
}