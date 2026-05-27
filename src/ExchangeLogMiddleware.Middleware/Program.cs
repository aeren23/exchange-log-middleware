using ExchangeLogMiddleware.Middleware.Configuration;
using ExchangeLogMiddleware.Middleware.Pipeline;
using ExchangeLogMiddleware.Middleware.Pipeline.Handlers;
using ExchangeLogMiddleware.Middleware.Services;
using ExchangeLogMiddleware.Shared.Extensions;
using ExchangeLogMiddleware.Shared.Interfaces;

var builder = Host.CreateApplicationBuilder(args);

// 1. Pipeline konfigürasyon modeli
builder.Services.Configure<PipelineSettings>(
    builder.Configuration.GetSection(PipelineSettings.SectionName));

// 2. Message Broker (RabbitMQ / AzureServiceBus — Provider'a göre seçilir)
builder.Services.AddMessageBroker(builder.Configuration);

// 3. Bounded Channel buffer (Singleton — tek channel örneği)
builder.Services.AddSingleton<ChannelProvider>();

// 4. IMemoryCache — DeduplicationFilterHandler (Step 2) tarafından kullanılır
builder.Services.AddMemoryCache();

// 5. IPerformanceTracker (Singleton — thread-safe metrik sayaçları)
//    Not: Phase 7'de raporlama özelliğine sahip gerçek PerformanceTracker ile değiştirilecek.
builder.Services.AddSingleton<IPerformanceTracker, NoOpPerformanceTracker>();

// 6. Pipeline Handler'ları — Chain of Responsibility zincir sırası
//    DI'a kayıt sırası kesinlikle korunmalıdır (Spec §5, Pipeline.md §4):
//    LevelFilter → DeduplicationFilter → KvkkAnonymizer → MetadataEnricher → (RouterAndFormatter Phase 6)
builder.Services.AddSingleton<IPipelineHandler, LevelFilterHandler>();
builder.Services.AddSingleton<IPipelineHandler, DeduplicationFilterHandler>();
builder.Services.AddSingleton<IPipelineHandler, KvkkAnonymizerHandler>();
builder.Services.AddSingleton<IPipelineHandler, MetadataEnricherHandler>();

// 7. Pipeline Orchestrator (Singleton — zincir bir kez kurulur)
builder.Services.AddSingleton<PipelineOrchestrator>();

// 8. Background Services
builder.Services.AddHostedService<BrokerListenerService>(); // Broker → Channel
builder.Services.AddHostedService<PipelineWorkerService>(); // Channel → Pipeline

var host = builder.Build();
host.Run();
