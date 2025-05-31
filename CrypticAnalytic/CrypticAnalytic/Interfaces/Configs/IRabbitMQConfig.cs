namespace CrypticAnalytic.Interfaces.Configs;

public interface IRabbitMQConfig
{
    string RabbitMQ__Username { get; }
    string RabbitMQ__Password { get; }
    string RabbitMQ__HostName { get; }
    ushort RabbitMQ__Port { get; }
    string RabbitMQ__VHost { get; }
}