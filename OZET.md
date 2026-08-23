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
   hesaplarından bulunur. Sıra: IBAN → hesabın **eşleştirme anahtarları** → banka adı.
   Her iki metin adımında da **en uzun eşleşen kazanır** (`Otomatik Süpürme` >
   `Vakıfbank`), eşleşme tam kelime sınırıyla aranır (`TEB` anahtarı `OTEBANK` içinde
   tutmaz). En uzunda beraberlik varsa — aynı bankanın iki hesabı, açıklamada yalnız
   `Vakıfbank` geçiyor — tahmin edilmez: **kod önerilmez**, adaylar listelenir ve satır
   onaya düşer. Tek hesaplı bankada (Fibabanka) anahtar olmadan banka adından çözülür.
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

## Eşleştirme anahtarları

`102` grubu, cevap anahtarındaki en büyük kalem (Vakıfbank 48, İş Bankası 30, Ziraat 19,
Akbank 10 satır) ve **aynı bankada birden fazla hesap** var:

```
102 1 1 01    Vakıfbank, Vadesiz TL
102 1 1 04    Vakıfbank, Vadeli TL - Otomatik Süpürme Hesabı
```

Açıklamada yalnız `BankaAdi` ("Vakıfbank") arandığı sürece bu ikisi ayrılamıyordu.
`BankaHesabi.EslestirmeAnahtarlari` (nullable, virgülle ayrılmış, 300 karakter) hesaba
özel ayırt edici ifadeleri tutuyor:

| Açıklama | Anahtar | Sonuç |
|---|---|---|
| `Otomatik Süpürme Pkf Aday` | `Otomatik Süpürme` (102 1 1 04) | otomatik, `102 1 1 04` |
| `Hesaplar Arası Eft - Vakıfbank` | yok | **onaya düşer**, iki Vakıfbank hesabı aday |
| `Hesaplararası Virman - Fibabanka` | yok, tek hesaplı banka | otomatik, banka adından |

Karşılaştırma `Normalizasyon.MetinNormalize` üzerinden (Türkçe sadeleştirme, büyük-küçük
harf duyarsız, alfanümerik dışı boşluk) ve `IfadeVarMi` ile **tam kelime sınırında**
yapılıyor. 3 karakterden kısa anahtarlar yok sayılıyor.

**Öneri.** Formda hesap adı yazılınca (anahtar alanı boşsa) `GET
.../banka-hesaplari/anahtar-onerisi` çağrılıyor: banka adı, genel kelimeler (`Vadesiz`,
`TL`, `Hesabı`…) ve hesap numaraları atıldıktan sonra kalan ifadeler öneriliyor.
`"Vakıfbank, Vadeli Tl - Otomatik Süpürme Hesabı"` → `"Otomatik Süpürme"`. Kullanıcı
düzenleyebiliyor; kaydedilen değer formdaki değer (KARARLAR §38).

**Banka adı alanı.** İpucu netleştirildi: buraya **kısa banka adı** yazılır. 25 karakteri
aşan veya virgül/tire içeren ad kaydedilir ama yumuşak uyarı gösterilir — engelleme yok
(KARARLAR §39).

---

## Ayrıştırıcısı olmayan hesap

Hesapların çoğuna ekstre yüklenmiyor; vadeli, süpürme, blokaj ve yatırım hesapları yalnız
**karşı hesap olarak bulunabilmek** için tanımlı. `BankaHesabi.ParserTipi` bu yüzden
nullable:

- Formda "Yok — ekstre yüklenmez" seçeneği var ve **yeni hesabın varsayılanı** bu.
- Ayrıştırıcısız hesap İşleme ekranında **kart göstermiyor** (sekmeler de yalnız ekstre
  yüklenebilen hesaplardan türüyor); ekranın altında kaç hesabın gizlendiği yazıyor.
- O hesaba ekstre yüklenmeye çalışılırsa anlaşılır hata dönüyor, yükleme kaydı açılmıyor.
- Kayıt defterinde ve eşleştirmede eskisi gibi kullanılıyor.

Eski satırlarda "yok" boş metinle saklanıyordu; migration bunları `NULL`'a çeviriyor.

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
| `Eşleştirme Anahtarları` | hayır | Virgülle ayrılmış; boş hücre mevcut anahtarı **silmez** |

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
| `Services/EslestirmeAnahtari.cs` | Anahtar listesinin ayrıştırılması/saklanması + hesap adından öneri üretimi |
| `Services/HesapEslestirici.cs` | 4 katman + kapalı IBAN/VKN + çoklu token çıpası + aile ayrımı + `HesapPlaniIndeksi` + **anahtar/banka adı eşlemesi ve belirsizlikte onaya düşürme** |
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
| `GET  .../banka-hesaplari/anahtar-onerisi` | Hesap adından eşleştirme anahtarı önerisi |
| `POST .../ekstre/{id}/duzeltilmis-ekstre` | Dışa aktarımın 1. parçası (xlsx dosya) |
| `GET  .../eslesmeler?q=` | Öğrenilen eşleşme araması |
| `PUT  .../eslesmeler/{id}` | Eşleşmeyi düzelt (kod / yön / ayırt edici ek) |
| `DELETE .../eslesmeler/{id}` | Eşleşmeyi sil |

### İstemci — `src/Client/BlazorWebApp/WebApp`

