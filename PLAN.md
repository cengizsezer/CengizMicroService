# PLAN — Banka Ekstresi İşleme Modülü

## İnceleme özeti (mevcut kalıplar)

| Konu | Repoda bulunan | Karar |
|---|---|---|
| Servis yerleşimi | `Services/CatalogService/CatalogService.Api/Features/<Modul>/{Controllers,Domain,Dtos,Services}` (Muhasebe, SmmmTakip, FirmaKontrol aynı) | Aynı kalıp: `Features/BankaEkstre` |
| DbContext | Tek `CatalogContext`, `catalog` şeması, `IEntityTypeConfiguration` sınıfları `Infrastructure/EntityConfigurations` altında | Aynı |
| Tenant | `TenantEntity` + `HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo)`; `SaveChangesAsync` TenantNo'yu doldurur | Firma-özel tablolar `TenantEntity`; banka-özel referans tabloları (şablon/desen/kural) global |
| Migration | `Migrations/` altında `dotnet ef migrations add`, uygulama açılışta `Database.MigrateAsync()` | Yeni migration: `AddBankaEkstre` |
| Gateway | Ocelot tek route: `/catalog/{everything}` → `/api/catalog/{everything}` | Controller route'ları `api/catalog/banka-ekstre/*` → **gateway değişmez** |
| Excel | `ClosedXML 0.105.0` (KdvMizanExcelParser, HeaderMap) | Aynı kütüphane, `HeaderMap` benzeri başlık eşleme |
| Blazor | Radzen; `Pages/<Modul>/*.razor`, `Application/Services/<Modul>Api.cs` + `Interfaces/I<Modul>Api.cs`, DI: `ServiceCollectionExtensions.AddApiClients`, DTO: `Shared/Dto/<Modul>` | Aynı |
| Menü | `Layout/MainLayout.razor` içinde `RadzenPanelMenuItem` ağacı | "Banka İşleme" grubu eklenecek |
| Klavye | `wwwroot/js/fisKisayol.js` + `[JSInvokable]` geri çağrı | `wwwroot/js/bankaEkstre.js` aynı kalıpta |
| Test | `Services/CatalogService/CatalogService.UnitTests` (xunit + EFCore.InMemory) | `BankaEkstre/` klasörü eklenecek |

## Eklenecek dosyalar

### CatalogService.Api — Features/BankaEkstre
- `Domain/`: `BankaEkstreEnums.cs`, `BankaHesabi.cs`, `EkstreYukleme.cs`, `EkstreSatiri.cs`,
  `OgrenmeKaydi.cs`, `HesapPlaniKaydi.cs`, `AciklamaSablonu.cs`, `UnvanDeseni.cs`, `SabitKural.cs`
- `Dtos/`: `BankaHesabiDtos.cs`, `EkstreDtos.cs`, `HesapPlaniKaydiDtos.cs`
- `Services/Parsing/`: `IEkstreParser.cs`, `EkstreParseSonuc.cs`, `VakifbankVadesizParser.cs`, `EkstreParserSecici.cs`
- `Services/`: `Normalizasyon.cs`, `Benzerlik.cs`, `IUnvanCikarici.cs`+impl, `IAciklamaUretici.cs`+impl,
  `IHesapEslestirici.cs`+impl, `IEkstreService.cs`+impl, `IBankaHesabiService.cs`+impl,
  `IEkstreHesapPlaniService.cs`+impl, `BankaEkstreKuralException.cs`
- `Controllers/`: `BankaHesaplariController.cs`, `EkstreController.cs`, `EkstreHesapPlaniController.cs`
- `BankaEkstreSeed.cs` (şablon / desen / sabit kural tabloları — Vakıfbank)

### CatalogService.Api — Infrastructure
- `EntityConfigurations/BankaEkstre*.cs` (8 adet)
- `Context/CatalogContext.cs`: DbSet + ApplyConfiguration + query filter
- `Program.cs`: DI kayıtları + seed çağrısı
- `Migrations/*_AddBankaEkstre.cs`

### WebApp (Blazor)
- `Shared/Dto/BankaEkstre/*.cs`
- `Application/Services/BankaEkstreApi.cs`, `Interfaces/IBankaEkstreApi.cs`
- `Pages/BankaEkstre/BankaHesaplariPage.razor`, `EkstreYuklePage.razor`, `EkstreOnayPage.razor`
- `wwwroot/js/bankaEkstre.js` + `wwwroot/index.html` script kaydı
- `Layout/MainLayout.razor` menü
- `StartupExtensions/ServiceExtensions/ServiceCollectionExtensions.cs` DI

### CatalogService.UnitTests — BankaEkstre
- `VakifbankParserTests.cs`, `UnvanCikariciTests.cs`, `NormalizasyonTests.cs`,
  `AciklamaUreticiTests.cs`, `HesapEslestiriciTests.cs`, `BankaEkstreTestOrtami.cs`

## Sıra
1. Domain + enum
2. EF configuration + DbContext + migration
3. Parser + normalizasyon + benzerlik
4. Açıklama üretimi + unvan çıkarma
5. Katmanlı eşleştirme + öğrenme
6. Servisler + controller'lar
7. Seed
8. Blazor (API client → DTO → sayfalar → menü)
9. Testler
10. Derleme + migration + KARARLAR.md + OZET.md
