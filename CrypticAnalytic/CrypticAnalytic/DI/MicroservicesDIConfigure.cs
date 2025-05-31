using Cryptic.BlockchainInteraction.Rpc;
using Cryptic.PortfolioAnalytic.Rpc;
using CrypticAnalytic.Interfaces.Configs;

namespace CrypticAnalytic.DI;

public static class MicroservicesDIConfigure
{
    public static void ConfigureMicroservices(this IServiceCollection services, IMicroservicesConfig cfg)
    {
        var customHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => true
        };
        
        services.AddGrpcClient<WalletService.WalletServiceClient>(opt =>
        {
            opt.Address = new Uri(cfg.BlockchainInteractionConnString);
        }).ConfigurePrimaryHttpMessageHandler(() => customHandler);
        
        services.AddGrpcClient<TransactionService.TransactionServiceClient>(opt =>
        {
            opt.Address = new Uri(cfg.BlockchainInteractionConnString);
        }).ConfigurePrimaryHttpMessageHandler(() => customHandler);
        
        services.AddGrpcClient<TokenPriceService.TokenPriceServiceClient>(opt =>
        {
            opt.Address = new Uri(cfg.BlockchainInteractionConnString);
        }).ConfigurePrimaryHttpMessageHandler(() => customHandler);
    }
}