# CENG302 - Data Middleware System Specification

## 1. Project Overview
This project is an enterprise-grade, event-driven Data Middleware system for a stock market application. It consists of two Dockerized microservices communicating via a message broker. The system generates high-volume structured logs, filters them for performance and deduplication (Idempotency), anonymizes sensitive data (KVKK), enriches them using Message Broker metadata (Headers/Properties), and routes them to role-specific output files using format strategies.

## 2. Technology Stack
* **Framework:** .NET 8 (C#)
* **Architecture:** Microservices, Event-Driven
* **Concurrency:** `System.Threading.Channels` for memory-safe internal pipeline buffering.
* **Message Broker:** RabbitMQ (Primary) with Adapter support for Azure Service Bus.
* **Containerization:** Docker & docker-compose

## 3. Core Design Patterns
The AI Agent MUST implement these patterns strictly:
1. **Chain of Responsibility:** The core Middleware processing pipeline. Strict order: `LevelFilter -> DeduplicationFilter -> KVKKAnonymizer -> MetadataEnricher -> FormatterAndRouter`.
2. **Strategy:** For role-based log formatting (`JsonFormatterStrategy`, `CsvFormatterStrategy`, `MarkdownFormatterStrategy`).
3. **Adapter:** Abstracts the broker behind an `IMessageBroker` interface. The adapter MUST map broker-specific messages into a generic `MessageEnvelope<T>` BEFORE passing them to the internal pipeline.
4. **Factory:** To instantiate the correct processing pipeline and formatters.

## 4. Data Models

### 4.1. Producer Payload (Structured Log)
The Generator (Container 1) MUST produce lightweight JSON logs. It provides the Category for routing, but NO metadata (timestamp, id).
```json
{
  "Level": "string (INFO, WARN, ERROR, CRITICAL)",
  "Category": "string (Database, Auth, System)",
  "Message": "string (Business log text)",
  "RawData": "string (Optional sensitive data like TCKN: 12345678910)"
}

```

### 4.2. Generic Message Envelope (Boundary Translation)

Created strictly by the Broker Adapter before the message enters the pipeline:

```csharp
public class MessageEnvelope<T> 
{
    public T Payload { get; set; } // The JSON log payload (4.1)
    public string MessageId { get; set; }
    public string SenderId { get; set; }
    public DateTime Timestamp { get; set; }
}

```

### 4.3. Enriched Log (Middleware Internal State)

The Middleware builds this final object during processing:

```json
{
  "MessageId": "string (Extracted from Envelope)",
  "Timestamp": "datetime (Extracted from Envelope)",
  "SenderId": "string (Extracted from Envelope)",
  "Criticality": "string (Calculated from Level)",
  "Level": "string",
  "Category": "string",
  "SanitizedMessage": "string",
  "TargetRoles": ["Developer", "Security"]
}

```

## 5. The Pipeline (Chain of Responsibility Steps)

The entire pipeline `Handle` mechanism MUST accept and process the `MessageEnvelope<Payload>` object, NOT raw broker data.

### Step 1: Performance Level Filter

* Reads `MinimumLogLevel` from `appsettings.json` (e.g., ERROR).
* If the incoming `envelope.Payload.Level` is lower (INFO, WARN), **drop it immediately**. Do not process further.

### Step 2: Deduplication Filter (Idempotency)

* Checks the `envelope.MessageId` from the generic envelope against an `IMemoryCache`.
* If the `MessageId` exists, it is a duplicate. **Drop it immediately**.
* If it does not exist, add it to the cache with a short expiration (e.g., 10 minutes) and proceed.

### Step 3: Anonymizer (Security/KVKK)

* Scans `envelope.Payload.Message` and `envelope.Payload.RawData` fields via Regex.
* **TCKN Masking:** Masks 11-digit numbers (`12*********`).
* **Credit Card Masking:** Masks middle 8 digits of 16-digit patterns (`5235 **** **** 1234`).
* **Email Masking:** Masks email addresses (`a****@****.com`).
* **Phone Number Masking:** Masks phone numbers (`+90 5*********`). etc.

### Step 4: Metadata Enricher

* Extracts contextual data strictly from the generic `MessageEnvelope` properties (Broker-Agnostic).
* **Transaction No / Id:** Mapped from `envelope.MessageId`.
* **Sender Id:** Mapped from `envelope.SenderId`.
* **Timestamp:** Mapped from `envelope.Timestamp`.


* Calculates `Criticality` based on `envelope.Payload.Level` (e.g., CRITICAL = "High", ERROR = "Medium").
* Packages the sanitized payload, tags, and category into the final `EnrichedLog` object (4.3).

### Step 5: Router & Formatter

* Maps the `Category` to specific target roles using a configuration dictionary.
* `Database` -> Developer
* `Auth` -> Security
* `System` -> SysAdmin


* Applies **Fan-out/Multicast**: If a log matches multiple roles, it is processed for all of them.
* Invokes the correct Strategy Formatter:
* Developer -> Outputs `{...}` formatted JSON.
* Security -> Outputs `<...>` formatted CSV.
* SysAdmin -> Outputs `{~...~}` formatted Markdown.

> **[ARCHITECTURAL DECISION — 2026-05-27]** SysAdmin Formatter Output Format:
> SysAdmin output requires flexibility to support both **Markdown (`.md`)** and **HTML (`.html`)** formats. The architecture must allow enabling either or both simultaneously via configuration.
> **Future migration path:** We will introduce an `HtmlFormatterStrategy` alongside the current `MarkdownFormatterStrategy`. The router will be capable of mapping the `SysAdmin` role to multiple strategies simultaneously based on configuration, fully adhering to the Open/Closed Principle (OCP).


* Writes concurrently to shared Docker Volumes.

## 6. System Components Details

### Container 1: Log Producer

* MUST use a BackgroundService to simulate high-frequency stock operations.
* Controlled via `docker-compose.yml` environment variables: `LogsPerSecond`, `ErrorRate`.
* MUST set `MessageId`, `AppId`, and `Timestamp` on the broker's message headers/properties before publishing.

### Container 2: Middleware

* Configures dependencies via `IServiceCollection`.
* Implements resilient broker connection (Polly retries).

### 6.3. Metrics & Performance Tracking (Throughput Reporter)
MUST implement a thread-safe Singleton PerformanceTracker injected into the pipeline.

Tracking Points:

TotalReceived: Incremented when the adapter reads a message.

DroppedByFilter: Incremented in Steps 1 and 2.

SuccessfullyProcessed: Incremented after Step 5 file writing.

Reporting: A background timer MUST log these metrics to the console every 5 seconds. This provides real-time proof of the system's performance range and bottleneck (I/O vs CPU) during the stress test.

## 7. Execution Constraints

* Do not introduce LLM calls.
* Write clean, SOLID, and highly concurrent C# code.
* Provide a unified `docker-compose.yml` that stands up RabbitMQ, Producer, and Middleware together.

```