| Dosya | İş |
|---|---|
| `Pages/BankaEkstre/AktarPage.razor` | **`/banka-otomasyon/aktar`** — günlük ana ekran: dönem seçici, banka sekmeleri (onay bekleyen rozetiyle), hesap kartları, sürükle-bırak yükleme |
| `Pages/BankaEkstre/TanimlarPage.razor` | **`/banka-otomasyon/tanimlar`** — firmanın kurulumu: hesap planı, **banka hesapları (tam CRUD)**, öğrenilen eşleşmeler, kişi yönlendirmeleri, vergi kodları, hesap sahibi unvanları |
| `Pages/BankaEkstre/Bolumler/HesapPlaniBolumu.razor` | Son içe aktarım + sayı + "Güncelle" |
| `Pages/BankaEkstre/Bolumler/BankaHesaplariBolumu.razor` | Hesap CRUD + kapalı katman bayrakları + **Toplu İçe Aktar / örnek şablon indir + satır bazlı sonuç raporu** |
| `Pages/BankaEkstre/Bolumler/OgrenilenEslesmelerBolumu.razor` | Öğrenilen eşleşmeler: arama, düzenleme, silme |
| `Pages/BankaEkstre/EkstreOnayPage.razor` | `/banka-otomasyon/onay/{id}` — klavye odaklı onay, **çok üyeli aday listesi**, iki parça dışa aktarım |
| `Shared/Dto/BankaEkstre/BankaEkstreDtos.cs` | DTO'lar + `BankaEkstreEtiket` (Türkçe etiketler, tr-TR biçim) |
| `Application/Services/BankaEkstreApi.cs` | HTTP istemcisi, `{ field, message }` hata sözleşmesi, dosya indirme |
| `Layout/MainLayout.razor` | "Banka Otomasyon" (→ firma listesi) → Aktar / Tanımlar |

