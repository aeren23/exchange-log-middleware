namespace ExchangeLogMiddleware.Middleware.Pipeline.Formatters;

using System.Text;
using ExchangeLogMiddleware.Shared.Enums;
using ExchangeLogMiddleware.Shared.Interfaces;
using ExchangeLogMiddleware.Shared.Models;

/// <summary>
/// SysAdmin rolü için Markdown formatlayıcı strateji.
/// </summary>
public sealed class MarkdownFormatterStrategy : IFormatterStrategy
{
    /// <inheritdoc/>
    public TargetRole TargetRole => TargetRole.SysAdmin;

    /// <inheritdoc/>
    public string FileExtension => ".md";

    /// <inheritdoc/>
    public string Format(EnrichedLog log)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"**MessageId:** `{log.MessageId}`  ");
        sb.AppendLine($"**Timestamp:** `{log.Timestamp:O}`  ");
        sb.AppendLine($"**SenderId:** `{log.SenderId}`  ");
        sb.AppendLine($"**Level:** `{log.Level}`  ");
        sb.AppendLine($"**Criticality:** `{log.Criticality}`  ");
        sb.AppendLine($"**Category:** `{log.Category}`  ");
        sb.AppendLine("> " + log.SanitizedMessage);
        
        sb.AppendLine();
        sb.AppendLine("**Raw Data:**");
        if (!string.IsNullOrEmpty(log.SanitizedRawData))
        {
            sb.AppendLine("```text");
            sb.AppendLine(log.SanitizedRawData);
            sb.AppendLine("```");
        }
        else
        {
            sb.AppendLine("`N/A - No Sensitive Data`");
        }
        
        return sb.ToString();
    }
}
