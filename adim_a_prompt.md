# ADIM A — SovosService.Api Microservice

## Amaç
Mevcut microservice mimarisine **SovosService.Api** adında yeni bir Web API ekle. Bu servis, Sovos firmalarını yönetmek için CRUD endpoint'leri sunacak. Frontend (Blazor) bu servise HTTP ile erişecek.

## Mimari bağlam (önemli — uydurma)

Sistem **mikroservis tabanlı**. Mevcut servisler:
- `IdentityService` (port 5005) — kullanıcı yönetimi
- `CatalogService.Api` (port 5004)
- `OCRService.Api` (port 5002)
- `FileApiService.Api` (port 5009)
- `Web.ApiGateway` (port 5000) — Ocelot API gateway
- `WebApp` (port 2000) — Blazor frontend

Her servis:
- Kendi Dockerfile'ı, kendi container'ı
- Consul'a kendini kaydeder (`ConsulConfig`)
- ASP.NET Identity DEĞİL — Sovos için sadece kendi DbContext'i
- ApiGateway üzerinden gelen istekleri karşılar
- `net_backendservices` network'üne bağlanır
- Sql Server: `s_sqlserver` container'ı
- RabbitMQ: `s_rabbitmq` container'ı
- Service Discovery: `s_consul` container'ı

## Yeni servis için kararlaştırılan ayarlar

