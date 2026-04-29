# ADIM C — Frontend Sovos Admin Panel (Razor + Radzen)

## Amaç
Mevcut `BlazorWebApp/WebApp` projesinde **Sovos firma yönetimi** için Razor sayfaları ekle. SovosService.Api'ye (localhost:5010) HTTP ile bağlanacak. Mevcut `Pages/Admin/AdminUsers.razor` pattern'ini birebir takip edecek.

## Mevcut altyapı (önemli)

Frontend zaten lokalde çalışıyor. Mevcut admin paneli pattern'i:

- **Konum:** `src/Client/BlazorWebApp/WebApp/`
- **UI Kit:** Radzen Blazor (RadzenCard, RadzenDataGrid, RadzenButton, RadzenDropDown, RadzenStack)
- **Yetki:** `[Authorize(Roles = "Admin")]`
- **Service pattern:** `Application/Services/Interfaces/IUserAdminService.cs` + `Application/Services/UserAdminService.cs`
- **HttpClient:** Endpoint base `auth/admin/...` (Gateway routing)
- **Dialog pattern:** `Dialog.OpenAsync<T>(...)` + `Dialog.Close(true/false)`
- **Notification:** `NotificationService.Notify(...)`
- **Validation:** `RadzenTemplateForm` + `RadzenRequiredValidator`

## SovosService.Api endpoint'leri (zaten hazır)

Backend localde çalışıyor: `http://localhost:5010`

| Method | Endpoint | DTO |
|---|---|---|
| GET | `/api/sovos/admin/companies?p=0&ps=50&q=...` | `PageDto<SovosCompanyListItemDto>` |
| GET | `/api/sovos/admin/companies/{id}` | `SovosCompanyDetailDto` |
| POST | `/api/sovos/admin/companies` | `NewSovosCompanyDto` body |
| PUT | `/api/sovos/admin/companies/{id}` | `SovosCompanyEditDto` body |
| POST | `/api/sovos/admin/companies/{id}/password` | `SovosCompanyPasswordDto` body |
| DELETE | `/api/sovos/admin/companies/{id}` | — |
| POST | `/api/sovos/admin/companies/{id}/test-login` | (stub, 501) |
| POST | `/api/sovos/admin/run-now` | (stub, 501) |
| POST | `/api/sovos/admin/run-now/{id}` | (stub, 501) |

> Frontend, gateway üzerinden çağrı yapacaksa endpoint base `sovos/admin/...` olabilir. **Mevcut `UserAdminService.cs`'in nasıl çağrı yaptığına bak — `auth/admin` mı, `api/auth/admin` mı? Sovos için aynı pattern'i kullan.**

## Yapılacaklar — 3 alt adım

### C.1: Service layer + DTO'lar

**Konum 1:** `src/Client/BlazorWebApp/WebApp/Shared/Dto/Sovos/`

(eğer `Shared/Dto/` klasörü yoksa, mevcut yapıdaki DTO klasör pattern'ini takip et — `Shared/Dto/Admin/UserListItemDto.cs` nerede ise, Sovos için `Shared/Dto/Sovos/...` aynı seviyede)

DTO'lar (backend ile **birebir** aynı property'ler):

1. `SovosCompanyListItemDto.cs`:
```csharp
public class SovosCompanyListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string CompanyCode { get; set; } = "";
    public string Username { get; set; } = "";
    public string NotificationEmails { get; set; } = "";
    public bool IsActive { get; set; }
    public bool HasPassword { get; set; }
    public DateTime? LastSuccessfulRunAt { get; set; }
    public DateTime? LastFailedRunAt { get; set; }
    public string? LastErrorMessage { get; set; }
    public int? InvoiceCountLastRun { get; set; }
}
```

2. `NewSovosCompanyDto.cs`:
```csharp
public class NewSovosCompanyDto
{
    public string Name { get; set; } = "";
    public string CompanyCode { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string NotificationEmails { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
```

3. `SovosCompanyEditDto.cs`:
```csharp
public class SovosCompanyEditDto
{
    public int Id { get; set; }   // dialog'dan edit için lazım
    public string Name { get; set; } = "";
    public string CompanyCode { get; set; } = "";
    public string Username { get; set; } = "";
    public string NotificationEmails { get; set; } = "";
    public bool IsActive { get; set; }
}
```

4. `SovosCompanyPasswordDto.cs`:
```csharp
public class SovosCompanyPasswordDto
{
    public string NewPassword { get; set; } = "";
}
```

**Konum 2:** `src/Client/BlazorWebApp/WebApp/Application/Services/`

