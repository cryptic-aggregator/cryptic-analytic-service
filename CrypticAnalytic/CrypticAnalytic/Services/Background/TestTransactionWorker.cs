using CrypticAnalytic.Database.Repos;
using CrypticAnalytic.Services.Processors;

namespace CrypticAnalytic.Services.Background;

public class TransactionWorker : BackgroundService
{
    private readonly ILogger<TransactionWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public TransactionWorker(
        ILogger<TransactionWorker> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var txRepo = scope.ServiceProvider
                    .GetRequiredService<FactTransactionRepo>();
                var syncProc = scope.ServiceProvider
                    .GetRequiredService<SyncTransactionProcessor>();

                var seeds = await txRepo.GetUninitializedWalletsAsync();

                foreach (var seed in seeds)
                {
                    _logger.LogInformation(
                        "Seeding wallet {WalletId} @ {Address}/{Chain} since {Ts}",
                        seed.WalletId, seed.WalletAddress, seed.Chain, seed.SinceTs);

                    try
                    {
                        await syncProc.SyncForWalletAsync(
                            seed.WalletId,
                            seed.WalletAddress,
                            seed.Chain,
                            stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error syncing wallet {WalletId} ({Address})",
                            seed.WalletId, seed.WalletAddress);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in TestTransactionWorker");
            }

            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }
}