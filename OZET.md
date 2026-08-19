# ÖZET — Banka Ekstresi İşleme Modülü

Vakıfbank vadesiz TL ekstresi yüklenip her satır için muhasebe açıklaması üretiliyor,
karşı hesap katmanlı olarak çözülüyor; belirsiz satırlar klavye odaklı onay ekranına
düşüyor, onaylar öğreniliyor. Çıktı ORKA'ya aktarılacak satır listesi.

Mimari kararlar ve gerekçeleri: **KARARLAR.md**. Plan ve dosya listesi: **PLAN.md**.

---

## Ne yapıldı

### Sunucu — `Services/CatalogService/CatalogService.Api/Features/BankaEkstre`

| Dosya | İş |
|---|---|
| `Domain/BankaEkstreEnums.cs` | `HesapTipi`, `Yon`, `YuklemeDurum`, `SatirDurum`, `KaynakKatman`, `AnahtarTipi`, `EslesmeTuru` |
| `Domain/BankaHesabi.cs` | Ekstresi işlenen hesap; aynı zamanda banka kayıt defteri (Katman 3) |
| `Domain/EkstreYukleme.cs` | Dosya yüklemesi + dönem + parser uyarıları |
| `Domain/EkstreSatiri.cs` | Ham veri + üretilen açıklama + öneri/onay + `KaynakKatman` |
| `Domain/OgrenmeKaydi.cs` | Anahtar (hash / IBAN / VKN) → hesap kodu, yön bazlı |
| `Domain/HesapPlaniKaydi.cs` | ORKA hesap planı satırı (boşluklu kod, `AnaGrup`, `BaslangicHarfi`) |
| `Domain/AciklamaSablonu.cs` | İşlem tipi → açıklama şablonu, `BankalarArasi` bayrağı |
| `Domain/UnvanDeseni.cs` | Unvan çıkarma regex'i, banka bazlı, sıralı |
| `Domain/SabitKural.cs` | Katman 4: işlem tipi → hesap kodu |
| `Services/Normalizasyon.cs` | Türkçe sadeleştirme, gürültü temizliği, hash, IBAN çıkarma, Title Case, kod yardımcıları |
| `Services/Benzerlik.cs` | Levenshtein oranı + ilk-14-karakter önek kuralı (0.95) |
| `Services/Parsing/IEkstreParser.cs` | Banka ayrıştırıcı arayüzü + `AyrilanSatir` / `EkstreParseSonuc` |
| `Services/Parsing/VakifbankVadesizParser.cs` | Vakıfbank vadesiz TL xlsx |
| `Services/Parsing/EkstreParserSecici.cs` | `ParserTipi` → ayrıştırıcı |
| `Services/UnvanCikarici.cs` | Desenleri sırayla dener, ilk yakalayan kazanır |
| `Services/AciklamaUretici.cs` | Şablon seçimi + yer tutucu doldurma + 50 karakter sınırı |
| `Services/HesapEslestirici.cs` | 5 katman + yön→ana grup + karar eşikleri |
| `Services/EkstreService.cs` | Yükle/işle, satır listeleme, onay, öğrenme, dışa aktarım |
| `Services/BankaHesabiService.cs` | Banka hesapları CRUD |
| `Services/EkstreHesapPlaniService.cs` | Hesap planı xlsx içe aktarımı + arama |
| `Controllers/*.cs` | 3 controller, hepsi `api/catalog/banka-ekstre/...` |
| `BankaEkstreSeed.cs` | Vakıfbank şablon / desen / sabit kural satırları (idempotent) |

Ayrıca:
- `Infrastructure/EntityConfigurations/BankaEkstreEntityTypeConfigurations.cs` (8 tablo)
- `Infrastructure/Context/CatalogContext.cs` — DbSet + konfigürasyon + tenant filtreleri
- `Program.cs` — DI kayıtları + seed çağrısı
- `Migrations/20260819183905_AddBankaEkstre.cs`

Tablolar (`catalog` şeması): `EkstreBankaHesaplari`, `EkstreYuklemeler`, `EkstreSatirlari`,
`EkstreOgrenmeKayitlari`, `EkstreHesapPlani`, `EkstreAciklamaSablonlari`,
`EkstreUnvanDesenleri`, `EkstreSabitKurallar`.

### İstemci — `src/Client/BlazorWebApp/WebApp`

