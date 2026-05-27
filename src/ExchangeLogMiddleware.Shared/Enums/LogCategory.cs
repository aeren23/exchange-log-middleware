namespace ExchangeLogMiddleware.Shared.Enums;

/// <summary>
/// Log mesajının kaynak kategorisi.
/// </summary>
/// <remarks>
/// Router (Phase 6 — Step 5) bu değere göre hedef rolleri belirler:
/// <list type="bullet">
///   <item><see cref="Database"/> → Developer rolüne yönlendirilir.</item>
///   <item><see cref="Auth"/> → Security rolüne yönlendirilir.</item>
///   <item><see cref="System"/> → SysAdmin rolüne yönlendirilir.</item>
/// </list>
/// </remarks>
public enum LogCategory
{
    /// <summary>
    /// Veritabanı operasyonlarına ait loglar (sorgu, bağlantı, transaction).
    /// Developer rolüne yönlendirilir → JSON çıktı.
    /// </summary>
    Database,

    /// <summary>
    /// Kimlik doğrulama ve yetkilendirme operasyonlarına ait loglar.
    /// Security rolüne yönlendirilir → CSV çıktı.
    /// </summary>
    Auth,

    /// <summary>
    /// Sistem seviyesi operasyonlara ait loglar (servis başlatma, kaynak tüketimi).
    /// SysAdmin rolüne yönlendirilir → Markdown çıktı.
    /// </summary>
    System
}
