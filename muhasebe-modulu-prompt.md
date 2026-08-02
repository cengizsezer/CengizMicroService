# Görev: DijitalMasraf — Hesap Planı ve Fiş (Yevmiye) modülü

## Bağlam

DijitalMasraf projesine, tekdüzen hesap planı (THP) mantığında çalışan bir muhasebe
çekirdeği ekliyoruz. İlk kullanım senaryosu kişisel/ev bütçesinin "Sezer A.Ş." adlı
sanal bir şirket gibi çift taraflı kayıt yöntemiyle tutulması; ancak modül baştan
çok firmalı (multi-tenant) ve ileride PKF müşterilerinde kullanılabilecek şekilde
tasarlanmalı. Yani hiçbir yerde "ev bütçesi" varsayımı hard-code edilmemeli.

Mevcut mimari:

- Blazor WebAssembly frontend
- ASP.NET Core Web API mikroservisleri, Ocelot API Gateway arkasında
- SQL Server (Docker), Entity Framework Core
- MinIO dosya depolama, nginx reverse proxy
- Repo: `C:\GitHub\CengizMicroService`

Yeni servis mevcut mikroservis desenini birebir takip etsin: aynı klasör yapısı,
aynı DI kayıt biçimi, aynı hata yönetimi ve loglama yaklaşımı, aynı Dockerfile
şablonu, Ocelot'a yeni route eklenmesi. Yeni bir mimari icat etme; önce mevcut bir
servisi (örneğin KDV Beyanname veya Firma Kontrol servisini) inceleyip aynı
konvansiyonlara uy.

---

## Kapsam

### Yapılacaklar

1. Hesap planı: sınıf → grup → kebir → muavin (n seviyeli) ağaç yönetimi
2. Fiş girişi: çift taraflı, borç/alacak dengesi zorunlu
3. T cetveli (hesap ekstresi) ve mizan görünümleri
4. THP seed verisi ve firma bazlı hesap planı kopyalama

### Bu görevde yapılmayacaklar

- WhatsApp entegrasyonu (sonraki fazda; ancak veri modeli `Kaynak` alanıyla buna
  hazır olacak)
- Enflasyon düzeltmesi, amortisman otomasyonu, beyanname üretimi
- Dönem kapanış fişi otomasyonu (sadece manuel fiş girişi)

---

## Veri modeli

### Kod maskesi

Segment uzunlukları firma bazında ayarlanabilir olacak, varsayılan `3-2-2-4`:

```
XXX . YY . YY . NNNN
102 . 01 . 01 . 0046   →  Akbank vadesiz TL
```

Segmentler sabit uzunlukta ve sola sıfır dolgulu. `KodMaskesi` tablosunda firma
başına bir kayıt tutulacak; tüm kod üretme ve doğrulama mantığı bu maskeyi okuyacak,
hiçbir yerde `3-2-2-4` sabit yazılmayacak.

### Tablolar

```sql
CREATE TABLE KodMaskesi (
    FirmaId          INT PRIMARY KEY,
    SegmentUzunluk   VARCHAR(50) NOT NULL,          -- "3,2,2,4"
    Ayrac            CHAR(1) NOT NULL DEFAULT '.'
);

CREATE TABLE HesapPlani (
    Id            INT IDENTITY PRIMARY KEY,
    FirmaId       INT NOT NULL,
    UstHesapId    INT NULL REFERENCES HesapPlani(Id),
    Kod           VARCHAR(30) NOT NULL,             -- "102.01.01.0046"
    KodDuz        VARCHAR(30) NOT NULL,             -- "10201010046"  (sıralama için)
    SegmentKod    VARCHAR(10) NOT NULL,             -- "0046"
    Ad            NVARCHAR(150) NOT NULL,
    Seviye        TINYINT NOT NULL,                 -- 1=sınıf, 2=grup, 3=kebir, 4+=muavin
    HesapTuru     TINYINT NOT NULL,                 -- Sinif/Grup/Kebir/Muavin
    Karakter      TINYINT NOT NULL,                 -- Aktif/Pasif/Gelir/Gider/Nazim/Maliyet
    HareketGorur  BIT NOT NULL DEFAULT 0,
    SistemHesabi  BIT NOT NULL DEFAULT 0,           -- MSUGT standart hesabı → kod kilitli
    ParaBirimi    CHAR(3) NULL,                     -- döviz takipli hesaplarda dolu
    BankaKodu     CHAR(4) NULL,                     -- TCMB EFT kodu
    Iban          VARCHAR(34) NULL,
    Yol           VARCHAR(200) NOT NULL,            -- "/1/10/102/" materialized path
    Aktif         BIT NOT NULL DEFAULT 1,
    CONSTRAINT UQ_HesapKod UNIQUE (FirmaId, Kod)
);
CREATE INDEX IX_HesapPlani_Yol ON HesapPlani(FirmaId, Yol);
CREATE INDEX IX_HesapPlani_KodDuz ON HesapPlani(FirmaId, KodDuz);

CREATE TABLE MasrafMerkezi (
    Id       INT IDENTITY PRIMARY KEY,
    FirmaId  INT NOT NULL,
    Kod      VARCHAR(10) NOT NULL,
    Ad       NVARCHAR(100) NOT NULL,
    Aktif    BIT NOT NULL DEFAULT 1
);

CREATE TABLE Fis (
    Id            INT IDENTITY PRIMARY KEY,
    FirmaId       INT NOT NULL,
    DonemYil      SMALLINT NOT NULL,
    FisNo         VARCHAR(20) NOT NULL,             -- "2026/000148"
    Tarih         DATE NOT NULL,
    FisTuru       TINYINT NOT NULL,                 -- Acilis/Tahsil/Tediye/Mahsup/Kapanis
    BelgeNo       VARCHAR(50) NULL,
    Aciklama      NVARCHAR(250) NULL,
    Kaynak        TINYINT NOT NULL DEFAULT 0,       -- Manuel/WhatsApp/Ekstre/Otomatik
    Durum         TINYINT NOT NULL DEFAULT 0,       -- 0=Taslak 1=Kesinlesmis
    OlusturanId   INT NOT NULL,
    OlusturmaT    DATETIME2 NOT NULL,
    GuncellemeT   DATETIME2 NULL,
    CONSTRAINT UQ_FisNo UNIQUE (FirmaId, DonemYil, FisNo)
);

CREATE TABLE FisSatir (
    Id               INT IDENTITY PRIMARY KEY,
    FisId            INT NOT NULL REFERENCES Fis(Id) ON DELETE CASCADE,
    SiraNo           SMALLINT NOT NULL,
    HesapId          INT NOT NULL REFERENCES HesapPlani(Id),
    MasrafMerkeziId  INT NULL REFERENCES MasrafMerkezi(Id),
    Aciklama         NVARCHAR(250) NULL,
    Borc             DECIMAL(19,2) NOT NULL DEFAULT 0,
    Alacak           DECIMAL(19,2) NOT NULL DEFAULT 0,
    ParaBirimi       CHAR(3) NOT NULL DEFAULT 'TRY',
    Doviz            DECIMAL(19,4) NULL,
    Kur              DECIMAL(19,6) NULL,
    CONSTRAINT CK_TekTaraf CHECK (
        (Borc > 0 AND Alacak = 0) OR (Alacak > 0 AND Borc = 0)
    )
);
CREATE INDEX IX_FisSatir_Hesap ON FisSatir(HesapId, FisId);
```

Parasal alanların tamamı `decimal`. Hiçbir yerde `double`/`float` kullanma.

---

## İş kuralları

Bunlar servis katmanında zorlanacak ve her biri için birim testi yazılacak.

### Hesap planı

