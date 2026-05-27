namespace ExchangeLogMiddleware.Tests.Pipeline.Formatters;

using ExchangeLogMiddleware.Middleware.Pipeline.Formatters;
using ExchangeLogMiddleware.Shared.Enums;
using ExchangeLogMiddleware.Shared.Models;

public sealed class HtmlFormatterStrategyTests
{
    private readonly HtmlFormatterStrategy _strategy = new();

    [Fact]
    public void TargetRole_IsSysAdmin()
    {
        Assert.Equal(TargetRole.SysAdmin, _strategy.TargetRole);
    }

    [Fact]
    public void FileExtension_IsHtml()
    {
        Assert.Equal(".html", _strategy.FileExtension);
    }

    [Fact]
    public void Format_ReturnsHtmlBlock()
    {
        // Arrange
        var log = new EnrichedLog
        {
            MessageId = "msg-1",
            SenderId = "sender-1",
            Timestamp = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Criticality = "Medium",
            Level = LogLevel.ERROR,
            Category = LogCategory.System,
            SanitizedMessage = "Out of memory"
        };

        // Act
        var result = _strategy.Format(log);

        // Assert
        Assert.Contains("<div class=\"log-entry\"", result);
        Assert.Contains("<code>msg-1</code>", result);
        Assert.Contains("<span class=\"level-error\">ERROR</span>", result);
        Assert.Contains("<blockquote", result);
        Assert.Contains("Out of memory</blockquote>", result);
    }
}
