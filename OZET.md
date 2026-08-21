# ÖZET — Banka Ekstresi İşleme Modülü

Banka ekstresi yüklenip her satır için muhasebe açıklaması üretiliyor, karşı hesap katmanlı
olarak çözülüyor; belirsiz satırlar klavye odaklı onay ekranına düşüyor, onaylar öğreniliyor.
Çıktı iki parça: ORKA'ya yüklenecek düzeltilmiş ekstre dosyası + gride yazılacak karşı hesap
kodu listesi.

Mimari kararlar ve gerekçeleri: **KARARLAR.md**. Plan ve dosya listesi: **PLAN.md**.

> Bu belge **düzeltmeler turundan sonraki** hâli anlatıyor
> (`claude-code-prompt-banka-modulu-duzeltmeler.md`). Değişen kararlar KARARLAR.md §21–32.

---

## Nasıl çalışıyor

**Katman sırası** (ilk çözen kazanır, hangi katmanın çözdüğü satırda `KaynakKatman`'da durur):

1. **Geçmiş onay** — `EkstreHesapEslesmeleri`, anahtar normalize **unvan çekirdeği**
   (`DAGI GIYIM`); unvansız satırlarda `ISLEM:<işlem tipi>`. Önce genişletilmiş anahtar
   (çekirdek + ayırt edici kelime), tutmazsa sade çekirdek.
2. **Banka kayıt defteri** — bankalar arası hareketlerde karşı taraf tanımlı banka
   hesaplarından bulunur.
3. **Sabit kural** — işlem tipi → hesap kodu (banka masrafı → 770, HGS → 740).
4. **Unvan benzerliği** — yön → ana grup (giren 120, çıkan 329) daraltmasından sonra
   normalize unvanın **her token'ı sırayla çıpa**: çıpayla başlayan hesapları getirip
   kalan metinle skorlar, en yüksek skoru alır. Çıpanın kaç aday getirdiğine bakılmaz —
   kalabalık çıpalar meşru cari aileleri (PKF 89, PARDUS 101, ISTANBUL 126), eleyince
   doğru eşleşme kayboluyor (KARARLAR §25b). Yanlış eşleşmeyi 0.85 eşiği ve 0.05 aday
   farkı zaten tutuyor. Hiçbir çıpa aday getirmezse kod önerilmez, satır onaya düşer.
   Arama, ana grup başına sıralı bir ön indeks (`HesapPlaniIndeksi`) üzerinden ikili
   aramayla yapılıyor; indeks yükleme başına bir kez kuruluyor.

**IBAN ve VKN katmanları kapalı.** Kod duruyor ama `BankaHesabi.IbanKatmaniAktif` /
`VknKatmaniAktif` bayrakları varsayılan `false`. Vakıfbank'ta VKN kolonu karşı tarafın
değil hesap sahibinin VKN'si (286 satırın hepsinde aynı değer); IBAN verisi de düzenli
tutulmuyor. Bankalar arası eşleştirmenin kendi IBAN kontrolü bundan ayrı ve açık
(kullanıcının Tanımlar'da girdiği hesap IBAN'ı).

**Aile ayrımı.** En iyi adayla 0.05 içindeki adaylar aile sayılır (Park Plaza Aidat /
Elektrik / 19. Kat). Üyelerin ortak olmayan kelimeleri ham banka açıklamasında aranır;
tam bir üye bulunursa seçilir ve anahtara ek olarak yazılır, aksi hâlde satır onaya düşer
ve **tüm aile** aday olarak listelenir.

---

## Banka hesaplarının toplu içe aktarımı

19 hesabı tek tek girmek yerine Tanımlar > Banka hesapları > **Toplu İçe Aktar**. Kalıp
hesap planı içe aktarımıyla birebir aynı: tek sayfa xlsx, kolonlar **başlık adıyla**
bulunur (sıra önemsiz, büyük-küçük harf ve Türkçe karakter toleranslı), doğrulama satır
bazlıdır.

| Kolon | Zorunlu | Not |
|---|---|---|
| `Orka Hesap Kodu` | evet | Boşluklu saklanır (`102 1 32 87`); hesap planında kayıtlı olmalı |
| `Hesap Adı` | evet | Yeni alan: `BankaHesabi.HesapAdi` |
| `Banka Adı` | evet | Katman 3 metin eşlemesinde kullanılan ad |
| `Hesap Tipi` | evet | `Vadesiz` \| `Vadeli` |
| `Para Birimi` | evet | TL → `TRY`; USD, EUR |
| `Parser Tipi` | hayır | Boşsa hesap tanımlanır, ekstresi yüklenemez (uyarı) |
| `IBAN` | hayır | Boşluklar atılır, büyük harfe çevrilir |

**Upsert:** anahtar `OrkaHesapKodu` + firma. Varsa güncellenir, yoksa eklenir. Dosyada
olmayan hesaplara **dokunulmaz** (hesap planının aksine pasife çekilmez — KARARLAR §33).

**Satır bazlı doğrulama** — hata satırı düşürür, uyarı düşürmez:

| Durum | Sonuç |
|---|---|
| Kod hesap planında yok / pasif | hata, satır atlanır |
| Kod `102` ile başlamıyor | uyarı, satır yine eklenir |
| `Hesap Tipi` tanınmıyor | hata |
| `Parser Tipi` kayıtlı ayrıştırıcı değil | hata (geçerli tipler mesajda) |
| Aynı kod dosyada iki kez | hata, ilk satır korunur |
| Kod / hesap adı / banka adı / para birimi boş | hata |
| Zorunlu kolon başlığı yok | **dosyanın tamamı** reddedilir (400) |

Rapor: okunan / eklenen / güncellenen / atlanan sayıları + her sorun için satır numarası
ve sebep. Sorunlar mevcut `{ field, message }` sözleşmesini kullanıyor (`SatirNo` eklendi).
"Örnek şablon indir" bağlantısı doğru başlıklara sahip boş bir xlsx üretir; ikinci sayfada
kolon açıklamaları ve geçerli ayrıştırıcı listesi var.

---

## Ne yapıldı

### Sunucu — `Services/CatalogService/CatalogService.Api/Features/BankaEkstre`

| Dosya | İş |
|---|---|
| `Domain/BankaEkstreEnums.cs` | `HesapTipi`, `Yon`, `YuklemeDurum`, `SatirDurum`, `KaynakKatman`, `AnahtarTipi`, `EslesmeTuru` |
| `Domain/BankaHesabi.cs` | Ekstresi işlenen hesap + banka kayıt defteri + kapalı katman bayrakları |
| `Domain/EkstreYukleme.cs` | Dosya yüklemesi, dönem, uyarılar, **kaynak dosya içeriği**, açıklama kolonu |
| `Domain/EkstreSatiri.cs` | Ham veri + üretilen açıklama + öğrenme anahtarı + aday listesi + `EslesenKarsiSatirId` |
| `Domain/HesapEslesmesi.cs` | **Firma bazlı** öğrenilen eşleşme (çekirdek + ayırt edici ek → kod) |
| `Domain/KimlikKaydi.cs` | **Global** kimlik: bir unvanın kim olduğu her firmada aynı |
| `Domain/HesapPlaniKaydi.cs` | ORKA hesap planı satırı (boşluklu kod, `AnaGrup`, `BaslangicHarfi`, `SonGuncelleme`) |
| `Domain/AciklamaSablonu.cs` | İşlem tipi → açıklama şablonu, `BankalarArasi` bayrağı |
| `Domain/UnvanDeseni.cs` | Unvan çıkarma regex'i, banka bazlı, sıralı |
| `Domain/SabitKural.cs` | İşlem tipi → hesap kodu |
| `Services/Normalizasyon.cs` | Türkçe sadeleştirme, gürültü temizliği, **`UnvanCekirdek`**, `IslemAnahtari`, `MetinNormalize`, IBAN, Title Case, kod yardımcıları |
| `Services/Benzerlik.cs` | Levenshtein oranı + ilk-14-karakter önek kuralı (0.95) |
| `Services/Parsing/*` | `IEkstreParser`, `VakifbankVadesizParser`, `EkstreParserSecici` |
| `Services/UnvanCikarici.cs` | Desenleri sırayla dener, ilk yakalayan kazanır |
| `Services/AciklamaUretici.cs` | Şablon seçimi + yer tutucu doldurma + 50 karakter sınırı |
| `Services/HesapEslestirici.cs` | 4 katman + kapalı IBAN/VKN + çoklu token çıpası + aile ayrımı + `HesapPlaniIndeksi` |
| `Services/HesapEslesmeService.cs` | Öğrenilen eşleşmelerin yazımı, arama, düzeltme, silme |
| `Services/EkstreService.cs` | Yükle/işle, satır listeleme, onay, dışa aktarım (iki parça) |
| `Services/BankaHesabiService.cs` | Banka hesapları CRUD |
| `Services/EkstreHesapPlaniService.cs` | Hesap planı xlsx içe aktarımı (pasife çekmeli), arama, özet |
| `Services/BankaHesabiIceAktarimService.cs` | **Banka hesaplarının toplu xlsx içe aktarımı** + örnek şablon üretimi |
| `Controllers/*.cs` | 4 controller, hepsi `api/catalog/banka-ekstre/...` |
| `BankaEkstreSeed.cs` | Vakıfbank şablon / desen / sabit kural satırları (idempotent) |

Tablolar (`catalog` şeması): `EkstreBankaHesaplari`, `EkstreYuklemeler`, `EkstreSatirlari`,
`EkstreHesapEslesmeleri` *(firma)*, `EkstreKimlikKayitlari` *(global)*, `EkstreHesapPlani`,
`EkstreAciklamaSablonlari`, `EkstreUnvanDesenleri`, `EkstreSabitKurallar`.
`EkstreOgrenmeKayitlari` düşürüldü (KARARLAR §23).

Yeni uç noktalar:

| Uç nokta | İş |
|---|---|
| `GET  .../hesap-plani/ozet` | Kayıt sayısı + son içe aktarım + gün farkı |
| `POST .../banka-hesaplari/ice-aktar` | **Banka hesaplarının toplu içe aktarımı** (multipart xlsx) |
| `GET  .../banka-hesaplari/sablon` | **Doğru başlıklara sahip boş şablon** (xlsx dosya) |
| `POST .../ekstre/{id}/duzeltilmis-ekstre` | Dışa aktarımın 1. parçası (xlsx dosya) |
| `GET  .../eslesmeler?q=` | Öğrenilen eşleşme araması |
| `PUT  .../eslesmeler/{id}` | Eşleşmeyi düzelt (kod / yön / ayırt edici ek) |
| `DELETE .../eslesmeler/{id}` | Eşleşmeyi sil |

### İstemci — `src/Client/BlazorWebApp/WebApp`

| Dosya | İş |
|---|---|
| `Pages/BankaEkstre/IslemePage.razor` | **`/banka-isleme`** — günlük ana ekran: dönem seçici, banka sekmeleri (onay bekleyen rozetiyle), hesap kartları, sürükle-bırak yükleme |
| `Pages/BankaEkstre/TanimlarPage.razor` | **`/banka-isleme/tanimlar`** — üç bölümü barındırır |
| `Pages/BankaEkstre/Bolumler/HesapPlaniBolumu.razor` | Son içe aktarım + sayı + "Güncelle" |
| `Pages/BankaEkstre/Bolumler/BankaHesaplariBolumu.razor` | Hesap CRUD + kapalı katman bayrakları + **Toplu İçe Aktar / örnek şablon indir + satır bazlı sonuç raporu** |
| `Pages/BankaEkstre/Bolumler/OgrenilenEslesmelerBolumu.razor` | Öğrenilen eşleşmeler: arama, düzenleme, silme |
| `Pages/BankaEkstre/EkstreOnayPage.razor` | `/banka-isleme/onay/{id}` — klavye odaklı onay, **çok üyeli aday listesi**, iki parça dışa aktarım |
| `Shared/Dto/BankaEkstre/BankaEkstreDtos.cs` | DTO'lar + `BankaEkstreEtiket` (Türkçe etiketler, tr-TR biçim) |
| `Application/Services/BankaEkstreApi.cs` | HTTP istemcisi, `{ field, message }` hata sözleşmesi, dosya indirme |
| `Layout/MainLayout.razor` | "Banka İşleme" → İşleme / Tanımlar |

Kaldırılanlar: `Pages/BankaEkstre/EkstreYuklePage.razor` (`/banka-isleme/yukle`),
`/banka-isleme/hesaplar` rotası (bileşen olarak Tanımlar'a taşındı).

### Testler — `Services/CatalogService/CatalogService.UnitTests/BankaEkstre`

`BankaEkstreTestOrtami.cs` (bellek içi context — artık tenant parametreli, xlsx üretici),
`VakifbankParserTests`, `UnvanCikariciTests`, `NormalizasyonTests`, `AciklamaUreticiTests`,
`HesapEslestiriciTests`, `EkstreServiceTests`, `EkstreHesapPlaniServiceTests`,
`BankaHesabiIceAktarimServiceTests` (12 test: upsert, satır bazlı doğrulama, kolon sırası,
firma izolasyonu, şablon).

---

## Doğrulama

| # | Kabul kriteri | Durum |
|---|---|---|
| 1 | Çözüm derleniyor, yeni uyarı yok; testler geçiyor | ✅ `dotnet build SmartExpenseSystem.sln` → 0 hata. Temiz `-t:Rebuild` sonrası 218 uyarı çıkıyor, **hiçbiri BankaEkstre dosyalarından değil** (repoda zaten vardı) |
| 2 | Migration oluşturuldu ve uygulanıyor | ✅ `20260821061931_AddBankaEkstreDuzeltmeleri`; `dotnet ef database update` uygulandı, `has-pending-model-changes` temiz |
| 3 | VKN katmanı kapalı; parser `KarsiVkn` doldurmuyor | ✅ `Vkn_katmani_varsayilan_kapali`, `Vkn_katmani_bayrak_acilinca_calisir`, `VakifbankParserTests` (`Assert.Null(ilk.KarsiVkn)`) |
| 4 | IBAN katmanı eşleştirmede kullanılmıyor | ✅ `Iban_katmani_varsayilan_kapali` — IBAN kaydı varken bile geçmiş onay kazanıyor |
| 5 | Öğrenme anahtarı unvan çekirdeği; ikinci ay geçmiş onaydan çözülüyor | ✅ `Ayni_cari_farkli_sorgu_numarasiyla_ikinci_kez_gecmis_onaydan_cozulur` (farklı sorgu no + farklı tarih + farklı tutar), `Ayni_cari_farkli_sorgu_numarasiyla_ayni_cekirdege_duser` |
| 6 | Çoklu token çıpası: `NAOSKZ NAOS İSTANBUL KOZMETİK` → `120 N15` | ✅ `Coklu_token_cipasi_banka_ic_kodunu_atlar` |
| 7 | ~~Aday patlaması olan çıpa dikkate alınmıyor~~ — **kural kaldırıldı** | ⛔ Gerçek hesap planıyla ölçüldü, zarar verdiği görüldü (KARARLAR §25b). Yerine ön indeks kondu. Regresyon testleri: `Kalabalik_cipa_elenmez_pkf_ailesi_dogru_cariye_gider` (89 aday → `120 P44`), `Kalabalik_cipa_elenmez_istanbul_portfoy_dogru_cariye_gider` (126 aday → `120 I61`), ikisi de skor > 0.90 |
| 8 | Park Plaza ailesi onaya düşüyor, üç aday da listeleniyor | ✅ `Park_plaza_ailesi_onaya_duser_ve_tum_uyeler_listelenir`; ayırt edici kelime metinde geçerse `Aile_ayirt_edici_kelime_metinde_geciyorsa_cozulur` |
| 9 | İki firma birbirinin planını ve eşleşmelerini görmüyor | ✅ `Iki_firma_birbirinin_hesap_planini_ve_eslesmelerini_gormez` — aynı veritabanı, iki tenant, aynı unvan farklı koda eşleşiyor |
| 10 | Menü İşleme / Tanımlar; hesap planı günlük ekranda değil | ✅ `MainLayout.razor` + `IslemePage` (yalnız uyarı ve bağlantı) |
| 11 | Öğrenilen eşleşmeler düzenlenebiliyor / silinebiliyor | ✅ `OgrenilenEslesmelerBolumu.razor` + `HesapEslesmeleriController` |
| 12 | Dışa aktarım iki parça; eksik satır varken 400 | ✅ `Duzeltilmis_ekstre_aciklama_kolonunu_degistirir`, `Cozulemeyen_satir_onaya_duser_ve_disa_aktarimi_engeller` (her iki uç nokta da aynı kontrolden geçiyor) |
| — | Bilinmeyen kod kaydediliyor ama öğrenilmiyor | ✅ `Bilinmeyen_kod_kaydedilir_ama_ogrenilmez` |
| — | Geçmiş onaydan çözülen satır düzeltilince öğrenme de düzeliyor | ✅ `Gecmis_onaydan_cozulen_satir_duzeltilince_ogrenme_kaydi_guncellenir` |

Banka hesabı toplu içe aktarımı (`claude-code-prompt-banka-hesap-ice-aktarim.md`):

| # | Kabul kriteri | Durum |
|---|---|---|
| 1 | Derleme temiz, testler geçiyor | ✅ CatalogService.Api + WebApp 0 hata; 236 + 18 test |
| 2 | Migration üretildi ve uygulandı | ✅ `20260821082234_AddBankaHesabiIceAktarim` (`HesapAdi` + benzersiz index); `database update` uygulandı, `has-pending-model-changes` temiz |
| 3 | Şablon indirme çalışıyor | ✅ `GET .../banka-hesaplari/sablon`; `Sablon_dogru_basliklarla_uretilir` üretilen dosyayı geri okuyor |
| 4 | Hatalı satır tüm içe aktarımı düşürmüyor | ✅ `Gecersiz_hesap_tipi_satiri_atlanir_digerleri_islenir`, `Hesap_planinda_olmayan_kod_atlanir_ve_raporlanir`, `Ayni_kod_dosyada_iki_kez_gecerse_ikincisi_hata` |
| 5 | Tekli CRUD bozulmadı | ✅ `BankaHesabiService` yalnız `HesapAdi` eşlemesiyle genişledi; içe aktarım ayrı serviste, controller'daki mevcut uç noktalar aynı |
| — | Kolon sırası değişse de okunuyor | ✅ `Kolon_sirasi_degisse_de_okunur` (ters sıra + Türkçe karaktersiz başlıklar) |
| — | Farklı firmada aynı kod ayrı kayıt | ✅ `Farkli_firmada_ayni_kod_ayri_kayit_olur` (aynı veritabanı, iki tenant) |
| — | İkinci kez aynı dosya → güncelleme | ✅ `Ayni_dosya_ikinci_kez_guncelleme_sayar` (3 güncellendi, 0 eklendi) |
| — | Dosyada olmayan hesaba dokunulmuyor | ✅ `Dosyada_olmayan_mevcut_hesaba_dokunulmaz` |

Test sonucu: **236 test, 0 başarısız** (`CatalogService.UnitTests`),
**18 test, 0 başarısız** (`WebApp.UnitTests`).

Çalıştırılan doğrulama komutları:

```
dotnet build SmartExpenseSystem.sln
dotnet ef migrations add AddBankaEkstreDuzeltmeleri   # Services/CatalogService/CatalogService.Api
dotnet ef migrations add AddBankaHesabiIceAktarim     # banka hesabı toplu içe aktarımı
dotnet ef migrations has-pending-model-changes
dotnet ef database update
dotnet test Services/CatalogService/CatalogService.UnitTests/CatalogService.UnitTests.csproj
dotnet test src/Client/BlazorWebApp/WebApp.UnitTests/WebApp.UnitTests.csproj
```

---

## Ne eksik kaldı

- **Ekranlar tarayıcıda denenmedi.** `WebApp.UnitTests` saf xunit (bUnit yok), Razor
  tarafının otomatik testi yok. Elle bakılacaklar: `/banka-isleme` banka sekmeleri ve
  rozetler, boş karta dosya sürükleme, Tanımlar'daki üç bölüm, onay ekranında `Alt+1..9`
  ile üç üyeli aileden seçim, "Düzeltilmiş ekstre" indirmesi, firma değiştirince
  sayfaların yenilenmesi.
- **Gerçek Vakıfbank dosyasıyla denenmedi.** Testler ölçülen yapıyı taklit eden üretilmiş
  xlsx kullanıyor. Gerçek dosyada kolon başlıkları farklı adlanıyorsa parser sabit
  indekslere düşer ve `Uyarilar`'a yazar; kart bu uyarıyı gösteriyor.
- **Kaynak ekstre dosyası veritabanında saklanıyor** (`EkstreYuklemeler.DosyaIcerik`,
  varbinary(max)). Yükleme başına birkaç yüz KB; yıllar içinde büyürse eski yüklemelerin
  içeriği temizlenebilir (dosya olmayan yüklemede kod listesi hâlâ üretiliyor).
- **`EslesenKarsiSatirId` boş.** Alan ve migration hazır, grup içi çapraz doğrulama
  mantığı yazılmadı (prompt §11 böyle istiyor).
- **Sabit kurallar ana hesap seviyesinde** (`770`, `740`); muavin kırılımı firmaya özel
  olduğu için uydurulmadı. `EkstreSabitKurallar` tablosundan düzenlenmeli.
  `Vergi Tahsilatı` için kural yok — o satırlar onaya düşer.
- **Şablon/desen/kural tabloları için yönetim ekranı yok.** Şimdilik seed + SQL.
- **Banka hesabı içe aktarımı tarayıcıda denenmedi.** Servis ve şablon üretimi birim
  testli; "Toplu İçe Aktar" düğmesi, dosya seçici ve rapor listesi elle görülecek.
- **İçe aktarım `Aktif` bayrağını değiştirmiyor.** Dosyada `Aktif` kolonu yok; pasife
  alınmış bir hesap dosyada geçse bile pasif kalır, ekrandan açılır (KARARLAR §34).

---

## Sonraki banka parser'ı eklerken nereye dokunulacak

Mimari buna hazır; **kod değişikliği tek dosya**:

1. **Yeni parser yaz:** `Features/BankaEkstre/Services/Parsing/AkbankVadesizParser.cs`,
   `IEkstreParser` uygula. `ParserTipi` sabiti benzersiz olmalı (ör. `"AKBANK_VADESIZ"`).
   `VakifbankVadesizParser`'ı kopyalayıp kolon eşlemesini değiştirmek yeterli;
   başlık-önce-isimle-ara / sonra-indekse-düş kalıbını koruyun.
   **Unutmayın:** `AyrilanSatir.KaynakSatirNo` ve `EkstreParseSonuc.AciklamaKolonu`
   doldurulmalı — düzeltilmiş ekstre dışa aktarımı bu ikisini kullanıyor.
   `KarsiVkn`'yi yalnız gerçekten karşı tarafın VKN'si geliyorsa doldurun.

2. **DI'a kaydet:** `Program.cs` içinde tek satır —
   `builder.Services.AddSingleton<IEkstreParser, AkbankVadesizParser>();`
   `EkstreParserSecici` otomatik toplar, seçici/servis/controller değişmez.

3. **Yapılandırma satırlarını ekle:** `BankaEkstreSeed.cs` içine yeni bankanın
   `ParserTipi`'yle şablon / unvan deseni / sabit kural satırları. Seed idempotent.

4. **Kullanıcı tarafı:** Tanımlar > Banka hesapları'ndan yeni hesap açılır, ayrıştırıcı
   listesinden yeni banka seçilir. Karşı tarafın IBAN/VKN'si güvenilir geliyorsa ilgili
   katman bayrağı burada açılır. İşleme ekranındaki banka sekmesi kendiliğinden çıkar.
   **Menü, gateway, migration, DTO, sayfa değişmez.**

Değişmeyecek yerler: `EkstreService`, `HesapEslestirici`, `HesapEslesmeService`,
`AciklamaUretici`, `UnvanCikarici`, controller'lar, Blazor sayfaları, Ocelot yapılandırması.
