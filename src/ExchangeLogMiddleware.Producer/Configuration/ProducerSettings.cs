namespace ExchangeLogMiddleware.Producer.Configuration;

/// <summary>
/// Producer servisi konfigürasyon modeli.
/// </summary>
/// <remarks>
/// appsettings.json "Producer" bölümüne ve docker-compose.yml
/// <c>Producer__LogsPerSecond</c> / <c>Producer__ErrorRate</c> ortam değişkenlerine bind edilir.
/// </remarks>
public sealed class ProducerSettings
{
    /// <summary>appsettings.json bölüm adı.</summary>
    public const string SectionName = "Producer";

    /// <summary>
    /// Saniyede üretilecek log sayısı.
    /// Mesajlar arası gecikme <c>1000 / LogsPerSecond</c> ms olarak hesaplanır.
    /// </summary>
    public int LogsPerSecond { get; init; } = 10;

    /// <summary>
    /// ERROR ve CRITICAL seviyeli log üretim oranı (0.0 - 1.0).
    /// Örnek: 0.3 → logların %30'u hata seviyesinde üretilir.
    /// </summary>
    public double ErrorRate { get; init; } = 0.3;
}
