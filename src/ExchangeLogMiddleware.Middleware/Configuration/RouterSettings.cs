namespace ExchangeLogMiddleware.Middleware.Configuration;

using ExchangeLogMiddleware.Shared.Enums;

/// <summary>
/// Phase 6: Router & Formatter (Step 5) konfigürasyonu.
/// </summary>
public sealed class RouterSettings
{
    public const string SectionName = "RouterSettings";

    /// <summary>
    /// Log kategorisini hedef rollere (TargetRole) haritalar.
    /// Örn: "Database" -> ["Developer"]
    /// </summary>
    public Dictionary<LogCategory, List<TargetRole>> CategoryRoutes { get; init; } = [];

    /// <summary>
    /// Hedef rollerin kullanacağı formatter stratejilerini (isim olarak) haritalar.
    /// Örn: "SysAdmin" -> ["Markdown", "Html"]
    /// Formatter sınıfının ismindeki "FormatterStrategy" kısmı atılarak eşleştirilir (örn. "MarkdownFormatterStrategy" -> "Markdown").
    /// </summary>
    public Dictionary<TargetRole, List<string>> RoleFormatters { get; init; } = [];

    /// <summary>
    /// Log dosyalarının yazılacağı temel dizin yolu.
    /// </summary>
    public string OutputDirectory { get; init; } = "output";
}
