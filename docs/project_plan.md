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

- [ ] **3.1** Create `LogGeneratorService` (BackgroundService)
- [ ] **3.2** Configuration model: `LogsPerSecond`, `ErrorRate` (environment variables)
- [ ] **3.3** Generate random but realistic stock exchange log messages
- [ ] **3.4** Log level distribution (based on `ErrorRate` configuration)
- [ ] **3.5** Inject sensitive data (National ID, credit card, email, phone — for testing)
- [ ] **3.6** Send messages via `IMessageBroker.Publish()` call
- [ ] **3.7** Attach metadata via headers/properties (MessageId, AppId, Timestamp)
- [ ] **3.8** Production rate control and throttling mechanism
- [ ] **3.9** Producer integration test (with RabbitMQ)

---

## Phase 4: Pipeline Infrastructure & Channel Buffering (Middleware Core)

Async pipeline infrastructure based on System.Threading.Channels.

- [ ] **4.1** Create `System.Threading.Channels` bounded channel
- [ ] **4.2** Write-to-channel mechanism (RabbitMqAdapter -> Channel)
- [ ] **4.3** Read-from-channel mechanism (Worker thread)
- [ ] **4.4** Create `PipelineOrchestrator` class (executes handlers in order)
- [ ] **4.5** Chain of Responsibility base class / abstract handler
- [ ] **4.6** Pipeline DI registration (IServiceCollection configuration)
- [ ] **4.7** Graceful shutdown support (CancellationToken propagation)
- [ ] **4.8** Channel backpressure and capacity management

---

## Phase 5: Pipeline Processing Steps (Chain of Responsibility Handlers)

Implementation of the ordered pipeline handlers as defined in the spec.

### 5A: Performance Level Filter (Step 1)
- [ ] **5A.1** Create `LevelFilterHandler` class
- [ ] **5A.2** Read `MinimumLogLevel` setting from `appsettings.json`
- [ ] **5A.3** Level comparison logic (INFO < WARN < ERROR < CRITICAL)
- [ ] **5A.4** DROP logs below threshold and update PerformanceTracker
- [ ] **5A.5** Unit test: level filtering validation

### 5B: Deduplication Filter (Step 2)
- [ ] **5B.1** Create `DeduplicationFilterHandler` class
- [ ] **5B.2** `IMemoryCache` integration (Microsoft.Extensions.Caching.Memory)
- [ ] **5B.3** MessageId-based cache check & insert (TTL: 10 minutes)
- [ ] **5B.4** DROP duplicate logs and update PerformanceTracker
- [ ] **5B.5** Unit test: deduplication validation

### 5C: KVKK Anonymizer (Step 3)
- [ ] **5C.1** Create `KvkkAnonymizerHandler` class
- [ ] **5C.2** National ID (TCKN) masking Regex (11 digits → `12*********`)
- [ ] **5C.3** Credit card masking Regex (16 digits → `5235 **** **** 1234`)
- [ ] **5C.4** Email masking Regex (`a****@****.com`)
- [ ] **5C.5** Phone number masking Regex (`+90 5*********`)
- [ ] **5C.6** Apply masking to Message and RawData fields
- [ ] **5C.7** Unit test: all masking scenarios

### 5D: Metadata Enricher (Step 4)
- [ ] **5D.1** Create `MetadataEnricherHandler` class
- [ ] **5D.2** Extract metadata from Envelope (MessageId, SenderId, Timestamp)
- [ ] **5D.3** Calculate Criticality (CRITICAL→High, ERROR→Medium, etc.)
- [ ] **5D.4** Build `EnrichedLog` object and pass it down the pipeline
- [ ] **5D.5** Unit test: enrichment validation

---

## Phase 6: Router & Formatter (Strategy Pattern - Step 5)

Category-based routing and role-based formatting.

- [ ] **6.1** Category → TargetRole mapping configuration
- [ ] **6.2** Fan-out/Multicast logic (a single log can be routed to multiple roles)
- [ ] **6.3** `JsonFormatterStrategy` implementation (Developer → `.json`)
- [ ] **6.4** `CsvFormatterStrategy` implementation (Security → `.csv`)
- [ ] **6.5** `MarkdownFormatterStrategy` implementation (SysAdmin → `.md`)
- [ ] **6.6** Create `FormatterFactory` (selects strategy based on role)
- [ ] **6.7** Thread-safe file writing (`SemaphoreSlim` / async streams)
- [ ] **6.8** Verify writes to Docker shared volume
- [ ] **6.9** Unit test: formatting and routing validation

---

## Phase 7: Performance Monitoring & Metrics (Observability)

Singleton PerformanceTracker and real-time reporting.

- [ ] **7.1** `PerformanceTracker` Singleton implementation (thread-safe)
- [ ] **7.2** Metric counters: TotalReceived, DroppedByFilter, SuccessfullyProcessed
- [ ] **7.3** Thread-safe increment operations (Interlocked)
- [ ] **7.4** Log metrics to console every 5 seconds via background timer
- [ ] **7.5** Throughput calculation (messages/second)
- [ ] **7.6** Unit test: thread-safety and metric validation

---

## Phase 8: Integration & Docker Compose (End-to-End)

End-to-end validation of all components working together.

- [ ] **8.1** Bring up the full system with `docker-compose up`
- [ ] **8.2** Validate full flow: RabbitMQ → Producer → Middleware → Output files
- [ ] **8.3** Verify output files are in the correct format
- [ ] **8.4** KVKK masking end-to-end validation
- [ ] **8.5** Deduplication end-to-end validation
- [ ] **8.6** Verify performance metrics console output
- [ ] **8.7** Stress test: high-volume log production and pipeline durability
- [ ] **8.8** Memory leak check (under Channel buffering load)
- [ ] **8.9** Graceful shutdown validation

---

## Phase 9: Quality & Documentation (Final Polish)

Code quality, test coverage, and project documentation.

- [ ] **9.1** XML documentation for all public APIs
- [ ] **9.2** Update README.md (setup, run instructions, architecture overview)
- [ ] **9.3** Code format and linting check (C# conventions)
- [ ] **9.4** SOLID principles compliance check (per coding_standarts.md)
- [ ] **9.5** Magic number/string check — all values must be moved to configuration
- [ ] **9.6** Error scenario and edge-case tests
- [ ] **9.7** Final update of `docs/state.md`

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
