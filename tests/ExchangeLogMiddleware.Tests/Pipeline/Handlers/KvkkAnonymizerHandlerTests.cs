namespace ExchangeLogMiddleware.Tests.Pipeline.Handlers;

using ExchangeLogMiddleware.Middleware.Pipeline.Handlers;
using ExchangeLogMiddleware.Shared.Interfaces;
using ExchangeLogMiddleware.Shared.Models;
using ExchangeLogMiddleware.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using LogLevel = ExchangeLogMiddleware.Shared.Enums.LogLevel;

/// <summary>
/// <see cref="KvkkAnonymizerHandler"/> (Step 3) unit testleri.
/// </summary>
public sealed class KvkkAnonymizerHandlerTests
{
    private readonly KvkkAnonymizerHandler _handler;
    private readonly IPipelineHandler _nextHandler;

    public KvkkAnonymizerHandlerTests()
    {
        var logger = Substitute.For<ILogger<KvkkAnonymizerHandler>>();
        _handler = new KvkkAnonymizerHandler(logger);
        _nextHandler = Substitute.For<IPipelineHandler>();
        _handler.SetNext(_nextHandler);
    }

    // ─── TCKN Maskeleme ───

    [Fact]
    public async Task HandleAsync_ValidTckn_MasksCorrectly()
    {
        // 10000000146 geçerli bir TCKN'dir (checksum doğru)
        var context = TestDataFactory.CreateContext(
            message: "Kullanıcı TCKN: 10000000146 ile giriş yaptı");

        await _handler.HandleAsync(context);

        Assert.Equal("Kullanıcı TCKN: 10********* ile giriş yaptı", context.Envelope.Payload.Message);
    }

    [Fact]
    public async Task HandleAsync_InvalidTcknChecksum_DoesNotMask()
    {
        // 12345678900 — checksum yanlış, maskelenmemeli
        var context = TestDataFactory.CreateContext(
            message: "İşlem numarası: 12345678900");

        await _handler.HandleAsync(context);

        Assert.Equal("İşlem numarası: 12345678900", context.Envelope.Payload.Message);
    }

    [Fact]
    public async Task HandleAsync_ValidTcknInRawData_MasksCorrectly()
    {
        var context = TestDataFactory.CreateContext(
            message: "Normal mesaj",
            rawData: "TCKN: 10000000146");

        await _handler.HandleAsync(context);

        Assert.Equal("TCKN: 10*********", context.Envelope.Payload.RawData);
    }

    // ─── Kredi Kartı Maskeleme ───

    [Fact]
    public async Task HandleAsync_CreditCardWithoutSpaces_MasksCorrectly()
    {
        var context = TestDataFactory.CreateContext(
            message: "Kart no: 5235123456781234 ile ödeme yapıldı");

        await _handler.HandleAsync(context);

        Assert.Equal("Kart no: 5235 **** **** 1234 ile ödeme yapıldı", context.Envelope.Payload.Message);
    }

    [Fact]
    public async Task HandleAsync_CreditCardWithSpaces_MasksCorrectly()
    {
        var context = TestDataFactory.CreateContext(
            message: "Kart: 5235 1234 5678 1234 onaylandı");

        await _handler.HandleAsync(context);

        Assert.Equal("Kart: 5235 **** **** 1234 onaylandı", context.Envelope.Payload.Message);
    }

    // ─── Email Maskeleme ───

    [Fact]
    public async Task HandleAsync_Email_MasksCorrectly()
    {
        var context = TestDataFactory.CreateContext(
            message: "Email: ahmet.yilmaz@gmail.com adresine bildirim gönderildi");

        await _handler.HandleAsync(context);

        Assert.Equal("Email: a****@****.com adresine bildirim gönderildi", context.Envelope.Payload.Message);
    }

    [Fact]
    public async Task HandleAsync_ShortEmail_MasksCorrectly()
    {
        var context = TestDataFactory.CreateContext(
            message: "Giriş: a@b.co yapıldı");

        await _handler.HandleAsync(context);

        Assert.Equal("Giriş: a****@****.co yapıldı", context.Envelope.Payload.Message);
    }

    // ─── Telefon Maskeleme ───

    [Fact]
    public async Task HandleAsync_PhoneNumber_MasksCorrectly()
    {
        var context = TestDataFactory.CreateContext(
            message: "Telefon: +90 5321234567 numarasına SMS gönderildi");

        await _handler.HandleAsync(context);

        Assert.Equal("Telefon: +90 5********* numarasına SMS gönderildi", context.Envelope.Payload.Message);
    }

    [Fact]
    public async Task HandleAsync_PhoneNumberWithoutSpace_MasksCorrectly()
    {
        var context = TestDataFactory.CreateContext(
            message: "Tel: +905321234567 arandı");

        await _handler.HandleAsync(context);

        Assert.Equal("Tel: +90 5********* arandı", context.Envelope.Payload.Message);
    }

    // ─── Birden Fazla Hassas Veri ───

    [Fact]
    public async Task HandleAsync_MultiplePatterns_MasksAll()
    {
        var context = TestDataFactory.CreateContext(
            message: "TCKN: 10000000146, Kart: 5235123456781234, Mail: test@example.com, Tel: +90 5551234567");

        await _handler.HandleAsync(context);

        var expected = "TCKN: 10*********, Kart: 5235 **** **** 1234, Mail: t****@****.com, Tel: +90 5*********";
        Assert.Equal(expected, context.Envelope.Payload.Message);
    }

    // ─── Edge Case'ler ───

    [Fact]
    public async Task HandleAsync_NoSensitiveData_MessageUnchanged()
    {
        var originalMessage = "Normal borsa işlemi tamamlandı. Hisse: THYAO, Fiyat: 125.50 TL";
        var context = TestDataFactory.CreateContext(message: originalMessage);

        await _handler.HandleAsync(context);

        Assert.Equal(originalMessage, context.Envelope.Payload.Message);
    }

    [Fact]
    public async Task HandleAsync_NullRawData_DoesNotThrow()
    {
        var context = TestDataFactory.CreateContext(
            message: "Test mesajı",
            rawData: null);

        var exception = await Record.ExceptionAsync(() => _handler.HandleAsync(context));

        Assert.Null(exception);
    }

    [Fact]
    public async Task HandleAsync_AlwaysCallsNextHandler()
    {
        // KVKK anonymizer asla DROP yapmaz
        var context = TestDataFactory.CreateContext(message: "Any message");

        await _handler.HandleAsync(context);

        await _nextHandler.Received(1).HandleAsync(
            context,
            Arg.Any<CancellationToken>());
    }
}
