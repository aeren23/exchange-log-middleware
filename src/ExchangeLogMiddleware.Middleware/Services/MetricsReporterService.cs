namespace ExchangeLogMiddleware.Middleware.Services;

using ExchangeLogMiddleware.Middleware.Configuration;
using ExchangeLogMiddleware.Shared.Interfaces;
using Microsoft.Extensions.Options;

/// <summary>
/// Performans metriklerini belirli aralıklarla konsola raporlayan BackgroundService.
/// </summary>
/// <remarks>
/// <para>
/// Spec §6.3: "A background timer MUST log these metrics to the console every 5 seconds.
/// This provides real-time proof of the system's performance range and bottleneck
/// (I/O vs CPU) during the stress test."
/// </para>
/// <para>
/// SRP: Bu servis yalnızca <see cref="IPerformanceTracker"/>'dan metrikleri okuyup
/// konsola yazdırmaktan sorumludur. Sayaç yönetimi <c>PerformanceTracker</c> sınıfına aittir.
/// </para>
/// <para>
/// Throughput hesaplama: Her tick'te önceki snapshot ile fark alınarak
/// işlenen mesaj/saniye değeri hesaplanır.
/// </para>
/// <para>
/// Raporlama aralığı <c>PipelineSettings.MetricsReportIntervalSeconds</c> konfigürasyonundan
/// okunur (varsayılan: 5 saniye).
/// </para>
/// </remarks>
public sealed class MetricsReporterService(
    IPerformanceTracker performanceTracker,
    IOptions<PipelineSettings> options,
    ILogger<MetricsReporterService> logger) : BackgroundService
{
    private readonly int _intervalSeconds = options.Value.MetricsReportIntervalSeconds;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "MetricsReporterService başlatıldı — raporlama aralığı: {IntervalSeconds} saniye",
            _intervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_intervalSeconds));

        var previousProcessed = 0L;
        var previousTimestamp = DateTime.UtcNow;

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var snapshot = CaptureSnapshot();
            var throughput = CalculateThroughput(snapshot.SuccessfullyProcessed, ref previousProcessed, ref previousTimestamp);

            logger.LogInformation(
                "[METRICS] TotalReceived: {TotalReceived} | DroppedByFilter: {DroppedByFilter} | " +
                "SuccessfullyProcessed: {SuccessfullyProcessed} | Throughput: {Throughput:F1} msg/s",
                snapshot.TotalReceived,
                snapshot.DroppedByFilter,
                snapshot.SuccessfullyProcessed,
                throughput);
        }

        logger.LogInformation("MetricsReporterService durduruldu.");
    }

    /// <summary>
    /// Mevcut metrik değerlerinin anlık görüntüsünü alır.
    /// </summary>
    /// <returns>Sayaç değerlerini içeren tuple.</returns>
    private (long TotalReceived, long DroppedByFilter, long SuccessfullyProcessed) CaptureSnapshot()
        => (performanceTracker.TotalReceived,
            performanceTracker.DroppedByFilter,
            performanceTracker.SuccessfullyProcessed);

    /// <summary>
    /// İki ölçüm arasındaki işlem sayısını geçen süreye bölerek throughput hesaplar.
    /// </summary>
    /// <param name="currentProcessed">Şu anki başarıyla işlenen mesaj sayısı.</param>
    /// <param name="previousProcessed">Önceki tick'teki başarıyla işlenen mesaj sayısı (ref — güncellenir).</param>
    /// <param name="previousTimestamp">Önceki tick zamanı (ref — güncellenir).</param>
    /// <returns>Mesaj/saniye cinsinden throughput değeri.</returns>
    private static double CalculateThroughput(
        long currentProcessed,
        ref long previousProcessed,
        ref DateTime previousTimestamp)
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - previousTimestamp).TotalSeconds;
        var delta = currentProcessed - previousProcessed;

        previousProcessed = currentProcessed;
        previousTimestamp = now;

        return elapsed > 0 ? delta / elapsed : 0;
    }
}
