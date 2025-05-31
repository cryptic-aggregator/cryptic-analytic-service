using System.Text;
using Cryptic_Domain.Database.Config.Interfaces;
using CrypticAnalytic.Interfaces.Configs;

namespace CrypticAnalytic.Services.Config;

public class ConfigService : IDatabaseConfiguration, IMicroservicesConfig, IRabbitMQConfig
{
    public ConfigService() 
    {
        ConnString = Environment.GetEnvironmentVariable(nameof(this.ConnString)) ?? throw DrawAllConfigVars();
        Schema = $"\"{Environment.GetEnvironmentVariable(nameof(this.Schema))}\"" ?? throw DrawAllConfigVars();
        BlockchainInteractionConnString = Environment.GetEnvironmentVariable(nameof(BlockchainInteractionConnString)) ?? throw new Exception($"You could provide a {nameof(BlockchainInteractionConnString)} env var");
        RabbitMQ__Username = Environment.GetEnvironmentVariable(nameof(RabbitMQ__Username)) ?? throw new Exception($"You could provide a {nameof(RabbitMQ__Username)} env var");
        RabbitMQ__Password = Environment.GetEnvironmentVariable(nameof(RabbitMQ__Password)) ?? throw new Exception($"You could provide a {nameof(RabbitMQ__Password)} env var");
        RabbitMQ__HostName = Environment.GetEnvironmentVariable(nameof(RabbitMQ__HostName)) ?? throw new Exception($"You could provide a {nameof(RabbitMQ__HostName)} env var");
        RabbitMQ__Port =
            ushort.TryParse(Environment.GetEnvironmentVariable(nameof(RabbitMQ__Port)), out var rmqport)
                ? rmqport
                : throw new Exception($"You could provide a {nameof(RabbitMQ__Port)} env var");
        RabbitMQ__VHost = Environment.GetEnvironmentVariable(nameof(RabbitMQ__VHost)) ?? throw new Exception($"You could provide a {nameof(RabbitMQ__VHost)} env var");
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

    public string RabbitMQ__Username { get; }
    public string RabbitMQ__Password { get; }
    public string RabbitMQ__HostName { get; }
    public ushort RabbitMQ__Port { get; }
    public string RabbitMQ__VHost { get; }
}