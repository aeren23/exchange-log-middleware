using ExchangeLogMiddleware.Shared.Broker;
using ExchangeLogMiddleware.Shared.Configuration;
using ExchangeLogMiddleware.Shared.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExchangeLogMiddleware.Shared.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// appsettings.json üzerindeki "MessageBroker:Provider" ayarına göre
    /// doğru IMessageBroker adaptörünü (RabbitMQ veya AzureServiceBus)
    /// DI container'a kaydeder.
    /// </summary>
    public static IServiceCollection AddMessageBroker(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(MessageBrokerSettings.SectionName);
        var settings = section.Get<MessageBrokerSettings>();

        if (settings is null || string.IsNullOrWhiteSpace(settings.Provider))
        {
            throw new InvalidOperationException("MessageBroker konfigürasyonu veya Provider bulunamadı.");
        }

        // Ayarları IOptionsPattern ile kullanılabilir hale getir
        services.Configure<MessageBrokerSettings>(section);

        // Provider kontrolü
        switch (settings.Provider.ToUpperInvariant())
        {
            case "RABBITMQ":
                services.AddSingleton<IMessageBroker, RabbitMqAdapter>();
                break;
            case "AZURESERVICEBUS":
                services.AddSingleton<IMessageBroker, AzureServiceBusAdapter>();
                break;
            default:
                throw new InvalidOperationException($"Desteklenmeyen broker provider: {settings.Provider}");
        }

        return services;
    }
}
