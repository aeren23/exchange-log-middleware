# Exchange Log Middleware — Proje Raporu

## İçindekiler

1. [Projeye Genel Bakış](#1-projeye-genel-bakış)
2. [Sistem Mimarisi](#2-sistem-mimarisi)
3. [Tasarım Kalıpları (Design Patterns)](#3-tasarım-kalıpları-design-patterns)
4. [Pipeline Akışı (Veri İşleme Hattı)](#4-pipeline-akışı-veri-i̇şleme-hattı)
5. [Veri Modelleri](#5-veri-modelleri)
6. [Sistem Performansı ve Metrikler](#6-sistem-performansı-ve-metrikler)
7. [Konteynerizasyon ve Altyapı](#7-konteynerizasyon-ve-altyapı)
8. [Test Stratejisi ve Doğrulama](#8-test-stratejisi-ve-doğrulama)
9. [SOLID Prensiplerinin Uygulanması](#9-solid-prensiplerinin-uygulanması)

---

## 1. Projeye Genel Bakış

Bu proje, bir borsa (stock market) uygulaması için tasarlanmış **kurumsal düzeyde (enterprise-grade), olay tabanlı (event-driven) bir veri ara katman (middleware) sistemidir.**

Sistem iki Docker konteynerinde çalışan iki mikroservisten oluşur:

| Bileşen | Görev | Konteyner |
|---|---|---|
| **Log Producer** | Yüksek frekanslı borsa log verisi üretir ve kuyruğa yayınlar | `exchange-producer` |
| **Middleware** | Kuyruktaki logları filtreler, anonimleştirir, zenginleştirir ve formatlar | `exchange-middleware` |

Bu iki servis arasındaki iletişim, **RabbitMQ mesaj kuyruğu** üzerinden asenkron olarak gerçekleştirilir. Sistem ayrıca konfigürasyon değişikliği ile **Azure Service Bus** desteği de sunacak şekilde tasarlanmıştır.

### Teknoloji Yığını (Technology Stack)

- **Framework:** .NET 8 (C#)
- **Mimari:** Mikroservisler, Olay Tabanlı (Event-Driven)
- **Eşzamanlılık:** `System.Threading.Channels` (bellek güvenli dahili tamponlama)
- **Mesaj Kuyruğu:** RabbitMQ (birincil), Azure Service Bus (adaptör desteği ile)
- **Konteynerizasyon:** Docker & docker-compose
- **Dayanıklılık (Resilience):** Polly kütüphanesi (Exponential Backoff ile yeniden deneme)

---

## 2. Sistem Mimarisi

### Genel Akış

Sistem, bir üretici–kuyruk–tüketici (Producer–Queue–Consumer) mimarisi üzerine kuruludur:

```
┌──────────────┐     ┌──────────────┐     ┌──────────────────────────────────────────┐
│              │     │              │     │          MIDDLEWARE (Tüketici)            │
│   PRODUCER   │────>│Message Broker│────>│                                          │
│  (Üretici)   │     │   (Kuyruk)   │     │  Channel → Pipeline → Output Dosyaları  │
│              │     │              │     │                                          │
└──────────────┘     └──────────────┘     └──────────────────────────────────────────┘
```

### Neden Bu Mimari?

1. **Gevşek Bağlılık (Loose Coupling):** Producer ve Middleware birbirini tanımaz. RabbitMQ araya girerek bu iki servisi birbirinden tamamen bağımsız hale getirir. Biri çökse bile diğeri çalışmaya devam eder; mesajlar kuyrukta birikir.

2. **Asenkron İletişim:** Producer saniyede yüzlerce log üretirken, Middleware kendi hızında bu logları işler. Kuyruk, ikisi arasındaki hız farkını yumuşatır (backpressure).

3. **Ölçeklenebilirlik:** Gerekirse birden fazla Middleware konteyneri aynı kuyruktan okuyarak yatay ölçeklendirme yapılabilir.

---

## 3. Tasarım Kalıpları (Design Patterns)

Projede 4 ana tasarım kalıbı kullanılmıştır. Her birinin seçilme nedeni ve uygulanma şekli aşağıda detaylandırılmıştır.

### 3.1 Chain of Responsibility (Sorumluluk Zinciri)

**Nerede:** Middleware içindeki ana işleme hattı (`Pipeline/Handlers/`)

**Neden Seçildi:** Bir log mesajının üzerinden sırasıyla 5 farklı işlemin (filtreleme, tekilleştirme, maskeleme, zenginleştirme, yönlendirme) geçmesi gerekiyordu. Bu işlemlerin hepsini tek bir devasa fonksiyona yazmak hem okunabilirliği bozar hem de test edilebilirliği neredeyse imkânsız hale getirir. Sorumluluk Zinciri kalıbı sayesinde her bir adım kendi bağımsız sınıfında yaşar, sadece kendi işini yapar ve mesajı bir sonraki adıma iletir.

**Nasıl Uygulandı:** Tüm handler'lar `BasePipelineHandler` soyut sınıfından türer. Bu sınıf zincir mantığını (`SetNext`, `NextAsync`) merkezileştirir:

```csharp
// BasePipelineHandler.cs — Zincir altyapısı
public abstract class BasePipelineHandler : IPipelineHandler
{
    private IPipelineHandler? _nextHandler;

    public IPipelineHandler SetNext(IPipelineHandler next)
    {
        _nextHandler = next;
        return next; // Fluent chaining: handler1.SetNext(handler2).SetNext(handler3)
    }

    protected Task NextAsync(PipelineContext context, CancellationToken ct)
        => _nextHandler?.HandleAsync(context, ct) ?? Task.CompletedTask;
}
```

**DROP ve PASS Mekanizması:**
- Bir handler mesajı geçirmek istediğinde `await NextAsync(context, ct)` çağrısı yapar → mesaj zincirde ilerler.
- Bir handler mesajı düşürmek istediğinde `NextAsync` çağırmadan doğrudan `return` yapar → zincir durur, mesaj işlenmez.

**Zincir Sırası (asla değiştirilmez):**

```
LevelFilter → DeduplicationFilter → KvkkAnonymizer → MetadataEnricher → RouterAndFormatter
```

### 3.2 Adapter Pattern (Adaptör Kalıbı)

**Nerede:** Mesaj kuyruğu iletişim katmanı (`Shared/Broker/`)

**Neden Seçildi:** Middleware'in çekirdek pipeline mantığı, **hangi mesaj kuyruğu sisteminin kullanıldığını bilmemelidir.** Yarın RabbitMQ yerine Azure Service Bus veya Kafka kullanılsa bile pipeline kodlarının tek bir satırının bile değişmemesi gerekir. Bu karar, SOLID prensiplerinden **Dependency Inversion Principle (DIP)** ile doğrudan örtüşür.

**Nasıl Uygulandı:** Ortak bir `IMessageBroker` arayüzü tanımlandı. Her kuyruk sistemi bu arayüzü kendi API'sine göre implemente eder:

```csharp
// IMessageBroker.cs — Adaptör sözleşmesi
public interface IMessageBroker : IAsyncDisposable
{
    Task PublishAsync<T>(T payload, CancellationToken ct = default) where T : class;

    Task SubscribeAsync<T>(
        Func<MessageEnvelope<T>, Task> onMessageReceived,
        CancellationToken ct = default) where T : class;

    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
```

**Boundary Translation (Sınır Çevirimi):**
Adaptörün en kritik görevi, broker'a özgü mesajı (RabbitMQ header'ları, property'leri vb.) pipeline'ın anlayacağı genel bir formata (`MessageEnvelope<T>`) dönüştürmektir:

```csharp
// RabbitMqAdapter.cs — Sınır çevirimi
var envelope = new MessageEnvelope<T>
{
    Payload   = payload,                                          // JSON body
    MessageId = ea.BasicProperties.MessageId ?? Guid.NewGuid().ToString(), // RabbitMQ MessageId
    SenderId  = ea.BasicProperties.AppId ?? "Unknown",           // RabbitMQ AppId
    Timestamp = publishTime                                      // Özel header'dan çıkarılır
};
```

Bu çevirim sayesinde pipeline, `MessageEnvelope<T>` ile çalışır ve RabbitMQ'nun varlığından habersizdir.

### 3.3 Strategy Pattern (Strateji Kalıbı)

**Nerede:** Çıktı formatlama katmanı (`Pipeline/Formatters/`)

**Neden Seçildi:** Farklı departmanlar logları farklı formatlarda okumak ister:
- **Developer** → JSON (`.jsonl`) — makine tarafından okunabilir
- **Security** → CSV (`.csv`) — tablo formatında analiz için
- **SysAdmin** → Markdown (`.md`) ve HTML (`.html`) — okunabilir raporlar

Her format için ayrı bir `if-else` bloğu yazmak yerine, ortak bir arayüz üzerinden polimorfizm kullanarak her stratejiyi bağımsız bir sınıfta tanımlamak, **Open/Closed Principle (OCP)** gereğidir: "Yeni format eklemek için mevcut kodu değiştirme, sadece yeni sınıf ekle."

**Nasıl Uygulandı:**

```csharp
// IFormatterStrategy.cs — Strateji sözleşmesi
public interface IFormatterStrategy
{
    TargetRole TargetRole { get; }     // Hangi departman için?
    string FileExtension { get; }      // Dosya uzantısı (.jsonl, .csv, .md)
    string Format(EnrichedLog log);    // Formatlama mantığı
}
```

Her strateji bu arayüzü kendi formatına göre implemente eder. Örneğin CSV formatlayıcı:

```csharp
// CsvFormatterStrategy.cs
public sealed class CsvFormatterStrategy : IFormatterStrategy
{
    public TargetRole TargetRole => TargetRole.Security;
    public string FileExtension => ".csv";

    public string Format(EnrichedLog log)
    {
        var safeMessage = log.SanitizedMessage?.Replace("\"", "\"\"") ?? string.Empty;
        return $"{log.MessageId},{log.Timestamp:O},{log.SenderId},...,\"{safeMessage}\"";
    }
}
```

**Yeni format eklemek istendiğinde** (örneğin XML):
1. `XmlFormatterStrategy` sınıfı oluşturulur.
2. DI konteynerine kaydedilir.
3. Hiçbir mevcut kod değiştirilmez — **OCP tam olarak budur.**

### 3.4 Factory Pattern (Fabrika Kalıbı)

**Nerede:** `Pipeline/Formatters/FormatterFactory.cs`

**Neden Seçildi:** Router handler'ı bir kategoriye göre hedef rolleri belirler, ardından bu roller için uygun formatlayıcı stratejileri alması gerekir. Hangi rolün hangi stratejiyi kullanacağı konfigürasyona bağlıdır (örneğin SysAdmin rolü hem Markdown hem HTML üretebilir). Bu kararı Router'ın kendisine vermek SRP'yi ihlal ederdi.

**Nasıl Uygulandı:** `FormatterFactory`, DI konteynerinden kayıtlı tüm `IFormatterStrategy` implementasyonlarını alır ve konfigürasyona göre ilgili role ait aktif stratejileri döner.

---

## 4. Pipeline Akışı (Veri İşleme Hattı)

Her log mesajı, Middleware tarafından aşağıdaki 5 adımlık boru hattından **sırayla** geçirilir. Sıralama kasıtlıdır ve değiştirilemez:

### Adım 1: Performance Level Filter (Seviye Filtresi)

**Görev:** Konfigürasyondaki `MinimumLogLevel` (varsayılan: ERROR) değerinin altındaki logları hemen düşürür.

**Neden en başta?** Pipeline'ın en ağır işlemleri (Regex maskeleme, dosya yazma) sonraki adımlardadır. Düşük öncelikli logları (INFO, WARN) en başta elersek, bu loglar için boşuna CPU tüketmemiş oluruz. Bu bir "Fail Fast" optimizasyonudur.

```csharp
// LevelFilterHandler.cs
if (incomingLevel < _minimumLevel)
{
    _performanceTracker.IncrementDroppedByFilter();
    return; // DROP — NextAsync çağrılmaz, zincir durur
}
await NextAsync(context, cancellationToken); // PASS
```

### Adım 2: Deduplication Filter (Tekilleştirme Filtresi)

**Görev:** `MessageId`'yi `IMemoryCache`'de kontrol eder. Daha önce işlenmiş bir mesaj gelirse düşürür.

**Neden gerekli?** Mesaj kuyruğu sistemleri "en az bir kez teslim (at-least-once delivery)" garantisi verir. Ağ dalgalanmalarında aynı mesaj birden fazla kez iletilebilir. Bu katman veri bütünlüğünü sağlar ve idempotent (tekrarlanabilir) bir tüketici davranışı oluşturur.

```csharp
// DeduplicationFilterHandler.cs
if (_cache.TryGetValue(messageId, out _))
{
    _performanceTracker.IncrementDroppedByFilter();
    return; // DROP — duplicate mesaj
}
_cache.Set(messageId, true, cacheOptions); // Yeni mesaj → cache'e ekle
await NextAsync(context, cancellationToken);
```

### Adım 3: KVKK Anonymizer (Kişisel Veri Maskeleme)

**Görev:** Mesajın `Message` ve `RawData` alanlarını Regex ile tarar; TCKN, kredi kartı, e-posta ve telefon numarası gibi kişisel verileri maskeler.

**Neden zenginleştirmeden (Enrichment) önce?** Maskeleme, zenginleştirme ve yönlendirmeden **önce** yapılmalıdır. Aksi halde ham kişisel veri çıktı dosyalarına sızabilir.

**Maskeleme kuralları:**

| Veri Türü | Girdi Örneği | Çıktı Örneği |
|---|---|---|
| TCKN (11 hane) | `33709045226` | `33*********` |
| Kredi Kartı (16 hane) | `5235 1234 5678 9012` | `5235 **** **** 9012` |
| E-posta | `ahmet@gmail.com` | `a****@****.com` |
| Telefon (+90) | `+90 532 123 4567` | `+90 5*********` |

**TCKN Checksum Doğrulaması:**
Basit bir "11 haneli sayıyı maskele" yaklaşımı kullanılsaydı, borsa işlem numaraları gibi 11 haneli sayılar da yanlışlıkla maskelenirdi. Bunun yerine Türkiye Cumhuriyeti Kimlik Numarası algoritması uygulanır. Yalnızca bu algoritmayı geçen sayılar gerçek TCKN kabul edilip maskelenir:

```csharp
// KvkkAnonymizerHandler.cs — Checksum doğrulaması
private static bool IsValidTckn(string digits)
{
    if (digits[0] == '0') return false; // İlk hane 0 olamaz

    // Kural: ((d1+d3+d5+d7+d9)*7 - (d2+d4+d6+d8)) mod 10 = d10
    var oddSum  = d[0] + d[2] + d[4] + d[6] + d[8];
    var evenSum = d[1] + d[3] + d[5] + d[7];
    var tenthDigit = (oddSum * 7 - evenSum) % 10;

    if (tenthDigit != d[9]) return false;

    // Kural: (d1+d2+...+d10) mod 10 = d11
    return totalSum % 10 == d[10];
}
```

### Adım 4: Metadata Enricher (Veri Zenginleştirme)

**Görev:** `MessageEnvelope` üzerindeki broker metadata'sını (MessageId, SenderId, Timestamp) ve payload bilgilerini birleştirerek nihai `EnrichedLog` nesnesini oluşturur.

**Criticality (Kritiklik) Hesaplaması:**

| Log Seviyesi | Kritiklik |
|---|---|
| CRITICAL | High |
| ERROR | Medium |
| WARN | Low |
| INFO | Low |

### Adım 5: Router & Formatter (Yönlendirme ve Formatlama)

**Görev:** Kategoriye göre hedef departmanı belirler, uygun formatlayıcı stratejiyi seçer ve dosyaya yazar.

**Routing (Yönlendirme) kuralları:**

| Kategori | Hedef Rol | Çıktı Formatı |
|---|---|---|
| Database | Developer | `.jsonl` (JSON Lines) |
| Auth | Security | `.csv` (Comma-Separated Values) |
| System | SysAdmin | `.md` (Markdown) + `.html` (HTML) |

**Fan-out (Çoklu Yayın):** Bir log birden fazla role yönlendiriliyorsa, her hedef rol için ayrı ayrı formatlanır ve ilgili dosyaya yazılır.

---

## 5. Veri Modelleri

Veri, sistemde 3 farklı model olarak evrilerek ilerler:

### 5.1 LogPayload (Producer Çıktısı)

Producer'ın ürettiği ham, hafif JSON verisi. Metadata (zaman damgası, ID) içermez — bunlar broker header'larına eklenir:

```json
{
  "Level": "ERROR",
  "Category": "Auth",
  "Message": "Failed login attempt for account...",
  "RawData": "Müşteri TCKN: 33709045226 ile işlem doğrulandı"
}
```

### 5.2 MessageEnvelope\<T\> (Sınır Çevirimi Sonrası)

Adaptör tarafından broker-specific veriden oluşturulan genel zarf. Pipeline yalnızca bu modeli tanır:

```csharp
public class MessageEnvelope<T>
{
    public T Payload { get; set; }       // LogPayload
    public string MessageId { get; set; } // Broker'ın atadığı benzersiz ID
    public string SenderId { get; set; }  // Broker'ın AppId header'ı
    public DateTime Timestamp { get; set; } // Yayınlanma zamanı
}
```

### 5.3 EnrichedLog (Pipeline Çıktısı)

Tüm pipeline adımlarından geçtikten sonra dosyaya yazılmaya hazır nihai model:

```csharp
public sealed class EnrichedLog
{
    public required string MessageId { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string SenderId { get; init; }
    public required string Criticality { get; init; }  // "High", "Medium", "Low"
    public required LogLevel Level { get; init; }
    public required LogCategory Category { get; init; }
    public required string SanitizedMessage { get; init; }  // Maskelenmiş mesaj
    public string? SanitizedRawData { get; init; }           // Maskelenmiş ham veri
}
```

---

## 6. Sistem Performansı ve Metrikler

### Thread-Safe Performans İzleme

`PerformanceTracker` sınıfı, pipeline boyunca 3 kritik metriği **lock-free** ve **thread-safe** olarak takip eder:

```csharp
// PerformanceTracker.cs — Interlocked ile atomik sayaç artırımı
public void IncrementTotalReceived()
    => Interlocked.Increment(ref _totalReceived);
```

| Metrik | Artırım Noktası | Açıklama |
|---|---|---|
| `TotalReceived` | Broker'dan mesaj alındığında | Toplam alınan mesaj sayısı |
| `DroppedByFilter` | Adım 1 veya Adım 2'de DROP yapıldığında | Filtrelenen mesaj sayısı |
| `SuccessfullyProcessed` | Adım 5'te dosyaya yazıldığında | Başarıyla işlenen mesaj sayısı |

**Neden `Interlocked`?** Birden fazla worker thread aynı anda sayaçları artırabilir. `lock` kullanmak performansı düşürür. `Interlocked` ise donanım seviyesinde atomik operasyon sağlayarak sıfır kilit (lock-free) çözüm sunar.

### Gerçek Zamanlı Raporlama

`MetricsReporterService` (BackgroundService), her 5 saniyede bir konsola metrik raporu basar:

```
[METRICS] TotalReceived: 1992 | DroppedByFilter: 1370 | SuccessfullyProcessed: 619 | Throughput: 31.2 msg/s
```

### Thread-Safe Dosya Yazımı

`ThreadSafeFileWriter`, `ConcurrentDictionary` ve `SemaphoreSlim` kullanarak eşzamanlı (concurrent) dosya yazımını güvenli hale getirir:

```csharp
// ThreadSafeFileWriter.cs — Fine-grained locking
var semaphore = _locks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
await semaphore.WaitAsync(cancellationToken);
try
{
    await File.AppendAllTextAsync(filePath, content + Environment.NewLine, ct);
}
finally
{
    semaphore.Release();
}
```

**Neden dosya başına ayrı kilit?** `developer.jsonl` dosyasına yazan bir thread, `security.csv` dosyasına yazan başka bir thread'i bloklamamalıdır. Her dosya için ayrı bir `SemaphoreSlim` tutularak darboğaz (bottleneck) önlenir.

### Dahili Tamponlama

`System.Threading.Channels`, broker'dan gelen mesajları pipeline worker'larına güvenli bir şekilde iletmek için kullanılır. Bu yapı:
- Bellek sızıntılarını (memory leak) önler
- Üretici ve tüketici arasındaki hız farkını yumuşatır (backpressure)
- Birden fazla worker thread'in aynı anda güvenli bir şekilde mesaj okumasını sağlar

---

## 7. Konteynerizasyon ve Altyapı

### Docker Compose Mimarisi

Tüm sistem tek bir `docker-compose up -d` komutuyla ayağa kalkar:

| Konteyner | Görüntü (Image) | Görev |
|---|---|---|
| `exchange-rabbitmq` | `rabbitmq:3-management` | Mesaj kuyruğu (AMQP + Yönetim paneli) |
| `exchange-producer` | Özel Dockerfile (Multi-stage) | Log üretimi |
| `exchange-middleware` | Özel Dockerfile (Multi-stage) | Log işleme pipeline'ı |
| `exchange-sqledge` | `mcr.microsoft.com/azure-sql-edge` | ASB emülatörü için SQL backend |
| `exchange-servicebus-emulator` | `mcr.microsoft.com/azure-messaging/servicebus-emulator` | Azure Service Bus emülatörü |

### Multi-Stage Dockerfile

Hem Producer hem Middleware için aynı strateji uygulanır:
1. **Build Stage:** `dotnet/sdk:8.0` görüntüsünde derleme yapılır.
2. **Runtime Stage:** `dotnet/aspnet:8.0` hafif görüntüsünde yalnızca derlenmiş çıktı kopyalanarak çalıştırılır.

Bu yaklaşım sayesinde final konteyner boyutu SDK'ya kıyasla ~3 kat küçülür.

### Paylaşımlı Docker Volume

Producer ve Middleware, `output-data` adlı paylaşımlı bir Docker volume üzerinden ortak çıktı dizinine (`/app/output`) yazarlar. Bu volume, yerel makinedeki `./output` klasörüne bağlıdır (bind mount).

### Konfigürasyon Yönetimi

Tüm çevresel ayarlar `.env` dosyasından okunur ve `docker-compose.yml` aracılığıyla konteynerlere aktarılır:

| Değişken | Varsayılan | Açıklama |
|---|---|---|
| `BROKER_PROVIDER` | `RabbitMQ` | Kullanılacak kuyruk sistemi |
| `PRODUCER_LOGS_PER_SECOND` | `10` | Saniyede üretilecek log sayısı |
| `PRODUCER_ERROR_RATE` | `0.3` | ERROR/CRITICAL log üretim oranı (%30) |
| `PIPELINE_MINIMUM_LOG_LEVEL` | `ERROR` | Filtreleme eşik seviyesi |
| `PIPELINE_DEDUP_CACHE_TTL_MINUTES` | `10` | Tekilleştirme cache süresi (dakika) |

---

## 8. Test Stratejisi ve Doğrulama

### Birim Testleri (Unit Tests)

Proje boyunca toplam **82+ birim testi** yazılmıştır. Her handler, formatter, factory ve I/O bileşeni için ayrı test sınıfları mevcuttur. Örnek test konuları:

- KVKK maskeleme doğruluğu (geçerli TCKN maskelenir, geçersiz TCKN maskelenmez)
- Level filtrenin doğru seviyeleri düşürmesi
- Deduplication filtrenin aynı MessageId'yi yakalaması
- ThreadSafeFileWriter'ın 100 thread'lik eşzamanlı yazma testinden geçmesi
- Formatter stratejilerinin doğru formatta çıktı üretmesi

### Uçtan Uca (E2E) Doğrulama

`scripts/validate-e2e.ps1` betiği, sistemi 16 farklı kontrol noktasından otomatik olarak denetler:

1. **Konteyner Durumu:** Tüm servislerin çalışıp çalışmadığı
2. **Dosya Mevcudiyeti:** `developer.jsonl`, `security.csv`, `sysadmin.md` dosyalarının oluşup oluşmadığı
3. **JSON Format Doğrulaması:** JSONL dosyasındaki her satırın geçerli JSON olup olmadığı
4. **CSV Format Doğrulaması:** Sütun başlıkları ile veri satırlarının tutarlılığı
5. **Markdown Format Doğrulaması:** Markdown yapısının doğruluğu
6. **KVKK Sızdırmazlık Testi:** Çıktı dosyalarında maskelenmemiş TCKN, kredi kartı veya telefon numarası aranması
7. **Performans Metrikleri:** Middleware loglarında `[METRICS]` çıktısının varlığı
8. **Pipeline Akış Doğrulaması:** Handler'ların doğru sırada başlatıldığının loglardan teyidi

Başarılı çalıştırma sonucu:

```
============================================
 RESULT: 16 PASS / 0 FAIL
============================================
```

---

## 9. SOLID Prensiplerinin Uygulanması

### Single Responsibility Principle (SRP — Tek Sorumluluk)

Her sınıf **tek bir işten** sorumludur:
- `LevelFilterHandler` → Sadece seviye filtreler
- `KvkkAnonymizerHandler` → Sadece kişisel verileri maskeler
- `PerformanceTracker` → Sadece sayaçları tutar (raporlamaz)
- `MetricsReporterService` → Sadece raporlar (sayaç tutmaz)
- `ThreadSafeFileWriter` → Sadece dosyaya yazar (formatlama yapmaz)

### Open/Closed Principle (OCP — Açık/Kapalı)

Yeni bir formatlayıcı strateji (örneğin XML) eklemek için:
- Mevcut hiçbir sınıf değiştirilmez ✅
- Sadece yeni bir `XmlFormatterStrategy` sınıfı oluşturulur ✅
- DI konteynerine kaydedilir ✅

### Liskov Substitution Principle (LSP — Yerine Koyma)

Tüm handler'lar `BasePipelineHandler`'dan türer ve `HandleAsync` metodunu override eder. Herhangi bir handler, pipeline'da başka bir handler ile yer değiştirebilir — sistem bozulmaz.

### Interface Segregation Principle (ISP — Arayüz Ayrımı)

Her arayüz yalnızca ilgili sorumluluğa ait metotları içerir:
- `IMessageBroker` → Sadece broker operasyonları (Publish, Subscribe, IsHealthy)
- `IFormatterStrategy` → Sadece formatlama (Format, FileExtension, TargetRole)
- `IPerformanceTracker` → Sadece metrik işlemleri (Increment*, Read)
- `IFileWriter` → Sadece dosya yazımı (AppendLineAsync)

### Dependency Inversion Principle (DIP — Bağımlılık Tersine Çevirme)

Üst seviye modüller (Pipeline), alt seviye modüllere (RabbitMQ kütüphanesi) **doğrudan bağımlı değildir.** Her iki seviye de soyutlamalara (interface'lere) bağımlıdır:

```
Pipeline → IMessageBroker (soyutlama) ← RabbitMqAdapter (somut)
Pipeline → IFileWriter    (soyutlama) ← ThreadSafeFileWriter (somut)
```

Bu sayede test ortamında `Mock<IMessageBroker>` kullanılarak gerçek bir kuyruk sistemi olmadan birim testler yazılabilir.