Kaldırılanlar: `Pages/BankaEkstre/EkstreYuklePage.razor` (`/banka-isleme/yukle`),
`/banka-isleme/hesaplar` rotası (bileşen olarak Tanımlar'a taşındı).

> Rotalar sonradan `/banka-otomasyon/...` oldu; eski adresler yönlendiriyor. Bkz. son bölüm.

### Testler — `Services/CatalogService/CatalogService.UnitTests/BankaEkstre`

`BankaEkstreTestOrtami.cs` (bellek içi context — artık tenant parametreli, xlsx üretici),
`VakifbankParserTests`, `UnvanCikariciTests`, `NormalizasyonTests`, `AciklamaUreticiTests`,
`HesapEslestiriciTests`, `EkstreServiceTests`, `EkstreHesapPlaniServiceTests`,
`BankaHesabiIceAktarimServiceTests` (upsert, satır bazlı doğrulama, kolon sırası, firma
izolasyonu, şablon, **`Eşleştirme Anahtarları` kolonu**), `BankaHesabiServiceTests`
(ayrıştırıcı isteğe bağlı, anahtarların temizlenerek saklanması, öneri),
`EslestirmeAnahtariTests` (liste ayrıştırma + öneri üretimi).
İstemci tarafında `src/Client/BlazorWebApp/WebApp.UnitTests/BankaEkstre/BankaAdiDenetimiTests.cs`
(banka adı yumuşak uyarısı).

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

Eşleştirme anahtarları + isteğe bağlı ayrıştırıcı (`claude-code-prompt-eslestirme-anahtarlari.md`):

| # | Kabul kriteri | Durum |
|---|---|---|
| 1 | Derleme temiz, tüm testler geçiyor | ✅ `dotnet build SmartExpenseSystem.sln` → 0 hata; 267 + 28 test |
| 2 | Migration üretildi ve uygulandı, `has-pending-model-changes` temiz | ✅ `20260821105315_AddBankaHesabiEslestirmeAnahtarlari`; `database update` uygulandı, "No changes have been made to the model" |
| 3 | Prompt'taki testlerin hepsi yazıldı | ✅ aşağıdaki satırlar |
| 4 | Mevcut tekli CRUD ve toplu içe aktarım bozulmadı | ✅ mevcut testler değişmeden geçiyor; tek güncellenen bekleyiş `ParserTipi` boş metin → `NULL` |
| 5 | Ayrıştırıcısız hesap eşleştirmede kullanılıyor, ekstre kabul etmiyor | ✅ `Ayristiricisiz_hesap_kaydedilebilir` + `Ayristiricisiz_hesaba_ekstre_yuklenemez`; eşleştirme hesapları `ParserTipi`'ne bakmadan tarıyor |
| — | `"Otomatik Süpürme Pkf Aday"` → `102 1 1 04` (anahtar), `102 1 1 01` değil | ✅ `Anahtar_ayni_bankanin_dogru_hesabini_secer` |
| — | `"Hesaplar Arası Eft - Vakıfbank"` → onaya düşer, iki aday listelenir | ✅ `Anahtar_tutmayinca_ayni_bankada_iki_hesap_varsa_onaya_duser` (öneri kodu boş, 2 aday) |
| — | Tek hesaplı banka (Fibabanka) banka adından çözülür | ✅ `Tek_hesapli_bankada_anahtar_olmadan_banka_adindan_cozulur` |
| — | En uzun anahtar kazanır | ✅ `En_uzun_anahtar_kazanir` (`Vakıfbank` vs `Otomatik Süpürme`) |
| — | Türkçe karakter / büyük-küçük harf duyarsız | ✅ `Anahtar_eslesmesi_turkce_karakter_ve_buyuk_kucuk_harf_duyarsiz` (`"otomatık supurme"`) |
| — | Kelime ortasında eşleşme olmuyor | ✅ `Anahtar_kelime_ortasinda_eslesmez` (`TEB` / `OTEBANK`), `Ifade_tam_kelime_siniriyla_aranir` |
| — | Toplu içe aktarım `Eşleştirme Anahtarları` kolonunu okur | ✅ `Eslestirme_anahtarlari_kolonu_okunur`, `Bos_anahtar_hucresi_mevcut_anahtari_silmez` |
| — | Hesap adından anahtar önerisi | ✅ `EslestirmeAnahtariTests` (üç gerçek hesap adı), `Anahtar_onerisi_hesap_adindan_uretilir` |
| — | Banka adı uyarısı engellemiyor | ✅ `BankaAdiDenetimiTests` (WebApp.UnitTests) |

Test sonucu: **267 test, 0 başarısız** (`CatalogService.UnitTests`),
**28 test, 0 başarısız** (`WebApp.UnitTests`).

Çalıştırılan doğrulama komutları:

```
dotnet build SmartExpenseSystem.sln
dotnet ef migrations add AddBankaEkstreDuzeltmeleri   # Services/CatalogService/CatalogService.Api
dotnet ef migrations add AddBankaHesabiIceAktarim     # banka hesabı toplu içe aktarımı
dotnet ef migrations add AddBankaHesabiEslestirmeAnahtarlari   # anahtarlar + ayrıştırıcı nullable
dotnet ef migrations has-pending-model-changes
dotnet ef database update
dotnet test Services/CatalogService/CatalogService.UnitTests/CatalogService.UnitTests.csproj
dotnet test src/Client/BlazorWebApp/WebApp.UnitTests/WebApp.UnitTests.csproj
```

**Uç noktalar çalışırken doğrulandı** (derlemeyle değil, gerçek istekle). CatalogService
`http://localhost:5004` üzerinde ayağa kaldırılıp elle üretilen bir dev JWT ile çağrıldı:

| İstek | Sonuç |
|---|---|
| `GET .../banka-hesaplari/sablon` (tokensiz) | `401` — `[Authorize]` çalışıyor |
| `GET .../banka-hesaplari/sablon` | `200`, 8046 bayt, `Content-Type: ...spreadsheetml.sheet`, `Content-Disposition: attachment; filename=banka-hesaplari-sablon.xlsx` |
| indirilen dosyanın başlıkları | `Orka Hesap Kodu │ Hesap Adı │ Banka Adı │ Hesap Tipi │ Para Birimi │ Parser Tipi │ IBAN │ Eşleştirme Anahtarları` + "Açıklama" sayfası |
| `POST .../banka-hesaplari/ice-aktar` (tek satırlı xlsx) | `200` — `{"okunan":1,"atlanan":1,"hatalar":[{"satirNo":2,"message":"'102 1 1 04' hesap planında yok…"}]}`; multipart bağlama ve satır bazlı doğrulama çalışıyor |
| `POST .../banka-hesaplari/ice-aktar` (`.txt`) | `415` — "Sadece .xlsx veya .xls dosyaları desteklenir." |
| `GET .../banka-hesaplari/anahtar-onerisi` | `200` — hesap adından öneri dönüyor |

İstemci tarafında tarayıcıya servis edilen `_framework/WebApp.wasm` indirilip içinde
`Toplu İçe Aktar`, `banka-hesaplari/ice-aktar`, `banka-hesaplari/sablon` ve
`Eşleştirme anahtarları` metinlerinin bulunduğu doğrulandı.

---

## Ne eksik kaldı

- **Ekranlar tarayıcıda denenmedi.** `WebApp.UnitTests` saf xunit (bUnit yok), Razor
  tarafının otomatik testi yok. Elle bakılacaklar: `/banka-otomasyon/aktar` banka sekmeleri ve
  rozetler, boş karta dosya sürükleme, Tanımlar'daki bölümler, onay ekranında `Alt+1..9`
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
- **Banka hesabı içe aktarımı tarayıcıda denenmedi** — ama **uç noktalar çalışırken
  doğrulandı** (aşağıya bakın). Kalan elle kontrol: "Toplu İçe Aktar" düğmesi, dosya
  seçici ve rapor listesinin ekranda görünmesi.
- **Eşleştirme anahtarları hiçbir hesapta dolu değil.** Migration alanı ekliyor, doldurmuyor.
  102 grubu satırların doğru hesaba gitmesi için Tanımlar > Banka hesapları'ndan her
  hesabın anahtarı girilmeli (form "Hesap adından öner" düğmesiyle taslak üretiyor) veya
  toplu içe aktarım dosyasına `Eşleştirme Anahtarları` kolonu eklenmeli. **Anahtar
  girilmeden**, aynı bankada birden fazla hesabı olan satırlar otomatik çözülmek yerine
  onaya düşer — eskisi gibi yanlış hesaba gitmez ama onay kuyruğu büyür.
- **Anahtar önerisi gerçek hesap adlarıyla denenmedi.** Kural tabanlı: banka adı, genel
  kelimeler ve rakamlar atılıyor. `"Teb, Marifetli Tl - Maslak, 129-154401190"` için
  `"Marifetli, Maslak"` üretiyor (şube adı da geliyor); kullanıcı fazlasını siler.
- **Ayrıştırıcısız hesabın ekranda gizlenmesi tarayıcıda denenmedi.** Sunucu tarafı testli
  (yükleme reddi), kart/sekme filtresi Razor tarafında ve otomatik testi yok.
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

---

# Tur 2 — gerçek veri düzeltmeleri (birleşik)

Yedi madde de uygulandı. Ayrıntılı gerekçeler `KARARLAR.md` §42–56'da.

## Ne değişti

| # | Madde | Nerede |
|---|---|---|
| 1 | Benzersiz önek katmanı (ters yönde eşleştirme) | `Services/CariOnekIndeksi.cs` (yeni), `HesapEslestirici.OnekleCoz` |
| 2 | Yön kuralı sahte belirsizliği çözüyor | `CariOnekIndeksi.YonleCoz` |
| 3 | Belirsizlik çözümü öğreniliyor (aday kümesi özetiyle) | `AnahtarTipi.Belirsizlik`, `HesapEslesmeService.BelirsizlikYazAsync` |
| 4 | Hesap sahibi unvanı çoklu + kapsama + öneri | `Services/HesapSahibiKimligi.cs` (yeni), `BankaHesabi.HesapSahibiTakmaAdlari` |
| 5 | Açıklamanın sonundaki satıcı adı deseni | `BankaEkstreSeed.TahsilatDeseni`, `UnvanCikarici.UnvanAlanindanMi` |
| 6 | 0.40 altındaki öneri hiç gösterilmiyor | `HesapEslestirici.EnAzOneriEsigi` |
| 7 | Vergi kodu tablosu + plaka anahtarı | `Services/VergiPlakaCozucu.cs` (yeni), `Domain/VergiKoduEslemesi.cs` (yeni) |

**Yeni katman sırası:** geçmiş onay → banka kayıt defteri → **vergi/plaka** → sabit kural
→ **benzersiz önek** → desen tabanlı unvan benzerliği.

## Veritabanı

Migration: `20260822082201_AddBankaEkstreTur2` — **üretildi ve uygulandı**.

- `EkstreVergiKodlari` (yeni tablo, global): `VergiKodu`, `AnahtarKelime`, `HesapKodu`,
  `HesapAdi`, `Sira`, `Aktif`.
- `EkstreBankaHesaplari.HesapSahibiTakmaAdlari` (nvarchar(1000), null).
- `EkstreHesapEslesmeleri.AdayKumesiOzeti` (nvarchar(64), null).
- `EkstreSatirlari.BelirsizlikAnahtari` (nvarchar(200), null),
  `EkstreSatirlari.AdayKumesiOzeti` (nvarchar(64), null).

Seed üç vergi kodu satırı ekliyor (`9085`, `0040`, `0033`) ve `TahsilatDeseni`'ni sıra 5'e
yazıyor. Seed satır bazında idempotent; mevcut kayıtların üzerine yazmıyor.

## Arayüz

- **Tanımlar** ekranına dördüncü bölüm: **Vergi kodları** (CRUD).
- **Banka hesapları** formuna "Hesap sahibinin diğer yazımları" (çok satırlı) +
  "Yüklenmiş ekstrelerde ara" düğmesi; bulunan yazımlar tek tıkla ekleniyor.
- **Öğrenilen eşleşmeler** listesine "Tür" sütunu; belirsizlik kayıtları sarı rozetle
  ayrılıyor ve ipucu aday kümesi kuralını anlatıyor.
- **Onay ekranı**: çoklu adaylı satırda "Seçiminiz öğrenilecek — `<n-gram>` belirsizliği
  bir daha sorulmayacak" notu. Eşik altı satırlarda kod kutusu boş geliyor (sunucu kod
  önermiyor).

