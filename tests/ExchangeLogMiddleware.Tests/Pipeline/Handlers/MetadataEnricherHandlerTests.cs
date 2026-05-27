namespace ExchangeLogMiddleware.Tests.Pipeline.Handlers;

using ExchangeLogMiddleware.Middleware.Pipeline.Handlers;
using ExchangeLogMiddleware.Shared.Enums;
using ExchangeLogMiddleware.Shared.Interfaces;
using ExchangeLogMiddleware.Shared.Models;
using ExchangeLogMiddleware.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using LogLevel = ExchangeLogMiddleware.Shared.Enums.LogLevel;

/// <summary>
/// <see cref="MetadataEnricherHandler"/> (Step 4) unit testleri.
/// </summary>
public sealed class MetadataEnricherHandlerTests
{
    private readonly MetadataEnricherHandler _handler;
    private readonly IPipelineHandler _nextHandler;

    public MetadataEnricherHandlerTests()
    {
        var logger = Substitute.For<ILogger<MetadataEnricherHandler>>();
        _handler = new MetadataEnricherHandler(logger);
        _nextHandler = Substitute.For<IPipelineHandler>();
        _handler.SetNext(_nextHandler);
    }

    [Fact]
    public async Task HandleAsync_SetsEnrichedLogOnContext()
    {
        // Arrange
        var context = TestDataFactory.CreateContext();

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.NotNull(context.EnrichedLog);
    }

    [Fact]
    public async Task HandleAsync_MapsMessageIdFromEnvelope()
    {
        // Arrange
        var context = TestDataFactory.CreateContext(messageId: "txn-42");

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.Equal("txn-42", context.EnrichedLog!.MessageId);
    }

    [Fact]
    public async Task HandleAsync_MapsTimestampFromEnvelope()
    {
        // Arrange
        var context = TestDataFactory.CreateContext();
        var expectedTimestamp = context.Envelope.Timestamp;

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.Equal(expectedTimestamp, context.EnrichedLog!.Timestamp);
    }

    [Fact]
    public async Task HandleAsync_MapsSenderIdFromEnvelope()
    {
        // Arrange
        var context = TestDataFactory.CreateContext();

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.Equal("test-producer", context.EnrichedLog!.SenderId);
    }

    [Fact]
    public async Task HandleAsync_MapsSanitizedMessageFromPayload()
    {
        // Arrange — mesaj Step 3'te maskelenmiş olabilir, enricher olduğu gibi aktarır
        var context = TestDataFactory.CreateContext(message: "KVKK maskelenmiş mesaj");

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.Equal("KVKK maskelenmiş mesaj", context.EnrichedLog!.SanitizedMessage);
    }

    [Theory]
    [InlineData(LogLevel.CRITICAL, "High")]
    [InlineData(LogLevel.ERROR, "Medium")]
    [InlineData(LogLevel.WARN, "Low")]
    [InlineData(LogLevel.INFO, "Low")]
    public async Task HandleAsync_CalculatesCriticalityCorrectly(LogLevel level, string expectedCriticality)
    {
        // Arrange
        var context = TestDataFactory.CreateContext(level: level);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.Equal(expectedCriticality, context.EnrichedLog!.Criticality);
    }

    [Fact]
    public async Task HandleAsync_MapsLevelFromPayload()
    {
        // Arrange
        var context = TestDataFactory.CreateContext(level: LogLevel.CRITICAL);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.Equal(LogLevel.CRITICAL, context.EnrichedLog!.Level);
    }

    [Fact]
    public async Task HandleAsync_MapsCategoryFromPayload()
    {
        // Arrange
        var context = TestDataFactory.CreateContext(category: LogCategory.Auth);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.Equal(LogCategory.Auth, context.EnrichedLog!.Category);
    }

    [Fact]
    public async Task HandleAsync_AlwaysCallsNextHandler()
    {
        // MetadataEnricher asla DROP yapmaz
        var context = TestDataFactory.CreateContext();

        await _handler.HandleAsync(context);

        await _nextHandler.Received(1).HandleAsync(
            context,
            Arg.Any<CancellationToken>());
    }
}
