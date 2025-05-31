using System.Text;
using Cryptic_Domain.Database.Config.Interfaces;
using CrypticAnalytic.Interfaces.Configs;

namespace CrypticAnalytic.Services.Config;

public class ConfigService : IDatabaseConfiguration, IMicroservicesConfig
{
    public ConfigService() 
    {
        ConnString = Environment.GetEnvironmentVariable(nameof(this.ConnString)) ?? throw DrawAllConfigVars();
        Schema = $"\"{Environment.GetEnvironmentVariable(nameof(this.Schema))}\"" ?? throw DrawAllConfigVars();
        BlockchainInteractionConnString = Environment.GetEnvironmentVariable(nameof(BlockchainInteractionConnString)) ?? throw new Exception($"You could provide a {nameof(BlockchainInteractionConnString)} env var");
    }
    
    public string ConnString { get; private set; }
    public string Schema { get; private set; }

    public string BlockchainInteractionConnString { get; private set; }

    private Exception DrawAllConfigVars()
    {
        var configType = this.GetType();
        var props = configType.GetProperties();
        var sb = new StringBuilder("\nYou must provide all undefined variables by environment\n");
        foreach (var prop in props)
        {
            sb.AppendLine($"available setting {prop.Name}, with type: {prop.PropertyType.Name}, current val = {prop.GetValue(this)}");
        }
        return new Exception(sb.ToString());
    }
}