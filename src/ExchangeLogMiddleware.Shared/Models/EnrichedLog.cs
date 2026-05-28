namespace ExchangeLogMiddleware.Shared.Models;

using ExchangeLogMiddleware.Shared.Enums;

/// <summary>
/// Pipeline'ın nihai çıktı nesnesi.
/// </summary>
/// <remarks>
/// <para>
/// <c>MetadataEnricherHandler</c> (Step 4) tarafından oluşturulur.
/// <c>RouterAndFormatterHandler</c> (Step 5) bu nesneyi formatlayarak dosyaya yazar.
/// </para>
/// <para>Spec §4.3'e uygun olarak tasarlanmıştır.</para>
/// </remarks>
public sealed class EnrichedLog
{
    /// <summary>
    /// Broker'dan alınan benzersiz mesaj/transaction ID'si.
    /// <c>MessageEnvelope.MessageId</c>'den taşınır.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Mesajın broker'a teslim edildiği UTC zaman damgası.
    /// <c>MessageEnvelope.Timestamp</c>'den taşınır.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Mesajı gönderen servisin kimliği.
    /// <c>MessageEnvelope.SenderId</c>'den taşınır.
    /// </summary>
    public required string SenderId { get; init; }

    /// <summary>
    /// Log seviyesine göre hesaplanan kritiklik derecesi.
    /// Hesaplama kuralı: CRITICAL → "High", ERROR → "Medium", WARN → "Low", INFO → "Low".
    /// </summary>
    public required string Criticality { get; init; }

    /// <summary>Orijinal log seviyesi. <c>MessageEnvelope.Payload.Level</c>'dan taşınır.</summary>
    public required LogLevel Level { get; init; }

    /// <summary>
    /// Log kategorisi. Router (Step 5) tarafından <see cref="TargetRoles"/> belirlemede kullanılır.
    /// </summary>
    public required LogCategory Category { get; init; }

    /// <summary>
    /// KVKK anonimleştirilmiş mesaj metni.
    /// <c>KvkkAnonymizerHandler</c> (Step 3) maskeleme işlemini tamamladıktan sonra bu alana yazılır.
    /// </summary>
    public required string SanitizedMessage { get; init; }

    /// <summary>
    /// KVKK anonimleştirilmiş ekstra/ham veri (varsa).
    /// </summary>
    public string? SanitizedRawData { get; init; }

    /// <summary>
    /// Bu logun hedeflendiği rol listesi.
    /// Step 5 (Router) konfigürasyondan kategoriye göre doldurur (Fan-out).
    /// </summary>
    public IReadOnlyList<TargetRole> TargetRoles { get; set; } = [];
}
