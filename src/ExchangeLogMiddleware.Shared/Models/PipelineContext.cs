namespace ExchangeLogMiddleware.Shared.Models;

/// <summary>
/// Pipeline boyunca handler'lar arası taşınan bağlam nesnesi.
/// </summary>
/// <remarks>
/// <para>
/// Bu sınıf, <see cref="MessageEnvelope{T}"/> ile pipeline handler'ları arasında
/// tek sorumluluklu bir köprü görevi görür. Handler'lar doğrudan envelope üzerinde
/// değişiklik yapmak yerine, bu context üzerinden veri paylaşır.
/// </para>
/// <para>
/// <strong>Yaşam döngüsü:</strong> Her mesaj için <see cref="Services.PipelineWorkerService"/>
/// tarafından oluşturulur ve pipeline zinciri boyunca referans olarak taşınır.
/// </para>
/// <para>
/// <strong>Veri akışı:</strong>
/// <list type="number">
///   <item>Step 1-2: Filtreleme — yalnızca <see cref="Envelope"/> okunur.</item>
///   <item>Step 3: KVKK — <see cref="Envelope.Payload"/> üzerinde maskeleme yapılır.</item>
///   <item>Step 4: Enrichment — <see cref="EnrichedLog"/> oluşturulur ve context'e yazılır.</item>
///   <item>Step 5: Formatting — <see cref="EnrichedLog"/> okunur, dosyaya yazılır.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class PipelineContext
{
    /// <summary>
    /// Broker'dan gelen orijinal mesaj zarfı.
    /// Tüm handler'lar bu zarfın <see cref="MessageEnvelope{T}.Payload"/> alanına erişir.
    /// </summary>
    public MessageEnvelope<LogPayload> Envelope { get; }

    /// <summary>
    /// <see cref="Pipeline.Handlers.MetadataEnricherHandler"/> (Step 4) tarafından oluşturulan
    /// zenginleştirilmiş log nesnesi.
    /// </summary>
    /// <remarks>
    /// Step 1-3 boyunca <c>null</c> olur. Step 4 sonrası doldurulur.
    /// Step 5 (<c>RouterAndFormatterHandler</c>) bu değerin <c>null</c> olmadığını varsayar.
    /// </remarks>
    public EnrichedLog? EnrichedLog { get; set; }

    /// <summary>
    /// Yeni bir pipeline context oluşturur.
    /// </summary>
    /// <param name="envelope">İşlenecek mesaj zarfı. Null olamaz.</param>
    /// <exception cref="ArgumentNullException"><paramref name="envelope"/> null ise fırlatılır.</exception>
    public PipelineContext(MessageEnvelope<LogPayload> envelope)
    {
        Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
    }
}
