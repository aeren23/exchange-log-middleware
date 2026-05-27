namespace ExchangeLogMiddleware.Shared.Configuration;

/// <summary>
/// Message broker konfigürasyon kök nesnesi.
/// "MessageBroker" appsettings bölümüne bind edilir.
/// </summary>
public sealed class MessageBrokerSettings
{
    public const string SectionName = "MessageBroker";

    /// <summary>
    /// Aktif broker provider: "RabbitMQ" veya "AzureServiceBus".
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// RabbitMQ özel konfigürasyonları.
    /// </summary>
    public RabbitMqSettings RabbitMQ { get; init; } = new();

    /// <summary>
    /// Azure Service Bus özel konfigürasyonları.
    /// </summary>
    public AzureServiceBusSettings AzureServiceBus { get; init; } = new();
}