| Dosya | İş |
|---|---|
| `Shared/Dto/BankaEkstre/BankaEkstreDtos.cs` | DTO'lar + `BankaEkstreEtiket` (Türkçe etiketler, tr-TR biçim) |
| `Application/Services/Interfaces/IBankaEkstreApi.cs` | İstemci arayüzü |
| `Application/Services/BankaEkstreApi.cs` | HTTP istemcisi, `{ field, message }` hata sözleşmesi |
| `Pages/BankaEkstre/BankaHesaplariPage.razor` | `/banka-isleme/hesaplar` — CRUD |
| `Pages/BankaEkstre/EkstreYuklePage.razor` | `/banka-isleme/yukle` — hesap planı + ekstre yükleme, sayaçlar |
| `Pages/BankaEkstre/EkstreOnayPage.razor` | `/banka-isleme/onay/{id}` — klavye odaklı onay |
| `wwwroot/js/bankaEkstre.js` | Odak yardımcıları (`odakla`, `temizle`) |
| `wwwroot/index.html` | Script kaydı |
| `Layout/MainLayout.razor` | "Banka İşleme" menü grubu |
| `StartupExtensions/.../ServiceCollectionExtensions.cs` | `IBankaEkstreApi` kaydı |

### Testler — `Services/CatalogService/CatalogService.UnitTests/BankaEkstre`

`BankaEkstreTestOrtami.cs` (bellek içi context + gerçek dosya yapısını taklit eden xlsx üretici),
`VakifbankParserTests`, `UnvanCikariciTests`, `NormalizasyonTests`, `AciklamaUreticiTests`,
`HesapEslestiriciTests`, `EkstreServiceTests` (uçtan uca), `EkstreHesapPlaniServiceTests`.

---

## Doğrulama

| # | Kabul kriteri | Durum |
|---|---|---|
| 1 | Çözüm derleniyor, uyarı üretmiyor | ✅ `dotnet build SmartExpenseSystem.sln` → 0 hata. Yeni modülden **tek uyarı yok** (mevcut 40 uyarı repoda zaten vardı) |
| 2 | Migration oluşturuldu ve uygulanıyor | ✅ `AddBankaEkstre` üretildi, `dotnet ef database update` yerel veritabanına uygulandı |
| 3 | Hesap planı xlsx içe aktarımı | ✅ `EkstreHesapPlaniServiceTests` (kolon eşleme, upsert, kod formatı, başlık hatası) |
| 4 | Ekstre satırları ayrışıyor, tarih/tutar/yön doğru | ✅ `VakifbankParserTests` + `EkstreServiceTests` |
| 5 | Açıklama 50 karakteri aşmıyor | ✅ `AciklamaUreticiTests.Elli_karakteri_asmaz` + DB'de `HasMaxLength(50)` |
| 6 | Katman sırası doğru, `KaynakKatman` doluyor | ✅ `HesapEslestiriciTests` (katman 1/1b/2/3/4/5 ayrı testler) |
| 7 | Eşik altı ve yakın adaylı satırlar `OnayBekliyor` | ✅ `Tek_yuksek_aday_otomatik_gecer`, `Yakin_ikinci_aday_varsa_onaya_duser`, `Esik_altindaki_skor_otomatik_gecmez` |
| 8 | Onay sonrası öğrenme, aynı açıklama Katman 2'den çözülüyor | ✅ `Onay_ogrenme_kaydi_yazar_ve_ikinci_yuklemede_katman2_cozer` |
| 9 | Onay ekranı tamamen klavyeyle | ⚠️ Kodda var (Enter/↓/↑/Esc + otomatik odak), **otomatik testi yok** — tarayıcıda elle denenmeli |
| 10 | Eksik satır varken dışa aktarım engelleniyor | ✅ `Cozulemeyen_satir_onaya_duser_ve_disa_aktarimi_engeller` |

Test sonucu: **210 test, 0 başarısız** (`CatalogService.UnitTests`), **18 test, 0 başarısız** (`WebApp.UnitTests`).

Çalıştırılan doğrulama komutları:

```
dotnet build SmartExpenseSystem.sln
dotnet ef migrations add AddBankaEkstre        # Services/CatalogService/CatalogService.Api
dotnet ef database update
dotnet test Services/CatalogService/CatalogService.UnitTests/CatalogService.UnitTests.csproj
dotnet test src/Client/BlazorWebApp/WebApp.UnitTests/WebApp.UnitTests.csproj
```

