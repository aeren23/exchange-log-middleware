namespace ExchangeLogMiddleware.Middleware.IO;

using System.Collections.Concurrent;
using ExchangeLogMiddleware.Shared.Interfaces;

/// <summary>
/// <see cref="IFileWriter"/> arayüzünün thread-safe (eşzamanlı kilitli) implementasyonu.
/// </summary>
/// <remarks>
/// <para>
/// Spec §5 Step 5: "Writes concurrently to shared Docker Volumes."
/// Spec §4: "Thread-Safe I/O: File writing in the final step must handle concurrent writes gracefully
/// (e.g., using SemaphoreSlim) since multiple logs might hit the same output file simultaneously."
/// </para>
/// <para>
/// Bu sınıf, her dosya yolu için ayrı bir <see cref="SemaphoreSlim"/> yönetir.
/// Böylece `developer.json` dosyasına yazan bir thread, `security.csv` dosyasına yazan
/// bir diğer thread'i bloklamaz (Fine-grained locking). Ancak aynı dosyaya yazmaya çalışan
/// thread'ler sıraya sokulur.
/// </para>
/// </remarks>
public sealed class ThreadSafeFileWriter : IFileWriter, IDisposable
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task AppendLineAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        // Dosya yoluna ait kilidi (semaphore) al veya oluştur
        var semaphore = _locks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));

        // Aynı dosyaya aynı anda tek bir thread girebilir
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // Dosyanın bulunduğu klasör yoksa oluştur (örn: "output/" dizini)
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.AppendAllTextAsync(filePath, content + Environment.NewLine, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public void Dispose()
    {
        foreach (var semaphore in _locks.Values)
        {
            semaphore.Dispose();
        }
        _locks.Clear();
    }
}
