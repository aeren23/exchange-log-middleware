# Exchange Log Middleware - Project Plan

> **This document is designed to be used by AI Agents.**  
> When a phase is completed, the corresponding checkbox must be marked as `[x]`.  
> The agent must also update `docs/state.md` at every working session.

---

## Phase 0: Project Foundation & Skeleton

In this phase, the solution structure, Docker infrastructure, and base configuration files are created.

- [x] **0.1** Create .NET 8 Solution file (`ExchangeLogMiddleware.sln`)
- [x] **0.2** Create `src/ExchangeLogMiddleware.Producer` project (Worker Service)
- [x] **0.3** Create `src/ExchangeLogMiddleware.Middleware` project (Worker Service)
- [x] **0.4** Create `src/ExchangeLogMiddleware.Shared` project (Class Library - shared models/interfaces)
- [x] **0.5** Set up inter-project references (Shared <- Producer, Shared <- Middleware)
- [x] **0.6** Create `docker-compose.yml` (RabbitMQ, Producer, Middleware services)
- [x] **0.7** Create `Dockerfile` for each project
- [x] **0.8** Create `appsettings.json` files (with environment variable support)
- [x] **0.9** Configure shared output volume (docker-compose volumes)
- [x] **0.10** Verify solution build and Docker build

---

## Phase 1: Data Models & Contracts

The foundational models and interfaces upon which the entire system is built are defined here.

- [x] **1.1** Create `LogPayload` model (Level, Category, Message, RawData)
- [x] **1.2** Create `MessageEnvelope<T>` generic model (Payload, MessageId, SenderId, Timestamp)
- [x] **1.3** Create `EnrichedLog` model (final pipeline output)
- [x] **1.4** Define `LogLevel` enum (INFO, WARN, ERROR, CRITICAL)
- [x] **1.5** Define `LogCategory` enum (Database, Auth, System)
- [x] **1.6** Define `TargetRole` enum (Developer, Security, SysAdmin)
- [x] **1.7** Define `IMessageBroker` interface (Adapter Pattern contract)
- [x] **1.8** Define `IPipelineHandler` interface (Chain of Responsibility contract)
- [x] **1.9** Define `IFormatterStrategy` interface (Strategy Pattern contract)
- [x] **1.10** Define `IPerformanceTracker` interface (Metrics contract)

---

## Phase 2: Message Broker Layer (Adapter & Integration)

RabbitMQ connection, Adapter pattern implementation, and boundary translation.

- [x] **2.1** Add RabbitMQ NuGet packages (RabbitMQ.Client) & Azure Service Bus & Polly
- [x] **2.2** Create `RabbitMqAdapter` & `AzureServiceBusAdapter` (`IMessageBroker` implementation)
- [x] **2.3** Read connection settings from `appsettings.json` (MessageBroker pattern)
- [x] **2.4** Implement Publish method — append MessageId, AppId, Timestamp to headers
- [x] **2.5** Implement Subscribe/Consume method
- [x] **2.6** Translate broker message into `MessageEnvelope<LogPayload>` (Boundary Translation)
- [x] **2.7** Implement Polly retry/resilience policy
- [x] **2.8** Connection health check
- [x] **2.9** Docker environment integration (RabbitMQ + Azure SQL Edge + ASB Emulator)

---

## Phase 3: Log Producer Service (Container 1)

A BackgroundService that generates high-frequency stock exchange log data.

- [x] **3.1** Create `LogGeneratorService` (BackgroundService)
- [x] **3.2** Configuration model: `LogsPerSecond`, `ErrorRate` (environment variables)
- [x] **3.3** Generate random but realistic stock exchange log messages
- [x] **3.4** Log level distribution (based on `ErrorRate` configuration)
- [x] **3.5** Inject sensitive data (National ID, credit card, email, phone — for testing)
- [x] **3.6** Send messages via `IMessageBroker.Publish()` call
- [x] **3.7** Attach metadata via headers/properties (MessageId, AppId, Timestamp)
- [x] **3.8** Production rate control and throttling mechanism
- [x] **3.9** Producer integration test (with RabbitMQ)

---

## Phase 4: Pipeline Infrastructure & Channel Buffering (Middleware Core)

Async pipeline infrastructure based on System.Threading.Channels.

- [x] **4.1** Create `System.Threading.Channels` bounded channel
- [x] **4.2** Write-to-channel mechanism (RabbitMqAdapter -> Channel)
- [x] **4.3** Read-from-channel mechanism (Worker thread)
- [x] **4.4** Create `PipelineOrchestrator` class (executes handlers in order)
- [x] **4.5** Chain of Responsibility base class / abstract handler
- [x] **4.6** Pipeline DI registration (IServiceCollection configuration)
- [x] **4.7** Graceful shutdown support (CancellationToken propagation)
- [x] **4.8** Channel backpressure and capacity management

---

## Phase 5: Pipeline Processing Steps (Chain of Responsibility Handlers)

Implementation of the ordered pipeline handlers as defined in the spec.

