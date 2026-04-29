# Görev: Sovos NETInvoice Onay Bekleyen Fatura Takip Worker Service

## Önemli — bu prompt'u nasıl kullan
Bu spec uzun ama **hepsini oku**. Sondaki "Yapma sırası" bölümüne **harfiyen 
uy** — adım adım git, her adımda onay bekle. Verilmeyen bir bilgi varsa 
**uydurmak yerine bana sor**. Selector'lar ve ID'ler aşağıda **gerçek tespit 
edilmiş** değerlerdir, bunları değiştirme.

## Amaç
8 firma için saatte bir **Sovos NETInvoice (Digital Planet)** portalına otomatik 
giriş yapan, "E-Fatura → Gelen Kutusu → Onay Bekleyen" sayfasındaki faturaları 
DOM'dan parse eden, önceki çalışma ile karşılaştırıp **yeni gelen fatura varsa 
mail atan** bir .NET 8 Worker Service yazılacak. API erişimi yok, **Playwright 
ile UI otomasyonu** yapılacak.

## Mevcut altyapı (DOKUNMA)
- Solution'da zaten bir **ASP.NET Web API** projesi var. Bu projeye dokunma.
- DB: **MS SQL Server**. Web API'nin connection string'ini Worker da kullanacak 
  (aynı DB, ama yeni tablolar — `Companies`, `Invoices`).

## Teknik bağlam (Portal hakkında — uydurma)

Portal **DevExpress ASPxControls + ASP.NET WebForms** tabanlı. Aşağıdaki 
selector'lar gerçek tespit edilmiş değerlerdir.

### Login sayfası — kesin tespit edildi
- **Login URL:** `https://portal.digitalplanet.com.tr/login/index.html?returnUrl=/`
- **Şirket Kısa Kodu:** `#txtCorporateCode`
- **Kullanıcı Adı:** `#txtLoginName`
- **Şifre:** `#txtLoginPassword`
- **Giriş butonu:** `page.GetByRole(AriaRole.Button, new() { Name = "Giriş" })`
  (gerçek selector login butonuna F12 yapılınca netleşir, text-based en güvenli)
