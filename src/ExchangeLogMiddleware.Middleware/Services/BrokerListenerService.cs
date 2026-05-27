namespace ExchangeLogMiddleware.Middleware.Services;

using ExchangeLogMiddleware.Middleware.Pipeline;
using ExchangeLogMiddleware.Shared.Interfaces;
using ExchangeLogMiddleware.Shared.Models;

/// <summary>
/// Message broker'ı dinleyen ve gelen mesajları <see cref="ChannelProvider.LogChannel"/>'a yazan
/// BackgroundService.
/// </summary>
/// <remarks>
/// <para>
/// Sorumluluk (SRP): Bu servis yalnızca broker'dan mesaj alıp Channel'a yazmaktan sorumludur.
/// Mesajları işleme görevi <see cref="PipelineWorkerService"/>'e aittir.
/// </para>
/// <para>
/// Veri akışı: <c>IMessageBroker.SubscribeAsync</c> → callback → Channel.Writer.WriteAsync
/// </para>
/// <para>
/// Graceful Shutdown:
/// <list type="bullet">
///   <item>
///     <c>stoppingToken</c> iptal edildiğinde <c>ChannelWriter.Complete()</c> çağrılır.
///   </item>
///   <item>
///     Bu sinyal <see cref="PipelineWorkerService"/>'in <c>ReadAllAsync</c> döngüsünü
///     temiz bir şekilde sonlandırmasını sağlar.
///   </item>
/// </list>
/// </para>
/// <para>
/// Metrik: Mesaj alındığında <c>IPerformanceTracker.IncrementTotalReceived()</c> çağrılır.
/// Bu, spec §6.3 TotalReceived sayacını günceller.
/// </para>
/// </remarks>
public sealed class BrokerListenerService(
    IMessageBroker messageBroker,
    ChannelProvider channelProvider,
    IPerformanceTracker performanceTracker,
    ILogger<BrokerListenerService> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("BrokerListenerService başlatıldı — broker aboneliği kuruluyor.");

        // Graceful shutdown: token iptal edildiğinde Writer tamamlanır,
        // PipelineWorkerService'in ReadAllAsync döngüsü temiz kapanır.
        stoppingToken.Register(() =>
        {
            channelProvider.LogChannel.Writer.Complete();
            logger.LogInformation(
                "BrokerListenerService durduruldu — Channel Writer tamamlandı (Complete).");
        });

        await messageBroker.SubscribeAsync<LogPayload>(
            onMessageReceived: async envelope =>
            {
                await WriteToChannelAsync(envelope, stoppingToken);
            },
            cancellationToken: stoppingToken);

        // SubscribeAsync event-driven çalışır; bu await bloğu token iptal edilene kadar sürer.
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Mesajı Channel'a yazar ve performans metriğini günceller.
    /// </summary>
    /// <param name="envelope">Broker'dan gelen mesaj zarfı.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    private async Task WriteToChannelAsync(
        MessageEnvelope<LogPayload> envelope,
        CancellationToken cancellationToken)
    {
        try
        {
            // Backpressure: Channel doluysa WriteAsync burada bloklanır (BoundedChannelFullMode.Wait)
            await channelProvider.LogChannel.Writer.WriteAsync(envelope, cancellationToken);

            // Spec §6.3 — TotalReceived metriği
            performanceTracker.IncrementTotalReceived();

            logger.LogDebug(
                "Mesaj Channel'a yazıldı — MessageId: {MessageId}, Level: {Level}, Category: {Category}",
                envelope.MessageId,
                envelope.Payload.Level,
                envelope.Payload.Category);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown — beklenen durum
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Mesaj Channel'a yazılırken hata oluştu — MessageId: {MessageId}",
                envelope.MessageId);
        }
    }
}
