using ExchangeLogMiddleware.Middleware.Configuration;
using ExchangeLogMiddleware.Middleware.Pipeline;
using ExchangeLogMiddleware.Middleware.Pipeline.Handlers;
using ExchangeLogMiddleware.Middleware.Services;
using ExchangeLogMiddleware.Middleware.Pipeline.Formatters;
using ExchangeLogMiddleware.Middleware.IO;
using ExchangeLogMiddleware.Shared.Extensions;
using ExchangeLogMiddleware.Shared.Interfaces;

var builder = Host.CreateApplicationBuilder(args);

// 1. Pipeline konfigürasyon modeli
builder.Services.Configure<PipelineSettings>(
    builder.Configuration.GetSection(PipelineSettings.SectionName));

builder.Services.Configure<RouterSettings>(
    builder.Configuration.GetSection(RouterSettings.SectionName));

// 2. Message Broker (RabbitMQ / AzureServiceBus — Provider'a göre seçilir)
builder.Services.AddMessageBroker(builder.Configuration);

// 3. Bounded Channel buffer (Singleton — tek channel örneği)
builder.Services.AddSingleton<ChannelProvider>();

// 4. IMemoryCache — DeduplicationFilterHandler (Step 2) tarafından kullanılır
builder.Services.AddMemoryCache();

// 5. IPerformanceTracker (Singleton — thread-safe metrik sayaçları)
builder.Services.AddSingleton<IPerformanceTracker, PerformanceTracker>();

// 6. Formatter Stratejileri ve Factory (Phase 6)
builder.Services.AddSingleton<IFormatterStrategy, JsonFormatterStrategy>();
builder.Services.AddSingleton<IFormatterStrategy, CsvFormatterStrategy>();
builder.Services.AddSingleton<IFormatterStrategy, MarkdownFormatterStrategy>();
builder.Services.AddSingleton<IFormatterStrategy, HtmlFormatterStrategy>();
builder.Services.AddSingleton<IFormatterFactory, FormatterFactory>();

// 7. Thread-Safe I/O Writer
builder.Services.AddSingleton<IFileWriter, ThreadSafeFileWriter>();

// 8. Pipeline Handler'ları — Chain of Responsibility zincir sırası
//    DI'a kayıt sırası kesinlikle korunmalıdır (Spec §5, Pipeline.md §4):
//    LevelFilter → DeduplicationFilter → KvkkAnonymizer → MetadataEnricher → RouterAndFormatter
builder.Services.AddSingleton<IPipelineHandler, LevelFilterHandler>();
builder.Services.AddSingleton<IPipelineHandler, DeduplicationFilterHandler>();
builder.Services.AddSingleton<IPipelineHandler, KvkkAnonymizerHandler>();
builder.Services.AddSingleton<IPipelineHandler, MetadataEnricherHandler>();
builder.Services.AddSingleton<IPipelineHandler, RouterAndFormatterHandler>();

// 9. Pipeline Orchestrator (Singleton — zincir bir kez kurulur)
builder.Services.AddSingleton<PipelineOrchestrator>();

// 10. Background Services
builder.Services.AddHostedService<BrokerListenerService>();  // Broker → Channel
builder.Services.AddHostedService<PipelineWorkerService>();  // Channel → Pipeline
builder.Services.AddHostedService<MetricsReporterService>(); // Spec §6.3 — 5 sn raporlama

var host = builder.Build();
host.Run();
