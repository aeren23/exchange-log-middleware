namespace ExchangeLogMiddleware.Middleware.Pipeline.Handlers;

using System.Text.RegularExpressions;
using ExchangeLogMiddleware.Shared.Models;

/// <summary>
/// Pipeline Step 3 — KVKK Anonymizer (Kişisel Verilerin Korunması).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="LogPayload.Message"/> ve <see cref="LogPayload.RawData"/> alanlarında
/// bulunan hassas verileri (TCKN, kredi kartı, e-posta, telefon) regex ile maskeler.
/// </para>
/// <para>
/// Spec §5 Step 3: "Scans envelope.Payload.Message and envelope.Payload.RawData fields via Regex."
/// </para>
/// <para>
/// Pipeline.md §3: "Privacy must be applied *before* any enrichment or routing
/// to ensure no raw data leaks into output files."
/// </para>
/// <para>
/// <strong>Maskeleme sırası:</strong> TCKN → Kredi Kartı → Email → Telefon.
/// Bu sıra önemlidir: TCKN (11 hane) önce maskelenir, böylece 16 haneli kredi kartı
/// pattern'ı yanlışlıkla TCKN'yi yakalamaz.
/// </para>
/// <para>
/// <strong>TCKN Doğrulama:</strong> Salt 11 haneli rakam kontrolü yerine, Türkiye Cumhuriyeti
/// Kimlik Numarası checksum algoritması uygulanır. Bu sayede borsa işlem numaraları gibi
/// 11 haneli sayıların yanlışlıkla maskelenmesi önlenir.
/// </para>
/// <para>
/// Bu handler asla DROP yapmaz — maskeleme sonrası her zaman NextAsync çağrılır.
/// </para>
/// </remarks>
public sealed class KvkkAnonymizerHandler : BasePipelineHandler
{
    private readonly ILogger<KvkkAnonymizerHandler> _logger;

    /// <summary>
    /// 11 haneli ardışık rakam kalıbı — TCKN adaylarını yakalar.
    /// Gerçek maskeleme <see cref="MaskTckn"/> MatchEvaluator'ında checksum doğrulamasından
    /// geçen adaylara uygulanır.
    /// </summary>
    /// <remarks>
    /// <c>\b</c> word boundary: sayının daha uzun bir rakam dizisinin parçası olmadığını garanti eder.
    /// </remarks>
    private static readonly Regex TcknRegex = new(
        @"\b(\d{11})\b",
        RegexOptions.Compiled);

    /// <summary>
    /// 16 haneli kredi kartı numarası kalıbı — boşluklu ve boşluksuz formatları destekler.
    /// </summary>
    /// <remarks>
    /// Desteklenen formatlar:
    /// <list type="bullet">
    ///   <item><c>5235123456781234</c> (boşluksuz, 16 ardışık rakam)</item>
    ///   <item><c>5235 1234 5678 1234</c> (boşluklu, 4-4-4-4)</item>
    /// </list>
    /// İlk 4 ve son 4 hane korunur, ortadaki 8 hane maskelenir.
    /// </remarks>
    private static readonly Regex CreditCardRegex = new(
        @"\b(\d{4})\s?(\d{4})\s?(\d{4})\s?(\d{4})\b",
        RegexOptions.Compiled);

    /// <summary>
    /// E-posta adresi kalıbı.
    /// İlk karakter ve domain uzantısı korunur, geri kalanı maskelenir.
    /// </summary>
    private static readonly Regex EmailRegex = new(
        @"\b([a-zA-Z0-9])[a-zA-Z0-9._%+\-]*@[a-zA-Z0-9.\-]*\.([a-zA-Z]{2,})\b",
        RegexOptions.Compiled);

    /// <summary>
    /// Türkiye telefon numarası kalıbı (+90 ile başlayan).
    /// Ülke kodu ve ilk rakam korunur, kalan 9 hane maskelenir.
    /// </summary>
    private static readonly Regex PhoneRegex = new(
        @"(\+90\s?)5\d{9}\b",
        RegexOptions.Compiled);

