namespace ExchangeLogMiddleware.Middleware.Pipeline.Formatters;

using ExchangeLogMiddleware.Middleware.Configuration;
using ExchangeLogMiddleware.Shared.Enums;
using ExchangeLogMiddleware.Shared.Interfaces;
using Microsoft.Extensions.Options;

/// <summary>
/// <see cref="IFormatterFactory"/> implementasyonu.
/// </summary>
public sealed class FormatterFactory : IFormatterFactory
{
    private readonly IEnumerable<IFormatterStrategy> _allStrategies;
    private readonly RouterSettings _routerSettings;

    /// <summary>
    /// DI üzerinden sisteme kayıtlı tüm stratejileri ve ayarları alır.
    /// </summary>
    public FormatterFactory(
        IEnumerable<IFormatterStrategy> allStrategies,
        IOptions<RouterSettings> options)
    {
        _allStrategies = allStrategies;
        _routerSettings = options.Value;
    }

    /// <inheritdoc/>
    public IEnumerable<IFormatterStrategy> GetStrategies(TargetRole role)
    {
        // Tüm stratejiler içinden sadece bu role ait olanları filtrele
        var strategiesForRole = _allStrategies.Where(s => s.TargetRole == role);

        // appsettings.json'da bu rol için hangi formatlar açık? (Örn: SysAdmin -> ["Markdown", "Html"])
        if (_routerSettings.RoleFormatters.TryGetValue(role, out var allowedFormats) && allowedFormats.Count > 0)
        {
            return strategiesForRole.Where(s => 
                allowedFormats.Exists(f => s.GetType().Name.StartsWith(f, StringComparison.OrdinalIgnoreCase)));
        }

        // Eğer konfigürasyonda özel bir ayar yoksa, bu role ait tüm stratejileri döndür (Fallback)
        return strategiesForRole;
    }
}
