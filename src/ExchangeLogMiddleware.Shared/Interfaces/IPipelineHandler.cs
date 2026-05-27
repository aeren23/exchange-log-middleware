namespace ExchangeLogMiddleware.Shared.Interfaces;

using ExchangeLogMiddleware.Shared.Models;

/// <summary>
/// Chain of Responsibility pattern sözleşmesi — pipeline handler'ları için.
/// </summary>
/// <remarks>
/// <para>
/// Her handler bir <see cref="MessageEnvelope{LogPayload}"/> alır, işler ve uygunsa
/// zincirdeki sonraki handler'a iletir.
/// </para>
/// <para>
/// Pipeline sırası değiştirilemez (Spec §5, Pipeline.md §4):
/// <c>LevelFilter → DeduplicationFilter → KvkkAnonymizer → MetadataEnricher → RouterAndFormatter</c>
/// </para>
/// <para>
/// <strong>DROP mekanizması:</strong> Handler mesajı düşürmek istediğinde
/// sonraki handler'ı çağırmadan <c>return</c> yapar.
/// </para>
/// </remarks>
public interface IPipelineHandler
{
    /// <summary>
    /// Bu handler'ı zincirdeki bir sonraki handler'a bağlar.
    /// Fluent chaining: <c>handler1.SetNext(handler2).SetNext(handler3)</c>
    /// </summary>
    /// <param name="next">Zincirdeki sıradaki handler.</param>
    /// <returns>
    /// Sıradaki handler referansı — fluent zincir kurulumunu destekler.
    /// </returns>
    IPipelineHandler SetNext(IPipelineHandler next);

    /// <summary>
    /// Mesajı işler ve gerekirse zincirdeki sonraki handler'a iletir.
    /// </summary>
    /// <remarks>
    /// Handler mesajı DROPLAYABİLİR (filtreleme veya hata durumunda).
    /// DROP: sonraki handler çağrılmaz, metot döner.
    /// PASS: <c>_nextHandler?.HandleAsync(envelope, cancellationToken)</c> çağrılır.
    /// </remarks>
    /// <param name="envelope">İşlenecek mesaj zarfı.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    Task HandleAsync(
        MessageEnvelope<LogPayload> envelope,
        CancellationToken cancellationToken = default);
}