1. **Yalnızca yaprak hesap hareket görür.** Bir hesabın altına çocuk eklendiği anda
   `HareketGorur = false` yapılır.
2. **Hareketi olan hesabın altına çocuk eklenemez.** `FisSatir` içinde kaydı varsa
   ekleme reddedilir ve kullanıcıya "önce hareketleri alt hesaba taşıyın" mesajı
   döner.
3. **Kod, üst hesabın kodu ile başlamak zorunda.** Kullanıcıdan tam kod alınmaz;
   üst hesap seçilir, yalnızca son segment girilir, tam kodu servis birleştirir.
4. **Segment, maskedeki uzunluğu aşamaz** ve sadece rakam içerir. Kısa girilirse
   sola sıfır doldurulur (`1` → `0001`).
5. **Karakter alanı kullanıcıdan alınmaz**, üst hesaptan miras alınır. Kök sınıftan
   türetme: 1–2 → Aktif, 3–5 → Pasif, 6 → Gelir, 7 → Gider, 8 → Maliyet,
   9 → Nazım.
6. **`SistemHesabi = true` olan hesapların kodu değiştirilemez ve silinemez.**
   Sadece `Ad` alanı güncellenebilir.
7. **Kullanıcı kendi kebirini açabilir**, ancak sadece MSUGT'de boş bırakılmış kod
   aralıklarında. Kebir ekleme ekranı serbest kod girişi kabul etmez; ilgili grubun
   altındaki kullanılmamış kodları listeleyip seçtirir.
8. **Silme yok.** Hareketi olmayan hesap silinebilir, hareketi olan yalnızca
   pasife çekilir (`Aktif = false`). Pasif hesap yeni fişlerde seçilemez, geçmiş
   raporlarda görünür.
9. **`Yol` alanı her eklemede üst hesaptan türetilir:** `{ust.Yol}{ust.Id}/`.
   Hesap taşıma işlemi bu fazda yok; olmadığı için alt ağaç `Yol` güncellemesi de
   gerekmiyor.

### Fiş

10. **Fiş en az iki satır içermeli.**
11. **Borç toplamı = alacak toplamı.** Eşit değilse kayıt reddedilir; hata mesajı
    borç, alacak ve farkı ayrı ayrı yazar.
12. **Toplam tutar sıfır olamaz.**
13. **Bir satırda ya borç ya alacak dolu olur**, ikisi birden değil. Veritabanı
    seviyesinde `CK_TekTaraf` constraint'i ile de garanti altına alınır.
14. **Sadece `HareketGorur = true` ve `Aktif = true` hesaba fiş kesilebilir.**
15. **`Durum = 1` (kesinleşmiş) fiş güncellenemez ve silinemez.** Düzeltme için
    ters kayıt fişi oluşturulur.
16. **Fiş numarası firma + dönem bazında sıralı üretilir**, boşluk bırakılmaz.
    Eşzamanlı istekte çakışma olmaması için numara üretimi transaction içinde ve
    kilitli yapılır.
17. **Döviz satırında** `Doviz` ve `Kur` zorunlu; `Borc`/`Alacak` alanı TL karşılığı
    tutar.

### Bakiye

18. **Bakiye hiçbir tabloda saklanmaz**, her zaman `FisSatir` üzerinden hesaplanır.
19. **Üst hesap bakiyesi**, `Yol LIKE @yol + '%'` ile alt ağacın toplamıdır.
20. **Bakiye yönü karaktere göre belirlenir:** Aktif/Gider/Maliyet hesaplarda
    `Borç − Alacak`, Pasif/Gelir hesaplarda `Alacak − Borç`.
21. Mizan ve T cetveli yalnızca `Durum = 1` fişleri içerir; taslaklar ayrı
    gösterilir.

---

## API uçları

Yeni mikroservis: `Muhasebe.Api`. Ocelot'a `/api/muhasebe/*` route'u eklenecek.

### Hesap planı

```
GET    /api/muhasebe/hesap-plani                  ağacın tamamı (düz liste + Yol)
GET    /api/muhasebe/hesap-plani/ara?q=           kod veya isimde arama
GET    /api/muhasebe/hesap-plani/hareket-gorenler  fiş girişi seçim listesi
GET    /api/muhasebe/hesap-plani/{id}
GET    /api/muhasebe/hesap-plani/{ustId}/sonraki-kod   ilk boş segmenti döner
GET    /api/muhasebe/hesap-plani/{grupId}/bos-kebirler kullanılmamış kebir kodları
POST   /api/muhasebe/hesap-plani                  { ustHesapId, segment, ad, ... }
PUT    /api/muhasebe/hesap-plani/{id}
PATCH  /api/muhasebe/hesap-plani/{id}/pasif
DELETE /api/muhasebe/hesap-plani/{id}             sadece hareketsizse
```

### Fiş

```
GET    /api/muhasebe/fis?bas=&bit=&durum=&hesapId=
GET    /api/muhasebe/fis/{id}
POST   /api/muhasebe/fis                          taslak veya kesinleşmiş
PUT    /api/muhasebe/fis/{id}                     sadece taslak
PATCH  /api/muhasebe/fis/{id}/kesinlestir
DELETE /api/muhasebe/fis/{id}                     sadece taslak
POST   /api/muhasebe/fis/{id}/ters-kayit          kesinleşmiş fişi tersleyen yeni fiş
```

### Raporlar

```
GET    /api/muhasebe/rapor/mizan?bas=&bit=&seviye=
GET    /api/muhasebe/rapor/ekstre/{hesapId}?bas=&bit=     T cetveli verisi
GET    /api/muhasebe/rapor/masraf-merkezi?bas=&bit=
```

Tüm uçlar mevcut JWT doğrulamasını kullanır ve `FirmaId`'yi token'dan alır, request
body'sinden değil.

---

## UI ekranları (Blazor WASM)

### 1. Hesap planı ağacı

- Ağaç görünümü, chevron ile aç/kapa, seviyeye göre girinti
- Kolonlar: kod (monospace), hesap adı, bakiye (sağa yaslı)
- Üst hesapların bakiyesi soluk renkte (hesaplanmış), yaprakların normal renkte
- `SistemHesabi` olanlarda kilit ikonu, kullanıcı açtıklarında "özel" rozeti
- Arama kutusu yazdıkça filtreler ve eşleşen düğümün tüm atalarını otomatik açar
- Satır üzerinde üç aksiyon: alt hesap ekle · düzenle · pasife al
- Filtre çipleri: tümü / sadece hareket görenler / bakiyeli
- Ağaç tek istekte çekilip client tarafında kurulur:

```csharp
var duz = await Http.GetFromJsonAsync<List<HesapDto>>("api/muhasebe/hesap-plani");
var sozluk = duz.ToDictionary(h => h.Id);
foreach (var h in duz.Where(h => h.UstHesapId is not null))
    sozluk[h.UstHesapId.Value].Cocuklar.Add(h);
var kok = duz.Where(h => h.UstHesapId is null).ToList();
```

### 2. Hesap ekleme diyaloğu

- Üst hesap seçici (ağaçtan seçilen düğüm ön dolu gelir)
- Kod alanı `sonraki-kod` ucundan gelen ilk boş değerle dolu açılır
- Hesap adı
- Banka muavini ekleniyorsa: banka seçici → kod TCMB EFT listesinden otomatik gelir,
  kullanıcı elle yazmaz
- Onay kutuları: hareket görür · döviz takipli (işaretliyse para birimi seçici açılır)
- Canlı önizleme: tam kod, ad, seviye, miras alınan karakter
- Kaydet butonu doğrulama geçmeden aktifleşmez

### 3. Fiş giriş ekranı

