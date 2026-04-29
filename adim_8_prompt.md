# Adım 8 — Worker + Hibrit Mail Sistemi

## Genel mantık

İki ayrı tetikleyici, ayrı mail tipleri:

```
HER SAAT (Worker:IntervalMinutes):
  Job: HourlyNewInvoiceCheck
  - Tüm IsActive firmaları sırayla tara
  - Yeni fatura → "yeni" maili at (mevcut akış)

HER SABAH (Worker:DailySummaryHour, default 9):
  Job: DailySummary  
  - Tüm IsActive firmaları sırayla tara
  - Onayda bekleyen TÜM faturalar (yeni + eski) → özet mail
  - Boş gün davranışı: BOS_GUN_DAVRANISI ile belirlenir
```

İki job birbirinden bağımsız.

## Yapılacaklar

### 1. IInvoiceOrchestrator interface güncelle

```csharp
public interface IInvoiceOrchestrator
{
    Task RunHourlyCheckAsync(CancellationToken ct);   // Saatlik: yeni fatura bildirimi
    Task RunDailySummaryAsync(CancellationToken ct);  // Sabah: özet
    Task RunForCompanyAsync(int companyId, CancellationToken ct);  // Manuel test
}
```

### 2. InvoiceOrchestrator implementasyonu

`Sovos.InvoiceWorker/Services/InvoiceOrchestrator.cs`

- `RunHourlyCheckAsync`:
  - DB'den IsActive=true firmaları çek
  - Her firma sıralı (5sn aralıklı):
    - try-catch içinde:
      - Scrape (mevcut)
      - SaveAndGetNewAsync (mevcut, retry-aware)
      - Boş değilse SendNewInvoicesAsync (mevcut "yeni" mail)
      - MarkAsNotifiedAsync
    - Hata: log + Company.LastFailedRunAt + LastErrorMessage
    - Başarı: Company.LastSuccessfulRunAt + InvoiceCountLastRun

- `RunDailySummaryAsync`:
  - DB'den IsActive=true firmaları çek
  - Her firma sıralı:
    - try-catch içinde:
      - Scrape (mevcut)
      - "Yeni" diff'i de yap (saatlik gibi davransın → eğer arada saatlik çalışmadıysa kaçanları yakala)
      - SONRA: tüm scraped listeyi mailSender.SendDailySummaryAsync'e ver
      - Boş ise BOS_GUN_DAVRANISI'na göre hareket et

### 3. IMailSender'a yeni metot ekle

```csharp
public interface IMailSender
{
    Task SendNewInvoicesAsync(Company company, List<Invoice> newInvoices, CancellationToken ct);
    Task SendDailySummaryAsync(Company company, List<ScrapedInvoice> allPending, CancellationToken ct);
}
```

`SendDailySummaryAsync` mail içeriği:
- Konu: `[Sovos Günlük Özet] {FirmaAdı} - {N} fatura onayda bekliyor`
- Body:
  - "{FirmaAdı} firması için günlük onay bekleyen fatura özeti:"
  - HTML tablosu (mevcut SendNewInvoicesAsync gibi: Fatura No, Tedarikçi, Tutar, Para Birimi, Düzenlenme, Son Ödeme)
  - Toplam tutar (para birimine göre gruplu)
  - "Bu, günlük otomatik özet mailidir."
- Boş gün: BOS_GUN_DAVRANISI'na göre

> BOS_GUN_DAVRANISI = "Boş günde de mail at"
> 
> Liste boş ise:
> - Konu: `[Sovos Günlük Özet] {FirmaAdı} - Onayda bekleyen fatura yok`
> - Body:
>   - "{FirmaAdı} firması için günlük onay bekleyen fatura özeti:"
>   - "✓ Şu an onayda bekleyen fatura bulunmuyor."
>   - "Bu, günlük otomatik özet mailidir."
> - Mail yine gönderilsin, log'la "Boş özet maili gönderildi"

### 4. InvoiceCheckWorker (BackgroundService)

`Sovos.InvoiceWorker/Workers/InvoiceCheckWorker.cs`

