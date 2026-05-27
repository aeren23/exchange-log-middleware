namespace ExchangeLogMiddleware.Shared.Enums;

/// <summary>
/// Log mesajının önem derecesi.
/// </summary>
/// <remarks>
/// Sayısal değerler kasıtlı olarak sıralıdır: INFO &lt; WARN &lt; ERROR &lt; CRITICAL.
/// Bu sıralama, <c>LevelFilterHandler</c>'da doğrudan karşılaştırma yapılmasını sağlar
/// (örn. <c>if (payload.Level &lt; minimumLevel) DROP</c>) — ek mapping tablosuna gerek kalmaz.
/// </remarks>
public enum LogLevel
{
    /// <summary>Bilgilendirme amaçlı log. Düşük öncelikli, yoğun üretim ortamında filtrelenebilir.</summary>
    INFO = 0,

    /// <summary>Uyarı niteliğinde log. İzlenmesi önerilen, henüz hata oluşmamış durumlar.</summary>
    WARN = 1,

    /// <summary>İşlenebilen hata durumu. Müdahale gerektirir.</summary>
    ERROR = 2,

    /// <summary>Kritik sistem hatası. Acil müdahale gerektirir.</summary>
    CRITICAL = 3
}
