namespace ExchangeLogMiddleware.Shared.Interfaces;

using ExchangeLogMiddleware.Shared.Enums;

/// <summary>
/// Strategy Pattern Fabrikası.
/// Hedef role göre uygun formatter stratejilerini döndürür.
/// </summary>
public interface IFormatterFactory
{
    /// <summary>
    /// Belirtilen hedef rol (<see cref="TargetRole"/>) için yapılandırılmış tüm formatter stratejilerini döndürür.
    /// Konfigürasyon (<c>appsettings.json</c>) üzerinden hangi stratejilerin aktif olduğuna karar verir.
    /// </summary>
    /// <param name="role">Stratejileri getirilecek hedef rol.</param>
    /// <returns>Aktif formatter stratejileri listesi.</returns>
    IEnumerable<IFormatterStrategy> GetStrategies(TargetRole role);
}
