namespace ExchangeLogMiddleware.Middleware.Configuration;

/// <summary>
/// Middleware pipeline altyapısı konfigürasyon modeli.
/// </summary>
/// <remarks>
/// <para>
/// appsettings.json "Pipeline" bölümüne ve docker-compose.yml
/// <c>Pipeline__MinimumLogLevel</c> / <c>Pipeline__DeduplicationCacheTtlMinutes</c> /
/// <c>Pipeline__ChannelCapacity</c> ortam değişkenlerine bind edilir.
/// </para>
/// <para>
/// Değerler sırasıyla şu kaynaktan okunur (yüksek öncelik → düşük):
/// <list type="number">
///   <item>Ortam değişkenleri (docker-compose / .env)</item>
///   <item>appsettings.json</item>
///   <item>Bu sınıftaki init-time varsayılan değerler</item>
/// </list>
/// </para>
/// </remarks>
public sealed class PipelineSettings
{
    /// <summary>appsettings.json bölüm adı.</summary>
    public const string SectionName = "Pipeline";

    /// <summary>
    /// Pipeline'a alınacak minimum log seviyesi.
    /// Bu seviyenin altındaki loglar <c>LevelFilterHandler</c> (Step 1) tarafından düşürülür.
    /// Kabul edilen değerler: INFO, WARN, ERROR, CRITICAL.
    /// </summary>
    public string MinimumLogLevel { get; init; } = "ERROR";

    /// <summary>
    /// Deduplication cache'inin TTL süresi (dakika).
    /// <c>DeduplicationFilterHandler</c> (Step 2) bu değeri <c>IMemoryCache</c> girişi için kullanır.
    /// </summary>
    public int DeduplicationCacheTtlMinutes { get; init; } = 10;

    /// <summary>
    /// <c>System.Threading.Channels</c> bounded channel'ının maksimum kapasitesi.
    /// Kapasite aşıldığında <c>BoundedChannelFullMode.Wait</c> devreye girer —
    /// broker callback bloklanır (backpressure). Bu, memory leak'i önler.
    /// </summary>
    public int ChannelCapacity { get; init; } = 1000;

    /// <summary>
    /// <c>MetricsReporterService</c>'in performans metriklerini konsola yazma aralığı (saniye).
    /// Spec §6.3: "A background timer MUST log these metrics to the console every 5 seconds."
    /// </summary>
    public int MetricsReportIntervalSeconds { get; init; } = 5;
}
