namespace ExchangeLogMiddleware.Shared.Models;

using ExchangeLogMiddleware.Shared.Enums;

/// <summary>
/// Producer (Container 1) tarafından üretilen ham log verisi.
/// </summary>
/// <remarks>
/// <para>
/// Bu model yalnızca iş mantığı alanlarını içerir.
/// Metadata (MessageId, SenderId, Timestamp) Producer tarafından değil,
/// broker'ın mesaj özelliklerine (headers/properties) yazılır.
/// Spec §4.1'e uygun olarak tasarlanmıştır.
/// </para>
/// <para>
/// <strong>Serialization:</strong> JSON serializasyonunda <c>Level</c> ve <c>Category</c> alanları
/// <c>JsonStringEnumConverter</c> aracılığıyla string olarak serileştirilir
/// ("INFO", "WARN", "ERROR", "CRITICAL" ve "Database", "Auth", "System").
/// </para>
/// </remarks>
public sealed class LogPayload
{
    /// <summary>
    /// Log mesajının önem derecesi.
    /// LevelFilterHandler (Step 1) bu değere göre filtreleme yapar.
    /// </summary>
    public required LogLevel Level { get; init; }

    /// <summary>
    /// Log mesajının kaynak kategorisi.
    /// Router (Step 5) bu değere göre <see cref="TargetRole"/> ataması yapar.
    /// </summary>
    public required LogCategory Category { get; init; }

    /// <summary>
    /// İş mantığı log metni. Hassas veri içerebilir.
    /// KvkkAnonymizerHandler (Step 3) tarafından maskelenir.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Opsiyonel ek veri alanı. TCKN, kredi kartı numarası, e-posta veya telefon gibi
    /// hassas veriler bu alana yerleştirilebilir (test senaryoları için).
    /// KvkkAnonymizerHandler (Step 3) tarafından maskelenir.
    /// </summary>
    public string? RawData { get; init; }
}
