namespace ExchangeLogMiddleware.Middleware.Pipeline.Formatters;

using System.Text.Json;
using ExchangeLogMiddleware.Shared.Enums;
using ExchangeLogMiddleware.Shared.Interfaces;
using ExchangeLogMiddleware.Shared.Models;

/// <summary>
/// Developer rolü için JSON formatlayıcı strateji.
/// </summary>
public sealed class JsonFormatterStrategy : IFormatterStrategy
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false // Performans ve tek satırda log yazımı için
    };

    /// <inheritdoc/>
    public TargetRole TargetRole => TargetRole.Developer;

    /// <inheritdoc/>
    public string FileExtension => ".json";

    /// <inheritdoc/>
    public string Format(EnrichedLog log)
    {
        return JsonSerializer.Serialize(log, Options);
    }
}
