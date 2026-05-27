namespace ExchangeLogMiddleware.Tests.Pipeline.Formatters;

using ExchangeLogMiddleware.Middleware.Pipeline.Formatters;
using ExchangeLogMiddleware.Shared.Enums;
using ExchangeLogMiddleware.Shared.Models;
using System.Text.Json;

public sealed class JsonFormatterStrategyTests
{
    private readonly JsonFormatterStrategy _strategy = new();

    [Fact]
    public void TargetRole_IsDeveloper()
    {
        Assert.Equal(TargetRole.Developer, _strategy.TargetRole);
    }

    [Fact]
    public void FileExtension_IsJson()
    {
        Assert.Equal(".json", _strategy.FileExtension);
    }

    [Fact]
    public void Format_SerializesEnrichedLogToJson()
    {
        // Arrange
        var log = new EnrichedLog
        {
            MessageId = "msg-1",
            SenderId = "sender-1",
            Timestamp = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Criticality = "High",
            Level = LogLevel.CRITICAL,
            Category = LogCategory.Database,
            SanitizedMessage = "Test message"
        };

        // Act
        var result = _strategy.Format(log);

        // Assert
        Assert.StartsWith("{", result);
        Assert.EndsWith("}", result);
        Assert.Contains("\"MessageId\":\"msg-1\"", result);
        Assert.Contains("\"SenderId\":\"sender-1\"", result);
        Assert.Contains("\"SanitizedMessage\":\"Test message\"", result);
        
        // Deserialize edilebilir olduğunu doğrula
        var deserialized = JsonSerializer.Deserialize<EnrichedLog>(result);
        Assert.NotNull(deserialized);
        Assert.Equal("msg-1", deserialized.MessageId);
    }
}
