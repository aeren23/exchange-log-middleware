namespace ExchangeLogMiddleware.Middleware.Pipeline;

using ExchangeLogMiddleware.Shared.Interfaces;

/// <summary>
/// <see cref="IPerformanceTracker"/> arayüzünün geçici (stub) implementasyonu.
/// </summary>
/// <remarks>
/// <para>
/// Phase 7'de gerçek <c>PerformanceTracker</c> Singleton implementasyonu oluşturulana kadar
/// kullanılır. Tüm sayaç artırma işlemleri <see cref="System.Threading.Interlocked"/> ile
/// thread-safe gerçekleştirilir — Phase 7'ye hazırlık.
/// </para>
/// <para>
/// Bu stub, raporlama özelliği içermez (konsola metrik yazdırmaz).
/// Phase 7 sorumluluğu: "A background timer MUST log these metrics to the console every 5 seconds."
/// </para>
/// <para>
/// DI container'a Singleton yaşam döngüsüyle kaydedilir.
/// </para>
/// </remarks>
public sealed class NoOpPerformanceTracker : IPerformanceTracker
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
