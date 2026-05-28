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
        
        if (!string.IsNullOrEmpty(log.SanitizedRawData))
        {
            sb.AppendLine();
            sb.AppendLine("**Raw Data:**");
            sb.AppendLine("```text");
            sb.AppendLine(log.SanitizedRawData);
            sb.AppendLine("```");
        }
        
        return sb.ToString();
    }
}
