namespace ExchangeLogMiddleware.Tests.IO;

using ExchangeLogMiddleware.Middleware.IO;

public sealed class ThreadSafeFileWriterTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ThreadSafeFileWriter _writer;

    public ThreadSafeFileWriterTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "ExchangeLogMiddleware_Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _writer = new ThreadSafeFileWriter();
    }

    public void Dispose()
    {
        _writer.Dispose();
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AppendLineAsync_CreatesFileAndWritesContent()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");
        var content = "Hello World";

        // Act
        await _writer.AppendLineAsync(filePath, content);

        // Assert
        Assert.True(File.Exists(filePath));
        var readContent = await File.ReadAllTextAsync(filePath);
        Assert.Equal(content + Environment.NewLine, readContent);
    }

    [Fact]
    public async Task AppendLineAsync_ConcurrentWrites_DoesNotCorruptFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "concurrent.log");
        var tasks = new List<Task>();
        var iterations = 100;

        // Act
        // 100 farklı thread'in aynı dosyaya aynı anda yazmaya çalışmasını simüle ediyoruz
        for (var i = 0; i < iterations; i++)
        {
            var lineNumber = i; // capture
            tasks.Add(Task.Run(() => _writer.AppendLineAsync(filePath, $"Line {lineNumber}")));
        }

        await Task.WhenAll(tasks);

        // Assert
        var lines = await File.ReadAllLinesAsync(filePath);
        Assert.Equal(iterations, lines.Length);
        
        // Veri kaybı olmamalı
        for (var i = 0; i < iterations; i++)
        {
            Assert.Contains($"Line {i}", lines);
        }
    }
}