## Test

`Services/CatalogService/CatalogService.UnitTests/BankaEkstre/Tur2GercekVeriTests.cs` —
**gerçek Vakıfbank ekstresinin (287 satır) kendi açıklama metinleriyle**, 15 kabul
kriterinin tamamı. Satırlar sıra numarasıyla değil açıklamada geçen ifadeyle bulunuyor.

Hesap planı fikstürü: `GercekHesapPlani.cs` — 81 kayıt; gerçek planın ölçülen özellikleri
korundu (50 karakterde kesik adlar, banka isimli cariler, firmanın kendi adını taşıyan
gider hesapları, 159+329 kopyaları, Park Plaza / Pardus / Cms Jant aileleri, plakalı araç
hesapları).

**Durum:** çözüm derleniyor, 395 testin tamamı geçiyor (CatalogService 363, WebApp 31,
Sovos 1).

## Gerçek dosyayla ölçüm — ve neden Tur 1 sayılarıyla karşılaştırılamıyor

Tur 1 sayıları (**128 otomatik / 123 onay bekliyor / 36 çözülemedi**) kullanıcının
**gerçek 6.128 kayıtlık ORKA hesap planıyla** ölçülmüştü. O plan depoda yok; testler
81 kayıtlık bir fikstürle çalışıyor. Fikstür planıyla aynı dosya:

```
47 otomatik / 110 onay bekliyor / 130 çözülemedi
katman dağılımı: benzersiz önek 104, banka kayıt defteri 22, sabit kural 26, vergi/plaka 5
```

Bu sayılar **Tur 1 ile karşılaştırılabilir değil**: 130 "çözülemedi"nin büyük kısmı,
karşı tarafın fikstür planında hiç bulunmamasından geliyor (gerçek planda o cariler var).
Karşılaştırma ancak gerçek hesap planı yüklenip ekstre yeniden işlenerek yapılabilir —
**bu adım ofiste, gerçek planla yapılmalı.**

Fikstürle ölçülen ve anlamlı olan şey, **otomatik çözülen satırlarda yanlış kayıt
olmaması**: 47 otomatik satırın tamamı elle doğrulandı. Tur 2 boyunca yakalanıp
düzeltilen yanlış otomatik eşleşmeler:

| Satır | Yanlış eşleşme | Sebep | Düzeltme |
|---|---|---|---|
| 13 Pardus satırı | `PARDUS PORTFÖY MARMARA …` → `120 F07 Para Piyasası` | aile ayrımı kırpılmış 8 aday üzerinden karar veriyordu | KARARLAR §50 |
| `SAĞLAMOĞLU YETKİLİ MÜESSESE …` | → `120 H30 Hakan Yetkili Müessese` | alt metin yedeği adın ortasına oturuyordu | KARARLAR §51 |
| `CMS JANT VE MAKİNA …` | → `120 C11 Cms Jant Makina` (onaya düşmeliydi) | `Cms Jant`'ın ayırt edici kelimesi yok, ayrım taraflıydı | KARARLAR §50 |
| `ZİRAAT BANKASI` geçen 16 satır | → `320 1 10011 Ziraat Bank` | banka isimli cariler indeksteydi | KARARLAR §42 |
| `Superonline` / `Turknet` | → `329 A33 Adobe`, `329 N21 Novatek` (0.20 skor) | eşik yoktu | KARARLAR §48 |

## Kalan eksikler (Tur 2 sonrası)

- **Gerçek hesap planıyla ölçüm yapılmadı.** Yukarıdaki sayılar fikstür planına ait.
  Ofiste: gerçek planı Tanımlar > Hesap planı'ndan içe aktarın, aynı ekstreyi yükleyin,
  otomatik / onay bekleyen / çözülemeyen sayılarını Tur 1'in 128/123/36'sıyla
  karşılaştırın.
- **Hesap sahibi takma adları boş geliyor.** Migration alanı ekliyor, doldurmuyor.
  Ölçülen dosyada `ADAY BAĞIMSIZ DENETİM VE SMMM A.Ş.` yazımı **takma ad olarak
  eklenmeden elenmiyor**. Tanımlar > Banka hesapları > (hesabı düzenle) >
  "Yüklenmiş ekstrelerde ara" düğmesi bunu bulup öneriyor.
- **Vergi eşleme tablosu üç satırla geliyor.** Ölçülen dosyadaki `0010/KURUMLAR V.`
  eşlemesi yok, o satır onaya düşüyor. Kullanıcı Tanımlar > Vergi kodları'ndan ekler.
