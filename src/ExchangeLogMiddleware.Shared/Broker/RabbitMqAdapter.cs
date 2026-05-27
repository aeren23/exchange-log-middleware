using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExchangeLogMiddleware.Shared.Configuration;
using ExchangeLogMiddleware.Shared.Interfaces;
using ExchangeLogMiddleware.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ExchangeLogMiddleware.Shared.Broker;

public sealed class RabbitMqAdapter : IMessageBroker
{
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqAdapter> _logger;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly AsyncRetryPolicy _retryPolicy;
    
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public RabbitMqAdapter(IOptions<MessageBrokerSettings> options, ILogger<RabbitMqAdapter> logger)
    {
        _settings = options.Value.RabbitMQ;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password,
            DispatchConsumersAsync = true // SubscribeAsync callback desteği için şart
        };

        // Bağlantıyı başlat
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        
        _channel.QueueDeclare(
            queue: _settings.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        // Polly Resilience: Ağ dalgalanmalarına karşı Exponential Backoff
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, "RabbitMQ publish failed. Retrying ({RetryCount}/3) in {Seconds}s...", retryCount, timeSpan.TotalSeconds);
                });
    }

    public async Task PublishAsync<T>(T payload, CancellationToken cancellationToken = default) where T : class
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = _channel.CreateBasicProperties();
            properties.MessageId = Guid.NewGuid().ToString();
            properties.AppId = "ExchangeLogProducer";
            
            // Hassas DateTime bilgisini saklamak için özel header eklenir
            properties.Headers = new Dictionary<string, object>
            {
                { "PublishTime", DateTime.UtcNow.ToString("O") }
            };

            _channel.BasicPublish(
                exchange: "",
                routingKey: _settings.QueueName,
                basicProperties: properties,
                body: body);

            await Task.CompletedTask;
        });
    }

    public Task SubscribeAsync<T>(Func<MessageEnvelope<T>, Task> onMessageReceived, CancellationToken cancellationToken = default) where T : class
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                var payload = JsonSerializer.Deserialize<T>(json, _jsonOptions);

                if (payload is null)
                {
                    _logger.LogWarning("Deserialized payload is null. MessageId: {MessageId}", ea.BasicProperties.MessageId);
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                // Boundary Translation (RabbitMQ -> Pipeline Envelope)
                DateTime publishTime = DateTime.UtcNow;
                if (ea.BasicProperties.Headers != null && ea.BasicProperties.Headers.TryGetValue("PublishTime", out var publishTimeObj))
                {
                    if (publishTimeObj is byte[] bytes)
                    {
                        var timeStr = Encoding.UTF8.GetString(bytes);
                        DateTime.TryParse(timeStr, out publishTime);
                    }
                }

                var envelope = new MessageEnvelope<T>
                {
                    Payload = payload,
                    MessageId = ea.BasicProperties.MessageId ?? Guid.NewGuid().ToString(),
                    SenderId = ea.BasicProperties.AppId ?? "Unknown",
                    Timestamp = publishTime
                };

                await onMessageReceived(envelope);
                
                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message {MessageId}", ea.BasicProperties.MessageId);
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        _channel.BasicConsume(
            queue: _settings.QueueName,
            autoAck: false,
            consumer: consumer);

        return Task.CompletedTask;
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_connection is { IsOpen: true } && _channel is { IsOpen: true });
    }

    public ValueTask DisposeAsync()
    {
        if (_channel is { IsOpen: true }) _channel.Close();
        if (_connection is { IsOpen: true }) _connection.Close();
        
        _channel?.Dispose();
        _connection?.Dispose();
        
        return ValueTask.CompletedTask;
    }
}
