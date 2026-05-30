using ExchangeLogMiddleware.Producer.Configuration;
using ExchangeLogMiddleware.Shared.Enums;
using ExchangeLogMiddleware.Shared.Models;
using Microsoft.Extensions.Options;

namespace ExchangeLogMiddleware.Producer.Generators;

/// <summary>
/// Rastgele ve gerçekçi borsa log verisi üreten üretici sınıf.
/// </summary>
/// <remarks>
/// <para>
/// Her çağrıda bir <see cref="LogPayload"/> üretir. Üretim mantığı tamamen bu sınıfta kapsüllenir
/// (SRP — <c>LogGeneratorService</c> yalnızca orkestrasyon yapar).
/// Test verileri (seed-data.json) <see cref="SeedDataOptions"/> üzerinden dependency injection ile alınır (OCP).
/// </para>
/// <para>
/// Log seviyesi dağılımı <paramref name="errorRate"/> parametresine göre belirlenir:
/// <list type="bullet">
///   <item>Normal band (1 - errorRate): INFO %60, WARN %40</item>
///   <item>Hata bandı (errorRate): ERROR %70, CRITICAL %30</item>
/// </list>
/// Hassas veri enjeksiyonu ~%25 olasılıkla gerçekleşir (KVKK anonymizer testleri için).
/// </para>
/// </remarks>
public sealed class LogDataGenerator
{
    private readonly SeedDataOptions _seedData;

    public LogDataGenerator(IOptions<SeedDataOptions> options)
    {
        _seedData = options.Value;
    }

    /// <summary>
    /// Konfigürasyona uygun rastgele bir <see cref="LogPayload"/> üretir.
    /// </summary>
    /// <param name="errorRate">ERROR/CRITICAL üretim oranı (0.0 - 1.0).</param>
    /// <returns>Pipeline'a gönderilmeye hazır log verisi.</returns>
    public LogPayload GenerateLogPayload(double errorRate)
    {
        var category = GetRandomCategory();
        var level = GetRandomLogLevel(errorRate);
        var message = GetRandomMessage(category);
        var rawData = GetSensitiveDataOrNull();

        return new LogPayload
        {
            Level = level,
            Category = category,
            Message = message,
            RawData = rawData
        };
    }

    private static LogCategory GetRandomCategory()
    {
        var values = Enum.GetValues<LogCategory>();
        return values[Random.Shared.Next(values.Length)];
    }

    private static ExchangeLogMiddleware.Shared.Enums.LogLevel GetRandomLogLevel(double errorRate)
    {
        if (Random.Shared.NextDouble() < errorRate)
        {
            return Random.Shared.NextDouble() < 0.7
                ? ExchangeLogMiddleware.Shared.Enums.LogLevel.ERROR
                : ExchangeLogMiddleware.Shared.Enums.LogLevel.CRITICAL;
        }

        return Random.Shared.NextDouble() < 0.6
            ? ExchangeLogMiddleware.Shared.Enums.LogLevel.INFO
            : ExchangeLogMiddleware.Shared.Enums.LogLevel.WARN;
    }

    private string GetRandomMessage(LogCategory category) => category switch
    {
        LogCategory.Database => _seedData.DatabaseMessages[Random.Shared.Next(_seedData.DatabaseMessages.Length)],
        LogCategory.Auth     => _seedData.AuthMessages[Random.Shared.Next(_seedData.AuthMessages.Length)],
        LogCategory.System   => _seedData.SystemMessages[Random.Shared.Next(_seedData.SystemMessages.Length)],
        _                    => "Unknown log category"
    };

    /// <summary>
    /// ~%25 olasılıkla KVKK kapsamında hassas veri içeren bir RawData string'i döner.
    /// TCKN, kredi kartı, e-posta veya telefon numarası enjekte edilir.
    /// </summary>
    private string? GetSensitiveDataOrNull()
    {
        const double sensitiveDataInjectionRate = 0.25;

        if (Random.Shared.NextDouble() > sensitiveDataInjectionRate)
        {
            return null;
        }

        return Random.Shared.Next(4) switch
        {
            0 => $"Müşteri TCKN: {_seedData.TcknNumbers[Random.Shared.Next(_seedData.TcknNumbers.Length)]} ile işlem doğrulandı",
            1 => $"Kart numarası: {_seedData.CreditCardNumbers[Random.Shared.Next(_seedData.CreditCardNumbers.Length)]} ile ödeme alındı",
            2 => $"Kullanıcı e-posta: {_seedData.EmailAddresses[Random.Shared.Next(_seedData.EmailAddresses.Length)]}",
            _ => $"İletişim: {_seedData.PhoneNumbers[Random.Shared.Next(_seedData.PhoneNumbers.Length)]}"
        };
    }
}
