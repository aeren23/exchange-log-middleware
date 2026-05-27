namespace ExchangeLogMiddleware.Middleware.Pipeline.Handlers;

using ExchangeLogMiddleware.Middleware.Configuration;
using ExchangeLogMiddleware.Shared.Interfaces;
using ExchangeLogMiddleware.Shared.Models;
using Microsoft.Extensions.Options;
using LogLevel = ExchangeLogMiddleware.Shared.Enums.LogLevel;

/// <summary>
/// Pipeline Step 1 — Performance Level Filter (Fail Fast).
/// </summary>
/// <remarks>
/// <para>
/// Konfigürasyondaki <c>MinimumLogLevel</c> değerinin altındaki logları düşürür.
/// Bu, pipeline'ın en başında gerçekleşir — gereksiz CPU tüketimini (cache lookup,
/// regex maskeleme, formatting) önler.
/// </para>
/// <para>
/// Spec §5 Step 1: "If the incoming envelope.Payload.Level is lower (INFO, WARN),
/// drop it immediately. Do not process further."
/// </para>
/// <para>
/// Pipeline.md §3: "Drops noise at the very beginning of the pipeline before it
/// consumes CPU for caching, regex, or formatting."
/// </para>
/// <para>
/// LogLevel enum sayısal sıralaması: INFO(0) &lt; WARN(1) &lt; ERROR(2) &lt; CRITICAL(3)
/// Bu sayede doğrudan karşılaştırma operatörü (&lt;) kullanılabilir.
/// </para>
/// </remarks>
public sealed class LevelFilterHandler : BasePipelineHandler
{
    private readonly LogLevel _minimumLevel;
    private readonly IPerformanceTracker _performanceTracker;
    private readonly ILogger<LevelFilterHandler> _logger;

    /// <summary>
    /// LevelFilterHandler'ı gerekli bağımlılıklarla oluşturur.
    /// </summary>
    /// <param name="options">Pipeline konfigürasyonu — <c>MinimumLogLevel</c> değerini içerir.</param>
    /// <param name="performanceTracker">DROP durumunda sayaç artırımı için performans izleyici.</param>
    /// <param name="logger">Loglama bağımlılığı.</param>
    /// <exception cref="ArgumentException">
    /// <c>MinimumLogLevel</c> geçerli bir <see cref="LogLevel"/> değerine parse edilemezse fırlatılır.
    /// </exception>
    public LevelFilterHandler(
        IOptions<PipelineSettings> options,
        IPerformanceTracker performanceTracker,
        ILogger<LevelFilterHandler> logger)
    {
        _performanceTracker = performanceTracker;
        _logger = logger;

        if (!Enum.TryParse<LogLevel>(options.Value.MinimumLogLevel, ignoreCase: true, out var parsed))
        {
            throw new ArgumentException(
                $"Geçersiz MinimumLogLevel değeri: '{options.Value.MinimumLogLevel}'. " +
                $"Kabul edilen değerler: {string.Join(", ", Enum.GetNames<LogLevel>())}");
        }

        _minimumLevel = parsed;

        _logger.LogInformation(
            "LevelFilterHandler başlatıldı — MinimumLogLevel: {MinimumLevel}",
            _minimumLevel);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <c>context.Envelope.Payload.Level &lt; _minimumLevel</c> → DROP (return, zincir durur).
    /// Aksi halde → PASS (NextAsync çağrılır, zincir devam eder).
    /// </remarks>
    public override async Task HandleAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var incomingLevel = context.Envelope.Payload.Level;

        if (incomingLevel < _minimumLevel)
        {
            _performanceTracker.IncrementDroppedByFilter();

            _logger.LogDebug(
                "DROP — Level {IncomingLevel} < MinimumLevel {MinimumLevel}, MessageId: {MessageId}",
                incomingLevel,
                _minimumLevel,
                context.Envelope.MessageId);

            return; // DROP — sonraki handler çağrılmaz
        }

        _logger.LogDebug(
            "PASS — Level {IncomingLevel} >= MinimumLevel {MinimumLevel}, MessageId: {MessageId}",
            incomingLevel,
            _minimumLevel,
            context.Envelope.MessageId);

        await NextAsync(context, cancellationToken);
    }
}