5. `Interfaces/ISovosAdminService.cs`:
```csharp
namespace WebApp.Application.Services.Interfaces;

public interface ISovosAdminService
{
    Task<PageDto<SovosCompanyListItemDto>> GetCompaniesAsync(
        int pageIndex = 0, int pageSize = 50, string? q = null);
    Task<SovosCompanyDetailDto?> GetCompanyByIdAsync(int id);
    Task<(bool ok, string? err)> CreateCompanyAsync(NewSovosCompanyDto dto);
    Task<bool> UpdateCompanyAsync(int id, SovosCompanyEditDto dto);
    Task<bool> ChangePasswordAsync(int id, SovosCompanyPasswordDto dto);
    Task<bool> DeleteCompanyAsync(int id);
}
```

6. `SovosAdminService.cs`:
- `UserAdminService.cs` pattern'iyle birebir aynı yapı
- `HttpClient _http` constructor injection
- Endpoint base: `sovos/admin` (eğer UserAdminService'te `auth/admin` ise, biz de aynı pattern: gateway prefix yok)
- Her metot HTTP çağrısı + response handling
- Error handling: `(bool ok, string? err)` pattern (UserAdminService'teki gibi)

**HttpClient kayıt:** Mevcut `Program.cs`'te `IUserAdminService` nasıl kayıt ediliyor? AddHttpClient<IUserAdminService, UserAdminService>(... base url ...) gibi olacaktır. Aynı pattern ile `ISovosAdminService` ekle.

> ÖNEMLİ: `IUserAdminService` registration'a bak, **base URL** muhtemelen Gateway URL'i (`http://localhost:5000` veya benzeri). SovosService de aynı gateway'den geçecek. Biz **lokalde gateway olmadan direkt** denemek istersek base URL'i `http://localhost:5010` yapabiliriz, ama mevcut pattern'i bozmayalım — gateway'i kullansın. Gateway'in routing'i için Ocelot config'i sonradan ekleriz.

### C.2: Liste sayfası

**Konum:** `src/Client/BlazorWebApp/WebApp/Pages/Admin/Sovos/SovosCompanies.razor`

(yeni klasör: `Pages/Admin/Sovos/`)

**Pattern:** `Pages/Admin/AdminUsers.razor`'ı **birebir taklit et**, sadece Sovos için adapt et:

- `@attribute [Authorize(Roles = "Admin")]`
- `@page "/admin/sovos/companies"`
- `@inject ISovosAdminService Sovos`
- `@inject DialogService Dialog`
- `@inject NotificationService Notif`

Layout (AdminUsers.razor pattern'iyle):
- Üstte: Arama kutusu + Yenile + Yeni Firma butonu
- RadzenDataGrid kolonları:
  - Firma Adı (Name)
  - Şirket Kodu (CompanyCode)
  - Kullanıcı (Username)
  - Mail Listesi (NotificationEmails)
  - Aktif (IsActive — tick/cross icon)
  - Son Başarılı (LastSuccessfulRunAt — "2 saat önce" gibi göster, opsiyonel)
  - Son Hata (LastErrorMessage — varsa kırmızı badge)
  - İşlemler:
    - Edit (kalem ikonu) → `SovosCompanyEditDialog`
    - Şifre (kilit ikonu) → `SovosCompanyPasswordDialog`
    - Test Login (oynat ikonu, opsiyonel) → şimdilik stub, "Yakında" toast
    - Şimdi Tara (yenile ikonu, opsiyonel) → şimdilik stub
    - Sil (çöp kutusu) → confirm dialog + delete

@code bloğu:
- `string search = "";`
- `List<SovosCompanyListItemDto> companies = new();`
- `OnInitializedAsync` → Load
- `Load()` → `Sovos.GetCompaniesAsync(...)` çağır, results companies'a
- `Reload()`, `Create()`, `Edit()`, `ChangePassword()`, `Delete()` metodları (AdminUsers.razor pattern)

### C.3: Dialog'lar

**Konum:** `src/Client/BlazorWebApp/WebApp/Pages/Admin/Sovos/`

#### `SovosCompanyCreateDialog.razor`
- `UserCreateDialog.razor` pattern'i
- `RadzenTemplateForm` + 6 alan: Name, CompanyCode, Username, Password, NotificationEmails, IsActive
- Required validation: Name, CompanyCode, Username, Password
- Password: `RadzenPassword` component
- NotificationEmails için `RadzenHint Text="Birden fazla mail için virgülle ayırın: a@x.com, b@x.com"`
- IsActive için `RadzenCheckBox`
- Vazgeç + Kaydet butonları
- Submit'te: `Sovos.CreateCompanyAsync(Model)` → success → Dialog.Close(true)

#### `SovosCompanyEditDialog.razor`
- `UserEditDialog.razor` pattern'i
- Aynı 5 alan ama **Password yok** (ayrı dialog'da)
- `[Parameter] public SovosCompanyEditDto Model { get; set; } = new();`
- Submit'te: `Sovos.UpdateCompanyAsync(Model.Id, Model)` → success → Dialog.Close(true)

#### `SovosCompanyPasswordDialog.razor`
- `AdminChangeUserPasswordDialog.razor` pattern'i
- Tek alan: NewPassword (RadzenPassword)
- + Şifre Tekrar (UI tarafında match kontrolü)
- Required, MinLength(4)
- Submit'te: `Sovos.ChangePasswordAsync(CompanyId, dto)` → success → Dialog.Close(true)

## Test akışı

1. SovosService.Api zaten lokalde çalışıyor (localhost:5010)
2. BlazorWebApp'i `dotnet run --project src/Client/BlazorWebApp/WebApp`
3. Tarayıcıda admin login ol (mevcut frontend zaten çalışıyor)
4. Manuel olarak adresi gir: `http://localhost:2000/admin/sovos/companies` (port mevcut frontend ile aynı)
5. Sayfa açılmalı, mevcut TestCompany görünmeli (Worker zaten oluşturmuştu)
6. **Yeni Firma** ile bir firma ekle (örnek: ADAYBAGIMS), kaydet
7. Listede yeni firma görünür
8. **Şifre güncelle** ile şifresini değiştir → 204 → toast
9. **Edit** ile bilgilerini düzenle → toast
10. **Sil** ile sil → confirm → toast → liste güncellenir

## Yapma sırası — ADIM ADIM

1. **C.1.a:** Mevcut `Pages/Admin/AdminUsers.razor` ve `UserAdminService.cs`'i oku, pattern'i çıkar, raporla. **DUR.**
2. **C.1.b:** DTO'lar oluştur (4 dosya). **DUR.**
3. **C.1.c:** `ISovosAdminService` + `SovosAdminService` oluştur, DI'a kaydet. Build et. **DUR.**
4. **C.2:** Liste sayfası `SovosCompanies.razor`. Build + manuel test (sayfa açılıyor mu, mevcut TestCompany görünüyor mu?). **DUR.**
5. **C.3.a:** `SovosCompanyCreateDialog.razor` + Yeni Firma butonu işlevi. Test: yeni firma ekle. **DUR.**
6. **C.3.b:** `SovosCompanyEditDialog.razor`. Test: edit. **DUR.**
7. **C.3.c:** `SovosCompanyPasswordDialog.razor`. Test: şifre güncelle. **DUR.**
8. **C.3.d:** Delete confirm + işlevi. Test: sil. **DUR.**

## Kritik kurallar

- **Mevcut Radzen pattern'i bozma.** Hiçbir yeni nuget paketi getirme.
- **AdminUsers.razor pattern'iyle %100 uyumlu** ol.
- **HttpClient base URL'ini mevcut UserAdminService'le aynı yap** (gateway veya direct).
- **Authorize attribute'unu unutma.** `[Authorize(Roles = "Admin")]`
- **Şifre asla DTO'da response olarak okunmasın** — backend zaten döndürmüyor, frontend'de de saklamayalım.
- **Verilmeyen bilgi varsa uydurma, sor.**

## Done tanımı

- [ ] `dotnet build` hatasız geçer
- [ ] BlazorWebApp `dotnet run` ile başlar
- [ ] /admin/sovos/companies sayfası açılır
- [ ] Mevcut TestCompany listede görünür
- [ ] Yeni firma eklenir, listede çıkar
- [ ] Edit ile bilgi güncellenir
- [ ] Şifre dialog ile şifre güncellenir
- [ ] Delete onayla + silme çalışır
- [ ] Tüm işlemlerde toast/notification gösterilir

## Kullanıcının verdiği bilgi

- Frontend mevcut, lokalde çalışıyor
- BlazorWebApp port: 2000 (docker-compose)
- SovosService.Api port: 5010
- Backend authentication: JWT (IdentityService üretiyor)
- Mevcut admin sayfaları çalışıyor, login olabiliyor
- AdminUsers.razor pattern'i zaten incelendi, 6 dosya kopya-yapıştır olarak gönderildi (referans için kod görmek istersen oku)

Önce C.1.a'ya başla — mevcut pattern'i incele, raporla, sonra devam et.