    /// <summary>
    /// KvkkAnonymizerHandler'ı gerekli bağımlılıklarla oluşturur.
    /// </summary>
    /// <param name="logger">Loglama bağımlılığı.</param>
    public KvkkAnonymizerHandler(ILogger<KvkkAnonymizerHandler> logger)
    {
        _logger = logger;

        _logger.LogInformation("KvkkAnonymizerHandler başlatıldı — TCKN checksum doğrulaması aktif.");
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Bu handler asla DROP yapmaz. Maskeleme sonrası her zaman NextAsync çağrılır.
    /// <c>Payload.Message</c> ve <c>Payload.RawData</c> alanları in-place güncellenir.
    /// </remarks>
    public override async Task HandleAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var payload = context.Envelope.Payload;

        // Message alanını maskele
        payload.Message = ApplyAllMaskingPatterns(payload.Message);

        // RawData null değilse maskele
        if (payload.RawData is not null)
        {
            payload.RawData = ApplyAllMaskingPatterns(payload.RawData);
        }

        _logger.LogDebug(
            "KVKK maskeleme tamamlandı — MessageId: {MessageId}",
            context.Envelope.MessageId);

        await NextAsync(context, cancellationToken); // Bu handler asla DROP yapmaz
    }

    /// <summary>
    /// Tüm KVKK maskeleme kalıplarını belirtilen metne sırasıyla uygular.
    /// </summary>
    /// <param name="input">Maskelenecek metin.</param>
    /// <returns>Maskelenmiş metin.</returns>
    /// <remarks>
    /// Maskeleme sırası: TCKN → Kredi Kartı → Email → Telefon.
    /// TCKN önce maskelenir çünkü 11 haneli sayı 16 haneli kredi kartının içinde yer alamaz
    /// (word boundary bunu zaten engelliyor, ancak sıra güvenlik katmanı olarak korunur).
    /// </remarks>
    private static string ApplyAllMaskingPatterns(string input)
    {
        input = TcknRegex.Replace(input, MaskTckn);
        input = CreditCardRegex.Replace(input, "$1 **** **** $4");
        input = EmailRegex.Replace(input, "$1****@****.$2");
        input = PhoneRegex.Replace(input, "+90 5*********");
        return input;
    }

    /// <summary>
    /// TCKN checksum doğrulaması yapan MatchEvaluator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Türkiye Cumhuriyeti Kimlik Numarası kuralları:
    /// <list type="number">
    ///   <item>11 haneli rakam.</item>
    ///   <item>İlk hane 0 olamaz.</item>
    ///   <item>((d1+d3+d5+d7+d9)*7 - (d2+d4+d6+d8)) mod 10 = d10</item>
    ///   <item>(d1+d2+d3+d4+d5+d6+d7+d8+d9+d10) mod 10 = d11</item>
    /// </list>
    /// </para>
    /// <para>
    /// Doğrulamayı geçmeyen 11 haneli sayılar (ör. borsa işlem numaraları) maskelenmez.
    /// </para>
    /// </remarks>
    private static string MaskTckn(Match match)
    {
        var digits = match.Value;

        if (!IsValidTckn(digits))
        {
            return digits; // Geçerli TCKN değil — değişiklik yapma
        }

        // İlk 2 hane korunur, kalan 9 hane maskelenir: "12*********"
        return $"{digits[0]}{digits[1]}*********";
    }

    /// <summary>
    /// TCKN checksum algoritması ile doğrulama yapar.
    /// </summary>
    /// <param name="digits">11 haneli rakam dizisi.</param>
    /// <returns>Geçerli TCKN ise <c>true</c>, değilse <c>false</c>.</returns>
    private static bool IsValidTckn(string digits)
    {
        // Kural 1: İlk hane 0 olamaz
        if (digits[0] == '0')
        {
            return false;
        }

        // Rakamları int dizisine dönüştür (tek seferlik parse — performans)
        Span<int> d = stackalloc int[11];
        for (var i = 0; i < 11; i++)
        {
            d[i] = digits[i] - '0';
        }

        // Kural 2: ((d1+d3+d5+d7+d9)*7 - (d2+d4+d6+d8)) mod 10 = d10
        var oddSum = d[0] + d[2] + d[4] + d[6] + d[8];
        var evenSum = d[1] + d[3] + d[5] + d[7];
        var tenthDigit = (oddSum * 7 - evenSum) % 10;

        // mod sonucu negatif olabilir — pozitife çevirme
        if (tenthDigit < 0)
        {
            tenthDigit += 10;
        }

        if (tenthDigit != d[9])
        {
            return false;
        }

        // Kural 3: (d1+d2+...+d10) mod 10 = d11
        var totalSum = 0;
        for (var i = 0; i < 10; i++)
        {
            totalSum += d[i];
        }

        return totalSum % 10 == d[10];
    }
}
