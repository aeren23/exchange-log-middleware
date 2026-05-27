namespace ExchangeLogMiddleware.Producer.Services;

using ExchangeLogMiddleware.Producer.Configuration;
using ExchangeLogMiddleware.Producer.Generators;
using ExchangeLogMiddleware.Shared.Interfaces;
using Microsoft.Extensions.Options;

/// <summary>
/// Yüksek frekanslı borsa log verisi üreten ve broker'a yayımlayan BackgroundService.
/// </summary>
/// <remarks>
/// <para>
/// Üretim mantığı <see cref="LogDataGenerator"/>'da kapsüllenir; bu sınıf yalnızca
/// döngü orkestrasonu ve hata yönetiminden sorumludur (SRP).
/// </para>
/// <para>
/// Spec §6 — Container 1 gereksinimleri:
/// <list type="bullet">
///   <item>BackgroundService olarak çalışır.</item>
///   <item>Üretim hızı <c>Producer__LogsPerSecond</c> ortam değişkeniyle kontrol edilir.</item>
///   <item>Metadata (MessageId, AppId, Timestamp) broker adaptörü tarafından header'lara eklenir.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class LogGeneratorService(
    IMessageBroker messageBroker,
    LogDataGenerator logDataGenerator,
    IOptions<ProducerSettings> options,
    ILogger<LogGeneratorService> logger) : BackgroundService
{
    private readonly ProducerSettings _settings = options.Value;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_settings.LogsPerSecond <= 0)
        {
            logger.LogError(
                "Geçersiz LogsPerSecond değeri: {Value}. LogGeneratorService başlatılamadı.",
                _settings.LogsPerSecond);
            return;
        }

        var intervalMs = 1000 / _settings.LogsPerSecond;

        logger.LogInformation(
            "LogGeneratorService başlatıldı — Hız: {Rate} log/sn, Hata oranı: {ErrorRate:P0}, Aralık: {IntervalMs}ms",
            _settings.LogsPerSecond,
            _settings.ErrorRate,
            intervalMs);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var hasNextTick = await timer.WaitForNextTickAsync(stoppingToken);
                if (!hasNextTick)
                {
                    break;
                }

                var payload = logDataGenerator.GenerateLogPayload(_settings.ErrorRate);
                await messageBroker.PublishAsync(payload, stoppingToken);

                logger.LogDebug(
                    "Log yayımlandı — Seviye: {Level}, Kategori: {Category}, RawData: {HasRawData}",
                    payload.Level,
                    payload.Category,
                    payload.RawData is not null);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Log payload yayımlanırken hata oluştu. Döngü devam ediyor...");
            }
        }

        logger.LogInformation("LogGeneratorService durduruldu.");
    }
}
