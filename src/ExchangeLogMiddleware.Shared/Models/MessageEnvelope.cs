namespace ExchangeLogMiddleware.Shared.Models;

/// <summary>
/// Broker-agnostic jenerik mesaj zarfı.
/// </summary>
/// <remarks>
/// <para>
/// <c>RabbitMqAdapter</c> tarafından broker-spesifik mesajdan oluşturulur (Boundary Translation).
/// Pipeline'daki tüm handler'lar bu nesne üzerinden çalışır — doğrudan broker verisine erişmez.
/// </para>
/// <para>
/// Bu tasarım Dependency Inversion Principle'ı uygular: iç pipeline RabbitMQ veya
/// Azure Service Bus hakkında hiçbir şey bilmez.
/// </para>
/// <para>Spec §4.2'ye uygun olarak tasarlanmıştır.</para>
/// </remarks>
/// <typeparam name="T">
/// Mesaj gövdesinin tipi. Tipik olarak <see cref="LogPayload"/>.
/// Sadece referans tipleri kabul edilir.
/// </typeparam>
public sealed class MessageEnvelope<T> where T : class
{
    /// <summary>
    /// Mesaj gövdesi — Producer'ın ürettiği JSON log payload'ı.
    /// </summary>
    public required T Payload { get; init; }

    /// <summary>
    /// Broker tarafından atanan veya Producer tarafından header'a yazılan benzersiz mesaj kimliği.
    /// <c>DeduplicationFilterHandler</c> (Step 2) bu değeri idempotency kontrolü için kullanır.
    /// <c>MetadataEnricherHandler</c> (Step 4) bu değeri Transaction No olarak <c>EnrichedLog</c>'a taşır.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Mesajı gönderen servisin kimliği (AppId).
    /// <c>MetadataEnricherHandler</c> (Step 4) tarafından <c>EnrichedLog.SenderId</c>'e taşınır.
    /// </summary>
    public required string SenderId { get; init; }

    /// <summary>
    /// Mesajın broker'a teslim edildiği UTC zaman damgası.
    /// <c>MetadataEnricherHandler</c> (Step 4) tarafından <c>EnrichedLog.Timestamp</c>'e taşınır.
    /// </summary>
    public required DateTime Timestamp { get; init; }
}
