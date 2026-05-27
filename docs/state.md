# Project State - Exchange Log Middleware

> **This file must be updated by AI Agents at every working session.**  
> The agent that last made changes must append a date and summary to the bottom of this file.

---

## Current Status

| Field | Value |
|-------|-------|
| **Active Phase** | Phase 3 - Log Producer Service (Container 1) |
| **Overall Progress** | 33% (Phase 0, 1 ve 2 tamamlandı) |
| **Last Updated** | 2026-05-27 |
| **Blocker** | None |
| **Next Step** | Phase 3.1 - Create `LogGeneratorService` |

---

## Completed Phases

| Phase | Description | Completed |
|-------|-------------|-----------|
| **0** | Project Foundation & Skeleton | ✅ 2026-05-27 |
| **1** | Data Models & Contracts | ✅ 2026-05-27 |
| **2** | Message Broker Layer | ✅ 2026-05-27 |

---

## Active Work Details

**Phase:** 3 - Log Producer Service (Container 1)  
**Status:** Not started  
**Notes:** Phase 2 tamamlandı. RabbitMQ ve Azure Service Bus emülatörü adaptörleri Polly desteğiyle hazır. DI konfigürasyonu tamamlandı.

---

## Resolved Problems

| # | Date | Problem | Resolution | Affected Phase |
|---|------|---------|------------|----------------|
| 1 | 2026-05-27 | Solution `.slnx` vs `.sln` formatı | `.slnx` korundu — zaten doğru yapılandırılmış, 3 proje dahil | Phase 0 |

---

## Known Risks & Points of Attention

- RabbitMQ Docker container must be healthy before the Producer starts (depends_on + healthcheck) ✅ Çözüldü — docker-compose healthcheck yapılandırıldı
- System.Threading.Channels bounded capacity must be carefully tuned to prevent memory leaks (Phase 4)
- SemaphoreSlim must be used for thread-safe file I/O (Phase 6)
- KVKK Regex patterns must be thoroughly tested for edge cases (Phase 5C)
- Pipeline handler order must **never** be changed (per spec)

---

## Architectural Decisions Log

