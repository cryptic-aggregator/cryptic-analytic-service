using Cryptic_Domain.Database.Config.Interfaces;
using Cryptic_Domain.Database.Interfaces;
using Cryptic_Domain.Services;
using CrypticAnalytic.Services.Config;

namespace CrypticAnalytic.DI;

public static class ConfigurationDIConfigure
{
    public static void InjectConfiguration(this IServiceCollection services, ConfigService config)
    {
        services.AddSingleton<IDatabaseConfiguration>(config);
        services.AddSingleton<IDatabaseConnectionService, DatabaseConnectionService>();
    }
}