```csharp
public class InvoiceCheckWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<InvoiceCheckWorker> _logger;
    
    private DateTime _lastHourlyRun = DateTime.MinValue;
    private DateTime _lastDailyRun = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var initialDelay = _config.GetValue<int>("Worker:InitialDelaySeconds", 30);
        var hourlyInterval = _config.GetValue<int>("Worker:IntervalMinutes", 60);
        var dailySummaryHour = _config.GetValue<int>("Worker:DailySummaryHour", 9);
        
        _logger.LogInformation(
            "Worker başlatılıyor. InitialDelay={Delay}sn, HourlyInterval={Interval}dk, DailyHour={Hour}",
            initialDelay, hourlyInterval, dailySummaryHour);
        
        await Task.Delay(TimeSpan.FromSeconds(initialDelay), ct);
        
        while (!ct.IsCancellationRequested)
        {
            var now = DateTime.Now;
            
            // Saatlik check
            if ((now - _lastHourlyRun).TotalMinutes >= hourlyInterval)
            {
                await RunSafelyAsync("HourlyCheck", 
                    o => o.RunHourlyCheckAsync(ct), ct);
                _lastHourlyRun = now;
            }
            
            // Günlük özet (sadece bir kez, belirlenen saatte)
            if (now.Hour == dailySummaryHour 
                && _lastDailyRun.Date != now.Date)
            {
                await RunSafelyAsync("DailySummary",
                    o => o.RunDailySummaryAsync(ct), ct);
                _lastDailyRun = now;
            }
            
            // 1 dakika bekle, sonra tekrar kontrol et
            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }
    
    private async Task RunSafelyAsync(string jobName, 
        Func<IInvoiceOrchestrator, Task> action, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider
                .GetRequiredService<IInvoiceOrchestrator>();
            
            _logger.LogInformation("=== {JobName} BAŞLADI ===", jobName);
            var sw = Stopwatch.StartNew();
            await action(orchestrator);
            _logger.LogInformation("=== {JobName} BİTTİ ({Duration}ms) ===", 
                jobName, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{JobName} hata fırlattı, döngü devam edecek", jobName);
        }
    }
}
```

Worker DI'ya ekle:
```csharp
builder.Services.AddHostedService<InvoiceCheckWorker>();
```

### 5. appsettings.json güncellemeleri

```json
"Worker": {
  "InitialDelaySeconds": 30,
  "IntervalMinutes": 60,
  "DailySummaryHour": 9
}
```

### 6. Test akışı

`TestScraperOnStartup` flag'i kalsın ama mantığını güncelle:
- `true` ise: Worker başlamasın, mevcut tek-firma testi çalışsın (debug için)
- `false` ise: Worker başlasın, döngü çalışsın

Şu an için test:
1. `TestScraperOnStartup = true` → tek firma testi çalışsın, eski davranış korunsun
2. Sonra `false` yapıp Worker'ı dene
3. Test için `IntervalMinutes = 1` yap (1 dakikada bir tara), 2-3 dk bekle, log'da "HourlyCheck BAŞLADI" görmeyi bekle
4. Hiçbir mail gelmemeli (zaten bildirilmiş, idempotent)
5. Sonra `IntervalMinutes = 60`'a geri döndür

### 7. Kritik kurallar

- Worker exception'da DURMAMALI, log'la, devam et
- Saatlik ve günlük job aynı anda çalışırsa karışıklık olmasın diye bir lock veya sıralı çağırma yapabilirsin (basit olsun, sıralı yeter)
- Türkiye saati (UTC+3) için DateTime.Now kullan, DateTime.UtcNow değil (DailySummaryHour saati TR'ye göre)
- Daily summary günde **sadece bir kez** çalışmalı — `_lastDailyRun.Date != now.Date` kontrolü kritik

## Done tanımı

- [ ] Worker BackgroundService olarak başlıyor
- [ ] 30sn initial delay sonrası ilk çalışma
- [ ] HourlyCheck doğru intervalde çalışıyor
- [ ] DailySummary belirtilen saatte (saat 9'da) günde 1 kez çalışıyor
- [ ] Boş gün → "Onayda fatura yok" maili atılıyor (sistem canlı sinyali)
- [ ] Hata olan firma diğerlerini etkilemiyor
- [ ] Company.LastSuccessfulRunAt / LastFailedRunAt güncelleniyor

Test ettikten sonra durumu paylaş.
