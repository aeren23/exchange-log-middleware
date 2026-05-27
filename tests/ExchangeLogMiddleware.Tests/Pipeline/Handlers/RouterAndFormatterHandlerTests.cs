namespace ExchangeLogMiddleware.Tests.Pipeline.Handlers;

using ExchangeLogMiddleware.Middleware.Configuration;
using ExchangeLogMiddleware.Middleware.Pipeline.Handlers;
using ExchangeLogMiddleware.Shared.Enums;
using ExchangeLogMiddleware.Shared.Interfaces;
using ExchangeLogMiddleware.Shared.Models;
using ExchangeLogMiddleware.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using LogLevel = ExchangeLogMiddleware.Shared.Enums.LogLevel;

public sealed class RouterAndFormatterHandlerTests
{
    private readonly IFormatterFactory _factory;
    private readonly IFileWriter _fileWriter;
    private readonly IPerformanceTracker _tracker;
    private readonly ILogger<RouterAndFormatterHandler> _logger;
    private readonly RouterSettings _settings;

    public RouterAndFormatterHandlerTests()
    {
        _factory = Substitute.For<IFormatterFactory>();
        _fileWriter = Substitute.For<IFileWriter>();
        _tracker = Substitute.For<IPerformanceTracker>();
        _logger = Substitute.For<ILogger<RouterAndFormatterHandler>>();
        _settings = new RouterSettings
        {
            OutputDirectory = "test_output",
            CategoryRoutes = new Dictionary<LogCategory, List<TargetRole>>
            {
                { LogCategory.Database, [TargetRole.Developer] },
                { LogCategory.System, [TargetRole.SysAdmin, TargetRole.Security] } // Fan-out for category
            }
        };
    }

    private RouterAndFormatterHandler CreateHandler()
    {
        return new RouterAndFormatterHandler(
            _factory,
            _fileWriter,
            _tracker,
            Options.Create(_settings),
            _logger);
    }

    [Fact]
    public async Task HandleAsync_NullEnrichedLog_DropsMessage()
    {
        var handler = CreateHandler();
        var context = TestDataFactory.CreateContext();
        context.EnrichedLog = null; // Spec gereği zenginleştirilememişse

        await handler.HandleAsync(context);

        await _fileWriter.DidNotReceiveWithAnyArgs().AppendLineAsync(default!, default!);
        _tracker.DidNotReceive().IncrementSuccessfullyProcessed();
    }

    [Fact]
    public async Task HandleAsync_UnmappedCategory_DropsMessage()
    {
        var handler = CreateHandler();
        var context = TestDataFactory.CreateContext(category: LogCategory.Auth); // Ayarlarda Auth yok
        context.EnrichedLog = new EnrichedLog
        {
            MessageId = "1", SenderId = "1", Timestamp = DateTime.UtcNow,
            Criticality = "High", Level = LogLevel.ERROR, Category = LogCategory.Auth,
            SanitizedMessage = "Test"
        };

        await handler.HandleAsync(context);

        await _fileWriter.DidNotReceiveWithAnyArgs().AppendLineAsync(default!, default!);
    }

    [Fact]
    public async Task HandleAsync_MappedCategory_WritesToFileAndIncrementsTracker()
    {
        // Arrange
        var handler = CreateHandler();
        var nextHandler = Substitute.For<IPipelineHandler>();
        handler.SetNext(nextHandler);

        var context = TestDataFactory.CreateContext(category: LogCategory.Database);
        var enrichedLog = new EnrichedLog
        {
            MessageId = "1", SenderId = "1", Timestamp = DateTime.UtcNow,
            Criticality = "High", Level = LogLevel.ERROR, Category = LogCategory.Database,
            SanitizedMessage = "Test"
        };
        context.EnrichedLog = enrichedLog;

        var mockStrategy = Substitute.For<IFormatterStrategy>();
        mockStrategy.FileExtension.Returns(".json");
        mockStrategy.Format(enrichedLog).Returns("formatted_text");
        _factory.GetStrategies(TargetRole.Developer).Returns([mockStrategy]);

        // Act
        await handler.HandleAsync(context);

        // Assert
        var expectedPath = Path.Combine("test_output", "developer.json");
        await _fileWriter.Received(1).AppendLineAsync(expectedPath, "formatted_text", Arg.Any<CancellationToken>());
        _tracker.Received(1).IncrementSuccessfullyProcessed();
        
        // Sonraki handler çağrılmalı (gerçi bu son adım ama zincir kuralı gereği)
        await nextHandler.Received(1).HandleAsync(context, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_CategoryFanOut_WritesToMultipleRoles()
    {
        // Arrange
        var handler = CreateHandler();
        var context = TestDataFactory.CreateContext(category: LogCategory.System); // System -> SysAdmin, Security
        var enrichedLog = new EnrichedLog
        {
            MessageId = "1", SenderId = "1", Timestamp = DateTime.UtcNow,
            Criticality = "High", Level = LogLevel.ERROR, Category = LogCategory.System,
            SanitizedMessage = "Test"
        };
        context.EnrichedLog = enrichedLog;

        var sysAdminStrategy = Substitute.For<IFormatterStrategy>();
        sysAdminStrategy.FileExtension.Returns(".md");
        sysAdminStrategy.Format(enrichedLog).Returns("md_text");
        _factory.GetStrategies(TargetRole.SysAdmin).Returns([sysAdminStrategy]);

        var securityStrategy = Substitute.For<IFormatterStrategy>();
        securityStrategy.FileExtension.Returns(".csv");
        securityStrategy.Format(enrichedLog).Returns("csv_text");
        _factory.GetStrategies(TargetRole.Security).Returns([securityStrategy]);

        // Act
        await handler.HandleAsync(context);

        // Assert
        await _fileWriter.Received(1).AppendLineAsync(Path.Combine("test_output", "sysadmin.md"), "md_text", Arg.Any<CancellationToken>());
        await _fileWriter.Received(1).AppendLineAsync(Path.Combine("test_output", "security.csv"), "csv_text", Arg.Any<CancellationToken>());
        _tracker.Received(1).IncrementSuccessfullyProcessed(); // 1 mesaj işlendi
    }
}