- Başlık: fiş türü, tarih, fiş no (otomatik), belge no, açıklama, kaynak rozeti
- Satır tablosu: hesap kodu · hesap adı · masraf merkezi · açıklama · borç · alacak · sil
- Hesap arama kutusu hem koda hem isme göre çalışır, sadece hareket gören hesapları
  listeler, `F2` ile hesap planı ağacı modal olarak açılır
- Borç yazılınca alacak alanı kilitlenir, tersi de geçerli
- Son satır her zaman boş durur; doldurulunca altına yenisi eklenir
- Kullanıcı son satırın tutar alanına geldiğinde fark, soluk renkte öneri olarak
  gösterilir; tek tuşla kabul edilir
- Altta canlı denge şeridi:
  - dengede → yeşil, "fiş dengede · fark 0,00", kaydet aktif
  - dengesiz → kırmızı, "borç X · alacak Y · fark Z", kaydet pasif
- Toplamlar her satır değişiminde yeniden hesaplanır, kaydete basınca değil
- Kısayollar: `Ctrl+S` kaydet, `Ctrl+Enter` kaydet ve yeni fiş, `Esc` vazgeç

### 4. T cetveli (hesap ekstresi)

- Başlıkta hesap kodu, adı, dönem, karakter
- İki kolon: solda borç hareketleri, sağda alacak hareketleri
- Her satırda tarih, açıklama, tutar; satıra tıklanınca ilgili fiş açılır
- Alt kısımda her iki kolonun toplamı, en altta bakiye ve yönü
  ("borç kalanı" / "alacak kalanı")

### 5. Mizan

- Kolonlar: hesap kodu, ad, borç toplam, alacak toplam, borç bakiye, alacak bakiye
- Seviye filtresi (sadece kebir / kebir + 1. muavin / tümü)
- Sayfa altında genel toplam satırı; borç ve alacak toplamları eşit olmalı,
  değilse uyarı bandı çıkar
- Excel dışa aktarım

---

## Seed verisi

`Muhasebe.Api/Data/Seed/thp-standart.json` dosyası oluşturulacak. İçerik: MSUGT
tekdüzen hesap planının sınıf, grup ve kebir seviyeleri, `SistemHesabi = true`
olarak işaretli. Firma oluşturulduğunda bu şablon kopyalanıp `HesapPlani` tablosuna
o firmanın `FirmaId`'si ile yazılır.

`KodDuz`, `Seviye`, `Karakter`, `Yol` alanları seed anında hesaplanır, JSON'da
elle yazılmaz.

Ayrıca `tcmb-banka-kodlari.json`: TCMB EFT katılımcı kodları ve banka adları.

---

## Kabul kriterleri

Aşağıdakilerin tamamı için xUnit testi yazılacak:

- [x] Hareketi olan hesabın altına çocuk eklenemiyor
- [x] Çocuk eklenen hesabın `HareketGorur` alanı otomatik `false` oluyor
- [x] `1` girilen segment maskeye göre `0001` olarak dolduruluyor
- [x] Aynı üst altında aynı segment ikinci kez eklenemiyor
- [x] `sonraki-kod` ucu `0001, 0002, 0004` dolu iken `0003` döndürüyor
- [x] Sistem hesabının kodu değiştirilemiyor, adı değiştirilebiliyor
- [x] Karakter üst hesaptan doğru miras alınıyor (770 altındaki muavin → Gider)
- [x] Borç ≠ alacak olan fiş kaydedilemiyor, hata mesajı farkı içeriyor
- [x] Tek satırlık fiş kaydedilemiyor
- [x] Hareket görmeyen hesaba fiş kesilemiyor
- [x] Pasif hesaba fiş kesilemiyor
- [x] Kesinleşmiş fiş güncellenemiyor ve silinemiyor
- [x] Ters kayıt fişi, kaynak fişin borç/alacağını yer değiştirmiş olarak üretiyor
- [x] Aktif karakterli hesapta bakiye `Borç − Alacak`, pasifte `Alacak − Borç`
- [x] Üst hesap bakiyesi alt ağacın toplamına eşit
- [x] Mizanda genel borç toplamı = genel alacak toplamı
- [x] Fiş numarası eşzamanlı 50 istekte tekrarsız ve boşluksuz üretiliyor

---

## Çalışma şekli

Aşağıdaki fazlara böl ve **her fazın sonunda dur, ne yaptığını özetle ve onay
iste.** Hepsini tek seferde yazma.

**Faz 0 — Keşif.** Mevcut repoda bir mikroservisi incele. Klasör yapısı, DI kaydı,
DbContext deseni, hata yönetimi, Ocelot route tanımı, Dockerfile ve Blazor tarafındaki
HttpClient kullanımı nasıl yapılmış, çıkar. Bu modülü hangi konvansiyonlarla
yazacağını maddeler halinde bana anlat. Kod yazma.

**Faz 1 — Veri katmanı.** Entity sınıfları, `MuhasebeDbContext`, EF Core
konfigürasyonları, migration, seed dosyaları. Migration'ı çalıştırıp doğrula.

**Faz 2 — Hesap planı servisi ve API.** İş kuralları 1–9, ilgili uçlar, birim
testleri.

**Faz 3 — Fiş servisi ve API.** İş kuralları 10–17, ilgili uçlar, birim testleri.

**Faz 4 — Rapor servisi.** Mizan, ekstre, masraf merkezi; iş kuralları 18–21.

**Faz 5 — Blazor ekranları.** Hesap planı ağacı ve ekleme diyaloğu.

**Faz 6 — Blazor fiş girişi.** Denge şeridi, klavye kısayolları, otomatik
denkleştirme.

**Faz 7 — Blazor raporlar.** T cetveli ve mizan, Excel dışa aktarım.

### Uyulacak kurallar

- Türkçe alan ve sınıf adları kullan (`HesapPlani`, `FisSatir`, `Borc`), mevcut
  projedeki dil tercihiyle tutarlı ol. Karışık dil kullanma.
- Tüm iş kuralları servis katmanında; controller sadece doğrulama ve yönlendirme
  yapsın. Blazor tarafındaki kontroller yalnızca kullanıcı deneyimi içindir, tek
  savunma hattı değildir.
- Hata mesajları Türkçe, kullanıcının anlayacağı dilde ve eyleme dönük olsun.
  Exception mesajını ham haliyle ekrana basma.
- Parasal alanlarda `decimal`, ekranda `ToString("N2")`.
- Bir belirsizlik olursa varsayım yapıp devam etme, sor.

---

## İlerleme

Modül ayrı bir `Muhasebe.Api` mikroservisi yerine `CatalogService.Api` içinde
feature slice olarak yazılıyor (Faz 0 kararı); route öneki
`api/catalog/muhasebe/*`, Ocelot'a dokunulmadı.

