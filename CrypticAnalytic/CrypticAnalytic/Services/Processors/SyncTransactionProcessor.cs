using System.Globalization;
using Cryptic.BlockchainInteraction.Models.Responses;
using CrypticAnalytic.Database.Repos;
using CrypticAnalytic.Database.Tables;
using CrypticAnalytic.Interfaces;

namespace CrypticAnalytic.Services.Processors;

public class SyncTransactionProcessor
{
    private readonly ILogger<SyncTransactionProcessor> _logger;
    private readonly ISyncTransactionService _grpcClient;
    private readonly DimTokenRepo _tokenRepo;
    private readonly FactTransactionRepo _txRepo;
    private readonly WalletSyncInfoRepo _walletSyncRepo;

    public SyncTransactionProcessor(
        ILogger<SyncTransactionProcessor> logger,
        ISyncTransactionService grpcClient,
        DimTokenRepo tokenRepo,
        FactTransactionRepo txRepo,
        WalletSyncInfoRepo walletSyncRepo)
    {
        _logger = logger;
        _grpcClient = grpcClient;
        _tokenRepo = tokenRepo;
        _txRepo = txRepo;
        _walletSyncRepo = walletSyncRepo;
    }

    public async Task<long> SyncForWalletAsync(
        int walletId,
        string walletAddress,
        string chain,
        CancellationToken ct)
    {
        var syncInfo = await _walletSyncRepo.GetByWalletIdAsync(walletId);
        long lastTs = syncInfo?.LastSyncedTs ?? 0;

        _logger.LogInformation(
            "SyncForWalletAsync: walletId={WalletId}, address={Address}, chain={Chain}, sinceTs={SinceTs}",
            walletId, walletAddress, chain, lastTs);
        
        GetWalletTransactionsResponse response;
        try
        {
            response = await _grpcClient.GetTransactionsAsync(walletAddress, chain, lastTs, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling gRPC GetWalletTransactions for {Address}", walletAddress);
            throw;
        }

        var remoteTxs = response.Transactions;
        if (remoteTxs == null || remoteTxs.Count == 0)
        {
            _logger.LogInformation("No new transactions for walletId={WalletId}", walletId);
            return lastTs;
        }

        _logger.LogInformation("Fetched {Count} transactions from gRPC", remoteTxs.Count);
        
        var distinctTokenAddresses = remoteTxs
            .Select(tx => tx.TokenAddress)
            .Where(addr => !string.IsNullOrWhiteSpace(addr))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        
        var existingTokens = await _tokenRepo.GetByContractAddressesAsync(distinctTokenAddresses);
        var lookup = existingTokens.ToDictionary(
            tok => tok.ContractAddress,
            tok => tok.TokenId,
            StringComparer.OrdinalIgnoreCase);
        
        var toBeCreated = new List<DimTokenTable>();
        foreach (var addr in distinctTokenAddresses)
        {
            if (!lookup.ContainsKey(addr))
            {
                var sampleTx = remoteTxs.FirstOrDefault(tx =>
                    string.Equals(tx.TokenAddress, addr, StringComparison.OrdinalIgnoreCase)
                );
                
                string sym = sampleTx?.Symbol ?? string.Empty;
                string name = sampleTx?.TokenName ?? string.Empty;
                string logo = sampleTx?.Logo ?? string.Empty;

                toBeCreated.Add(new DimTokenTable
                {
                    Symbol = sym,
                    Name = string.IsNullOrWhiteSpace(name) ? null : name,
                    ContractAddress = addr,
                    Decimals = 6,
                    LogoUri = string.IsNullOrWhiteSpace(logo) ? null : logo
                });
            }
        }
        
        if (toBeCreated.Count > 0)
        {
            var inserted = await _tokenRepo.CreateBatchAsync(toBeCreated);
            foreach (var tok in inserted)
            {
                lookup[tok.ContractAddress] = tok.TokenId;
            }
        }
        
        var factTxs = new List<FactTransactionTable>(remoteTxs.Count);
        long maxTsSeen = lastTs;

        foreach (var t in remoteTxs)
        {
            if (!lookup.TryGetValue(t.TokenAddress, out int tokenId))
            {
                _logger.LogWarning("TokenAddress {TokenAddress} still not found in lookup", t.TokenAddress);
                continue;
            }

            long txTs = t.Ts;
            if (txTs > maxTsSeen) maxTsSeen = txTs;

            if (!decimal.TryParse(
                    t.Amount,
                    NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out decimal parsedAmount))
            {
                _logger.LogWarning("Could not parse amount '{AmountStr}' for tx {TxHash}", t.Amount, t.TransactionHash);
                continue;
            }

            var fact = new FactTransactionTable
            {
                WalletId = walletId,
                TokenId = tokenId,
                TransactionHash = t.TransactionHash,
                FromAddress = t.FromAddress ?? string.Empty,
                ToAddress = t.ToAddress ?? string.Empty,
                Amount = parsedAmount,
                Ts = txTs,
                TransactionType = t.TransactionType,
                Chain = chain
            };
            factTxs.Add(fact);
        }
        
        try
        {
            await _txRepo.CreateBatchAsync(factTxs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch inserting fact_transaction");
            throw;
        }
        
        try
        {
            await _walletSyncRepo.UpsertAsync(walletId, maxTsSeen);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting wallet_sync_info for walletId={WalletId}", walletId);
            throw;
        }

        _logger.LogInformation(
            "Sync completed for walletId={WalletId}, new sinceTs={NewTs}",
            walletId, maxTsSeen);

        return maxTsSeen;
    }
}