### 5A: Performance Level Filter (Step 1)
- [x] **5A.1** Create `LevelFilterHandler` class
- [x] **5A.2** Read `MinimumLogLevel` setting from `appsettings.json`
- [x] **5A.3** Level comparison logic (INFO < WARN < ERROR < CRITICAL)
- [x] **5A.4** DROP logs below threshold and update PerformanceTracker
- [x] **5A.5** Unit test: level filtering validation

### 5B: Deduplication Filter (Step 2)
- [x] **5B.1** Create `DeduplicationFilterHandler` class
- [x] **5B.2** `IMemoryCache` integration (Microsoft.Extensions.Caching.Memory)
- [x] **5B.3** MessageId-based cache check & insert (TTL: 10 minutes)
- [x] **5B.4** DROP duplicate logs and update PerformanceTracker
- [x] **5B.5** Unit test: deduplication validation

### 5C: KVKK Anonymizer (Step 3)
- [x] **5C.1** Create `KvkkAnonymizerHandler` class
- [x] **5C.2** National ID (TCKN) masking Regex (11 digits → `12*********`)
- [x] **5C.3** Credit card masking Regex (16 digits → `5235 **** **** 1234`)
- [x] **5C.4** Email masking Regex (`a****@****.com`)
- [x] **5C.5** Phone number masking Regex (`+90 5*********`)
- [x] **5C.6** Apply masking to Message and RawData fields
- [x] **5C.7** Unit test: all masking scenarios

### 5D: Metadata Enricher (Step 4)
- [x] **5D.1** Create `MetadataEnricherHandler` class
- [x] **5D.2** Extract metadata from Envelope (MessageId, SenderId, Timestamp)
- [x] **5D.3** Calculate Criticality (CRITICAL→High, ERROR→Medium, etc.)
- [x] **5D.4** Build `EnrichedLog` object and pass it down the pipeline
- [x] **5D.5** Unit test: enrichment validation

---

## Phase 6: Router & Formatter (Strategy Pattern - Step 5)

Category-based routing and role-based formatting.

- [x] **6.1** Category → TargetRole mapping configuration
- [x] **6.2** Fan-out/Multicast logic (a single log can be routed to multiple roles)
- [x] **6.3** `JsonFormatterStrategy` implementation (Developer → `.json`)
- [x] **6.4** `CsvFormatterStrategy` implementation (Security → `.csv`)
- [x] **6.5** `MarkdownFormatterStrategy` implementation (SysAdmin → `.md`)
- [x] **6.6** Create `FormatterFactory` (selects strategy based on role)
- [x] **6.7** Thread-safe file writing (`SemaphoreSlim` / async streams)
- [x] **6.8** Verify writes to Docker shared volume
- [x] **6.9** Unit test: formatting and routing validation

---

## Phase 7: Performance Monitoring & Metrics (Observability)

Singleton PerformanceTracker and real-time reporting.

- [x] **7.1** `PerformanceTracker` Singleton implementation (thread-safe)
- [x] **7.2** Metric counters: TotalReceived, DroppedByFilter, SuccessfullyProcessed
- [x] **7.3** Thread-safe increment operations (Interlocked)
- [x] **7.4** Log metrics to console every 5 seconds via background timer
- [x] **7.5** Throughput calculation (messages/second)
- [x] **7.6** Unit test: thread-safety and metric validation

---

## Phase 8: Integration & Docker Compose (End-to-End)

End-to-end validation of all components working together.

- [x] **8.1** Bring up the full system with `docker-compose up`
- [x] **8.2** Validate full flow: RabbitMQ → Producer → Middleware → Output files
- [x] **8.3** Verify output files are in the correct format
- [x] **8.4** KVKK masking end-to-end validation
- [x] **8.5** Deduplication end-to-end validation
- [x] **8.6** Verify performance metrics console output
- [x] **8.7** Stress test: high-volume log production and pipeline durability
- [x] **8.8** Memory leak check (under Channel buffering load)
- [x] **8.9** Graceful shutdown validation

---

## Phase 9: Quality & Documentation (Final Polish)

Code quality, test coverage, and project documentation.

- [x] **9.1** XML documentation for all public APIs
- [x] **9.2** Update README.md (setup, run instructions, architecture overview)
- [x] **9.3** Code format and linting check (C# conventions)
- [x] **9.4** SOLID principles compliance check (per coding_standarts.md)
- [x] **9.5** Magic number/string check — all values must be moved to configuration
- [x] **9.6** Error scenario and edge-case tests
- [x] **9.7** Final update of `docs/state.md`

---

## Summary Table

| Phase | Description | Depends On |
|-------|-------------|------------|
| 0 | Project Foundation & Skeleton | - |
| 1 | Data Models & Contracts | Phase 0 |
| 2 | Message Broker Layer | Phase 1 |
| 3 | Log Producer Service | Phase 1, 2 |
| 4 | Pipeline Infrastructure & Channel | Phase 1 |
| 5 | Pipeline Processing Steps | Phase 1, 4 |
| 6 | Router & Formatter | Phase 1, 5 |
| 7 | Performance Monitoring | Phase 4 |
| 8 | Integration & Docker | Phase 0-7 |
| 9 | Quality & Documentation | Phase 0-8 |
