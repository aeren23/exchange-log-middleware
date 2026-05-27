namespace ExchangeLogMiddleware.Middleware.Pipeline.Handlers;

using ExchangeLogMiddleware.Shared.Interfaces;
using ExchangeLogMiddleware.Shared.Models;

/// <summary>
/// Chain of Responsibility pattern — tüm pipeline handler'larının miras aldığı abstract base class.
/// </summary>
/// <remarks>
/// <para>
/// Bu sınıf, <see cref="IPipelineHandler"/> sözleşmesindeki tekrarlayan altyapı kodunu
/// (<c>SetNext</c> ve <c>NextAsync</c>) merkezileştirir (DRY prensibi).
/// </para>
/// <para>
/// <strong>DROP mekanizması:</strong> Somut handler mesajı düşürmek istediğinde
/// <see cref="NextAsync"/> metodunu çağırmadan <c>return</c> yapar.
/// </para>
/// <para>
/// <strong>PASS mekanizması:</strong> Somut handler mesajı geçirmek istediğinde
/// <see cref="NextAsync"/> metodunu çağırır.
/// </para>
/// <para>
/// Fluent chaining örneği:
/// <code>
/// handler1.SetNext(handler2).SetNext(handler3);
/// </code>
/// </para>
/// <para>
/// Pipeline sırası (spec §5, pipeline.md §3) kesinlikle değiştirilemez:
/// <c>LevelFilter → DeduplicationFilter → KvkkAnonymizer → MetadataEnricher → RouterAndFormatter</c>
/// </para>
/// </remarks>
public abstract class BasePipelineHandler : IPipelineHandler
{
    private IPipelineHandler? _nextHandler;

    /// <inheritdoc/>
    public IPipelineHandler SetNext(IPipelineHandler next)
    {
        _nextHandler = next;
        return next; // Fluent chaining: handler1.SetNext(handler2).SetNext(handler3)
    }

    /// <inheritdoc/>
    public abstract Task HandleAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Zincirdeki sonraki handler'a context'i iletir.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>PASS:</strong> Somut handler, işlemi başarıyla tamamladıktan sonra bu metodu çağırır.
    /// </para>
    /// <para>
    /// <strong>DROP:</strong> Somut handler bu metodu çağırmadan <c>return</c> yapar.
    /// Böylece mesaj zincirde daha ileri geçmez.
    /// </para>
    /// <para>
    /// Zincirin sonuna gelindiğinde (<c>_nextHandler == null</c>) <c>Task.CompletedTask</c> döner.
    /// </para>
    /// </remarks>
    /// <param name="context">Zincirde iletilecek pipeline bağlam nesnesi.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    protected Task NextAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
        => _nextHandler?.HandleAsync(context, cancellationToken)
           ?? Task.CompletedTask;
}