- **Plaka hesapları firmaya özel.** Fikstürde `34 Mrp 081` var; gerçek planda hangi
  plakaların hesabı olduğu bilinmiyor. Plaka katmanı yalnız planda karşılığı olan
  plakalarda devreye giriyor, yoksa satır eskisi gibi çözülüyor.
- ~~Bilinen iki yanlış otomatik eşleşme (MARBAŞ, DEMET DÖVİZ)~~ — **düzeltildi**,
  aşağıdaki "Banka katmanı tetikleyicisi" bölümüne bakın.
- **Tarayıcıda denenmedi:** vergi kodları bölümü, takma ad öneri düğmesi, onay ekranındaki
  belirsizlik notu. Sunucu tarafı testli; Razor tarafının otomatik testi yok.

---

## Banka katmanı tetikleyicisi (Tur 2 eki)

Banka kayıt defteri katmanı fazla erken devreye giriyordu: tetikleyici pratikte
"açıklamada banka adı geçiyor"a iniyordu, ama müşteri ödemelerinde de **gönderenin
bankası** yazıyor. Ölçüm: 87 cari satırının **59'unda** açıklamada banka adı geçiyor.

Katman artık yalnız şu iki koşuldan **en az biri** sağlanınca çalışıyor (KARARLAR §57):

- **(a)** Metinde bankalar arası ifadesi var: `hesaplar arası`, `hesaplararası`,
  `virman`, `süpürme` — ham açıklamada veya işlem tipinde.
- **(b)** Çıkarılan karşı taraf hesap sahibinin kendisi (ya hiç unvan çıkmamış, ya da
  kalan yakalama bir banka adı).

İkisi de tutmazsa katman atlanıyor; satır cari katmanlarına düşüyor, orada da çözülemezse
onaya gidiyor.

**Ölçüm (fikstür planı, gerçek dosya):**

| | Önce | Sonra |
|---|---|---|
| Otomatik | 47 | **49** |
| Onay bekliyor | 110 | 110 |
| Çözülemedi | 130 | **128** |
| Banka kayıt defterinden çözülen | 22 | **20** |

Kaybedilen 2 satır ikisi de yanlıştı; kazanılan 2 satır doğru cariye gitti:

| Satır | Önce | Sonra |
|---|---|---|
| `MARBAŞ MENKUL DEĞERLER ÖDEME (… Akbank T.A.Ş. MARBAŞ … hesabından …)` | `102 1 4 01 Akbank` | `120 M40 Marbaş Menkul Değerler` |
| `PKF BAĞIMSIZ DENETİM FİRMASI ÖDEMESİ (… Türkiye İş Bankası A.Ş. DEMET DÖVİZ … )` | `102 1 5 01 İş Bankası` | `120 D50 Demet Döviz Yetkili Müessese` |

Bankalar arası satırların hiçbiri kaybedilmedi: 16'sı (a), 4'ü (b) ile çözülüyor.
`HESAPLAR ARASI E.F.T. VAKIFBANK/DENİZBANK` → `102 1 3 02` ve
`HESAPLAR ARASI EFT VAKIFBANK/TÜRKİYE İŞ BANKASI` → `102 1 5 01` eskisi gibi.
İfadesiz self-transferler (`İŞ BANKASI  (…)`, `DENİZBANK HESABINA (…)`) (b) ile
yakalanıyor.

**Testler** (`Tur2GercekVeriTests`, hepsi gerçek dosyanın kendi metinleriyle):
`Aciklamada_banka_adi_gecmesi_tek_basina_banka_katmanini_actirmaz`,
`Gonderenin_bankasi_yazan_musteri_odemesi_cari_katmanina_ulasir` (MARBAŞ + DEMET DÖVİZ),
`Ifadesiz_kendi_hesaplari_arasi_transfer_b_kosuluyla_yakalanir`,
`Banka_katmani_yalniz_iki_kosuldan_biri_tutunca_calisir` (tüm satırlar üzerinde tarama).

Fikstür planına iki cari eklendi: `120 M40 Marbaş Menkul Değerler Anonim Şti`,
`120 D50 Demet Döviz Yetkili Müessese Anonim Şirketi`.

**Durum:** çözüm derleniyor, 402 testin tamamı geçiyor (CatalogService 370, WebApp 31,
Sovos 1).

---

# Tur 3 — kişi eşleştirmesi, kişi yönlendirme tablosu, analiz dökümü

Gerçek Vakıfbank ekstresinin (287 satır) gerçek hesap planıyla çalıştırılmasından çıkan üç
sorun. Ofis ölçümü: **139 otomatik / 136 onay bekliyor / 12 çözülemedi**; gözle kontrol
edilen 48 çözülmüş satırın 48'i de doğru. Bu turun konusu doğru/yanlış **sayısı** değil,
onay kuyruğunda **hazır bekleyen yanlış öneri**.

## 1. Kural grubu içinde yanlış kişi seçiliyordu

Sabit kural (`İş Avansı`, `Masraf Ödemesi`, `Maaş Avansı` → `195`/`196`) ana grubu doğru
belirliyordu, ama grup içi alt hesap araması difflib benzerliğiyle yapılıyor ve yanlış
kişiyi öneriyordu. Arama artık **benzersiz önek** yöntemiyle (KARARLAR §58):

- Çıkarılan isim, hesap adının **token sınırında biten öneki** olmalı.
- Ad + soyad verilmiş ve grup içinde **tek** eşleşme varsa otomatik.
- Birden fazla eşleşmede satır onaya düşer, **hepsi** aday listelenir.
- Hiç eşleşme yoksa alt hesap boş kalır, yalnız ana grup önerilir. **Yakın isimli başka
  kişi asla önerilmez.**
- Tek kelimelik isim (`İlyas`) hiçbir zaman otomatik çözülmez.

Ayrıca kural ana grubu artık tek başına kilitlemiyor (KARARLAR §59): aynı ismin **başka
gruplardaki birebir eşleşmeleri** de aday oluyor ve satır onaya düşüyor.

