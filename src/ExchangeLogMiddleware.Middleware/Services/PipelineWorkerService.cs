namespace ExchangeLogMiddleware.Middleware.Services;

using ExchangeLogMiddleware.Middleware.Pipeline;

/// <summary>
/// <see cref="ChannelProvider.LogChannel"/>'dan mesajları okuyan ve
/// <see cref="PipelineOrchestrator"/> aracılığıyla pipeline'a ileten BackgroundService.
/// </summary>
/// <remarks>
/// <para>
/// Sorumluluk (SRP): Bu servis yalnızca Channel'dan mesaj okuyup pipeline'a iletmekten
/// sorumludur. Broker aboneliği <see cref="BrokerListenerService"/>'e, işleme mantığı
/// handler'lara aittir.
/// </para>
/// <para>
/// Veri akışı: Channel.Reader.ReadAllAsync → PipelineOrchestrator.ProcessAsync
/// </para>
/// <para>
/// Graceful Shutdown:
/// <list type="bullet">
///   <item>
///     <c>BrokerListenerService</c> durdurulduğunda <c>Channel.Writer.Complete()</c> çağrılır.
///   </item>
///   <item>
///     <c>ReadAllAsync</c> döngüsü, Channel boşaldığında ve Writer tamamlandığında otomatik biter.
///   </item>
///   <item>
///     In-flight mesajlar (Channel'da bekleyenler) önce işlenir, ardından döngü kapanır.
///   </item>
/// </list>
/// </para>
/// </remarks>
public sealed class PipelineWorkerService(
    ChannelProvider channelProvider,
    PipelineOrchestrator pipelineOrchestrator,
    ILogger<PipelineWorkerService> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PipelineWorkerService başlatıldı — Channel okuma döngüsü başlıyor.");

        try
        {
            // ReadAllAsync: Writer.Complete() çağrılana kadar Channel'ı tüketir.
            // stoppingToken: Servis durdurulduğunda döngüyü keser.
            await foreach (var envelope in channelProvider.LogChannel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await pipelineOrchestrator.ProcessAsync(envelope, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown sırasında pipeline işlemi iptal edildi — beklenen durum
                    break;
                }
                catch (Exception ex)
                {
                    // Pipeline hatası tek mesajı etkiler; döngü devam eder
                    logger.LogError(
                        ex,
                        "Pipeline işleme hatası — MessageId: {MessageId}. Döngü devam ediyor.",
                        envelope.MessageId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // stoppingToken iptali — graceful shutdown, beklenen durum
        }

        logger.LogInformation(
            "PipelineWorkerService durduruldu — tüm in-flight mesajlar işlendi.");
    }
}
