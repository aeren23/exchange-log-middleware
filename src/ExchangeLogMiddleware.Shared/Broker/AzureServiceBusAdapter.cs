using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Messaging.ServiceBus;
using ExchangeLogMiddleware.Shared.Configuration;
using ExchangeLogMiddleware.Shared.Interfaces;
using ExchangeLogMiddleware.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace ExchangeLogMiddleware.Shared.Broker;

public sealed class AzureServiceBusAdapter : IMessageBroker
{
    private readonly AzureServiceBusSettings _settings;
    private readonly ILogger<AzureServiceBusAdapter> _logger;
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;
    private ServiceBusProcessor? _processor;
    private readonly AsyncRetryPolicy _retryPolicy;
    
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public AzureServiceBusAdapter(IOptions<MessageBrokerSettings> options, ILogger<AzureServiceBusAdapter> logger)
    {
        _settings = options.Value.AzureServiceBus;
        _logger = logger;

        _client = new ServiceBusClient(_settings.ConnectionString);
        _sender = _client.CreateSender(_settings.QueueName);

        // Polly Resilience
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, "Azure Service Bus publish failed. Retrying ({RetryCount}/3)...", retryCount);
                });
    }

    public async Task PublishAsync<T>(T payload, CancellationToken cancellationToken = default) where T : class
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var message = new ServiceBusMessage(json)
            {
                MessageId = Guid.NewGuid().ToString(),
            };

            message.ApplicationProperties["AppId"] = "ExchangeLogProducer";
            message.ApplicationProperties["Timestamp"] = DateTime.UtcNow.ToString("O");

            await _sender.SendMessageAsync(message, cancellationToken);
        });
    }

    public Task SubscribeAsync<T>(Func<MessageEnvelope<T>, Task> onMessageReceived, CancellationToken cancellationToken = default) where T : class
    {
        _processor = _client.CreateProcessor(_settings.QueueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 10 // Aynı anda işlenecek mesaj sayısı
        });

        _processor.ProcessMessageAsync += async args =>
        {
            try
            {
                var body = args.Message.Body.ToString();
                var payload = JsonSerializer.Deserialize<T>(body, _jsonOptions);

                if (payload is null)
                {
                    _logger.LogWarning("Deserialized payload is null. MessageId: {MessageId}", args.Message.MessageId);
                    await args.DeadLetterMessageAsync(args.Message, "NullPayload", "Deserialized payload is null", cancellationToken);
                    return;
                }

                // Boundary translation (ASB -> Pipeline Envelope)
                string appId = args.Message.ApplicationProperties.TryGetValue("AppId", out var appIdObj) 
                    ? appIdObj.ToString() ?? "Unknown" : "Unknown";
                
                DateTime timestamp = DateTime.UtcNow;
                if (args.Message.ApplicationProperties.TryGetValue("Timestamp", out var tsObj) && tsObj is string tsStr)
                {
                    DateTime.TryParse(tsStr, out timestamp);
                }

                var envelope = new MessageEnvelope<T>
                {
                    Payload = payload,
                    MessageId = args.Message.MessageId,
                    SenderId = appId,
                    Timestamp = timestamp
                };

                await onMessageReceived(envelope);
                
                // Mesajı başarıyla işlendi olarak işaretle
                await args.CompleteMessageAsync(args.Message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {MessageId}", args.Message.MessageId);
                // Mesajı geri bırakır, MaxDeliveryCount değerine ulaşana kadar tekrar dener
                await args.AbandonMessageAsync(args.Message, cancellationToken: cancellationToken);
            }
        };

        _processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception, "Error in Service Bus Processor. Error Source: {ErrorSource}", args.ErrorSource);
            return Task.CompletedTask;
        };

        return _processor.StartProcessingAsync(cancellationToken);
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(!_client.IsClosed);
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor != null)
        {
            await _processor.DisposeAsync();
        }
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