| Faz | Kapsam | Durum | Nerede |
|-----|--------|-------|--------|
| 0 | Keşif — mevcut mikroservis konvansiyonları | ✅ Tamam | — |
| 1 | Veri katmanı: entity, DbContext, EF config, migration, seed | ✅ Tamam | `Features/Muhasebe/Domain/*`, `Infrastructure/EntityConfigurations/*`, `Migrations/20260731213245_AddMuhasebeTables`, `Infrastructure/Setup/SeedFiles/thp-standart.json`, `tcmb-banka-kodlari.json` |
| 2 | Hesap planı servisi ve API (kural 1–9) + birim testleri | ✅ Tamam | `Features/Muhasebe/Services/{MaskeBilgisi,HesapPlaniService,MuhasebeKuralException}.cs`, `Dtos/HesapPlaniDtos.cs`, `Controllers/HesapPlaniController.cs`, `CatalogService.UnitTests/Muhasebe/*` |
| 3 | Fiş servisi ve API (kural 10–17) + birim testleri | ✅ Tamam | `Features/Muhasebe/Services/{IFisService,FisService}.cs`, `Dtos/FisDtos.cs`, `Controllers/FisController.cs`, `CatalogService.UnitTests/Muhasebe/{FisServiceTests,SabitKullanici}.cs` |
| 4 | Rapor servisi: mizan, ekstre, masraf merkezi (kural 18–21) | ✅ Tamam | `Features/Muhasebe/Services/{IRaporService,RaporService,BakiyeKurali}.cs`, `Dtos/RaporDtos.cs`, `Controllers/RaporController.cs`, `CatalogService.UnitTests/Muhasebe/RaporServiceTests.cs` |
| 5 | Blazor: hesap planı ağacı ve ekleme diyaloğu | ✅ Tamam | `WebApp/Pages/Muhasebe/{HesapPlaniPage,HesapFormDialog}.razor`, `Application/Services/{MuhasebeApi,Interfaces/IMuhasebeApi}.cs`, `Shared/Dto/Muhasebe/*`, `Domain/Models/Muhasebe/TcmbBankalari.cs` |
| 6 | Blazor: fiş girişi, denge şeridi, kısayollar | ✅ Tamam | `WebApp/Pages/Muhasebe/{FisGirisPage,HesapSecModal}.razor`, `WebApp/wwwroot/js/fisKisayol.js`, `Shared/Dto/Muhasebe/FisDtos.cs`, `Features/Muhasebe/{Controllers/MasrafMerkeziController,Services/MasrafMerkeziService,Dtos/MasrafMerkeziDtos}.cs`, `CatalogService.UnitTests/Muhasebe/MasrafMerkeziServiceTests.cs` |
| 7 | Blazor: T cetveli, mizan, Excel dışa aktarım (+ fiş listesi, masraf merkezi yönetimi) | ✅ Tamam | `WebApp/Pages/Muhasebe/{MizanPage,EkstrePage,FisListePage,MasrafMerkeziPage,MasrafMerkeziFormDialog}.razor`, `Application/Services/CsvAktarim.cs`, `Shared/Dto/Muhasebe/RaporDtos.cs` |

### Faz 2 durumu

| İş kuralı | Durum |
|-----------|-------|
| 1 — Yalnızca yaprak hesap hareket görür | ✅ |
| 2 — Hareketi olan hesabın altına çocuk eklenemez | ✅ |
| 3 — Kod üst hesabın kodu ile başlar, tam kodu servis birleştirir | ✅ |
| 4 — Segment maskeyi aşamaz, sadece rakam, sola sıfır dolgulu | ✅ |
| 5 — Karakter üst hesaptan miras alınır | ✅ |
| 6 — Sistem hesabının kodu değişmez, silinemez | ✅ |
| 7 — Kullanıcı kebiri yalnızca boş kod aralıklarında (`bos-kebirler`) | ✅ |
| 8 — Silme yok; hareketi olan pasife çekilir | ✅ |
| 9 — `Yol` üst hesaptan türetilir | ✅ |

