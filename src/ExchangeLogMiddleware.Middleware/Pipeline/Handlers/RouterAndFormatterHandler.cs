namespace ExchangeLogMiddleware.Middleware.Pipeline.Handlers;

using ExchangeLogMiddleware.Middleware.Configuration;
using ExchangeLogMiddleware.Shared.Interfaces;
using ExchangeLogMiddleware.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;

/// <summary>
/// Pipeline Step 5 — Router & Formatter.
/// </summary>
/// <remarks>
/// <para>
/// Zenginleştirilmiş logu (<see cref="EnrichedLog"/>) okur, yapılandırılan kategorisine göre
/// hedef rolleri bulur, bu roller için aktif olan formatlayıcı stratejilerini seçer ve
/// her bir stratejinin ürettiği metni dosya sistemine asenkron/thread-safe olarak yazar.
/// </para>
/// <para>
/// Spec §5 Step 5: "Maps the Category to specific target roles using a configuration dictionary...
/// Applies Fan-out/Multicast... Invokes the correct Strategy Formatter...
/// Writes concurrently to shared Docker Volumes."
/// </para>
/// </remarks>
public sealed class RouterAndFormatterHandler : BasePipelineHandler
{
    private readonly IFormatterFactory _formatterFactory;
    private readonly IFileWriter _fileWriter;
    private readonly IPerformanceTracker _performanceTracker;
    private readonly RouterSettings _routerSettings;
    private readonly ILogger<RouterAndFormatterHandler> _logger;

    public RouterAndFormatterHandler(
        IFormatterFactory formatterFactory,
        IFileWriter fileWriter,
        IPerformanceTracker performanceTracker,
        IOptions<RouterSettings> options,
        ILogger<RouterAndFormatterHandler> logger)
    {
        _formatterFactory = formatterFactory;
        _fileWriter = fileWriter;
        _performanceTracker = performanceTracker;
        _routerSettings = options.Value;
        _logger = logger;

        _logger.LogInformation("RouterAndFormatterHandler başlatıldı. Çıktı Dizin: {OutputDir}", _routerSettings.OutputDirectory);
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var log = context.EnrichedLog;

        if (log is null)
        {
            _logger.LogWarning("DROP — PipelineContext.EnrichedLog null. MessageId: {MessageId}", context.Envelope.MessageId);
            return;
        }

        // 1. Kategoriden hedef rolleri bul (Routing)
        if (!_routerSettings.CategoryRoutes.TryGetValue(log.Category, out var targetRoles) || targetRoles.Count == 0)
        {
            _logger.LogDebug("DROP — Kategori {Category} için hedef rol bulunamadı.", log.Category);
            return;
        }

        // Fan-out log için target roles listesini güncelle (isteğe bağlı, referans amaçlı kalabilir)
        log.TargetRoles = targetRoles;

        var writeTasks = new List<Task>();

        // 2. Multicast / Fan-out
        foreach (var role in targetRoles)
        {
            // 3. Bu rol için aktif olan tüm stratejileri al
            var strategies = _formatterFactory.GetStrategies(role);

            foreach (var strategy in strategies)
            {
                // 4. Stratejiyi çalıştır ve formatlı metni al
                var formattedContent = strategy.Format(log);

                // 5. Dosya yolu oluştur (örn: output/developer.json)
                var fileName = $"{role.ToString().ToLowerInvariant()}{strategy.FileExtension}";
                var filePath = Path.Combine(_routerSettings.OutputDirectory, fileName);

                // 6. Yazma görevini başlat (Thread-Safe File Writer kullanılarak)
                var writeTask = _fileWriter.AppendLineAsync(filePath, formattedContent, cancellationToken);
                writeTasks.Add(writeTask);
                
                _logger.LogDebug("Log yazılıyor: {FilePath} (MessageId: {MessageId})", filePath, log.MessageId);
            }
        }

        // 7. Tüm yazma işlemlerinin bitmesini bekle
        if (writeTasks.Count > 0)
        {
            await Task.WhenAll(writeTasks);
            
            // Başarılı işlenen mesaj sayacını artır (Spec §6.3: Incremented after Step 5 file writing)
            _performanceTracker.IncrementSuccessfullyProcessed();
        }

        // Zinciri devam ettir (Şu an son handler, bu sayede Task.CompletedTask dönecek)
        await NextAsync(context, cancellationToken);
    }
}