## 2. Kişi yönlendirme tablosu (yeni)

`EkstreKisiYonlendirmeleri` — firma bazlı: `IsimCekirdegi`, `Isim`, `Yon`
(Giren/Çıkan/Farketmez), `HesapKodu`, `HesapAdi`, `Aciklama`, `Aktif`.

Katman **tüm katmanlardan önce** çalışıyor — sabit kuraldan da önce (KARARLAR §60): kişi
tabloda tanımlıysa "masraf ödemesi" ifadesi geçse bile satır oraya gidiyor, güven 1.0 ile
otomatik çözülüyor. Etiket: `kişi` (`KaynakKatman.KisiYonlendirme = 10`).

Yön ayırt edici: aynı kişinin giden ödemesi `331 02`, gelen tahsilatı başka bir hesap
olabilir. Eşleşme normalize isim çekirdeği üzerinden **tam eşleşme**; benzerlik hiç
kullanılmıyor.

## 3. Analiz dökümü (yeni)

`POST …/ekstre/{id}/analiz-dokumu` + "Analiz için dışa aktar" düğmesi. Durumu ne olursa
olsun tüm satırları veriyor:
`SiraNo | Tarih | Yon | Tutar | HamAciklama | UretilenAciklama | OnerilenHesapKodu |
OnerilenHesapAdi | GuvenSkoru | KaynakKatman | Durum | AdaySayisi`

Dosya **ORKA'ya yüklenmez**, yalnız inceleme içindir. "Kod listesi" ve "Düzeltilmiş
ekstre" eksik satır varken 400 dönmeye devam ediyor (KARARLAR §61).

## Veritabanı

- `EkstreKisiYonlendirmeleri` (yeni tablo, firma bazlı): `Id`, `TenantNo`,
  `IsimCekirdegi`, `Isim`, `Yon`, `HesapKodu`, `HesapAdi`, `Aciklama`, `Aktif`.
  Unique index: `(TenantNo, IsimCekirdegi, Yon)`.
- Migration: `20260823103138_AddEkstreKisiYonlendirme` — **üretildi ve uygulandı**,
  `has-pending-model-changes` temiz.
- Seed yok: tablo kullanıcının kendi tanımlarıyla dolar.

## Arayüz

- **Tanımlar** ekranına beşinci bölüm: **Kişi yönlendirmeleri** (CRUD). Hesap kodu
  yazılırken hesap planından öneri açılıyor; planda olmayan kod kaydedilmiyor.
- **Onay ekranı**: kişi adı okunabilen satırlarda "Bu kişiyi (<ad>) hep bu hesaba
  yönlendir" kutusu. İşaretlenip onaylanırsa yönlendirme kaydı otomatik oluşuyor; yön o
  satırın yönünden geliyor.
- **Onay ekranı**: "Analiz için dışa aktar" düğmesi — hiçbir zaman kilitlenmiyor, yanındaki
  açıklama dosyanın ORKA'ya yüklenmediğini söylüyor.

## Test

`Services/CatalogService/CatalogService.UnitTests/BankaEkstre/KisiEslestirmeTests.cs` —
20 test, çoğu **gerçek dosyanın kendi açıklama metinleriyle**. Hesap planı fikstürüne
ölçülen gerçek kişiler eklendi: `195 01 A20 Abdülkadir Yılmaz`, `195 01 D06 Dilara Kaya`,
`195 01 H13 İlyas Ömeroğlu`, `195 01 I02 İlyas Yücel`, `195 01 M05 Mesut Aktaş`,
`195 01 E03 Eda Budak`, `331 02 Abdulkadir Sayıcı`.

**Durum:** çözüm derleniyor, 422 testin tamamı geçiyor (CatalogService 390, WebApp 31,
Sovos 1).

## Gerçek dosyayla ölçüm

Fikstür planıyla, aynı gerçek dosya, önce/sonra:

```
Otomatik      58 → 58
Onay bekliyor 102 → 102
Çözülemedi    127 → 127
```

**Sayılar bilerek değişmedi.** Bu turun düzelttiği satırlar zaten onay kuyruğundaydı;
değişen, kutuda hazır bekleyen **öneri**. Satır satır fark tam olarak dört satır — ölçümde
bildirilen dört satırın kendisi, başka hiçbir satır etkilenmedi:

| Satır | Önce | Sonra |
|---|---|---|
| `… Akbank T.A.Ş. İlyas hesabına giden FAST` (2 satır) | `195 01 I02 İlyas Yücel` önerili | Öneri yok; **iki aday** (`195 01 H13`, `195 01 I02`) listeleniyor |
| `dilara sager masraf ödemesi` | `195 01 D06 Dilara Kaya` önerili | Alt hesap boş; yalnız `195` |
| `ABDULKADİR SAYICI Masraf Ödemesi Arta Tekmer` | `195 01 A20 Abdülkadir Yılmaz` önerili | Öneri yok; **aday** `331 02 Abdulkadir Sayıcı` |

Kişi yönlendirmesi (`ABDULKADİR SAYICI / Çıkan / 331 02`) tanımlandığında son satır
onay kuyruğundan çıkıp otomatik `331 02`'ye gidiyor (`KisiEslestirmeTests.Kisi06`).

Regresyon: ad + soyadı tam geçen satırlar eskisi gibi otomatik çözülüyor —
`Mesut Aktaş` (3 satır) → `195 01 M05`, `İlyas Ömeroğlu` (2 satır) → `195 01 H13`,
`Eda Budak` → `195 01 E03`, `Dilara Kaya` (2 satır) → `195 01 D06`. Fikstüre eklenen
`331 02` sayesinde `ABDULKADİR SAYICI  (… HESABINA YAPILAN … EFT)` satırı da benzersiz
önek katmanından otomatik çözülüyor.

