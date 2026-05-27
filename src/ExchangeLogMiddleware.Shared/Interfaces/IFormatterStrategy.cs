namespace ExchangeLogMiddleware.Shared.Interfaces;

using ExchangeLogMiddleware.Shared.Enums;
using ExchangeLogMiddleware.Shared.Models;

/// <summary>
/// Strategy Pattern sözleşmesi — role göre farklı formatlarda log çıktısı üretir.
/// </summary>
/// <remarks>
/// <para>
/// Mevcut implementasyonlar (Phase 6):
/// <list type="bullet">
///   <item><c>JsonFormatterStrategy</c> → <see cref="TargetRole.Developer"/> → .json</item>
///   <item><c>CsvFormatterStrategy</c> → <see cref="TargetRole.Security"/> → .csv</item>
///   <item><c>MarkdownFormatterStrategy</c> → <see cref="TargetRole.SysAdmin"/> → .md</item>
/// </list>
/// </para>
/// <para>
/// [ARCHITECTURAL DECISION — 2026-05-27] SysAdmin çıktısı konfigüre edilebilir olmalıdır.
/// Mevcut implementasyon Markdown desteklese de gelecekte HTML desteği (<c>HtmlFormatterStrategy</c>) 
/// eklenecektir. Sistem her ikisini de (veya aynı anda ikisini) destekleyecek esnekliktedir.
/// OCP prensibi gereği yeni format eklemek yalnızca yeni bir sınıf gerektirir — pipeline değişmez.
/// </para>
/// </remarks>
public interface IFormatterStrategy
{
    /// <summary>
    /// Bu formatter'ın hangi hedef rol için geçerli olduğunu belirtir.
    /// <c>FormatterFactory</c> bu özelliği doğru stratejiyi seçmek için kullanır.
    /// </summary>
    TargetRole TargetRole { get; }

    /// <summary>
    /// Çıktı dosyasının uzantısı (örn. <c>".json"</c>, <c>".csv"</c>, <c>".md"</c>).
    /// Dosya adı <c>{OutputBasePath}/{TargetRole}{FileExtension}</c> şeklinde oluşturulur.
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// <see cref="EnrichedLog"/> nesnesini hedef role özgü formata dönüştürür.
    /// Bu metot I/O işlemi yapmaz — sadece string çıktı üretir.
    /// Dosyaya yazma işlemi <c>RouterAndFormatterHandler</c> tarafından yapılır.
    /// </summary>
    /// <param name="log">Formatlanacak zenginleştirilmiş log nesnesi.</param>
    /// <returns>Formatlı string çıktı — dosyaya yazılmaya hazır.</returns>
    string Format(EnrichedLog log);
}
