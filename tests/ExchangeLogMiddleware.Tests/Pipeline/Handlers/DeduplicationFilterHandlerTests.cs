namespace ExchangeLogMiddleware.Tests.Pipeline.Handlers;

using ExchangeLogMiddleware.Middleware.Configuration;
using ExchangeLogMiddleware.Middleware.Pipeline;
using ExchangeLogMiddleware.Middleware.Pipeline.Handlers;
using ExchangeLogMiddleware.Shared.Interfaces;
using ExchangeLogMiddleware.Shared.Models;
using ExchangeLogMiddleware.Tests.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using LogLevel = ExchangeLogMiddleware.Shared.Enums.LogLevel;

/// <summary>
/// <see cref="DeduplicationFilterHandler"/> (Step 2) unit testleri.
/// </summary>
public sealed class DeduplicationFilterHandlerTests : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly IPerformanceTracker _tracker;
    private readonly ILogger<DeduplicationFilterHandler> _logger;

    public DeduplicationFilterHandlerTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
#pragma warning disable CS0618
        _tracker = new NoOpPerformanceTracker();
#pragma warning restore CS0618
        _logger = Substitute.For<ILogger<DeduplicationFilterHandler>>();
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    private DeduplicationFilterHandler CreateHandler(int ttlMinutes = 10)
    {
        var options = Options.Create(new PipelineSettings { DeduplicationCacheTtlMinutes = ttlMinutes });
        return new DeduplicationFilterHandler(_cache, options, _tracker, _logger);
    }

    [Fact]
    public async Task HandleAsync_FirstMessage_PassesToNextHandler()
    {
        // Arrange
        var handler = CreateHandler();
        var nextHandler = Substitute.For<IPipelineHandler>();
        handler.SetNext(nextHandler);
        var context = TestDataFactory.CreateContext(messageId: "unique-msg-001");

        // Act
        await handler.HandleAsync(context);

        // Assert
        await nextHandler.Received(1).HandleAsync(
            context,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DuplicateMessageId_DropsMessage()
    {
        // Arrange
        var handler = CreateHandler();
        var nextHandler = Substitute.For<IPipelineHandler>();
        handler.SetNext(nextHandler);

        var context1 = TestDataFactory.CreateContext(messageId: "duplicate-msg-001");
        var context2 = TestDataFactory.CreateContext(messageId: "duplicate-msg-001");

        // Act
        await handler.HandleAsync(context1); // İlk mesaj — PASS
        await handler.HandleAsync(context2); // Aynı ID — DROP

        // Assert — NextAsync sadece bir kez çağrılmalı
        await nextHandler.Received(1).HandleAsync(
            Arg.Any<PipelineContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DifferentMessageIds_PassesBoth()
    {
        // Arrange
        var handler = CreateHandler();
        var nextHandler = Substitute.For<IPipelineHandler>();
        handler.SetNext(nextHandler);

        var context1 = TestDataFactory.CreateContext(messageId: "msg-001");
        var context2 = TestDataFactory.CreateContext(messageId: "msg-002");

        // Act
        await handler.HandleAsync(context1);
        await handler.HandleAsync(context2);

        // Assert — her iki mesaj da geçmeli
        await nextHandler.Received(2).HandleAsync(
            Arg.Any<PipelineContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DuplicateMessage_IncrementsDroppedCounter()
    {
        // Arrange
        var handler = CreateHandler();
        var context1 = TestDataFactory.CreateContext(messageId: "dup-counter-test");
        var context2 = TestDataFactory.CreateContext(messageId: "dup-counter-test");

        // Act
        await handler.HandleAsync(context1);
        await handler.HandleAsync(context2);

        // Assert
        Assert.Equal(1, _tracker.DroppedByFilter);
    }

    [Fact]
    public async Task HandleAsync_MultipleUniqueMessages_NoDrops()
    {
        // Arrange
        var handler = CreateHandler();
        var nextHandler = Substitute.For<IPipelineHandler>();
        handler.SetNext(nextHandler);

        // Act
        for (var i = 0; i < 10; i++)
        {
            var context = TestDataFactory.CreateContext(messageId: $"unique-{i}");
            await handler.HandleAsync(context);
        }

        // Assert
        Assert.Equal(0, _tracker.DroppedByFilter);
        await nextHandler.Received(10).HandleAsync(
            Arg.Any<PipelineContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_MessageIdAddedToCache()
    {
        // Arrange
        var handler = CreateHandler();
        var context = TestDataFactory.CreateContext(messageId: "cache-check-001");

        // Act
        await handler.HandleAsync(context);

        // Assert — cache'de bu ID bulunmalı
        Assert.True(_cache.TryGetValue("cache-check-001", out _));
    }
}
