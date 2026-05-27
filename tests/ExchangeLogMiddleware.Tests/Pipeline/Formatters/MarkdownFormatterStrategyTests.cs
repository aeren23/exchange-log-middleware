namespace ExchangeLogMiddleware.Tests.Pipeline.Formatters;

using ExchangeLogMiddleware.Middleware.Pipeline.Formatters;
using ExchangeLogMiddleware.Shared.Enums;
using ExchangeLogMiddleware.Shared.Models;

public sealed class MarkdownFormatterStrategyTests
{
    private readonly MarkdownFormatterStrategy _strategy = new();

    [Fact]
    public void TargetRole_IsSysAdmin()
    {
        Assert.Equal(TargetRole.SysAdmin, _strategy.TargetRole);
    }

    [Fact]
    public void FileExtension_IsMd()
    {
        Assert.Equal(".md", _strategy.FileExtension);
    }

    [Fact]
    public void Format_ReturnsMarkdownBlock()
    {
        // Arrange
        var log = new EnrichedLog
        {
            MessageId = "msg-1",
            SenderId = "sender-1",
            Timestamp = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Criticality = "Low",
            Level = LogLevel.INFO,
            Category = LogCategory.System,
            SanitizedMessage = "System rebooted"
        };

        // Act
        var result = _strategy.Format(log);

        // Assert
        Assert.Contains("---", result);
        Assert.Contains("**MessageId:** `msg-1`", result);
        Assert.Contains("**Criticality:** `Low`", result);
        Assert.Contains("> System rebooted", result);
    }
}
