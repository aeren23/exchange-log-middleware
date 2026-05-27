namespace ExchangeLogMiddleware.Middleware.Pipeline;

using ExchangeLogMiddleware.Shared.Interfaces;
using ExchangeLogMiddleware.Shared.Models;

/// <summary>
/// Pipeline handler zincirini kuran ve mesajları zincirin başına ileten orkestratör.
/// </summary>
/// <remarks>
/// <para>
/// DI container'dan sıralı <see cref="IPipelineHandler"/> listesi alır ve
/// bu listeden Chain of Responsibility zinciri oluşturur.
/// </para>
/// <para>
/// Handler kayıt sırası kesinlikle şu şekilde olmalıdır (spec §5):
/// <list type="number">
///   <item><c>LevelFilterHandler</c></item>
///   <item><c>DeduplicationFilterHandler</c></item>
///   <item><c>KvkkAnonymizerHandler</c></item>
///   <item><c>MetadataEnricherHandler</c></item>
///   <item><c>RouterAndFormatterHandler</c></item>
/// </list>
/// Handler'ların DI'a kayıt sırası, zincir sırasını belirler.
/// </para>
/// <para>
/// Singleton yaşam döngüsüyle kaydedilir — zincir bir kez kurulur, yeniden kullanılır.
/// </para>
/// </remarks>
public sealed class PipelineOrchestrator
{
    private readonly IPipelineHandler _pipelineHead;
    private readonly ILogger<PipelineOrchestrator> _logger;

    /// <summary>
    /// Handler listesinden Chain of Responsibility zincirini kurar.
    /// </summary>
    /// <param name="handlers">
    /// DI'dan çözümlenen handler listesi. Kayıt sırası zincir sırasını belirler.
    /// </param>
    /// <param name="logger">Loglama bağımlılığı.</param>
    /// <exception cref="InvalidOperationException">
    /// Handler listesi boşsa fırlatılır.
    /// </exception>
    public PipelineOrchestrator(
        IEnumerable<IPipelineHandler> handlers,
        ILogger<PipelineOrchestrator> logger)
    {
        _logger = logger;

        var handlerList = handlers.ToList();

        if (handlerList.Count == 0)
        {
            throw new InvalidOperationException(
                "Pipeline'da en az bir IPipelineHandler kaydı bulunmalıdır. " +
                "Program.cs DI kayıtlarını kontrol edin.");
        }

        // Zinciri kur: handler[0] → handler[1] → ... → handler[n-1]
        for (var i = 0; i < handlerList.Count - 1; i++)
        {
            handlerList[i].SetNext(handlerList[i + 1]);
        }

        _pipelineHead = handlerList[0];

        _logger.LogInformation(
            "Pipeline zinciri kuruldu — {HandlerCount} handler, baş: {HeadHandler}",
            handlerList.Count,
            _pipelineHead.GetType().Name);
    }

    /// <summary>
    /// Verilen mesaj zarfı için <see cref="PipelineContext"/> oluşturur ve pipeline zincirinin
    /// başından işlemeye başlar.
    /// </summary>
    /// <remarks>
    /// Her handler kendi sorumluluğunu yerine getirir:
    /// <list type="bullet">
    ///   <item>DROP: Handler mesajı düşürür, zincir durur.</item>
    ///   <item>PASS: Handler sonrakini çağırır, zincir devam eder.</item>
    /// </list>
    /// </remarks>
    /// <param name="envelope">İşlenecek mesaj zarfı.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    public Task ProcessAsync(
        MessageEnvelope<LogPayload> envelope,
        CancellationToken cancellationToken = default)
    {
        var context = new PipelineContext(envelope);
        return _pipelineHead.HandleAsync(context, cancellationToken);
    }
}
