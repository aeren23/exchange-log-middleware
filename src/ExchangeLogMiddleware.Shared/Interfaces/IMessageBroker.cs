namespace ExchangeLogMiddleware.Shared.Interfaces;

using ExchangeLogMiddleware.Shared.Models;

/// <summary>
/// Message Broker soyutlama katmanı — Adapter Pattern sözleşmesi.
/// </summary>
/// <remarks>
/// <para>
/// Bu interface, RabbitMQ ve Azure Service Bus gibi broker implementasyonlarını
/// pipeline'dan gizler. Somut implementasyonlar (örn. <c>RabbitMqAdapter</c>)
/// broker-spesifik mesajları <see cref="MessageEnvelope{T}"/> jenerik zarfına çevirir
/// (Boundary Translation).
/// </para>
/// <para>
/// Pipeline.md §2: "The internal pipeline MUST NOT know about RabbitMQ or Azure Service Bus."
/// </para>
/// </remarks>
public interface IMessageBroker : IAsyncDisposable
{
    /// <summary>
    /// Mesajı broker kuyruğuna yayınlar.
    /// MessageId, AppId ve Timestamp değerleri broker mesaj header'larına/özelliklerine yazılır.
    /// </summary>
    /// <typeparam name="T">Payload tipi. Sadece referans tipleri kabul edilir.</typeparam>
    /// <param name="payload">Yayınlanacak mesaj gövdesi.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    Task PublishAsync<T>(T payload, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Broker kuyruğunu dinler ve gelen mesajları <see cref="MessageEnvelope{T}"/> olarak
    /// callback'e iletir. Boundary Translation bu metot içinde gerçekleşir.
    /// </summary>
    /// <typeparam name="T">Beklenen payload tipi.</typeparam>
    /// <param name="onMessageReceived">
    /// Her mesaj alındığında çağrılacak async callback.
    /// Mesajı <see cref="System.Threading.Channels.Channel{T}"/>'a yazmaktan sorumludur.
    /// </param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    Task SubscribeAsync<T>(
        Func<MessageEnvelope<T>, Task> onMessageReceived,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Broker bağlantısının sağlıklı olup olmadığını kontrol eder.
    /// Docker healthcheck ve Polly resilience policy tarafından kullanılır.
    /// </summary>
    /// <param name="cancellationToken">İptal token'ı.</param>
    /// <returns><c>true</c> bağlantı sağlıklıysa; aksi halde <c>false</c>.</returns>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
