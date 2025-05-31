using CrypticAnalytic.Database.Repos;

namespace CrypticAnalytic.DI;

public static class ReposDIConfigure
{
    public static void ConfigureRepositories(this IServiceCollection services)
    {
        services.AddScoped<DimTokenRepo>();
        services.AddScoped<FactTransactionRepo>();
        services.AddScoped<WalletSyncInfoRepo>();
        services.AddScoped<FactTokenPriceRepo>();
    }
}