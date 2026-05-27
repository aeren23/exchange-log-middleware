namespace ExchangeLogMiddleware.Shared.Interfaces;

/// <summary>
/// Thread-safe performans metrik izleme sözleşmesi.
/// </summary>
/// <remarks>
/// <para>
/// DI container'a Singleton yaşam döngüsüyle kaydedilir.
/// Tüm sayaç artırma işlemleri <c>Interlocked</c> sınıfı ile thread-safe gerçekleştirilir.
/// Spec §6.3: "MUST implement a thread-safe Singleton PerformanceTracker injected into the pipeline."
/// </para>
/// <para>
/// Sayaç artırım noktaları:
/// <list type="bullet">
///   <item><see cref="IncrementTotalReceived"/> — <c>RabbitMqAdapter</c>'da mesaj alındığında.</item>
///   <item><see cref="IncrementDroppedByFilter"/> — Step 1 (LevelFilter) veya Step 2 (DeduplicationFilter) DROP ettiğinde.</item>
///   <item><see cref="IncrementSuccessfullyProcessed"/> — Step 5 dosyaya yazma başarıyla tamamlandığında.</item>
/// </list>
/// </para>
/// <para>
/// Raporlama: Bir background timer her 5 saniyede bir metrikleri konsola yazar.
/// </para>
/// </remarks>
public interface IPerformanceTracker
{
    /// <summary>Adapter tarafından alınan toplam mesaj sayısı (thread-safe okuma).</summary>
    long TotalReceived { get; }

    /// <summary>
    /// Step 1 ve Step 2 tarafından düşürülen toplam mesaj sayısı (thread-safe okuma).
    /// </summary>
    long DroppedByFilter { get; }

    /// <summary>
    /// Step 5 sonrası başarıyla dosyaya yazılan mesaj sayısı (thread-safe okuma).
    /// </summary>
    long SuccessfullyProcessed { get; }

    /// <summary>
    /// <see cref="TotalReceived"/> sayacını atomik olarak 1 artırır.
    /// <c>RabbitMqAdapter.SubscribeAsync</c> callback'inde çağrılır.
    /// </summary>
    void IncrementTotalReceived();

    /// <summary>
    /// <see cref="DroppedByFilter"/> sayacını atomik olarak 1 artırır.
    /// <c>LevelFilterHandler</c> veya <c>DeduplicationFilterHandler</c> tarafından çağrılır.
    /// </summary>
    void IncrementDroppedByFilter();

    /// <summary>
    /// <see cref="SuccessfullyProcessed"/> sayacını atomik olarak 1 artırır.
    /// <c>RouterAndFormatterHandler</c> dosyaya yazmayı tamamladığında çağrılır.
    /// </summary>
    void IncrementSuccessfullyProcessed();

    /// <summary>
    /// Tüm sayaçları sıfırlar. Test senaryoları için kullanılır.
    /// </summary>
    void Reset();
}