| Ayar | Değer |
|---|---|
| Proje adı | `SovosService.Api` |
| Container adı | `c_sovosservice` |
| Port | `5010` |
| Consul Service Name | `SovosService` |
| Consul Service Id | `Sovos` |
| API base route | `api/sovos/admin` |
| Authorize | `[Authorize(Roles = "Admin")]` (mevcut JWT token'ı kullanır) |
| DB | Mevcut SQL Server (UserDataBase) — Sovos.InvoiceWorker ile aynı tablolar |

## Yapılacaklar

### 1. Proje yapısı

Mevcut `IdentityService` pattern'ini taklit et. **Kesin path'ler:**

- Yeni proje: `C:\GitHub\CengizMicroService\Services\SovosService\SovosService.Api\`
- Pattern referansı: `C:\GitHub\CengizMicroService\Services\IdentityService\IdentityService\`
- Worker referansı: `C:\GitHub\CengizMicroService\Sovos\Sovos.InvoiceWorker.Core\Sovos.InvoiceWorker.Core.csproj`

Klasör yapısı:

```
Services/SovosService/
└── SovosService.Api/
    ├── Controllers/
    │   └── SovosAdminController.cs
    ├── Application/
    │   └── Models/
    │       ├── PageDto.cs                  (mevcut pattern, generic)
    │       ├── SovosCompanyListItemDto.cs
    │       ├── SovosCompanyDetailDto.cs
    │       ├── NewSovosCompanyDto.cs
    │       ├── SovosCompanyEditDto.cs
    │       └── SovosCompanyPasswordDto.cs
    ├── Persistence/
    │   └── SovosServiceDbContext.cs        (sadece read için, Worker ile aynı tablolar)
    ├── Services/
    │   ├── ICredentialProtector.cs         (Worker'dakiyle aynı)
    │   └── CredentialProtector.cs
    ├── Properties/
    │   └── launchSettings.json
    ├── appsettings.json
    ├── appsettings.Docker.json
    ├── Program.cs
    ├── Dockerfile
    └── SovosService.Api.csproj
```

### 2. Bağımlılıklar (.csproj)

Mevcut IdentityService.csproj'a bak, aynı paket versiyonlarını kullan. Tahminen:

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.*" />
<PackageReference Include="Microsoft.AspNetCore.DataProtection" Version="8.*" />
<PackageReference Include="Consul" Version="..." />  <!-- IdentityService'teki versiyon -->
<PackageReference Include="Swashbuckle.AspNetCore" Version="..." />
```

> IdentityService.csproj'u önce oku, paket listesini kopyala ve gereksizleri çıkar. Identity-spesifik (Microsoft.AspNetCore.Identity.EntityFrameworkCore vs.) paketlere ihtiyaç yok.

### 3. Domain — Worker'dan referans al

`Sovos.InvoiceWorker.Core` projesinde zaten `Company` ve `Invoice` entity'leri var. **YENİDEN OLUŞTURMA**, **referans ekle**:

Yeni `SovosService.Api.csproj`'da (path: `Services/SovosService/SovosService.Api/`):

```xml
<ItemGroup>
  <ProjectReference Include="..\..\..\Sovos\Sovos.InvoiceWorker.Core\Sovos.InvoiceWorker.Core.csproj" />
</ItemGroup>
```

Path açıklaması:
- `..\..\..\` → `Services/SovosService/SovosService.Api/` → `Services/SovosService/` → `Services/` → kök
- Sonra `Sovos\Sovos.InvoiceWorker.Core\Sovos.InvoiceWorker.Core.csproj`

### 4. Persistence — DbContext

`SovosServiceDbContext.cs`:
- Aynı tablolara erişir: `Companies`, `Invoices`
- Worker'ın `SovosDbContext` ile **aynı yapıda** olmalı, özellikle:
  - Tablo isimleri (örn `SovosCompanies`, `SovosInvoices`)
  - Unique index (Invoice için CompanyId + FaturaNo + GondericiVkn)
- **Migration EKLEMEME**: Worker zaten oluşturmuş, biz okuyacağız ve yazacağız ama tablo şemasına dokunmuyoruz
- Connection string: `appsettings.json`'da `ConnectionStrings:DatabaseConnection`

### 5. CredentialProtector

Worker'daki `ICredentialProtector` ve `CredentialProtector`'ı buraya **kopyala** (aynı kod). Şifreleri Encrypt/Decrypt için lazım.

> İdeal olarak ortak bir Common library olur ama şimdilik kopyala, sonra refactor ederiz.

DataProtection key'lerinin **AYNI yere yazması kritik**: Worker hangi key'le encrypt ediyorsa SovosService aynı key ile decrypt edebilmeli. `Program.cs`'te:

```csharp
services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/dp-keys"))
    .SetApplicationName("SovosShared");  // Worker ile aynı isim
```

> Worker'da `SetApplicationName` ne ise aynısını kullan. Eğer `"SovosInvoiceWorker"` ise burada da `"SovosInvoiceWorker"` olsun ki key'ler eşleşsin.
> Docker'da `/dp-keys` volume olacak (compose'da tanımlanacak — bu adımda değil, sonra).

### 6. SovosAdminController

Mevcut `IdentityService/Controllers/AdminController.cs` pattern'ini birebir takip et. Endpoint'ler:

```csharp
[Route("api/sovos/admin")]
[ApiController]
[Authorize(Roles = "Admin")]
public class SovosAdminController : ControllerBase
{
    private readonly SovosServiceDbContext _db;
    private readonly ICredentialProtector _protector;

    public SovosAdminController(SovosServiceDbContext db, ICredentialProtector protector) { ... }

    // GET api/sovos/admin/companies?p=0&ps=50&q=...
    [HttpGet("companies")]
    public async Task<ActionResult<PageDto<SovosCompanyListItemDto>>> GetCompanies(
        [FromQuery] int p = 0, [FromQuery] int ps = 50, [FromQuery] string? q = null)
    {
        // mevcut pattern: filtrele, count, page, AsNoTracking, Select projection
        // ŞİFRE BURADA DÖNMEYECEK — sadece HasPassword: bool
    }

    // GET api/sovos/admin/companies/{id}
    [HttpGet("companies/{id:int}")]
    public async Task<ActionResult<SovosCompanyDetailDto>> GetCompany(int id) { ... }

    // POST api/sovos/admin/companies
    [HttpPost("companies")]
    public async Task<IActionResult> CreateCompany([FromBody] NewSovosCompanyDto dto)
    {
        // Validation: Name, CompanyCode, Username, Password, NotificationEmails zorunlu
        // Şifre encrypt: _protector.Encrypt(dto.Password)
        // DB'ye Company entity'si olarak ekle
        // 201 Created dön
    }

    // PUT api/sovos/admin/companies/{id}
    [HttpPut("companies/{id:int}")]
    public async Task<IActionResult> UpdateCompany(int id, [FromBody] SovosCompanyEditDto dto)
    {
        // Sadece bilgiler: Name, CompanyCode, Username, NotificationEmails, IsActive
        // Şifreye dokunma
    }

    // POST api/sovos/admin/companies/{id}/password
    [HttpPost("companies/{id:int}/password")]
    public async Task<IActionResult> ChangePassword(int id, [FromBody] SovosCompanyPasswordDto dto)
    {
        // Validation: NewPassword zorunlu, min 4 karakter
        // _protector.Encrypt(dto.NewPassword)
        // company.EncryptedPassword güncelle
        // SaveChanges
        // 204 NoContent
    }

    // DELETE api/sovos/admin/companies/{id}
    [HttpDelete("companies/{id:int}")]
    public async Task<IActionResult> DeleteCompany(int id) { ... }

    // POST api/sovos/admin/companies/{id}/test-login
    // POST api/sovos/admin/run-now
    // POST api/sovos/admin/run-now/{id}
    // → Bu üç endpoint için ŞİMDİLİK STUB (501 Not Implemented) bırak.
    //   ADIM B'de Worker'a HTTP çağrı eklenecek.
}
```

### 7. DTO örnekleri

**SovosCompanyListItemDto** — listede gösterilecek (şifre ASLA yok):
```csharp
public class SovosCompanyListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string CompanyCode { get; set; } = "";
    public string Username { get; set; } = "";
    public string NotificationEmails { get; set; } = "";
    public bool IsActive { get; set; }
    public bool HasPassword { get; set; }   // ← şifre tanımlı mı, plain text DEĞİL
    public DateTime? LastSuccessfulRunAt { get; set; }
    public DateTime? LastFailedRunAt { get; set; }
    public string? LastErrorMessage { get; set; }
    public int? InvoiceCountLastRun { get; set; }
}
```

**NewSovosCompanyDto** — yeni firma ekleme:
```csharp
public class NewSovosCompanyDto
{
    public string Name { get; set; } = "";
    public string CompanyCode { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";        // plain, encrypt edilecek
    public string NotificationEmails { get; set; } = ""; // virgülle ayrılmış
    public bool IsActive { get; set; } = true;
}
```

**SovosCompanyEditDto** — düzenleme (şifre yok):
```csharp
public class SovosCompanyEditDto
{
    public string Name { get; set; } = "";
    public string CompanyCode { get; set; } = "";
    public string Username { get; set; } = "";
    public string NotificationEmails { get; set; } = "";
    public bool IsActive { get; set; }
}
```

**SovosCompanyPasswordDto**:
```csharp
public class SovosCompanyPasswordDto
{
    public string NewPassword { get; set; } = "";
}
```

### 8. Program.cs

`IdentityService/Program.cs`'i oku, aynı yapıyı kur. Asıl bileşenler:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration.AddEnvironmentVariables();

// DbContext
builder.Services.AddDbContext<SovosServiceDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DatabaseConnection")));

// Data Protection (Worker ile UYUMLU olmalı)
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(/* keys path */))
    .SetApplicationName("SovosShared");  // Worker ile aynı

// Services
builder.Services.AddScoped<ICredentialProtector, CredentialProtector>();

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// JWT Auth — IdentityService'teki yapıyı kopyala
builder.Services.AddAuthentication("Bearer").AddJwtBearer(options => { ... });
builder.Services.AddAuthorization();

// Consul registration — IdentityService pattern
builder.Services.Configure<ConsulConfig>(builder.Configuration.GetSection("ConsulConfig"));
builder.Services.AddSingleton<IHostedService, ConsulHostedService>();
// veya IdentityService'teki yöntem ne ise aynısı

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### 9. appsettings.json

```json
{
  "Logging": {
    "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
  },
  "ConnectionStrings": {
    "DatabaseConnection": "Server=DESKTOP-I290HP6;Database=UserDataBase;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Issuer": "...",
    "Audience": "...",
    "Key": "..."
  },
  "ConsulConfig": {
    "Address": "http://localhost:8500",
    "ServiceAddress": "http://localhost:5010",
    "ServiceName": "SovosService",
    "ServiceId": "Sovos"
  }
}
```

> Jwt değerleri IdentityService'tekiyle AYNI olmalı (token doğrulama için). IdentityService'in appsettings'ini oku, kopyala.

`appsettings.Docker.json`:
```json
{
  "ConnectionStrings": {
    "DatabaseConnection": "Server=s_sqlserver;Database=UserDataBase;User Id=sa;Password=0-jofnvbsvCS!;TrustServerCertificate=True;"
  }
  // Diğer Docker-specific override'lar
}
```

### 10. Dockerfile

Mevcut `Services/IdentityService/IdentityService/Dockerfile`'ı oku, aynı pattern'i kullan. **Kesin path'lerle:**

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5010

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# csproj'ları kopyala (restore için)
COPY ["Services/SovosService/SovosService.Api/SovosService.Api.csproj", "Services/SovosService/SovosService.Api/"]
COPY ["Sovos/Sovos.InvoiceWorker.Core/Sovos.InvoiceWorker.Core.csproj", "Sovos/Sovos.InvoiceWorker.Core/"]

RUN dotnet restore "Services/SovosService/SovosService.Api/SovosService.Api.csproj"

# Tüm kaynak kodu kopyala
COPY . .
WORKDIR "/src/Services/SovosService/SovosService.Api"
RUN dotnet build "SovosService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SovosService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SovosService.Api.dll"]
```

> IdentityService'in Dockerfile'ı tam doğru pattern'i veriyor — her `COPY` ve `RUN` satırını oradakiyle karşılaştır, Worker.Core referansı için ekstra COPY satırı bizim eklediğimiz fark.

### 11. docker-compose entry (BU ADIMDA EKLEMEYELİM)

`docker-compose.yml` ve `docker-compose.override.yml`'a entry eklemeyi **şimdilik bırak**. Kullanıcı bunu manuel ekleyecek (kopyala-yapıştır pattern, basit). ADIM A'nın test'i lokal `dotnet run` ile yapılacak.

## Test akışı (yerel)

1. Çözüm derlensin: `dotnet build`
2. SovosService.Api lokal olarak ayağa kalksın: `dotnet run --project Services/SovosService/SovosService.Api`
3. Browser'dan: `http://localhost:5010/swagger` → Swagger UI
4. **JWT token al:** IdentityService üzerinden bir Admin kullanıcısıyla login ol, Bearer token'ı kopyala
5. Swagger UI'da Authorize butonuna `Bearer {token}` yapıştır
6. `GET /api/sovos/admin/companies` → mevcut TestCompany görünmeli (Worker tarafından oluşturulmuştu)
7. `POST /api/sovos/admin/companies` ile yeni firma ekle → 201
8. Tekrar `GET` → 2 firma görünmeli
9. `POST /api/sovos/admin/companies/{id}/password` ile şifre değiştir → 204

## Yapma sırası — ADIM ADIM

1. **A.1:** Proje iskeleti.
   - Klasör oluştur: `C:\GitHub\CengizMicroService\Services\SovosService\SovosService.Api\`
   - `dotnet new webapi -n SovosService.Api` (veya manuel csproj)
   - **Solution'a ekle:** `dotnet sln C:\GitHub\CengizMicroService\<solution-adı>.sln add Services/SovosService/SovosService.Api/SovosService.Api.csproj`
   - NuGet paketlerini kur (IdentityService.csproj'tan kopyala)
   - Boş Program.cs ile derleme test et
   - `dotnet build` hatasız geçsin. **DUR.**
2. **A.2:** Sovos.InvoiceWorker.Core'a proje referansı ekle (yukarıdaki path'le). DbContext'i Worker'dakiyle eşleşen şekilde yaz. **DUR.**
3. **A.3:** ICredentialProtector + CredentialProtector kopyala. DataProtection setup. **DUR.**
4. **A.4:** Program.cs'i tamamla — JWT auth, Consul registration (IdentityService pattern). **DUR.**
5. **A.5:** DTO'lar. **DUR.**
6. **A.6:** SovosAdminController — CRUD endpoint'leri (test-login ve run-now stub). **DUR.**
7. **A.7:** Lokal test: `dotnet run`, Swagger açılsın, GET companies çalışsın. **DUR.**
8. **A.8:** Dockerfile. Build test: `docker build -f .../Dockerfile .` hatasız. **DUR.**

## Kritik kurallar

- **IdentityService/AdminController.cs pattern'ini birebir takip et.** Aynı LINQ stili, aynı response shape (PageDto, ActionResult).
- **Şifre asla DTO'da plain dönmeyecek** — sadece `HasPassword: bool`.
- **JWT auth IdentityService'inkiyle aynı** — ortak token kullanılacak.
- **Migration ekleme** — Worker zaten oluşturmuş.
- **test-login ve run-now ŞİMDİLİK stub** — ADIM B'de implement.
- **docker-compose'a dokunma** — kullanıcı manuel ekleyecek.

## Done tanımı

- [ ] `dotnet build` hatasız geçer
- [ ] `dotnet run` ile servis 5010'da ayağa kalkar
- [ ] Swagger UI açılır
- [ ] JWT auth çalışır (token olmadan 401, token ile 200)
- [ ] GET /companies → mevcut Worker'ın oluşturduğu TestCompany'i döner
- [ ] POST /companies → yeni firma ekler, şifre encrypted DB'de
- [ ] PUT /companies/{id} → bilgi günceller (şifre HARİÇ)
- [ ] POST /companies/{id}/password → şifre günceller, encrypted
- [ ] DELETE /companies/{id} → siler
- [ ] test-login ve run-now → 501 Not Implemented dönüyor
- [ ] Dockerfile → `docker build` hatasız

## Test sırasında olası sorunlar

- **DataProtection key uyumsuzluğu:** Worker'ın encrypt ettiği şifre SovosService'te decrypt edilemezse, DataProtection ApplicationName'i farklı demektir. Eşleştir.
- **JWT token doğrulama hatası:** IdentityService ile aynı Issuer/Audience/Key kullanılmalı.
- **DbContext tablo adı uyuşmazlığı:** Worker'da `SovosCompanies` ise burada da aynı isim olsun. EF migration'a karışma.

Hadi başla. Her adımdan sonra dur, ne yaptığını özetle, onay bekle.