**Ofis ölçümüyle karşılaştırma (139 / 136 / 12) yapılmadı:** gerçek 6.128 kayıtlık ORKA
planı depoda yok, testler 88 kayıtlık fikstürle çalışıyor. Yukarıdaki dört satır farkı
gerçek planda da aynı davranışı verir (mekanizma plandan bağımsız), ama **toplam sayılar
ofiste ölçülmeli**: gerçek planı içe aktarın, aynı ekstreyi yükleyin, otomatik / onay
bekleyen / çözülemeyen sayılarını 139 / 136 / 12 ile karşılaştırın. Beklenti: otomatik
sayısı **aynı kalır ya da artar** (kişi yönlendirmeleri tanımlandıkça artar); asıl kazanç
onay kuyruğunda yanlış önerinin kalmaması.

## Kalan eksikler (Tur 3 sonrası)

- **Gerçek planla ölçüm yapılmadı** (yukarıya bakın).
- **Kişi yönlendirme tablosu boş geliyor.** Migration tabloyu açıyor, doldurmuyor. Ortak ve
  yöneticiler Tanımlar > Kişi yönlendirmeleri'nden ya da onay ekranındaki kısayoldan
  eklenir.
- **Tarayıcıda denenmedi:** kişi yönlendirmeleri bölümü, onay ekranındaki yönlendirme
  kutusu, analiz düğmesi. Sunucu tarafı testli; Razor tarafının otomatik testi yok.
- **Yönlendirme yalnız kişi adı okunabilen satırlarda kısayoldan oluşturulabiliyor.** Adı
  hiç çıkarılamayan satırda uyarı dönüyor; kayıt Tanımlar'dan elle eklenmeli.

---

# Banka Otomasyon — firma seçim ekranı + banka hesapları CRUD

Modülün adı `Banka İşleme` → **`Banka Otomasyon`**, günlük ekranın adı `İşleme` → **`Aktar`**.
Modüle girince önce **firma listesi** geliyor; seçilen firma tenant bağlamını gerçekten
değiştiriyor. Banka hesaplarının tam CRUD'u kapsülden **Tanımlar**'a döndü.

**Eşleştirme mantığına dokunulmadı** — katman sırası, eşikler, benzersiz önek algoritması,
desenler ve kurallar aynen duruyor. Mevcut testlerin tamamı değişmeden geçiyor.

## Gezinme

```
Banka Otomasyon              /banka-otomasyon            ← firma listesi (modülün girişi)
  └ GİRİŞ → firma içi
      Aktar                  /banka-otomasyon/aktar      ← günlük iş + banka kapsülü
      Tanımlar               /banka-otomasyon/tanimlar   ← firmanın kurulumu
      (onay ekranı)          /banka-otomasyon/onay/{id}
```

Eski adresler `EskiRotaYonlendirme.razor` ile yönlendiriliyor (`replace: true`):

| Eski | Yeni |
|---|---|
| `/banka-isleme` | `/banka-otomasyon` (firma listesi) |
| `/banka-isleme/firma-tanimlari`, `/banka-isleme/tanimlar` | `/banka-otomasyon/tanimlar` |
| `/banka-isleme/onay/{id}` | `/banka-otomasyon/onay/{id}` |

## Firma seçim ekranı

Kalıp `Raporlar` (`/firmakontrol`) ile birebir aynı: tablo + sağda `GİRİŞ`, satıra tıklamak
da giriyor.

| Kolon | İçerik |
|---|---|
| Firma | Unvan; hesap planı yoksa altında "kurulum gerekli" |
| VKN | Vergi numarası |
| Hesap planı | Kayıt sayısı, yoksa "yüklenmedi" |
| Banka hesabı | Tanımlı aktif hesap sayısı |
| Onay bekleyen | Tüm bankalar + tüm dönemler toplamı |
| — | `GİRİŞ` |

Hesap planı yüklenmemiş firmaya da girilebiliyor; kurulum Tanımlar'dan yapılıyor.

## Tenant bağlamı

`GİRİŞ` → `IAppSessionManager.SelectFirmAsync` → yeni access token, `tn` claim'i o firmaya
geçiyor. Sunucuda tenant önce JWT claim'inden okunduğu için (`HttpCurrentTenant`) istemciden
tenant'ı değiştirmenin tek gerçek yolu bu.

Firma içi her ekran açılışta `BaglamiHazirlaAsync()` çağırıp **ancak sonra** veri çekiyor;
ekranın ilk isteği bile doğru firmaya gidiyor. Seçim yoksa firma listesine yönlendiriliyor.

Üstteki genel `FİRMA DEĞİŞTİR` ile çelişki çıkarsa **sayfadaki seçim kazanıyor**: modül
kendi firmasını geri uyguluyor ve bildirimle uyarıyor. Bu üstünlük yalnız modül ekranı
açıkken geçerli. Seçim oturum boyunca hatırlanıyor (scoped servis + `sessionStorage`),
sekme değişiminde tekrar sorulmuyor.

## Sunucu

| Uç nokta | İş |
|---|---|
| `GET .../banka-ekstre/firmalar/ozet?tenantlar=201&tenantlar=106` | Firma seçim ekranının sayaçları; istenen her tenant için bir satır |

`FirmaOzetService` global query filter'ı `IgnoreQueryFilters()` ile atlıyor — ekran firmaya
girilmeden açıldığı için başka çare yok. Baypas **tek dosyada** ve yalnız adet üretiminde;
kayıt içeriği hiç dönmüyor, modülün geri kalanı izolasyonunu koruyor.

**Migration yok** — yeni varlık/alan eklenmedi, `has-pending-model-changes` temiz.

## Banka hesapları CRUD'u

Tam CRUD (`Yeni hesap`, `Toplu İçe Aktar`, `Örnek şablon indir`, düzenle, sil) Tanımlar'da,
hesap planının hemen altında. Gerekçe: banka hesabı tanımı bankaya değil **firmaya** ait bir
kayıt, ayrıca yeni banka eklenirken o bankanın sekmesi henüz yok.

Kapsüldeki "Bu bankanın kuralları → Ayrıştırıcı ayarları" kaldı ama yalnız ayrıştırıcı ve
katman bayraklarını düzenliyor; her satırda **Tam düzenleme →** bağlantısı var
(`/banka-otomasyon/tanimlar?hesap={id}`). Aktar'daki "Banka ekle" düğmeleri
`?yeniHesap=1` ile forma gidiyor.

