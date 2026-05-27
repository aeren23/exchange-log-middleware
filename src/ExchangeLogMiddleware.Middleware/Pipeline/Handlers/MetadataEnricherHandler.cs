namespace ExchangeLogMiddleware.Middleware.Pipeline.Handlers;

using ExchangeLogMiddleware.Shared.Enums;
using ExchangeLogMiddleware.Shared.Models;
using LogLevel = ExchangeLogMiddleware.Shared.Enums.LogLevel;

/// <summary>
/// Pipeline Step 4 — Broker Metadata Enricher.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MessageEnvelope{T}"/> özelliklerinden ve <see cref="LogPayload"/>'dan
/// final <see cref="EnrichedLog"/> nesnesini oluşturur ve <see cref="PipelineContext.EnrichedLog"/>
/// property'sine yazar.
/// </para>
/// <para>
/// Spec §5 Step 4: "Extracts contextual data strictly from the generic MessageEnvelope properties
/// (Broker-Agnostic)."
/// </para>
/// <para>
/// Pipeline.md §3: "Ensures enterprise-grade reliability and 100% accuracy
/// without relying on brittle text-parsing (Regex)."
/// </para>
/// <para>
/// Enrichment sırası:
/// <list type="number">
///   <item>MessageId ← <c>envelope.MessageId</c> (Transaction No)</item>
///   <item>Timestamp ← <c>envelope.Timestamp</c></item>
///   <item>SenderId ← <c>envelope.SenderId</c> (AppId)</item>
///   <item>Criticality ← <see cref="CalculateCriticality"/> (Level tabanlı hesaplama)</item>
///   <item>Level ← <c>envelope.Payload.Level</c></item>
///   <item>Category ← <c>envelope.Payload.Category</c></item>
///   <item>SanitizedMessage ← <c>envelope.Payload.Message</c> (KVKK maskelemesi Step 3'te yapılmış)</item>
///   <item>TargetRoles ← <see cref="ResolveTargetRoles"/> (Category tabanlı mapping)</item>
/// </list>
/// </para>
/// <para>
/// Bu handler asla DROP yapmaz — enrichment sonrası her zaman NextAsync çağrılır.
/// </para>
/// </remarks>
public sealed class MetadataEnricherHandler : BasePipelineHandler
{
    /// <summary>
    /// Criticality hesaplama sabitleri.
    /// </summary>
    /// <remarks>
    /// Magic string'ler yerine sabitler kullanılır (coding_standarts.md §3.3).
    /// </remarks>
    private const string CriticalityHigh = "High";
    private const string CriticalityMedium = "Medium";
    private const string CriticalityLow = "Low";

    private readonly ILogger<MetadataEnricherHandler> _logger;

    /// <summary>
    /// MetadataEnricherHandler'ı gerekli bağımlılıklarla oluşturur.
    /// </summary>
    /// <param name="logger">Loglama bağımlılığı.</param>
    public MetadataEnricherHandler(ILogger<MetadataEnricherHandler> logger)
    {
        _logger = logger;

        _logger.LogInformation("MetadataEnricherHandler başlatıldı.");
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <see cref="EnrichedLog"/> nesnesini oluşturur ve <see cref="PipelineContext.EnrichedLog"/>'a yazar.
    /// Sonraki handler (Step 5 — RouterAndFormatter) bu değeri okur.
    /// Bu handler asla DROP yapmaz.
    /// </remarks>
    public override async Task HandleAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var envelope = context.Envelope;
        var payload = envelope.Payload;

        var enrichedLog = new EnrichedLog
        {
            MessageId        = envelope.MessageId,
            Timestamp        = envelope.Timestamp,
            SenderId         = envelope.SenderId,
            Criticality      = CalculateCriticality(payload.Level),
            Level            = payload.Level,
            Category         = payload.Category,
            SanitizedMessage = payload.Message, // Step 3 (KVKK) maskelemesini tamamlamış
            TargetRoles      = ResolveTargetRoles(payload.Category)
        };

        context.EnrichedLog = enrichedLog;

        _logger.LogDebug(
            "Enrichment tamamlandı — MessageId: {MessageId}, Criticality: {Criticality}, " +
            "TargetRoles: [{Roles}]",
            enrichedLog.MessageId,
            enrichedLog.Criticality,
            string.Join(", ", enrichedLog.TargetRoles));

        await NextAsync(context, cancellationToken); // Bu handler asla DROP yapmaz
    }

    /// <summary>
    /// Log seviyesine göre kritiklik derecesi hesaplar.
    /// </summary>
    /// <param name="level">Log seviyesi.</param>
    /// <returns>Kritiklik string değeri: "High", "Medium" veya "Low".</returns>
    /// <remarks>
    /// Spec §4.3: CRITICAL = "High", ERROR = "Medium", WARN = "Low", INFO = "Low".
    /// </remarks>
    private static string CalculateCriticality(LogLevel level) => level switch
    {
        LogLevel.CRITICAL => CriticalityHigh,
        LogLevel.ERROR    => CriticalityMedium,
        LogLevel.WARN     => CriticalityLow,
        LogLevel.INFO     => CriticalityLow,
        _                 => CriticalityLow
    };

    /// <summary>
    /// Log kategorisine göre hedef rolleri belirler.
    /// </summary>
    /// <param name="category">Log kategorisi.</param>
    /// <returns>Hedef rol listesi. Fan-out için birden fazla rol döndürebilir.</returns>
    /// <remarks>
    /// Spec §5 Step 5 mapping:
    /// <list type="bullet">
    ///   <item>Database → Developer</item>
    ///   <item>Auth → Security</item>
    ///   <item>System → SysAdmin</item>
    /// </list>
    /// Fan-out/Multicast desteği: Gelecekte bir kategorinin birden fazla role yönlendirilmesi
    /// konfigürasyon değişikliğiyle mümkün olacaktır (Phase 6).
    /// </remarks>
    private static IReadOnlyList<TargetRole> ResolveTargetRoles(LogCategory category) => category switch
    {
        LogCategory.Database => [TargetRole.Developer],
        LogCategory.Auth     => [TargetRole.Security],
        LogCategory.System   => [TargetRole.SysAdmin],
        _                    => [TargetRole.Developer] // Fallback — bilinmeyen kategori
    };
}
