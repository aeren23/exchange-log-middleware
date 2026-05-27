namespace ExchangeLogMiddleware.Middleware.Pipeline;

using ExchangeLogMiddleware.Shared.Interfaces;

/// <summary>
/// <see cref="IPerformanceTracker"/> arayüzünün thread-safe Singleton implementasyonu.
/// </summary>
/// <remarks>
/// <para>
/// Spec §6.3: "MUST implement a thread-safe Singleton PerformanceTracker injected into the pipeline."
/// </para>
/// <para>
/// Tüm sayaç artırma ve okuma işlemleri <see cref="Interlocked"/> sınıfı aracılığıyla
/// lock-free ve thread-safe şekilde gerçekleştirilir.
/// </para>
/// <para>
/// Raporlama sorumluluğu (SRP): Konsola metrik yazdırma işlemi <c>MetricsReporterService</c>
/// (BackgroundService) tarafından yürütülür. Bu sınıf yalnızca sayaçları yönetir.
/// </para>
/// <para>
/// Sayaç artırım noktaları:
/// <list type="bullet">
///   <item><see cref="IncrementTotalReceived"/> — <c>BrokerListenerService</c> mesajı Channel'a yazdığında.</item>
///   <item><see cref="IncrementDroppedByFilter"/> — Step 1 (<c>LevelFilterHandler</c>) veya Step 2 (<c>DeduplicationFilterHandler</c>) DROP ettiğinde.</item>
///   <item><see cref="IncrementSuccessfullyProcessed"/> — Step 5 (<c>RouterAndFormatterHandler</c>) dosyaya yazmayı tamamladığında.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class PerformanceTracker : IPerformanceTracker
{
    private long _totalReceived;
    private long _droppedByFilter;
    private long _successfullyProcessed;

    /// <inheritdoc/>
    public long TotalReceived => Interlocked.Read(ref _totalReceived);

    /// <inheritdoc/>
    public long DroppedByFilter => Interlocked.Read(ref _droppedByFilter);

    /// <inheritdoc/>
    public long SuccessfullyProcessed => Interlocked.Read(ref _successfullyProcessed);

    /// <inheritdoc/>
    public void IncrementTotalReceived()
        => Interlocked.Increment(ref _totalReceived);

    /// <inheritdoc/>
    public void IncrementDroppedByFilter()
        => Interlocked.Increment(ref _droppedByFilter);

    /// <inheritdoc/>
    public void IncrementSuccessfullyProcessed()
        => Interlocked.Increment(ref _successfullyProcessed);

    /// <inheritdoc/>
    public void Reset()
    {
        Interlocked.Exchange(ref _totalReceived, 0);
        Interlocked.Exchange(ref _droppedByFilter, 0);
        Interlocked.Exchange(ref _successfullyProcessed, 0);
    }
}
