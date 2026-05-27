namespace ExchangeLogMiddleware.Tests.Helpers;

using ExchangeLogMiddleware.Shared.Models;
using LogLevel = ExchangeLogMiddleware.Shared.Enums.LogLevel;
using LogCategory = ExchangeLogMiddleware.Shared.Enums.LogCategory;

/// <summary>
/// Test senaryoları için yardımcı fabrika metotları.
/// </summary>
internal static class TestDataFactory
{
    /// <summary>
    /// Belirtilen parametrelerle bir <see cref="PipelineContext"/> oluşturur.
    /// </summary>
    public static PipelineContext CreateContext(
        LogLevel level = LogLevel.ERROR,
        LogCategory category = LogCategory.Database,
        string message = "Test log message",
        string? rawData = null,
        string? messageId = null)
    {
        var envelope = new MessageEnvelope<LogPayload>
        {
            Payload = new LogPayload
            {
                Level = level,
                Category = category,
                Message = message,
                RawData = rawData
            },
            MessageId = messageId ?? Guid.NewGuid().ToString(),
            SenderId = "test-producer",
            Timestamp = DateTime.UtcNow
        };

        return new PipelineContext(envelope);
    }
}
