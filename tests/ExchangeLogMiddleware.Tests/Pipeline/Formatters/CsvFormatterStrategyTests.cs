namespace ExchangeLogMiddleware.Tests.Pipeline.Formatters;

using ExchangeLogMiddleware.Middleware.Pipeline.Formatters;
using ExchangeLogMiddleware.Shared.Enums;
using ExchangeLogMiddleware.Shared.Models;

public sealed class CsvFormatterStrategyTests
{
    private readonly CsvFormatterStrategy _strategy = new();

    [Fact]
    public void TargetRole_IsSecurity()
    {
        Assert.Equal(TargetRole.Security, _strategy.TargetRole);
    }

    [Fact]
    public void FileExtension_IsCsv()
    {
        Assert.Equal(".csv", _strategy.FileExtension);
    }

    [Fact]
    public void Format_ReturnsCommaSeparatedString()
    {
        // Arrange
        var log = new EnrichedLog
        {
            MessageId = "msg-1",
            SenderId = "sender-1",
            Timestamp = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Criticality = "High",
            Level = LogLevel.CRITICAL,
            Category = LogCategory.Auth,
            SanitizedMessage = "Invalid login attempt"
        };

        // Act
        var result = _strategy.Format(log);

        // Assert
        // Format: MessageId,Timestamp,SenderId,Level,Criticality,Category,"SanitizedMessage"
        var expectedStart = "msg-1,2023-01-01T12:00:00.0000000Z,sender-1,CRITICAL,High,Auth,\"Invalid login attempt\"";
        Assert.Equal(expectedStart, result);
    }

    [Fact]
    public void Format_EscapesQuotesInMessage()
    {
        // Arrange
        var log = new EnrichedLog
        {
            MessageId = "msg-1",
            SenderId = "sender-1",
            Timestamp = DateTime.UtcNow,
            Criticality = "Low",
            Level = LogLevel.INFO,
            Category = LogCategory.Auth,
            SanitizedMessage = "User \"admin\" logged in"
        };

        // Act
        var result = _strategy.Format(log);

        // Assert
        Assert.EndsWith("\"User \"\"admin\"\" logged in\"", result);
    }
}