## Banka adı tutarsızlığı

`BankaAdi` alanı artık otomatik tamamlamalı (`RadzenAutoComplete`, verisi mevcut hesapların
banka adları). Listede olmayan yazım uyarı üretiyor: *"… mevcut hiçbir hesapla eşleşmiyor,
yeni bir banka sekmesi açılacak."*

Karşılaştırma **sekme şeridiyle birebir aynı** (`OrdinalIgnoreCase` + kırpma), yani uyarı
tam olarak "yeni bir sekme açılacak mı?" sorusunu yanıtlıyor. `ZIRAAT` ≡ `Ziraat`, ama
`İŞ BANKASI` ≠ `İş Bankası` — ordinal karşılaştırma `ı` ile `I`'yı eşlemiyor ve sekme
şeridi de tam bu yüzden ikiye bölünüyor. Eşleştirme mantığı değiştirilmedi; kullanıcı
tutarsızlığı görüp düzeltiyor.

## Yanlış firmaya veri girmeye karşı

- Firma adı Aktar, Tanımlar ve onay ekranının üstünde **sürekli** görünüyor, yanında firma
  listesine dönüş bağlantısı var.
- Ekstre yükleme, hesap planı içe aktarımı, banka hesabı toplu içe aktarımı ve hesap silme
  **firma adıyla** onaylanıyor.
- Sonuç bildirimleri firma adıyla başlıyor: `PKF Aday · 287 satır okundu · …`.

## Değişen / eklenen dosyalar

| Dosya | İş |
|---|---|
| `Features/BankaEkstre/Services/FirmaOzetService.cs` | **Yeni** — firma sayaçları, tenant filtresi baypası |
| `Features/BankaEkstre/Controllers/FirmaOzetController.cs` | **Yeni** — `.../firmalar/ozet` |
| `Application/Services/BankaOtomasyonOturumu.cs` | **Yeni** — modülün firma bağlamı + çakışma çözümü + oturum deposu |
| `Application/Services/Interfaces/IBankaOtomasyonOturumu.cs` | **Yeni** — arayüzler |
| `Pages/BankaEkstre/FirmaSecimPage.razor` | **Yeni** — `/banka-otomasyon` firma listesi |
| `Pages/BankaEkstre/EskiRotaYonlendirme.razor` | **Yeni** — eski adreslerin yönlendirmesi |
| `Pages/BankaEkstre/Bolumler/FirmaBasligi.razor` | **Yeni** — firma adı + Aktar/Tanımlar sekmeleri |
| `Pages/BankaEkstre/AktarPage.razor` | `IslemePage.razor`'dan yeniden adlandırıldı; bağlam koruması, yükleme onayı, hesap CRUD'u çıkarıldı |
| `Pages/BankaEkstre/TanimlarPage.razor` | `FirmaTanimlariPage.razor`'dan yeniden adlandırıldı; banka hesapları bölümü eklendi, sorgu parametreleri |
| `Pages/BankaEkstre/Bolumler/BankaHesaplariBolumu.razor` | Otomatik tamamlama + yeni yazım uyarısı, firma adlı onaylar, `BankaFiltresi` kaldırıldı |
| `Pages/BankaEkstre/Bolumler/BankaKurallariBolumu.razor` | Hesap CRUD'u çıkarıldı, satır başına "Tam düzenleme" bağlantısı |
| `Pages/BankaEkstre/Bolumler/HesapPlaniBolumu.razor` | Firma adlı içe aktarım onayı |
| `Layout/MainLayout.razor` | Menü: `Banka Otomasyon` → `Aktar` / `Tanımlar` |

## Testler

| Test | Ne doğruluyor |
|---|---|
| `CatalogService.UnitTests/BankaEkstre/FirmaTenantIzolasyonuTests` | **Aday seçiliyken yapılan hesap planı içe aktarımı Aday'ın kayıtlarına yazılıyor**, SMMM'ninkiler bozulmuyor (iki firma aynı veritabanını paylaşıyor); firma özeti her firmanın kendi sayaçlarını dönüyor |
| `WebApp.UnitTests/BankaEkstre/BankaOtomasyonOturumuTests` | Giriş tenant'ı çeviriyor, sekme değişiminde tekrar sorulmuyor, sayfa yenilemesinde seçim geri geliyor, çakışmada sayfadaki seçim kazanıyor + uyarı çıkıyor, modül kapalıyken karışılmıyor |
| `WebApp.UnitTests/BankaEkstre/BankaAdiDenetimiTests` | Yeni yazım uyarısı; Türkçe `ı`/`I` farkının ayrı sekme açtığı ve uyarının sekme şeridiyle aynı şeyi söylediği |

Sayılar: `CatalogService.UnitTests` 416 → **418**, `WebApp.UnitTests` 31 → **46**.
Tamamı geçiyor, mevcut testlerin hiçbiri değişmedi.

## Ne eksik kaldı

- **Ekranlar tarayıcıda denenmedi** (bUnit yok). Elle bakılacaklar: firma listesindeki
  sayaçlar, `GİRİŞ` sonrası doğru firmanın verisinin geldiği, sekme değişiminde firmanın
  korunduğu, üstteki `FİRMA DEĞİŞTİR` ile çakışma uyarısı, otomatik tamamlama açılır
  listesi, `?hesap={id}` bağlantısının doğru formu açtığı.
- **Onay diyaloğunda satır sayısı yok.** Dosya sunucuda ayrıştırılmadan bilinemiyor; onayda
  firma adı + dosya adı, sonuç bildiriminde firma adı + okunan satır sayısı var.
- **Firma özeti uç noktası, istenen tenant listesini doğrulayamıyor.** Token'da tek `tn`
  claim'i var. Yalnız adet döndüğü için etki sınırlı; gerçek çözüm, kullanıcının
  firmalarını da claim'e koymak (IdentityService değişikliği, kapsam dışı).