| # | Date | Decision | Rationale |
|---|------|----------|-----------:|
| 1 | 2025-05-27 | Project split into 10 phases (0-9) | Each phase is independently testable; clear separation of responsibility between agents |
| 2 | 2025-05-27 | Shared class library as a separate project | DIP principle; enables model/interface sharing |
| 3 | 2026-05-27 | `.slnx` format korundu (`.sln`'e dönüştürülmedi) | Zaten çalışır durumda; 3 proje doğru referanslanmış |
| 4 | 2026-05-27 | Multi-stage Dockerfile (SDK + Runtime) | Image boyutunu minimize eder; runtime image'da SDK tooling yok |
| 5 | 2026-05-27 | Non-root user Docker container'larında | Güvenlik best practice — container escape riskini azaltır |
| 6 | 2026-05-27 | `output-data` bind mount volume (`./output`) | Host dosya sisteminden erişimi kolaylaştırır; geliştirme sırasında log dosyalarını incelemeye olanak tanır |
| 7 | 2026-05-27 | `LogLevel`/`LogCategory` enum (spec'te string) | Type-safety + derleme zamanı doğrulama. `JsonStringEnumConverter` ile string uyumluluğu korunur |
| 8 | 2026-05-27 | SysAdmin formatter: Markdown (HTML yerine) | Ürün kararı — semantik olarak HTML daha uygun olsa da şimdilik Markdown. OCP gereği geçiş: `MarkdownFormatterStrategy` → `HtmlFormatterStrategy` (pipeline değişmez) |
| 9 | 2026-05-27 | `IPipelineHandler.HandleAsync` → `Task` döner | DROP = sonraki handler çağrılmaz. Basit ve açık DROP mekanizması; `Task<bool>` gereksiz karmaşıklık ekler |
| 10 | 2026-05-27 | Azure Service Bus Emulator (Docker) | Gerçek Azure kaynaklarına ihtiyaç duymadan lokal testi sağlamak |
| 11 | 2026-05-27 | Dual-Broker mimarisi (`MessageBroker` config) | İleride provider değişikliğini sadece appsettings üzerinden kod değişikliği olmadan yapabilmek |

---

## Changelog

### [2025-05-27] - Project Start
- `docs/project_plan.md` created (10 phases, 70+ sub-tasks)
- `docs/state.md` created (this file)
- Project has not yet entered code implementation stage
- Pipeline and Spec documents reviewed; phase structure designed accordingly

### [2026-05-27] - Phase 0 Completed — Antigravity
- `ExchangeLogMiddleware.slnx` — mevcut, format korundu
- `src/ExchangeLogMiddleware.Producer/` — mevcut Worker Service
- `src/ExchangeLogMiddleware.Middleware/` — mevcut Worker Service
- `src/ExchangeLogMiddleware.Shared/Class1.cs` — silindi (boilerplate temizlendi)
- `src/ExchangeLogMiddleware.Producer/ExchangeLogMiddleware.Producer.csproj` — Shared referansı eklendi
- `src/ExchangeLogMiddleware.Middleware/ExchangeLogMiddleware.Middleware.csproj` — Shared referansı eklendi
- `src/ExchangeLogMiddleware.Producer/appsettings.json` — RabbitMQ + Producer config eklendi
- `src/ExchangeLogMiddleware.Middleware/appsettings.json` — RabbitMQ + Pipeline + Output config eklendi
- `src/ExchangeLogMiddleware.Producer/Dockerfile` — multi-stage, non-root user
- `src/ExchangeLogMiddleware.Middleware/Dockerfile` — multi-stage, non-root user

### [2026-05-27] - Phase 1 Completed — Antigravity
- `docs/spec.md` — SysAdmin HTML→MD mimari karar notu eklendi
- `src/ExchangeLogMiddleware.Shared/Enums/LogLevel.cs` — sayısal sıralama ile enum
- `src/ExchangeLogMiddleware.Shared/Enums/LogCategory.cs` — routing mapping belgelendi
- `src/ExchangeLogMiddleware.Shared/Enums/TargetRole.cs` — format atamaları + HTML→MD karar notu
- `src/ExchangeLogMiddleware.Shared/Models/LogPayload.cs` — sealed, required, init
- `src/ExchangeLogMiddleware.Shared/Models/MessageEnvelope.cs` — generic T : class constraint
- `src/ExchangeLogMiddleware.Shared/Models/EnrichedLog.cs` — IReadOnlyList<TargetRole> fan-out
- `src/ExchangeLogMiddleware.Shared/Interfaces/IMessageBroker.cs` — IAsyncDisposable + callback
- `src/ExchangeLogMiddleware.Shared/Interfaces/IPipelineHandler.cs` — SetNext + HandleAsync Task
- `src/ExchangeLogMiddleware.Shared/Interfaces/IFormatterStrategy.cs` — Format() + FileExtension
- `src/ExchangeLogMiddleware.Shared/Interfaces/IPerformanceTracker.cs` — Singleton + Interlocked
- Build: **0 hata, 0 uyarı**

### [2026-05-27] - Phase 2 Completed — Antigravity
- `docker-compose.yml` — Azure Service Bus Emulator ve SQL Edge eklendi
- `ExchangeLogMiddleware.Shared.csproj` — RabbitMQ.Client, Azure.Messaging.ServiceBus, Polly eklendi
- `src/ExchangeLogMiddleware.Shared/Configuration/` — MessageBrokerSettings, RabbitMqSettings, AzureServiceBusSettings
- `src/ExchangeLogMiddleware.Shared/Broker/RabbitMqAdapter.cs` — Boundary Translation ve Polly retry
- `src/ExchangeLogMiddleware.Shared/Broker/AzureServiceBusAdapter.cs` — Boundary Translation ve Polly retry
- `src/ExchangeLogMiddleware.Shared/Extensions/ServiceCollectionExtensions.cs` — Provider'a göre adaptör seçimi (DI)
- `src/ExchangeLogMiddleware.Producer/appsettings.json` — MessageBroker bölümü eklendi
- `src/ExchangeLogMiddleware.Middleware/appsettings.json` — MessageBroker bölümü eklendi
- Build doğrulaması: `dotnet build` → **Başarılı (0 hata, 0 uyarı)**


- `docker-compose.yml` — RabbitMQ healthcheck, Producer, Middleware, shared output volume
- `output/.gitkeep` — shared output volume dizini
- Build doğrulaması: `dotnet build` → **Başarılı (0 hata, 0 uyarı)**
