namespace ExchangeLogMiddleware.Shared.Enums;

/// <summary>
/// Formatter ve Router tarafından kullanılan çıktı hedef rolleri.
/// Her rol, farklı bir dosya formatında çıktı üretir (Strategy Pattern).
/// </summary>
/// <remarks>
/// <para>
/// Mevcut format atamaları:
/// <list type="bullet">
///   <item><see cref="Developer"/> → JSON (.json) — makine okunabilir, hata ayıklama.</item>
///   <item><see cref="Security"/> → CSV (.csv) — tablo formatı, SIEM sistemleriyle uyumlu.</item>
///   <item><see cref="SysAdmin"/> → Markdown (.md) — insan okunabilir rapor formatı.</item>
/// </list>
/// </para>
/// <para>
/// [ARCHITECTURAL DECISION — 2026-05-27]
/// SysAdmin çıktısının esnek olması istenmektedir. Sistemin konfigürasyon üzerinden hem Markdown (.md)
/// hem de HTML (.html) üretebilmesi (veya ikisini aynı anda üretebilmesi) hedeflenmektedir.
/// Gelecekte <c>HtmlFormatterStrategy</c> eklenecek ve Router birden fazla stratejiyi destekleyecek şekilde
/// (OCP uyumlu) genişletilecektir.
/// </para>
/// </remarks>
public enum TargetRole
{
    /// <summary>Geliştirici rolü. JSON formatında çıktı alır (.json).</summary>
    Developer,

    /// <summary>Güvenlik rolü. CSV formatında çıktı alır (.csv).</summary>
    Security,

    /// <summary>
    /// Sistem yöneticisi rolü. Şu an Markdown (.md) formatında çıktı alır.
    /// Not: Mimari olarak hem Markdown hem de HTML formatını destekleyecek ve konfigürasyonla seçilebilir (hatta ikisi birden üretilebilir) bir yapı hedeflenmektedir.
    /// </summary>
    SysAdmin
}