Uçlar (hepsi `[Authorize]`, FirmaId token'dan gelen `TenantNo`, query filter ile izole):

```
GET    /api/catalog/muhasebe/hesap-plani
GET    /api/catalog/muhasebe/hesap-plani/ara?q=
GET    /api/catalog/muhasebe/hesap-plani/hareket-gorenler
GET    /api/catalog/muhasebe/hesap-plani/{id}
GET    /api/catalog/muhasebe/hesap-plani/{ustId}/sonraki-kod
GET    /api/catalog/muhasebe/hesap-plani/{grupId}/bos-kebirler
GET    /api/catalog/muhasebe/banka-kodlari          TCMB EFT listesi (seed dosyasından)
POST   /api/catalog/muhasebe/hesap-plani
PUT    /api/catalog/muhasebe/hesap-plani/{id}
PATCH  /api/catalog/muhasebe/hesap-plani/{id}/pasif
DELETE /api/catalog/muhasebe/hesap-plani/{id}
```

### Faz 3 durumu

| İş kuralı | Durum |
|-----------|-------|
| 10 — Fiş en az iki satır içermeli | ✅ |
| 11 — Borç toplamı = alacak toplamı; hata mesajı borç, alacak ve farkı ayrı yazar | ✅ |
| 12 — Toplam tutar sıfır olamaz | ✅ |
| 13 — Bir satırda ya borç ya alacak dolu olur | ✅ (servis + `CK_TekTaraf`) |
| 14 — Yalnızca `HareketGorur` + `Aktif` hesaba fiş kesilir | ✅ |
| 15 — Kesinleşmiş fiş güncellenemez/silinemez; düzeltme ters kayıtla | ✅ |
| 16 — Fiş no firma + dönem bazında sıralı, transaction içinde kilitli üretim | ✅ |
| 17 — Döviz satırında `Doviz` ve `Kur` zorunlu; `Borc`/`Alacak` TL karşılığı | ✅ |

Uçlar (hepsi `[Authorize]`, FirmaId token'dan gelen `TenantNo`, query filter ile izole):

```
GET    /api/catalog/muhasebe/fis?bas=&bit=&durum=&hesapId=
GET    /api/catalog/muhasebe/fis/{id}
POST   /api/catalog/muhasebe/fis                    { kesinlestir: false } → taslak
PUT    /api/catalog/muhasebe/fis/{id}               sadece taslak
PATCH  /api/catalog/muhasebe/fis/{id}/kesinlestir
DELETE /api/catalog/muhasebe/fis/{id}               sadece taslak
POST   /api/catalog/muhasebe/fis/{id}/ters-kayit
```

Faz 3 kararları:

- `FisNo` biçimi `2026/000001`; `DonemYil` ve numara kullanıcıdan alınmaz, `Tarih`ten ve
  dönem sırasından üretilir. Sıra `max + 1`; üretim ile kayıt aynı transaction içinde,
  SQL Server tarafında `WITH (UPDLOCK, HOLDLOCK)` okumasıyla yapılır. Süreç içi eşzamanlılık
  ayrıca firma + dönem bazlı bir semaphore ile seri hâle getirilir.
- Ters kayıt varsayılan olarak **taslak** açılır ve kaynak fişin tarihini alır; ikisi de
  istek gövdesinden değiştirilebilir. Kaynak fişe `Aciklama` üzerinden atıf yapılır
  (şemada ayrı bir "ters kaydı olduğu fiş" alanı yok).
- Kural 14 ters kayıtta da uygulanır: kaynak fişin hesabı sonradan pasife alındıysa ters
  kayıt reddedilir. Kural metni istisna tanımlamadığı için birebir uygulandı; pratikte
  sorun çıkarsa Faz 4'te gözden geçirilmeli.
- Taslak fiş silinince numarası boşta kalır (dönemin son fişi ise bir sonraki kayıtta
  yeniden kullanılır). Kural 16'nın "boşluk bırakılmaz" ifadesi numara **üretimi** için
  uygulanmıştır.

### Faz 4 durumu

| İş kuralı | Durum |
|-----------|-------|
| 18 — Bakiye saklanmaz, her istekte `FisSatir`'dan hesaplanır | ✅ |
| 19 — Üst hesap bakiyesi alt ağacın toplamı (`Yol` materialized path) | ✅ |
| 20 — Bakiye yönü karaktere göre (`BakiyeKurali`) | ✅ |
| 21 — Mizan ve T cetveli yalnızca kesinleşmiş fişleri içerir; taslaklar ayrı | ✅ |

Uçlar (hepsi `[Authorize]`, salt okunur, FirmaId token'dan gelen `TenantNo`):

```
GET    /api/catalog/muhasebe/rapor/mizan?bas=&bit=&seviye=
GET    /api/catalog/muhasebe/rapor/ekstre/{hesapId}?bas=&bit=
GET    /api/catalog/muhasebe/rapor/masraf-merkezi?bas=&bit=
```

Faz 4 kararları:

- **Alt ağaç toplamı** mizanda tek geçişte yapılır: her hareket, hesabın kendisine ve
  `Yol` içindeki tüm ata Id'lerine eklenir. Hesap başına `LIKE` sorgusu atılmaz; sonuç
  kural 19'un `Yol LIKE @yol + '%'` semantiğiyle aynıdır. Ekstre tek hesap için gerçekten
  `Yol LIKE` (EF `StartsWith`) sorgusu kullanır.
- **Mizan genel toplamı** satırların toplanmasıyla değil, hareket gören (yaprak) hesaplar
  üzerinden hesaplanır; satırlar alt ağaç toplamı taşıdığı için satır toplamı mükerrer
  sayım olurdu. `Dengede` alanı UI'ın uyarı bandını sürer.
- **`seviye` filtresi** "o seviyeye kadar" anlamındadır: 3 → kebire kadar (sınıf/grup
  satırları da gelir), 4 → kebir + 1. muavin, boş → tümü. Filtre genel toplamı değiştirmez.
- **Hareketsiz hesaplar mizana yazılmaz;** alt ağacında hareket olan üst hesaplar yazılır,
  böylece hiyerarşi kopmaz. Pasif hesaplar raporlarda görünmeye devam eder (kural 8).
- **Ekstrede devir bakiyesi var:** `DevirBorc`/`DevirAlacak`/`DevirBakiye`, `Bas`
  tarihinden önceki **kesinleşmiş** hareketlerin (alt ağaç dâhil) toplamıdır. `Bas`
  verilmezse rapor tüm geçmişi kapsadığı için devir 0'dır. `ToplamBorc`/`ToplamAlacak`
  yalnızca dönem hareketleridir; `KapanisBorc`/`KapanisAlacak` = devir + dönem ve
  `Bakiye` bu kapanış üzerinden kural 20 ile hesaplanır. T cetvelinde devir, kolonların
  en üstünde ayrı satır olarak gösterilecek.
- **`MizanSatirDto.YaprakMi`:** alt hesabı olmayan düğüm. `HareketGorur`den farklıdır
  (yaprak bir hesap hareket görmüyor olabilir). Üst satırlar alt ağaç toplamı taşıdığı
  için mükerrersiz toplam yalnızca yaprak satırlardadır; genel toplam da bununla
  doğrulanıyor.
- ⚠️ **Nazım hesaplar kural 20'de sayılmıyor.** `BakiyeKurali` bunları borç yönlü
  (`Borç − Alacak`) okuyor; borçlu nazım hesap yaygın olduğu için. Alacaklı nazım
  hesapların ayrı okunması gerekiyorsa karakter listesine yeni bir değer eklenmeli —
  onayınıza açık tek nokta bu.

Testler: `dotnet test Services/CatalogService/CatalogService.UnitTests` → 88/88 geçiyor
(38 hesap planı + 21 fiş + 29 rapor).

### Faz 5 durumu

Rota: `/muhasebe/hesap-plani` · Menü: **Muhasebe → Hesap Planı** (`MainLayout.razor`).
API'ye dokunulmadı; istemci mevcut `/catalog/muhasebe/*` uçlarını kullanıyor.

| Ekran maddesi | Durum |
|---------------|-------|
| Ağaç görünümü, chevron ile aç/kapa, seviyeye göre girinti | ✅ |
| Kolonlar: kod (monospace) · hesap adı · bakiye (sağa yaslı) | ✅ |
| Üst hesap bakiyesi soluk (hesaplanmış), yaprak normal | ✅ |
| `SistemHesabi` kilit ikonu, kullanıcı açtıklarında "özel" rozeti | ✅ (+ pasif rozeti) |
| Arama yazdıkça filtreler, eşleşenin atalarını otomatik açar | ✅ |
| Satır aksiyonları: alt hesap ekle · düzenle · pasife al | ✅ |
| Filtre çipleri: tümü / sadece hareket görenler / bakiyeli | ✅ |
| Ağaç tek istekte çekilip istemcide kurulur | ✅ |
| Diyalog: kod alanı `sonraki-kod` ile dolu açılır | ✅ |
| Diyalog: banka seçici → kod TCMB listesinden gelir, elle yazılmaz | ✅ |
| Diyalog: hareket görür · döviz takipli (→ para birimi seçici) | ✅ |
| Diyalog: canlı önizleme (tam kod, ad, seviye, miras karakter) | ✅ |
| Diyalog: kaydet doğrulama geçmeden aktifleşmez | ✅ |
| Kural 7 — kebir ekleme serbest kod kabul etmez, boş kodlar listelenir | ✅ |

Faz 5 kararları:

- **Kod maskesi istemcide bilinmiyor.** Segment uzunluğu ve ayraç, `sonraki-kod` ucunun
  döndüğü `Kod` ile `Segment` farkından çıkarılıyor (`önek = Kod − Segment`). Böylece
  hiçbir yere "3-2-2-4" veya "." yazılmadı; maske firma bazında değişirse ekran uyum sağlar.
- **Kebir modu** `bos-kebirler` ucunun 200 dönüp dönmemesine göre anlaşılıyor (uç yalnızca
  grup hesaplarında başarılı olur). Kebir seviyesinde metin kutusu yerine boş kod listesi
  çıkıyor. Düzenleme modunda hesabın mevcut kodu listeye ekleniyor.
- **Aynı diyalog hem ekleme hem düzenleme** için kullanılıyor; satırdaki "düzenle" aksiyonu
  bunu açıyor. Sistem hesabında yalnızca ad alanı açık, kalan alanlar kilitli (kural 6).
- **Üst hesap seçici yerine sabit üst hesap:** diyalog satır aksiyonundan açıldığı için üst
  hesap zaten belli; ayrı bir ağaç seçici koymak yerine başlıkta ve gövdede gösteriliyor.
- **Banka seçici** yalnızca segment uzunluğu 4 olan seviyede çıkıyor (TCMB EFT kodları 4
  hane). Varsayılan 3-2-2-4 maskesinde bu 6. seviyedir — promptun `102.01.01.0046`
  örneğiyle tutarlı.
- **TCMB banka listesi tek kaynakta.** İlk denemede liste istemciye kopyalanmıştı; Faz 6
  öncesi `GET /api/catalog/muhasebe/banka-kodlari` ucu eklendi ve istemcideki kopya
  (`Domain/Models/Muhasebe/TcmbBankalari.cs`) silindi. Uç, hesap planı seed'iyle **aynı**
  `SeedFiles/tcmb-banka-kodlari.json` dosyasını okur (`BankaKoduService`, singleton,
  dosya bir kez okunup bellekte tutulur).
- **DELETE ucu bu ekranda kullanılmıyor:** ekran şartnamesi satırda üç aksiyon istiyor
  (ekle · düzenle · pasife al), silme yok. Hareketsiz hesabı silme yeteneği API'de duruyor.
- **Bakiye kolonu** mizan ucundan geliyor (tarih filtresiz, tüm geçmiş); bakiye hiçbir
  tabloda saklanmıyor (kural 18) ve üst hesaplarda alt ağacın toplamı (kural 19).

Derleme: `dotnet build src/Client/BlazorWebApp/WebApp` → başarılı (yeni uyarı yok).

### Faz 5 doğrulaması (gerçek stack, 02.08.2026)

Ayağa kaldırılan: yerel SQL Server + CatalogService (:5004) + IdentityService (:5005) +
Ocelot gateway (:5000) + Consul (docker) + Blazor dev server (:2000). Doğrulamalar
**gateway üzerinden**, tenant 201'in gerçek seed verisiyle (293 hesap) yapıldı.

| Doğrulama | Sonuç |
|-----------|-------|
| `banka-kodlari` ucu seed dosyasından okuyor | ✅ 24 kayıt |
| Ağaç verisi: 293 hesap, 9 kök, **0 yetim** düğüm | ✅ |
| JSON sözleşmesi: camelCase + sayısal enum, istemci DTO'larıyla uyumlu | ✅ |
| `Yol` → ata Id çözümlemesi: 0 kırık referans | ✅ |
| Arama "102/Bankalar" → ataları (1, 10) otomatik açılıyor | ✅ |
| `sonraki-kod` önek çıkarımı (`102.01` − `01` = `102.`) | ✅ |
| Kebir modu: grup → 200 + boş kod listesi, kebir → 400 | ✅ |
| Hesap ekleme L4→L5→L6 zinciri, banka muavini dâhil | ✅ `102.01.01.0046` |
| Banka seçici yalnızca 4 haneli segmentte (L6) açılıyor | ✅ |
| Kural 1: çocuk eklenince üstün `HareketGorur` kapanıyor, silinince geri geliyor | ✅ |
| Kural 6: sistem hesabının kodu reddediliyor | ✅ |
| Kural 7: kebir yalnızca boş kod listesinden | ✅ |
| Düzenleme (PUT) kodu koruyor | ✅ |
| Pasife al (gövdesiz PATCH) gateway'den geçiyor | ✅ |
| Test verisi temizlendi (293 hesap, 0 kalıntı, 0 pasif, fişlere dokunulmadı) | ✅ |

Doğrulanamayanlar (tarayıcı otomasyonu bu oturumda kullanılamadı):

- Ekranın **görsel** davranışı: chevron/girinti, soluk-üst-bakiye, kilit/özel/pasif
  rozetleri, filtre çipleri, diyaloğun canlı önizlemesi. Sayfanın ve diyaloğun attığı
  HTTP dizisi ile istemci tarafı ağaç/arama algoritması gerçek veriyle birebir
  doğrulandı; kalan risk yalnızca render katmanında.
- **Bakiye kolonu boş görünecek**: henüz hiç kesinleşmiş fiş yok, dolayısıyla mizan 0
  satır dönüyor ve tüm bakiyeler `0,00`. "Bakiyeli" filtre çipi de bu yüzden boş liste
  verir. Kolon Faz 6'da fiş girilince anlam kazanacak.

Yerel çalıştırma notu: Consul docker container'ında, servisler ise host'ta `localhost`'a
bağlı çalışıyor. Servislerin kendi Consul kaydı `localhost:5004` adresini yazdığı için
container içindeki health check'e ulaşılamıyor ve servis 1 dk sonra deregister oluyor.
Yerelde gateway'i çalıştırmak için servisleri Consul'a health check'siz,
`Address=localhost` ile elle kaydetmek gerekiyor.

> Faz 6 güncellemesi (02.08.2026): Consul bu kez host'ta süreç olarak çalıştığı için
> (`consul.exe`, `127.0.0.1:8500`) servislerin kendi kaydı yeterli oldu; health check
> `localhost:5004/liveness`'a ulaştı ve elle kayıt gerekmedi. Elle kayıt yalnızca Consul
> docker'dayken gerekiyor.

### Faz 6 durumu

Rota: `/muhasebe/fis` ve `/muhasebe/fis/{id}` · Menü: **Muhasebe → Fiş Girişi**.

| Ekran maddesi | Durum |
|---------------|-------|
| Başlık: fiş türü, tarih, fiş no (otomatik), belge no, açıklama, kaynak rozeti | ✅ |
| Satır tablosu: hesap kodu · ad · masraf merkezi · açıklama · borç · alacak · sil | ✅ |
| Hesap arama hem koda hem isme göre, yalnızca hareket görenler | ✅ |
| `F2` ile hesap planı ağacı modal (`HesapSecModal`) | ✅ |
| Borç yazılınca alacak kilitlenir, tersi de geçerli | ✅ |
| Son satır her zaman boş durur; doldurulunca altına yenisi eklenir | ✅ |
| Son satırın tutar alanında fark soluk öneri; tek tuşla kabul | ✅ (`Enter` + şeritte "farkı son satıra uygula") |
| Canlı denge şeridi: dengede yeşil / dengesiz kırmızı, kaydet pasif | ✅ |
| Toplamlar her satır değişiminde yeniden hesaplanır | ✅ |
| Kısayollar: `Ctrl+S`, `Ctrl+Enter`, `Esc` (+ `F2`) | ✅ |

Kaydetme aksiyonları: Kaydet · Kaydet ve yeni · Kaydet ve kesinleştir · Vazgeç ·
Taslağı sil. Kesinleşmiş fişte yalnızca **Ters kayıt oluştur** ve Yeni fiş çıkar.

Faz 6 kararları:

- **Masraf merkezi listeleme ucu eklendi.** İlk hâlde istemci seçenekleri
  `rapor/masraf-merkezi` ucundan türetiyordu; o uç yalnızca **hareketi olan** merkezleri
  döndüğü için ilk fiş girilene kadar seçici boş kalıyordu. Artık ayrı uç var:

  ```
  GET   /api/catalog/muhasebe/masraf-merkezi[?pasifDahil=true]
  GET   /api/catalog/muhasebe/masraf-merkezi/{id}
  POST  /api/catalog/muhasebe/masraf-merkezi          { kod, ad }
  PATCH /api/catalog/muhasebe/masraf-merkezi/{id}/pasif
  ```

  Hesap planındaki kural 8 ile aynı çizgide **silme yok**; kullanılmayan merkez pasife
  çekilir, geçmiş fişlerde ve raporlarda görünmeye devam eder. Kod firma içinde tekildir
  (pasif merkez de kodu tutar), boş/10 haneden uzun kod ve boş ad reddedilir. Merkez
  **tanımlama ekranı bu fazda yok**; uçlar hazır, yönetim ekranı ayrıca kararlaştırılmalı.
- **Seçici yalnızca aktif merkezleri listeler.** Pasife alınmış bir merkeze bağlı eski
  taslak açılırsa o merkez `pasifDahil=true` ile tek seferde çekilip listeye eklenir ve
  "(pasif)" etiketiyle gösterilir; aksi hâlde seçim ekranda görünmeden kaybolur ve kayıtta
  sessizce düşerdi. Sunucu böyle bir satırı zaten reddettiği için satır turuncu uyarı
  aldı ve kaydet pasifleşiyor — kullanıcı sunucu hatasını görmeden önce uyarılıyor.
- **Döviz alanları ekranda düzenlenmiyor.** Yüklenen fişteki `ParaBirimi`/`Doviz`/`Kur`
  korunup aynen geri gönderiliyor, satırda para birimi rozeti gösteriliyor. Kural 17
  sunucuda zorlanıyor; ekranda döviz girişi ayrı bir iş (öneri: Faz 7 sonrası).
- **Kısayollar document seviyesinde JS ile yakalanıyor** (`fisKisayol.js`): `Ctrl+S` ve
  `F2` tarayıcının kendi davranışını tetiklediği için `preventDefault` gerekiyor ve bunu
  Blazor'un `@onkeydown`'ı ile koşullu yapmak mümkün değil. Açık bir Radzen diyaloğu
  varsa kısayollar devre dışı: `Esc` diyaloğu kapatmalı, `F2` ikinci modal açmamalı.
- **Fiş satırlarına `@key` verildi.** Satır silinince DOM öğeleri indekse göre yeniden
  eşleniyordu; silinen satırın kutu içeriğinin bir alttaki satırda kalma riski vardı.
- **Fiş listesi ekranı yok.** Menüdeki "Fiş Girişi" doğrudan boş fiş açar, mevcut fişe
  `/muhasebe/fis/{id}` ile gidilir. `GET /fis?bas=&bit=&durum=&hesapId=` ucu ve istemci
  metodu hazır; liste/arama ekranı Faz 7 ile birlikte değerlendirilmeli.

Testler: `dotnet test Services/CatalogService/CatalogService.UnitTests` → **98/98** geçiyor
(88 önceki + 10 masraf merkezi).

### Faz 6 doğrulaması (gerçek stack, 02.08.2026)

Ayağa kaldırılan: yerel SQL Server + CatalogService (:5004) + IdentityService (:5005) +
Ocelot gateway (:5000) + Consul (host süreci) + Blazor dev server (:2000). Tüm çağrılar
**gateway üzerinden**, tenant 201'in gerçek seed verisiyle (293 hesap, 229 hareket gören).
Test başlangıcında Fiş, FişSatır ve MasrafMerkezi tabloları boştu.

| Doğrulama | Sonuç |
|-----------|-------|
| Masraf merkezi: POST/GET/PATCH pasif, kod-ad kırpma, 409 tekil kod, 400 doğrulamalar | ✅ 14/14 |
| Ekran açılışı: `hareket-gorenler` + `masraf-merkezi` çağrıları | ✅ |
| Kural 10–14 hata mesajları (tek satır, denge, sıfır tutar, çift taraf, hareketsiz hesap) | ✅ |
| Kural 11 mesajı borç · alacak · farkı ayrı yazıyor | ✅ "Borç 1.000,00 · Alacak 600,00 · Fark 400,00" |
| Taslak kaydetme, geri okuma, PUT ile güncelleme, fiş no'nun korunması | ✅ `2026/000001` |
| Kesinleştirme (PATCH) ve PUT `kesinlestir=true` (ekranın "Kaydet ve kesinleştir" yolu) | ✅ |
| Kural 15: kesinleşmiş fiş güncellenemiyor (400) ve silinemiyor (409) | ✅ |
| Ters kayıt: taslak açılıyor, borç/alacak yer değiştiriyor, kaynak fişe atıf var | ✅ |
| Taslak silme (DELETE 204), silinen fiş 404 | ✅ |
| Kural 17: döviz satırında `Doviz`/`Kur` zorunlu; dolu gönderilince TL karşılığı `Borc`'ta | ✅ |
| Kural 14: pasif hesaba fiş kesilemiyor, hesap seçim listesinden düşüyor | ✅ |
| Pasif masraf merkezli taslak: okunuyor, kayıt reddediliyor, merkez boşaltılınca geçiyor | ✅ |
| Kural 16: **20 eşzamanlı istek** gerçek SQL Server'da tekrarsız ve boşluksuz numara | ✅ 5..24 |
| Mizan yalnızca kesinleşmişleri sayıyor, dengede, taslaklar ayrı | ✅ |
| Test verisi temizlendi (Fiş/FişSatır/MasrafMerkezi 0, 293 hesap, 0 pasif) | ✅ |

Doğrulanamayanlar:

- Ekranın **görsel/etkileşim** davranışı: denge şeridinin rengi, fark önerisinin placeholder
  olarak görünmesi, `Ctrl+S`/`Ctrl+Enter`/`F2`/`Esc` tuşlarının tarayıcıda tetiklenmesi,
  öneri kutusunun klavyeyle gezilmesi. Tarayıcı otomasyonu bu oturumda da kullanılamadı;
  ekranın attığı HTTP dizisi ve sunucu sözleşmesi gerçek veriyle birebir doğrulandı,
  kalan risk render ve tuş yakalama katmanında.
- Fiş girişi **hareketi olan** bir firmada denenmedi anlamında değil; test sırasında girilen
  fişler sonda silindiği için bakiye/mizan kolonları yine boş durumda. Faz 7 (T cetveli,
  mizan) için ekrana veri gerekirse önce birkaç kesinleşmiş fiş girilmeli.

### Faz 7 durumu

API'ye dokunulmadı; dört ekran da mevcut `/catalog/muhasebe/*` uçlarını kullanıyor.

| Rota | Ekran | Menü |
|------|-------|------|
| `/muhasebe/mizan` | Mizan | Muhasebe → Mizan |
| `/muhasebe/ekstre[/{hesapId}]` | T cetveli | Muhasebe → T Cetveli |
| `/muhasebe/fisler` | Fiş listesi | Muhasebe → Fişler |
| `/muhasebe/masraf-merkezi` | Masraf merkezi yönetimi + dağılım | Muhasebe → Masraf Merkezleri |

| Ekran maddesi | Durum |
|---------------|-------|
| **T cetveli** — başlıkta hesap kodu, adı, dönem, karakter | ✅ (+ tür, pasif/alt-ağaç rozeti) |
| T cetveli — solda borç, sağda alacak kolonu | ✅ |
| T cetveli — her satırda tarih, açıklama, tutar | ✅ (+ fiş no, üst hesapta alt hesap kodu) |
| T cetveli — satıra tıklanınca ilgili fiş açılır | ✅ `/muhasebe/fis/{fisId}` |
| T cetveli — **devir satırı** kolonların en üstünde | ✅ (`DevirBorc`/`DevirAlacak`, yalnız sıfırdan farklıysa) |
| T cetveli — kolon toplamları (devir dâhil) ve yönlü bakiye | ✅ "borç kalanı" / "alacak kalanı" |
| **Mizan** — kod, ad, borç/alacak toplam, borç/alacak bakiye kolonları | ✅ |
| Mizan — seviye filtresi (sadece kebir / kebir + 1. muavin / tümü) | ✅ |
| Mizan — **`YaprakMi`'ye göre soluk üst satırlar** | ✅ |
| Mizan — genel toplam satırı, eşit değilse uyarı bandı | ✅ |
| **Excel dışa aktarım** | ✅ dört ekranda da |
| Fiş listesi — dönem/durum/hesap filtresi, satırdan fişe gidiş | ✅ |
| Masraf merkezi — tanımlama, pasife alma, hesap kırılımı | ✅ |

Faz 7 kararları:

- **Excel çıktısı CSV olarak üretiliyor** (`CsvAktarim`). İstemcide xlsx yazan paket yok
  (`ExcelDataReader` yalnızca okur) ve API'ye dokunmamak gerekiyordu; mevcut ekranlardaki
  "Excel'e aktar" düğmeleri baytları sunucudan alıyor, burada o yol kapalıydı. Dosya
  Excel'de sorunsuz açılsın diye iki ayrıntı var: ilk satırda `sep=;` yönergesi (ayraç
  yerel ayardan bağımsız sabitlenir) ve UTF-8 BOM (Türkçe karakterler bozulmaz). Tutarlar
  `N2` ile yazıldığı için tr-TR ayracında sayı olarak okunur. Gerçek `.xlsx` isteniyorsa
  sunucuda bir uç açmak gerekir — bu fazın kapsamı dışıydı.
- **Mizanda üst satırlar soluk.** Satırlar alt ağaç toplamını taşıdığı için (kural 19)
  `YaprakMi = false` olanlar mükerrer sayıma yol açar; soluk gösterim bunu görsel olarak
  ayırıyor. Genel toplam satırların toplamı değil, sunucunun yaprak hesaplardan hesapladığı
  değerdir — Excel çıktısına da bu not düşülüyor. Doğrulamada yaprak satırların toplamının
  genel toplama eşit olduğu ölçüldü (14.500 = 14.500).
- **Mizan varsayılanı "sadece kebir"** (`seviye=3`); 293 hesaplı bir planda tüm seviyeleri
  açmak ilk açılışta okunaksız oluyor. Dönem filtresi boş başlar (tüm geçmiş).
- **Mizandan T cetveline geçiş** satırdaki ikonla yapılıyor ve seçili dönem query string
  ile taşınıyor; T cetveli aynı dönemle açılıyor.
- **T cetvelinde üst hesap seçilebiliyor.** `HesapSecModal`'a `TumHesaplarSecilebilir`
  parametresi eklendi: fiş girişinde kural 14 gereği yalnızca hareket gören + aktif hesap
  seçilebilirken, ekstrede üst hesap seçilince alt ağacın tamamı toplanıyor (kural 19).
  Aynı parametre fiş listesinin hesap filtresinde de kullanılıyor.
- **Devir satırı yalnızca sıfırdan farklıysa** çiziliyor; `Bas` verilmemişse devir zaten
  0'dır (rapor tüm geçmişi kapsar) ve satır çıkmaz. Bakiye şeridinde devir varsa devir ile
  dönem hareketi ayrı ayrı yazılıyor.
- **Taslaklar her ekranda ayrı gösteriliyor** (kural 21): mizanda ve T cetvelinde turuncu
  şerit, fiş listesinde durum rozeti. Mizandaki şeritten `/muhasebe/fisler?durum=0`
  bağlantısıyla taslak listesine gidiliyor.
- **Fiş listesinde silme yalnızca taslakta** çıkıyor (kural 15); kesinleşmiş fişin satırında
  aksiyon yok, düzeltme fiş ekranındaki ters kayıtla yapılıyor.
- **Masraf merkezi ekranı iki uçtan besleniyor:** tanım listesi (`masraf-merkezi`) ve
  dağılım raporu (`rapor/masraf-merkezi`). Hareketi olmayan merkez de listelenmeli, rapor
  ise yalnızca hareketi olanları döndüğü için ikisi ayrı çekilip eşleştiriliyor. Merkez
  satırı açılınca hesap kırılımı alt satır olarak geliyor; "masraf merkezi seçilmemiş
  hareketler" (`Dagitilmamis`) ayrı satırda gösteriliyor.
- **Masraf merkezinde güncelleme yok:** API'de yalnızca ekleme ve pasife alma var, ekran da
  bunu yansıtıyor. Ad düzeltme ihtiyacı çıkarsa API'ye `PUT` eklenmeli.

Derleme: `dotnet build src/Client/BlazorWebApp/WebApp` → başarılı, yeni uyarı yok.
Birim testleri değişmedi (98/98) — bu fazda sunucu kodu değişmedi.

### Faz 7 doğrulaması (gerçek stack, 02.08.2026)

Ekranların bağlandığı **JSON sözleşmesi** gateway üzerinden gerçek veriyle doğrulandı:
istemci DTO'ları elle yazıldığı için alan adı uyuşmazlığı sessizce boş değer verirdi.
Kurulan veri: 2026-01 tarihli kesinleşmiş açılış fişi (devir üretsin diye), 2026-07 tarihli
iki kesinleşmiş fiş, bir taslak ve bir masraf merkezi.

| Doğrulama | Sonuç |
|-----------|-------|
| `MizanDto` / `MizanSatirDto` / `MizanToplamDto` / `TaslakOzetDto` alanlarının tamamı | ✅ 22 alan |
| Mizan dengede, taslak ayrı sayılıyor | ✅ borç 14.500 = alacak, taslak 1 |
| Yaprak satırların toplamı = genel toplam (mükerrer sayım yok) | ✅ 14.500 |
| Hem yaprak hem üst satır dönüyor (soluk gösterim anlamlı) | ✅ 4 yaprak / 6 üst |
| Seviye filtresi (3 → 4) satır sayısını artırıyor | ✅ |
| `EkstreDto` / `EkstreSatirDto` alanlarının tamamı | ✅ 23 + 8 alan |
| **Devir:** 2026-06 başlangıçta önceki dönem 10.000 borç devri geliyor | ✅ |
| Kapanış = devir + dönem; bakiye 5.500 **borç kalanı** (aktif karakter, kural 20) | ✅ |
| Ekstre satırında `fisId` dolu (tıklanınca fiş açılacak) | ✅ |
| Üst hesap ekstresi alt ağacı topluyor, satırda alt hesap kodu var (kural 19) | ✅ |
| `MasrafMerkeziRaporDto` + hesap kırılımı + `Dagitilmamis` | ✅ |
| `FisOzetDto` alanları; durum ve hesap filtreleri | ✅ |
| Test verisi temizlendi (Fiş/FişSatır/MasrafMerkezi 0, 293 hesap, 0 pasif) | ✅ |

Doğrulanamayan: ekranların **görsel** davranışı (soluk üst satırlar, T cetvelinin iki
kolonu, devir satırı, uyarı bandı) ve **Excel dosyasının Excel'de açılışı**. Tarayıcı
otomasyonu bu oturumda da kullanılamadığı için CSV çıktısı gerçek Excel'de denenmedi;
üretim mantığı (ayraç, BOM, tırnaklama) kod düzeyinde sabit ama ilk indirmede gözle
kontrol edilmeli.

### Faz 7 sonrası düzeltmeler (tr-TR ve öneri kutusu)

- **Tarih kutuları Radzen seçicisine geçti.** Native `<input type="date">` .NET kültürünü
  değil **tarayıcı/işletim sistemi dilini** kullanır; İngilizce tarayıcıda `AA/GG/YYYY`
  çiziyordu ve `CultureInfo` ayarıyla düzelmiyordu. Beş ekrandaki dokuz kutu
  `RadzenDatePicker` + `DateFormat="dd.MM.yyyy"` + `Culture=tr-TR` oldu.
- **Biçimlendirme tek kaynakta:** `MuhasebeEtiket.Kultur` (tr-TR), `TarihBicimi`, `Tarih()`,
  `TarihKisa()`. Uygulama genelinde bir kültür sabitlenmediği için (projedeki diğer modüller
  de her çağrıda `new CultureInfo("tr-TR")` veriyor) muhasebe ekranları kültürü açıkça verir.
- **`Para()` kültüre bağlıydı.** `ToString("N2")` İngilizce tarayıcıda `1,250.50` üretiyordu;
  `TutarCoz` ise virgülü ondalık ayracı sayıp binlik noktasını attığı için aynı metni geri
  okuduğunda **1,2505**'e dönüştürüyordu. Fiş satırında tutar alanına geri dönülüp yazıldığında
  tutarı bozan bu tur, `Para()` tr-TR'ye sabitlenince kapandı.
- **Öneri kutusunda fareyle seçim çalışmıyordu.** `@onmousedown:preventDefault` bir
  `@onmousedown` **işleyicisi olmadan** yazılmıştı; Blazor o olaya dinleyici bağlamadığı için
  preventDefault hiç uygulanmıyor, input blur olup öneri kutusu kapanıyor ve `@onclick`
  hedefine ulaşamıyordu (klavyeyle seçim blur içermediği için çalışıyordu). Seçim artık
  `mousedown`'da yapılıyor, kutuya ve öğelere gerçek işleyici + `preventDefault` verildi.
- **Diğer iki seçicide aynı sorun yok:** `HesapSecModal` ağacı hiçbir blur olayıyla
  kapanmıyor (kalıcı olarak render ediliyor), masraf merkezi seçicisi ise native `<select>` —
  açılır listesi DOM öğesi değil. İkisi de değiştirilmedi.
