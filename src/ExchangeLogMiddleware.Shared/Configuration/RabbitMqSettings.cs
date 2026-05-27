namespace ExchangeLogMiddleware.Shared.Configuration;

/// <summary>
/// RabbitMQ bağlantı ayarları.
/// </summary>
public sealed class RabbitMqSettings
{
    public string HostName { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string QueueName { get; init; } = "exchange-logs";
}
