namespace ExchangeLogMiddleware.Middleware.Pipeline.Handlers;

using ExchangeLogMiddleware.Middleware.Configuration;
using ExchangeLogMiddleware.Shared.Interfaces;
using ExchangeLogMiddleware.Shared.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

/// <summary>
/// Pipeline Step 2 — Deduplication Filter (Idempotent Consumer).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MessageEnvelope{T}.MessageId"/> değerini <see cref="IMemoryCache"/>'de kontrol eder.
/// Daha önce işlenmiş bir mesaj geldiğinde (duplicate) düşürür.
/// </para>
/// <para>
/// Spec §5 Step 2: "Checks the envelope.MessageId from the generic envelope against an IMemoryCache.
/// If the MessageId exists, it is a duplicate. Drop it immediately."
/// </para>
/// <para>
/// Pipeline.md §3: "Message brokers guarantee 'at-least-once' delivery, meaning duplicate
/// messages can occur during network blips. This layer ensures data integrity."
/// </para>
/// <para>
/// Cache TTL değeri <see cref="PipelineSettings.DeduplicationCacheTtlMinutes"/> konfigürasyonundan
/// okunur (varsayılan: 10 dakika). Bu süre boyunca aynı MessageId'ye sahip mesajlar düşürülür.
/// </para>
/// </remarks>
public sealed class DeduplicationFilterHandler : BasePipelineHandler
{
    private readonly IMemoryCache _cache;
    private readonly int _cacheTtlMinutes;
    private readonly IPerformanceTracker _performanceTracker;
    private readonly ILogger<DeduplicationFilterHandler> _logger;

    /// <summary>
    /// DeduplicationFilterHandler'ı gerekli bağımlılıklarla oluşturur.
    /// </summary>
    /// <param name="cache">MessageId tabanlı duplicate kontrolü için memory cache.</param>
    /// <param name="options">Pipeline konfigürasyonu — <c>DeduplicationCacheTtlMinutes</c> değerini içerir.</param>
    /// <param name="performanceTracker">DROP durumunda sayaç artırımı için performans izleyici.</param>
    /// <param name="logger">Loglama bağımlılığı.</param>
    public DeduplicationFilterHandler(
        IMemoryCache cache,
        IOptions<PipelineSettings> options,
        IPerformanceTracker performanceTracker,
        ILogger<DeduplicationFilterHandler> logger)
    {
        _cache = cache;
        _cacheTtlMinutes = options.Value.DeduplicationCacheTtlMinutes;
        _performanceTracker = performanceTracker;
        _logger = logger;

        _logger.LogInformation(
            "DeduplicationFilterHandler başlatıldı — Cache TTL: {TtlMinutes} dakika",
            _cacheTtlMinutes);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <c>envelope.MessageId</c> cache'de varsa → DROP (duplicate, return).
    /// Cache'de yoksa → cache'e ekle (TTL ile) → PASS (NextAsync çağrılır).
    /// Cache value olarak <c>true</c> saklanır — yalnızca varlık kontrolü yapılır.
    /// </remarks>
    public override async Task HandleAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var messageId = context.Envelope.MessageId;

        if (_cache.TryGetValue(messageId, out _))
        {
            _performanceTracker.IncrementDroppedByFilter();

            _logger.LogDebug(
                "DROP — Duplicate MessageId: {MessageId}",
                messageId);

            return; // DROP — duplicate mesaj
        }

        // Yeni mesaj — cache'e ekle
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(_cacheTtlMinutes));

        _cache.Set(messageId, true, cacheOptions);

        _logger.LogDebug(
            "PASS — Yeni MessageId cache'e eklendi: {MessageId}, TTL: {TtlMinutes} dk",
            messageId,
            _cacheTtlMinutes);

        await NextAsync(context, cancellationToken);
    }
}
