namespace ExchangeLogMiddleware.Producer.Generators;

using ExchangeLogMiddleware.Shared.Enums;
using ExchangeLogMiddleware.Shared.Models;

/// <summary>
/// Rastgele ve gerçekçi borsa log verisi üreten üretici sınıf.
/// </summary>
/// <remarks>
/// <para>
/// Her çağrıda bir <see cref="LogPayload"/> üretir. Üretim mantığı tamamen bu sınıfta kapsüllenir
/// (SRP — <c>LogGeneratorService</c> yalnızca orkestrasyon yapar).
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
    private static readonly string[] DatabaseMessages =
    [
        "SELECT * FROM dbo.Trades WHERE Symbol='THYAO' executed in 47ms",
        "INSERT INTO dbo.Orders (Symbol, Quantity, Price) executed in 12ms",
        "UPDATE dbo.Positions SET Quantity=500 WHERE AccountId=30421 executed in 8ms",
        "Connection pool exhausted on TradesDB primary replica — queuing request",
        "Deadlock detected between Transaction TX-9821 and TX-9830 on dbo.Positions",
        "Index scan on dbo.MarketData exceeded 2000ms threshold — query plan review required",
        "Database failover initiated — switching to replica node db-replica-02",
        "Bulk insert completed: 15000 tick records loaded into dbo.MarketData in 320ms",
        "Foreign key constraint violation on dbo.Orders.AccountId — rollback executed",
        "Transaction TX-44521 committed successfully: 3 tables affected"
    ];

    private static readonly string[] AuthMessages =
    [
        "User trader@bist.com.tr authenticated via 2FA successfully",
        "Failed login attempt for account manager@exchange.com.tr from IP 192.168.4.22",
        "Session token refreshed for user analyst@borsa.com — TTL extended 30 min",
        "API key revoked for service account svc-market-data due to suspicious activity",
        "Role 'TraderAdmin' assigned to user senior.trader@bist.com.tr by admin",
        "MFA verification failed — account locked after 5 consecutive attempts: user@test.com",
        "OAuth2 token issued for client app 'TradingDashboard' — scope: read:orders",
        "Password reset requested for account ops@exchange.com.tr — verification email sent",
        "Unauthorized access attempt to /api/admin/accounts from IP 10.0.0.45 — blocked",
        "User session expired for trader2@bist.com.tr — re-authentication required"
    ];

    private static readonly string[] SystemMessages =
    [
        "CPU usage: 87.3% — performance threshold exceeded on exchange-middleware-01",
        "Memory allocation: 3.2GB / 4GB — GC pressure increasing",
        "Disk I/O latency: 145ms on /app/output volume — write queue depth: 48",
        "System.Threading.Channels buffer at 85% capacity (850/1000) — backpressure active",
        "RabbitMQ consumer heartbeat missed — reconnecting (attempt 1/3)",
        "Worker thread pool utilization: 92% — queue depth: 234 pending messages",
        "Docker container exchange-middleware RAM limit approaching: 3.8GB / 4GB",
        "Health check passed — RabbitMQ connection stable, latency: 3ms",
        "Graceful shutdown signal received — draining in-flight messages (128 remaining)",
        "Log output rotation triggered — new file segment created in /app/output"
    ];

    private static readonly string[] TcknNumbers =
    [
        "11111111110", // Checksum valid fake TCKN
        "23232323280", // Checksum valid fake TCKN
        "50505050550", // Checksum valid fake TCKN
        "71717171710"  // Checksum valid fake TCKN (fixed from 720)
    ];

    private static readonly string[] CreditCardNumbers =
    [
        "5235 1234 5678 9012",
        "4111 2222 3333 4444",
        "3714 4963 5398 4312", // 16-digit format
        "6011 0000 1234 5678",
        "5555 6666 7777 8888"
    ];

    private static readonly string[] EmailAddresses =
    [
        "trader@bist.com.tr",
        "admin@exchange.com.tr",
        "ops.manager@borsa.com",
        "analyst99@fintech.io"
    ];

    private static readonly string[] PhoneNumbers =
    [
        "+90 5321234567",
        "+90 5451234567",
        "+90 5071234567",
        "+90 5551234567"
    ];

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

    private static LogLevel GetRandomLogLevel(double errorRate)
    {
        if (Random.Shared.NextDouble() < errorRate)
        {
            return Random.Shared.NextDouble() < 0.7
                ? LogLevel.ERROR
                : LogLevel.CRITICAL;
        }

        return Random.Shared.NextDouble() < 0.6
            ? LogLevel.INFO
            : LogLevel.WARN;
    }

    private static string GetRandomMessage(LogCategory category) => category switch
    {
        LogCategory.Database => DatabaseMessages[Random.Shared.Next(DatabaseMessages.Length)],
        LogCategory.Auth     => AuthMessages[Random.Shared.Next(AuthMessages.Length)],
        LogCategory.System   => SystemMessages[Random.Shared.Next(SystemMessages.Length)],
        _                    => "Unknown log category"
    };

    /// <summary>
    /// ~%25 olasılıkla KVKK kapsamında hassas veri içeren bir RawData string'i döner.
    /// TCKN, kredi kartı, e-posta veya telefon numarası enjekte edilir.
    /// </summary>
    private static string? GetSensitiveDataOrNull()
    {
        const double sensitiveDataInjectionRate = 0.25;

        if (Random.Shared.NextDouble() > sensitiveDataInjectionRate)
        {
            return null;
        }

        return Random.Shared.Next(4) switch
        {
            0 => $"Müşteri TCKN: {TcknNumbers[Random.Shared.Next(TcknNumbers.Length)]} ile işlem doğrulandı",
            1 => $"Kart numarası: {CreditCardNumbers[Random.Shared.Next(CreditCardNumbers.Length)]} ile ödeme alındı",
            2 => $"Kullanıcı e-posta: {EmailAddresses[Random.Shared.Next(EmailAddresses.Length)]}",
            _ => $"İletişim: {PhoneNumbers[Random.Shared.Next(PhoneNumbers.Length)]}"
        };
    }
}
