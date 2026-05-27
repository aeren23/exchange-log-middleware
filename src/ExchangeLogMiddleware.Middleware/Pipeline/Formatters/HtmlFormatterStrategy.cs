namespace ExchangeLogMiddleware.Middleware.Pipeline.Formatters;

using System.Text;
using ExchangeLogMiddleware.Shared.Enums;
using ExchangeLogMiddleware.Shared.Interfaces;
using ExchangeLogMiddleware.Shared.Models;

/// <summary>
/// SysAdmin rolü için HTML formatlayıcı strateji.
/// [ARCHITECTURAL DECISION — 2026-05-27] Markdown ile eşzamanlı çalışabilir.
/// </summary>
public sealed class HtmlFormatterStrategy : IFormatterStrategy
{
    /// <inheritdoc/>
    public TargetRole TargetRole => TargetRole.SysAdmin;

    /// <inheritdoc/>
    public string FileExtension => ".html";

    /// <inheritdoc/>
    public string Format(EnrichedLog log)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<div class=\"log-entry\" style=\"border-bottom: 1px solid #ccc; padding: 10px;\">");
        sb.AppendLine($"  <div><strong>MessageId:</strong> <code>{log.MessageId}</code></div>");
        sb.AppendLine($"  <div><strong>Timestamp:</strong> <span>{log.Timestamp:O}</span></div>");
        sb.AppendLine($"  <div><strong>SenderId:</strong> <span>{log.SenderId}</span></div>");
        sb.AppendLine($"  <div><strong>Level:</strong> <span class=\"level-{log.Level.ToString().ToLower()}\">{log.Level}</span></div>");
        sb.AppendLine($"  <div><strong>Criticality:</strong> <span>{log.Criticality}</span></div>");
        sb.AppendLine($"  <div><strong>Category:</strong> <span>{log.Category}</span></div>");
        sb.AppendLine($"  <blockquote style=\"background: #f9f9f9; padding: 10px;\">{log.SanitizedMessage}</blockquote>");
        sb.AppendLine("</div>");
        
        return sb.ToString();
    }
}
