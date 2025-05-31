using Cryptic.BlockchainInteraction.Models.Requests;
using Cryptic.BlockchainInteraction.Rpc;
using CrypticAnalytic.Interfaces;
using CrypticAnalytic.Services.Processors;

namespace CrypticAnalytic.Services.Background;

public class TestTransactionWorker : BackgroundService
{
    private readonly ILogger<TestTransactionWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public TestTransactionWorker(
        ILogger<TestTransactionWorker> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        const string address = "0x2649A160Dc7DF59942Ab8Dc122e2B28E0faF1b69";
        const string chain = "eth";
        long sinceTs = 1726532288;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation(
                    "Syncing tx for {Address}@{Chain} since {SinceTs}",
                    address, chain, sinceTs);
                
                using var scope = _serviceProvider.CreateScope();
                var syncService = scope.ServiceProvider
                    .GetRequiredService<SyncTransactionProcessor>();

                var resp = await syncService.SyncForWalletAsync(1, address, chain, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sync");
            }

            await Task.Delay(TimeSpan.FromSeconds(300), stoppingToken);
        }
    }
}