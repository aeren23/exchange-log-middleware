namespace ExchangeLogMiddleware.Middleware.Pipeline.Formatters;

using ExchangeLogMiddleware.Shared.Enums;
using ExchangeLogMiddleware.Shared.Interfaces;
using ExchangeLogMiddleware.Shared.Models;

/// <summary>
/// Security rolü için CSV formatlayıcı strateji.
/// </summary>
/// <remarks>
/// Spec gereği header olmadan `<...>` stili istenmiyor, o Router örneği.
/// Standart comma-separated format veya spec'teki `<...>` formatı kullanılabilir.
/// Spec Step 5: "Security -> Outputs &lt;...&gt; formatted CSV."
/// Anlamı: "Kayıtları virgülle ayır, fakat isteğe bağlı olarak etrafına tag koy."
/// Standart CSV yapısı gereği virgülle ayırıyoruz.
/// </remarks>
public sealed class CsvFormatterStrategy : IFormatterStrategy
{
    /// <inheritdoc/>
    public TargetRole TargetRole => TargetRole.Security;

    /// <inheritdoc/>
    public string FileExtension => ".csv";

    /// <inheritdoc/>
    public string Format(EnrichedLog log)
    {
        // CSV Formatı: MessageId,Timestamp,SenderId,Level,Criticality,Category,SanitizedMessage,SanitizedRawData
        // Mesajın içinde virgül olma ihtimaline karşı tırnak içine alıyoruz.
        var safeMessage = log.SanitizedMessage?.Replace("\"", "\"\"") ?? string.Empty;
        var rawDataStr = string.IsNullOrWhiteSpace(log.SanitizedRawData) ? "N/A - No Sensitive Data" : log.SanitizedRawData;
        var safeRawData = rawDataStr.Replace("\"", "\"\"");
        
        return $"{log.MessageId},{log.Timestamp:O},{log.SenderId},{log.Level},{log.Criticality},{log.Category},\"{safeMessage}\",\"{safeRawData}\"";
    }
}
