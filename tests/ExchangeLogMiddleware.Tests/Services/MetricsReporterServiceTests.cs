namespace ExchangeLogMiddleware.Tests.Services;

using ExchangeLogMiddleware.Middleware.Configuration;
using ExchangeLogMiddleware.Middleware.Pipeline;
using ExchangeLogMiddleware.Middleware.Services;
using ExchangeLogMiddleware.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

/// <summary>
/// <see cref="MetricsReporterService"/> unit testleri.
/// </summary>
public sealed class MetricsReporterServiceTests
{
    private static IOptions<PipelineSettings> CreateOptions(int intervalSeconds = 1)
        => Options.Create(new PipelineSettings { MetricsReportIntervalSeconds = intervalSeconds });

    private static MetricsReporterService CreateService(
        IPerformanceTracker tracker,
        IOptions<PipelineSettings>? options = null,
        ILogger<MetricsReporterService>? logger = null)
    {
        return new MetricsReporterService(
            tracker,
            options ?? CreateOptions(),
            logger ?? Substitute.For<ILogger<MetricsReporterService>>());
    }

    // ─── Metrik okuma doğruluğu ──────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_AfterOneInterval_ReadsTotalReceivedFromTracker()
    {
        // Arrange
        var tracker = Substitute.For<IPerformanceTracker>();
        tracker.TotalReceived.Returns(42);
        tracker.DroppedByFilter.Returns(10);
        tracker.SuccessfullyProcessed.Returns(32);

        var service = CreateService(tracker, CreateOptions(intervalSeconds: 1));
        using var cts = new CancellationTokenSource();

        // Act — servisi başlat, 1 interval + buffer kadar bekle, iptal et
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(1400));
        await cts.CancelAsync();

        try { await service.StopAsync(CancellationToken.None); } catch { /* graceful */ }

        // Assert — tracker property'leri en az bir kez okunmuş olmalı
        var _ = tracker.Received().TotalReceived;
    }

    [Fact]
    public async Task ExecuteAsync_AfterOneInterval_LogsMetricsToLogger()
    {
        // Arrange
        var tracker = new PerformanceTracker();
        tracker.IncrementTotalReceived();
        tracker.IncrementSuccessfullyProcessed();

        var logger = Substitute.For<ILogger<MetricsReporterService>>();
        var service = CreateService(tracker, CreateOptions(intervalSeconds: 1), logger);
        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(1400));
        await cts.CancelAsync();

        try { await service.StopAsync(CancellationToken.None); } catch { /* graceful */ }

        // Assert — [METRICS] log satırı üretilmiş olmalı
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("[METRICS]")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ─── Graceful Shutdown ───────────────────────────────────────────────────

    [Fact]
    public async Task StopAsync_CancelsBackgroundLoop_CompletesCleanly()
    {
        // Arrange
        var tracker = Substitute.For<IPerformanceTracker>();
        var service = CreateService(tracker, CreateOptions(intervalSeconds: 30));
        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await cts.CancelAsync();

        var exception = await Record.ExceptionAsync(
            () => service.StopAsync(CancellationToken.None));

        // Assert — iptal sonrası exception fırlatılmamalı
        Assert.Null(exception);
    }

    // ─── Konfigürasyon doğruluğu ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithTwoSecondInterval_DoesNotLogBeforeInterval()
    {
        // Arrange
        var tracker = Substitute.For<IPerformanceTracker>();
        var logger = Substitute.For<ILogger<MetricsReporterService>>();
        var service = CreateService(tracker, CreateOptions(intervalSeconds: 2), logger);
        using var cts = new CancellationTokenSource();

        // Act — interval'dan kısa süre bekle
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        await cts.CancelAsync();

        try { await service.StopAsync(CancellationToken.None); } catch { /* graceful */ }

        // Assert — 500ms < 2000ms: [METRICS] satırı henüz yazdırılmamış olmalı
        logger.DidNotReceive().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(v => v.ToString()!.Contains("[METRICS]")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
