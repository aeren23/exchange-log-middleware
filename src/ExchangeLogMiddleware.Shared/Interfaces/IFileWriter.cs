namespace ExchangeLogMiddleware.Shared.Interfaces;

/// <summary>
/// Dosya sistemine yazma işlemleri için soyutlama.
/// </summary>
public interface IFileWriter
{
    /// <summary>
    /// Belirtilen dosyaya asenkron ve thread-safe olarak (aynı anda tek thread) içerik ekler.
    /// Sonuna yeni satır (Environment.NewLine) otomatik eklenmelidir.
    /// </summary>
    /// <param name="filePath">Dosya yolu.</param>
    /// <param name="content">Eklenecek içerik.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    Task AppendLineAsync(string filePath, string content, CancellationToken cancellationToken = default);
}
