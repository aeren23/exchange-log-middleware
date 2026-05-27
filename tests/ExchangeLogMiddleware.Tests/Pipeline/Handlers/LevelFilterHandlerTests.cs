namespace ExchangeLogMiddleware.Tests.Pipeline.Handlers;

using ExchangeLogMiddleware.Middleware.Configuration;
using ExchangeLogMiddleware.Middleware.Pipeline;
using ExchangeLogMiddleware.Middleware.Pipeline.Handlers;
using ExchangeLogMiddleware.Shared.Interfaces;
using ExchangeLogMiddleware.Shared.Models;
using ExchangeLogMiddleware.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using LogLevel = ExchangeLogMiddleware.Shared.Enums.LogLevel;

/// <summary>
/// <see cref="LevelFilterHandler"/> (Step 1) unit testleri.
/// </summary>
public sealed class LevelFilterHandlerTests
{
    private readonly IPerformanceTracker _tracker;
    private readonly ILogger<LevelFilterHandler> _logger;

    public LevelFilterHandlerTests()
    {
#pragma warning disable CS0618
        _tracker = new NoOpPerformanceTracker();
#pragma warning restore CS0618
        _logger = Substitute.For<ILogger<LevelFilterHandler>>();
    }

    private LevelFilterHandler CreateHandler(string minimumLevel = "ERROR")
    {
        var options = Options.Create(new PipelineSettings { MinimumLogLevel = minimumLevel });
        return new LevelFilterHandler(options, _tracker, _logger);
    }

    [Theory]
    [InlineData(LogLevel.INFO)]
    [InlineData(LogLevel.WARN)]
    public async Task HandleAsync_LevelBelowMinimum_DropsMessage(LogLevel level)
    {
        // Arrange
        var handler = CreateHandler("ERROR");
        var nextHandler = Substitute.For<IPipelineHandler>();
        handler.SetNext(nextHandler);
        var context = TestDataFactory.CreateContext(level: level);

        // Act
        await handler.HandleAsync(context);

        // Assert
        await nextHandler.DidNotReceive().HandleAsync(
            Arg.Any<PipelineContext>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(LogLevel.ERROR)]
    [InlineData(LogLevel.CRITICAL)]
    public async Task HandleAsync_LevelAtOrAboveMinimum_PassesMessage(LogLevel level)
    {
        // Arrange
        var handler = CreateHandler("ERROR");
        var nextHandler = Substitute.For<IPipelineHandler>();
        handler.SetNext(nextHandler);
        var context = TestDataFactory.CreateContext(level: level);

        // Act
        await handler.HandleAsync(context);

        // Assert
        await nextHandler.Received(1).HandleAsync(
            context,
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(LogLevel.INFO)]
    [InlineData(LogLevel.WARN)]
    public async Task HandleAsync_LevelBelowMinimum_IncrementsDroppedCounter(LogLevel level)
    {
        // Arrange
        var handler = CreateHandler("ERROR");
        var context = TestDataFactory.CreateContext(level: level);

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.Equal(1, _tracker.DroppedByFilter);
    }

    [Fact]
    public async Task HandleAsync_LevelAtMinimum_DoesNotIncrementDroppedCounter()
    {
        // Arrange
        var handler = CreateHandler("ERROR");
        var context = TestDataFactory.CreateContext(level: LogLevel.ERROR);

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.Equal(0, _tracker.DroppedByFilter);
    }

    [Fact]
    public async Task HandleAsync_MinimumLevelInfo_PassesAllLevels()
    {
        // Arrange
        var handler = CreateHandler("INFO");
        var nextHandler = Substitute.For<IPipelineHandler>();
        handler.SetNext(nextHandler);

        foreach (var level in Enum.GetValues<LogLevel>())
        {
            var context = TestDataFactory.CreateContext(level: level);

            // Act
            await handler.HandleAsync(context);
        }

        // Assert — tüm 4 seviye geçmeli
        await nextHandler.Received(4).HandleAsync(
            Arg.Any<PipelineContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_InvalidMinimumLogLevel_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => CreateHandler("INVALID_LEVEL"));
    }
}
