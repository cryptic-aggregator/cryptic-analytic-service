using Cryptic_Domain.Models.MassTransit;
using CrypticAnalytic.Database.Repos;
using CrypticAnalytic.Database.Tables;
using CrypticAnalytic.Helpers.Chain;

namespace CrypticAnalytic.Services.Processors.MassTransit;

public class WalletConnectedProcessor
    {
        private readonly ILogger<WalletConnectedProcessor> _logger;
        private readonly WalletSyncInfoRepo _walletRepo;
        private readonly WalletSyncInfoRepo _walletSyncInfoRepo;
        private readonly SyncTransactionProcessor _syncTxProcessor;

        public WalletConnectedProcessor(
            ILogger<WalletConnectedProcessor> logger,
            WalletSyncInfoRepo walletRepo,
            WalletSyncInfoRepo walletSyncInfoRepo,
            SyncTransactionProcessor syncTxProcessor)
        {
            _logger = logger;
            _walletRepo = walletRepo;
            _walletSyncInfoRepo = walletSyncInfoRepo;
            _syncTxProcessor = syncTxProcessor;
        }
        
        public async Task ProcessAsync(NewWalletConnectedMessage message, CancellationToken cancellationToken)
        {
            int walletId = message.WalletId;
            string walletAddress = message.WalletAddress.Trim();

            _logger.LogInformation("WalletConnectedProcessor: обробка WalletId={WalletId}, Address={Address}",
                walletId, walletAddress);
            
            var existingWallet = await _walletRepo.GetByIdAsync(walletId);
            if (existingWallet == null)
            {
                var newWallet = new WalletSyncInfoTable()
                {
                    WalletId = walletId,
                    LastSyncedTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                await _walletRepo.CreateAsync(newWallet);
                _logger.LogInformation("DimWallet з WalletId={WalletId} створено.", walletId);
            }

            var syncInfo = await _walletSyncInfoRepo.GetByWalletIdAsync(walletId);
            if (syncInfo == null)
            {
                var newSync = new WalletSyncInfoTable
                {
                    WalletId = walletId,
                    LastSyncedTs = 0
                };
                await _walletSyncInfoRepo.CreateAsync(newSync);
                _logger.LogInformation("WalletSyncInfo створено для WalletId={WalletId}.", walletId);
            }
            else
            {
                _logger.LogInformation("WalletSyncInfo вже існує для WalletId={WalletId}, LastSyncedTs={LastSyncedTs}.",
                    walletId, syncInfo.LastSyncedTs);
            }

            string chain = ChainDetector.DetectChain(walletAddress);
            _logger.LogInformation("Chain для адреси {Address} визначено як '{Chain}'.", walletAddress, chain);

            try
            {
                long newSinceTs = await _syncTxProcessor.SyncForWalletAsync(
                    walletId,
                    walletAddress,
                    chain,
                    cancellationToken);
                _logger.LogInformation(
                    "SyncTransactionProcessor завершив роботу для WalletId={WalletId} з newSinceTs={NewSinceTs}.",
                    walletId, newSinceTs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при виклику SyncTransactionProcessor для WalletId={WalletId}.", walletId);
            }
        }
    }