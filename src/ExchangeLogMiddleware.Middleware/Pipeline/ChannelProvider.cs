namespace ExchangeLogMiddleware.Middleware.Pipeline;

using System.Threading.Channels;
using ExchangeLogMiddleware.Middleware.Configuration;
using ExchangeLogMiddleware.Shared.Models;
using Microsoft.Extensions.Options;

/// <summary>
/// <see cref="System.Threading.Channels"/> bounded channel'ını oluşturan ve DI'a sunan sınıf.
/// </summary>
/// <remarks>
/// <para>
/// Singleton yaşam döngüsüyle kaydedilir. Uygulama boyunca tek bir channel örneği yaşar.
/// </para>
/// <para>
/// Tasarım kararları:
/// <list type="bullet">
///   <item>
///     <c>BoundedChannelFullMode.Wait</c> — Kapasite dolduğunda writer bloklanır (backpressure).
///     Bu sayede üretici hız sınırlanır ve memory leak oluşmaz.
///   </item>
///   <item>
///     <c>SingleWriter = false</c> — Birden fazla broker callback aynı anda yazabilir.
///   </item>
///   <item>
///     <c>SingleReader = true</c> — Tek <c>PipelineWorkerService</c> okur.
///     Bu güvence, Channel'ın internal lock overhead'ını azaltır.
///   </item>
///   <item>
///     Kapasite <c>Pipeline:ChannelCapacity</c> konfigürasyonundan okunur —
///     magic number yoktur; değer dışarıdan (.env → docker-compose) yönetilebilir.
///   </item>
/// </list>
/// </para>
/// <para>
/// Pipeline.md §2: "The Channel acts as an in-memory buffer, allowing worker threads to
/// process logs safely and asynchronously during the required stress test without memory leaks."
/// </para>
/// </remarks>
public sealed class ChannelProvider
{
    /// <summary>
    /// Pipeline boyunca mesajların taşındığı bounded channel.
    /// <c>BrokerListenerService</c> yazar; <c>PipelineWorkerService</c> okur.
    /// </summary>
    public Channel<MessageEnvelope<LogPayload>> LogChannel { get; }

    /// <summary>
    /// Konfigürasyona göre bounded channel'ı oluşturur.
    /// </summary>
    /// <param name="options">Pipeline konfigürasyon seçenekleri (<c>ChannelCapacity</c> kullanılır).</param>
    public ChannelProvider(IOptions<PipelineSettings> options)
    {
        var capacity = options.Value.ChannelCapacity;

        var channelOptions = new BoundedChannelOptions(capacity)
        {
            FullMode     = BoundedChannelFullMode.Wait, // Backpressure — memory leak önlemi
            SingleWriter = false,                        // Birden fazla broker callback desteklenir
            SingleReader = true                          // Tek reader — performans optimizasyonu
        };

        LogChannel = Channel.CreateBounded<MessageEnvelope<LogPayload>>(channelOptions);
    }
}
