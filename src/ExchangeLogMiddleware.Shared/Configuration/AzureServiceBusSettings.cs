namespace ExchangeLogMiddleware.Shared.Configuration;

/// <summary>
/// Azure Service Bus bağlantı ayarları.
/// </summary>
public sealed class AzureServiceBusSettings
{
    public string ConnectionString { get; init; } = string.Empty;
    public string QueueName { get; init; } = "exchange-logs";
}