---

## Yazarken bulunan ve düzeltilen üç gerçek hata

Testler yazılırken ortaya çıktı, üçü de sessiz veri bozulması üretiyordu:

1. **Tutar 100 katına çıkıyordu.** Sayısal Excel hücresi metne çevrilip tr-TR ile
   ayrıştırılınca `12500.75` → `1250075` oluyordu (nokta binlik ayracı sanılıyor).
   Artık sayısal hücre doğrudan `decimal` okunuyor.
2. **`A.Ş.` → `A.ş.`** Title Case kelimeyi boşlukla bölüyordu. Artık harf/harf-olmayan
   sınırıyla bölüyor.
3. **Doğru cari eşleşmesi boşuna onaya düşüyordu.** `A.Ş.` normalizasyonda `A` + `S`
   olarak ayrılıp gürültü listesindeki `AS` ile eşleşmiyordu; skor 1.00 yerine 0.71
   çıkıyordu. Artık nokta siliniyor, `AS` gürültü sayılıyor.

---

## Ne eksik kaldı

- **Onay ekranının klavye akışı otomatik test edilmedi** (bUnit yok, `WebApp.UnitTests`
  saf xunit). Elle denenmeli: `/banka-isleme/onay/{id}` açılınca odak ilk satırın kod
  kutusunda mı, Enter onaylayıp bir sonrakine atlıyor mu.
- **Gerçek Vakıfbank dosyasıyla denenmedi.** Testler ölçülen yapıyı (veri 8. satırdan,
  kolon indeksleri 2/5/6/8/14/15/16) taklit eden üretilmiş xlsx kullanıyor. Gerçek
  dosyada kolon başlıkları farklı adlanıyorsa parser sabit indekslere düşer ve
  `Uyarilar` alanına yazar — yükleme ekranı bu uyarıyı gösteriyor, ilk yüklemede bakın.
- **Dışa aktarım dosya üretmiyor**, JSON satır listesi dönüyor (KARARLAR §15).
  ORKA'nın beklediği dosya şeması netleşince `DisaAktarAsync`'in çıktısından xlsx/CSV
  üretmek tek bir servis metodu.
- **Sabit kurallar ana hesap seviyesinde** (`770`, `740`); muavin kırılımı firmaya
  özel olduğu için uydurulmadı. `EkstreSabitKurallar` tablosundan düzenlenmeli.
- **`Vergi Tahsilatı` için sabit kural yok** — 360/368 kırılımı firmaya özel, satır onaya düşer.
- **Şablon/desen/kural tabloları için yönetim ekranı yok.** Şimdilik seed + SQL.
  Yeni banka eklerken arayüz gerekirse basit bir CRUD sayfası yeterli.

---

## Sonraki banka parser'ı eklerken nereye dokunulacak

Mimari buna hazır; **kod değişikliği tek dosya**:

1. **Yeni parser yaz:** `Features/BankaEkstre/Services/Parsing/AkbankVadesizParser.cs`,
   `IEkstreParser` uygula. `ParserTipi` sabiti benzersiz olmalı (ör. `"AKBANK_VADESIZ"`).
   `VakifbankVadesizParser`'ı kopyalayıp kolon eşlemesini değiştirmek yeterli;
   başlık-önce-isimle-ara / sonra-indekse-düş kalıbını koruyun.

2. **DI'a kaydet:** `Program.cs` içinde tek satır —
   `builder.Services.AddSingleton<IEkstreParser, AkbankVadesizParser>();`
   `EkstreParserSecici` otomatik toplar, seçici/servis/controller değişmez.

3. **Yapılandırma satırlarını ekle:** `BankaEkstreSeed.cs` içine yeni bankanın
   `ParserTipi`'yle şablon / unvan deseni / sabit kural satırları. Seed idempotent,
   mevcut satırlara dokunmaz.

4. **Kullanıcı tarafı:** Banka Hesapları ekranından yeni hesap açılır, ayrıştırıcı
   listesinden yeni banka seçilir. **Menü, gateway, migration, DTO, sayfa değişmez.**

Değişmeyecek yerler: `EkstreService`, `HesapEslestirici`, `AciklamaUretici`,
`UnvanCikarici`, controller'lar, Blazor sayfaları, Ocelot yapılandırması.
