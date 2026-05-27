namespace ExchangeLogMiddleware.Middleware;

/// <summary>
/// Boilerplate Worker stub — DI'a kayıtlı değildir.
/// </summary>
/// <remarks>
/// Bu sınıf git geçmişi için korunmuştur.
/// Gerçek iş mantığı şu servislere taşındı:
/// <list type="bullet">
///   <item><c>BrokerListenerService</c> — broker aboneliği ve Channel yazma</item>
///   <item><c>PipelineWorkerService</c> — Channel okuma ve pipeline işleme</item>
/// </list>
/// </remarks>
public sealed class Worker : BackgroundService
{
    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.CompletedTask; // Stub — DI'a kayıtlı değil
}