- **Captcha:** Sayfada `#divCaptcha` var ama default `display:none`. **Birden 
  fazla başarısız login denemesinde sunucu captcha'yı açabilir.** Worker bunu 
  tespit etmeli:
  ```csharp
  var captchaVisible = await page.IsVisibleAsync("#divCaptcha");
  if (captchaVisible) {
      _logger.LogError("Firma {CompanyName}: Captcha aktif, manuel müdahale gerekli", company.Name);
      throw new SovosCaptchaActiveException();
  }
  ```
  Captcha açılırsa o firma için retry yapma, atla, log'a yaz, **diğer firmaları 
  da etkileme** (sadece bu firmanın session'ı problemli).
- **Login form:** Normal HTML form (DevExpress DEĞİL), `<form class="g-py-15 g-px-30">` 
  içinde. Submit normal şekilde çalışır, AJAX bekleme gerektirmez.

### Onay Bekleyen sayfası — kesin tespit edildi

| Element | Selector |
|---|---|
| Sayfa adı (URL'de geçer) | `ListInvoices` |
| Başlangıç tarihi (text input) | `#InvoiceFilterBeginDate_I` |
| Bitiş tarihi (text input) | `#InvoiceFilterEndDate_I` |
| Sorgula butonu | `input[name='ctl01$MainMasterPageSplitter$Content$ListInvoices$btnRefresh']` |
| Fatura grid container | `#InvoiceGrid` |
| Loading panel | `#InvoiceGridLoadingPanel` |
| Çıkış linki | text="Çıkış" |

### Site davranışı — bunlara dikkat
- Tüm sorgular **AJAX callback** ile çalışır (full postback değil).
- `InvoiceGridLoadingPanel` Sorgula sonrası görünür → bunun `Hidden` olmasını 
  beklemek **en güvenilir bekleme yöntemi**. `NetworkIdle` veya `Task.Delay` 
  kullanma.
- Tarih input'ları DevExpress ASPxDateEdit. `_I` suffix'li input'a doğrudan 
  `FillAsync()` çalışır (`dd.MM.yyyy` formatında). Sonra `Tab` tuşu gönder ki 
  DevExpress değeri yedirsin.
- Grid satırları: `tr[id^='InvoiceGrid_DXDataRow']` selector'ı ile bulunur.
- Boş grid durumunda satır yoktur veya "Faturalarınızı listelemek için..." 
  mesajı görünür.

### Tablo kolonları (sırası)
1. Firma Ünvanı, 2. Fatura No, 3. Gönderici VKN, 4. Para Birimi, 
5. Fatura Tutarı, 6. Toplam Vergi, 7. İskonto Tutarı, 8. Artırım, 
9. Sipariş No, 10. Son Ödeme Tarihi, 11. Düzenlenme Tarihi, 12. Oluşturulma Tarihi

> Not: Grid'in solunda checkbox kolonu olabilir, parse ederken text() içeriğine 
> bak, boşları atla. Selector için `td.dxgv` veya nth-child kullan, deneyerek 
> doğrula.

### Format örnekleri
- Tarih (saatli): `20.04.2026 00:00:00`
- Tarih (saatsiz): `23.04.2026`
- Tutar: `16.720,04` (Türkçe locale, virgül ondalık, nokta binlik)
- Para birimi: `TRY`
- Fatura No: `ORT2026000002006`
- VKN: `6480001898` (10 haneli)

## Proje yapısı

Mevcut solution'a 3 yeni proje ekle:
- `Sovos.InvoiceWorker` — Worker Service (.NET 8, `Microsoft.NET.Sdk.Worker`)
- `Sovos.InvoiceWorker.Core` — Class library (entity'ler, interface'ler, DTO'lar)
- `Sovos.InvoiceWorker.Tests` — xUnit (sadece diff service için)

Mevcut Web API projesine **DOKUNMA**.

## Bağımlılıklar

```xml
<!-- Sovos.InvoiceWorker -->
<PackageReference Include="Microsoft.Playwright" Version="1.47.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.*" />
<PackageReference Include="Serilog.AspNetCore" Version="8.*" />
<PackageReference Include="Serilog.Sinks.File" Version="6.*" />
<PackageReference Include="Serilog.Sinks.Console" Version="6.*" />
<PackageReference Include="MailKit" Version="4.*" />
<PackageReference Include="Microsoft.AspNetCore.DataProtection" Version="8.*" />
```

Playwright kurulumu için README'ye `playwright install chromium` adımını ekle.

## Domain modeli

```csharp
public class Company
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string CompanyCode { get; set; }       // Şirket Kısa Kodu
    public string Username { get; set; }
    public string EncryptedPassword { get; set; } // IDataProtection ile encrypted
    public string NotificationEmail { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastSuccessfulRunAt { get; set; }
    public DateTime? LastFailedRunAt { get; set; }
    public string LastErrorMessage { get; set; }
}

public class Invoice
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public string FaturaNo { get; set; }          // ORT2026000002006
    public string GondericiVkn { get; set; }      // 6480001898
    public string FirmaUnvani { get; set; }
    public string ParaBirimi { get; set; }        // TRY
    public decimal FaturaTutari { get; set; }
    public decimal ToplamVergi { get; set; }
    public decimal IskontoTutari { get; set; }
    public decimal Artirim { get; set; }
    public string SiparisNo { get; set; }
    public DateTime? SonOdemeTarihi { get; set; }
    public DateTime? DuzenlenmeTarihi { get; set; }
    public DateTime? OlusturulmaTarihi { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime? NotifiedAt { get; set; }     // null = mail henüz atılmadı
}

// Unique constraint: (CompanyId, FaturaNo, GondericiVkn)
```

## Mimari (interface'ler)

```csharp
public interface ICredentialProtector
{
    string Encrypt(string plain);
    string Decrypt(string cipher);
}

public interface ISovosScraper
{
    Task<List<ScrapedInvoice>> FetchPendingInvoicesAsync(
        Company company, string decryptedPassword, CancellationToken ct);
}

public interface IInvoiceDiffService
{
    Task<List<Invoice>> SaveAndGetNewAsync(
        int companyId, List<ScrapedInvoice> scraped, CancellationToken ct);

    Task MarkAsNotifiedAsync(IEnumerable<long> invoiceIds, CancellationToken ct);
}

public interface IMailSender
{
    Task SendNewInvoicesAsync(
        Company company, List<Invoice> newInvoices, CancellationToken ct);
}

public interface IInvoiceOrchestrator
{
    Task RunForAllCompaniesAsync(CancellationToken ct);
    Task RunForCompanyAsync(int companyId, CancellationToken ct);
}
```

## Worker

```csharp
public class InvoiceCheckWorker : BackgroundService
{
    // appsettings: Worker:IntervalMinutes (default 60)
    // Her interval'da IInvoiceOrchestrator.RunForAllCompaniesAsync çağır
    // try-catch ile sarmala, exception'da logla ama döngüden çıkma
    // İlk run'ı startup'tan 30 saniye sonra yap (uygulama tam ayağa kalksın)
}
```

## Scraping algoritması (her firma için)

```
1. Yeni Playwright BrowserContext aç (her firma izole olsun, cookie/session karışmasın)
2. Login sayfasına git: `https://portal.digitalplanet.com.tr/login/index.html?returnUrl=/`
3. Alanları doldur (gerçek ID'ler):
   - `#txtCorporateCode` → company.CompanyCode
   - `#txtLoginName` → company.Username
   - `#txtLoginPassword` → decryptedPassword
   - Giriş butonuna tıkla
   - **Captcha kontrolü:** `#divCaptcha` görünür mü? Görünürse exception fırlat, 
     bu firmayı atla.
4. Login başarısını doğrula:
   - "Çıkış" linki görünüyorsa OK
   - 15 saniye içinde görünmezse veya hata mesajı varsa → throw, atla
5. Onay Bekleyen sayfasına git:
   - Sol menüden tıklayarak: E-Fatura → Gelen Kutusu → Onay Bekleyen
   - Veya doğrudan URL ile (URL'i tespit ettikten sonra appsettings'e ekle)
6. Tarih filtreleri (içinde bulunulan ayın 1'i ile son günü):
   var today = DateTime.Today;
   var firstDay = new DateTime(today.Year, today.Month, 1);
   var lastDay  = firstDay.AddMonths(1).AddDays(-1);
   var trCulture = new CultureInfo("tr-TR");
   var startStr = firstDay.ToString("dd.MM.yyyy", trCulture);
   var endStr   = lastDay.ToString("dd.MM.yyyy", trCulture);
   
   await page.FillAsync("#InvoiceFilterBeginDate_I", startStr);
   await page.Keyboard.PressAsync("Tab");
   await page.FillAsync("#InvoiceFilterEndDate_I", endStr);
   await page.Keyboard.PressAsync("Tab");
7. Sorgula butonuna tıkla:
   await page.ClickAsync("input[name='ctl01$MainMasterPageSplitter$Content$ListInvoices$btnRefresh']");
8. Loading panel'in kaybolmasını bekle:
   await page.WaitForSelectorAsync(
       "#InvoiceGridLoadingPanel",
       new() { State = WaitForSelectorState.Hidden, Timeout = 30000 });
9. Sonuç değerlendirme:
   var rows = await page.QuerySelectorAllAsync("tr[id^='InvoiceGrid_DXDataRow']");
   if (rows.Count == 0) → fatura yok, logout, return empty list
10. Her satır için kolonları çek (td'ler sıralı, InnerText ile)
11. Çıkış linkine tıkla, BrowserContext'i kapat
```

## Veri parse

```csharp
private static readonly CultureInfo TrCulture = new("tr-TR");

decimal ParseAmount(string raw) =>
    string.IsNullOrWhiteSpace(raw) ? 0m :
    decimal.Parse(raw.Trim(), NumberStyles.Number, TrCulture);

DateTime? ParseDate(string raw) =>
    string.IsNullOrWhiteSpace(raw) ? (DateTime?)null :
    DateTime.ParseExact(raw.Trim(),
        new[] { "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy" },
        TrCulture, DateTimeStyles.None);
```

## Diff & kayıt mantığı

1. Scraped listesini al
2. DB'den ilgili `CompanyId` için tüm `Invoice`'ları sorgula
3. `(FaturaNo, GondericiVkn)` kombinasyonu DB'de olmayanlar = yeni faturalar
4. Yeni faturaları DB'ye ekle, `NotifiedAt = null`, `FirstSeenAt = UtcNow`
5. `SaveChanges`
6. Yeni faturaları return et
7. Mail başarılı olduktan **sonra** ayrı çağrıyla `NotifiedAt = UtcNow` set et
8. Mail başarısız → `NotifiedAt = null` kalır, sonraki turda tekrar denenir 
   (unique index sayesinde duplicate insert olmaz)

```csharp
modelBuilder.Entity<Invoice>()
    .HasIndex(x => new { x.CompanyId, x.FaturaNo, x.GondericiVkn })
    .IsUnique();
```

## Mail formatı

- **Konu:** `[Sovos] {FirmaAdı} - {N} yeni onay bekleyen fatura`
- **Body (HTML):**
  - Üstte 1 cümle özet
  - Tablo kolonları: Fatura No, Tedarikçi (Firma Ünvanı), Tutar, Para Birimi, 
    Düzenlenme Tarihi, Son Ödeme Tarihi
  - Altta toplam tutar (para birimine göre gruplu — TRY, USD, EUR ayrı satır)
  - "Bu e-posta otomatik oluşturulmuştur" notu
- SMTP ayarları `appsettings.json` (şifre User Secrets'tan)

## Hata toleransı

- Her firma kendi try-catch'inde. Bir firma patlasa diğerleri devam eder.
- Login fail → 3 retry, her birinde 5sn bekle, sonra atla.
- Default timeout 30sn, log'a yaz, sonraki firma.
- 8 firma **sıralı** işlensin, paralel **DEĞİL**. Aralarında 5sn bekle.
- Hata olan firma için DB'de `LastFailedRunAt` ve `LastErrorMessage` set edilsin.

## Güvenlik

- Şifreler **plain text DB'de tutulmasın**. `IDataProtectionProvider` ile 
  encrypt/decrypt eden `ICredentialProtector` implementasyonu yaz.
- DataProtection key'leri filesystem'de persist olsun:
  ```csharp
  services.AddDataProtection()
      .PersistKeysToFileSystem(new DirectoryInfo("dp-keys"))
      .SetApplicationName("SovosInvoiceWorker");
  ```
- SMTP şifresi User Secrets / environment variable'dan oku.
- `appsettings.json`'a sadece placeholder yaz: `"USER_SECRETS_ILE_VER"`.
- Firma şifrelerini DB'ye eklemek için ayrı bir küçük console utility de yaz 
  (`Sovos.InvoiceWorker.AdminCli` gibi) — admin elle plain şifreyi girer, 
  encrypt edip DB'ye yazar.

## Logging (Serilog)

- Console + günlük rolling file: `logs/worker-.log`, 30 gün retention
- Structured: `Log.Information("Firma {CompanyName} taranıyor", company.Name)`
- Her firma için: başlangıç, login durumu, fatura sayısı, yeni fatura sayısı, 
  mail durumu, geçen süre

## appsettings.json şablonu

```json
{
  "ConnectionStrings": {
    "Default": "Server=...;Database=...;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Worker": {
    "IntervalMinutes": 60,
    "InitialDelaySeconds": 30
  },
  "Sovos": {
    "PortalLoginUrl": "https://portal.digitalplanet.com.tr/login/index.html?returnUrl=/",
    "Headless": true,
    "SlowMoMs": 0,
    "DefaultTimeoutMs": 30000
  },
  "Smtp": {
    "Host": "smtp.office365.com",
    "Port": 587,
    "UseStartTls": true,
    "Username": "noreply@firma.com",
    "Password": "USER_SECRETS_ILE_VER",
    "FromAddress": "noreply@firma.com",
    "FromDisplayName": "Sovos Fatura Takip"
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "logs/worker-.log", 
        "rollingInterval": "Day", "retainedFileCountLimit": 30 } }
    ]
  }
}
```

## Yapma sırası — ÇOK ÖNEMLİ

Adım adım git, her adımı bitirince **DUR**, ne yaptığını özetle, onay bekle. 
Atlama yapma.

1. **Adım 1 — İskelet:** Solution'a 3 yeni proje ekle, NuGet paketlerini kur, 
   boş interface'ler ve appsettings şablonu oluştur, `Program.cs`'de DI 
   kayıtlarını yap. `dotnet build` hatasız geçsin. **DUR.**

2. **Adım 2 — DB:** EF Core entity'leri + DbContext + ilk migration 
   (`dotnet ef migrations add Init`). Migration'ı uygulama kodda 
   `db.Database.Migrate()` ile yapsın. **DUR.**

3. **Adım 3 — Yardımcı servisler:** `ICredentialProtector` (DataProtection ile) 
   ve `IMailSender` (MailKit ile) implementasyonları. Console app ile mini 
   smoke test. **DUR.**

4. **Adım 4 — Login & navigate:** `ISovosScraper`'ın sadece login + Onay 
   Bekleyen sayfasına gitme kısmını yaz. Headless=false ile manuel test 
   edebileyim diye `appsettings`'ten Headless okusun. **DUR — bana test 
   sonucunu göster.**

5. **Adım 5 — Filtreleme & parse:** Tarih filtresi + Sorgula + grid satır 
   parse. Önce 1 satırı doğru parse ettiğine emin ol. **DUR.**

6. **Adım 6 — Diff:** `IInvoiceDiffService` implementasyonu, unique index 
   migration'ı. **DUR.**

7. **Adım 7 — Mail:** Yeni fatura tespit edildiğinde mail atan akış. 
   `NotifiedAt` mantığı. **DUR.**

8. **Adım 8 — Worker & orchestrator:** `InvoiceCheckWorker` + 
   `InvoiceOrchestrator` — 8 firmayı sıralı dolaşma, hata toleransı, retry. **DUR.**

9. **Adım 9 — AdminCli:** Firma şifresi ekleyen küçük console utility. **DUR.**

10. **Adım 10 — Test & README:** `InvoiceDiffService` için unit test, 
    README.md (kurulum, `playwright install`, migration, çalıştırma, firma ekleme).

## Kritik kurallar

- **Uydurma.** Selector'lar yukarıda verildi. Verilmeyen bir bilgi varsa 
  (login URL, sol menü selector'ları gibi) `// TODO: ...` bırak ve **bana sor**.
- **Adım adım git.** Bir adımı bitirince ne yaptığını özetle, sonraki adıma 
  geçmek için onayımı bekle.
- **Her adımda derlet.** `dotnet build` geçmeden bir sonraki adıma geçme.
- **`Task.Delay` ile bekleme yapma.** Hep `WaitForSelectorAsync` veya benzeri 
  deterministik bekleme kullan.
- **Mevcut Web API projesine dokunma.** Sadece yeni projeler ekle.

## Done tanımı

- [ ] `dotnet build` hatasız geçer
- [ ] `dotnet run --project Sovos.InvoiceWorker` ile servis ayağa kalkar
- [ ] 1 test firması için login → fatura çek → (varsa) mail at akışı çalışır
- [ ] README.md kurulum adımlarını içerir
- [ ] `InvoiceDiffService` için en az 1 unit test yeşil
- [ ] Şifreler DB'de encrypted
- [ ] Loglar dosyaya akar
