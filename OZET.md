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

# Öğrenilen eşleşmeler — toplu içe aktarma

Öğrenme tablosu yalnız onay ekranından tek tek doluyordu. ORKA yevmiyesinden çıkarılmış
doğrulanmış eşleşmeler (PKF Aday: 402 satır, 7 aylık geçmiş) artık xlsx ile toplu
aktarılıyor. Bunlar kullanıcının geçmişte kendi verdiği kararlar — onay ekranından tek tek
geçmekle aynı şey, sadece toplu hâli.

**Eşleştirme mantığına dokunulmadı**: katman sırası, eşikler ve algoritma aynı. Mevcut
testlerin tamamı aynen geçiyor.

## Dosya formatı

Tek sayfa, kolonlar başlık **adıyla** bulunur (sıra önemsiz, Türkçe karakter ve
büyük/küçük harf toleranslı — banka hesabı içe aktarımıyla aynı kalıp).

| Kolon | Zorunlu | Not |
|---|---|---|
| `Anahtar Çekirdek` | evet | Unvan çekirdeği; en az 8 karakter, içe aktarılırken yeniden normalize edilir |
| `Hesap Kodu` | evet | Boşluklu ORKA formatı (`120 N15`), aynen saklanır; hesap planında olmalı |
| `Hesap Adı` | hayır | Yalnız bilgi; kaydedilen ad hesap planından okunur |
| `Yön` | hayır | `Giren` / `Çıkan` / `Farketmez`; boşsa Farketmez |
| `Kullanım Sayısı` | hayır | Boşsa 1 |
| `Son Kullanım` | hayır | `gg.aa.yyyy`; boşsa içe aktarım tarihi |

## Davranış

- Anahtar tipi **unvan çekirdeği** (`Belirsizlik` değil — o kayıt aday kümesi özetiyle
  anlamlı, geçmişten türetilen satırda o küme yok).
- **Kullanıcının kararı korunur**: aynı anahtar için kayıt varsa satır **atlanır**,
  üzerine yazılmaz.
- **`Farketmez` iki kayıt yazar.** `HesapEslesmesi.Yon` yalnız Giren/Çıkan tutuyor;
  enum'a değer eklemek eşleştirmeye dokunmak olurdu. Bir yön zaten doluysa o korunur.
- **Doğrulama satır bazlı**, hatalı satır dosyayı düşürmez: plan dışı kod, 8 karakterden
  kısa anahtar, hesap sahibinin kendi adını kapsayan anahtar, tanınmayan yön, dosyada
  yönleri kesişerek iki kez geçen anahtar.
- Kapsam **firma bazlı** (`?firmaId=`); aynı anahtar farklı firmada ayrı kayıt.

## Uç noktalar

```
POST /api/catalog/banka-ekstre/eslesmeler/ice-aktar   (multipart, xlsx, 20 MB)
GET  /api/catalog/banka-ekstre/eslesmeler/sablon
```

Görev metnindeki `banka-otomasyon/ogrenilen-eslesmeler` ekranın adresi; API öneki modülün
mevcut şemasını (`banka-ekstre`) izliyor. Gateway değişmedi.

## Arayüz

Tanımlar → Öğrenilen eşleşmeler bölümüne "**Toplu İçe Aktar**" düğmesi, dosya seçici ve
"**Örnek şablon indir**" bağlantısı. Onay kutusunda firma adı var. İçe aktarımdan sonra
liste yenileniyor; sonuç raporu (okunan / eklenen / atlanan (mevcut) / hatalı + satır
numarasıyla sebepler) ekranda kalıyor.

## Veritabanı

**Migration yok** — yeni kolon/tablo eklenmedi, mevcut `EkstreHesapEslesmeleri` şeması
kullanılıyor.

## Değişen / eklenen dosyalar

| Dosya | İş |
|---|---|
| `Features/BankaEkstre/Services/OgrenilenEslesmeIceAktarimService.cs` | **Yeni** — okuma, doğrulama, yazma, şablon |
| `Features/BankaEkstre/Controllers/HesapEslesmeleriController.cs` | `ice-aktar` ve `sablon` uçları |
| `Features/BankaEkstre/Dtos/BankaEkstreDtos.cs` | **Yeni** `OgrenilenEslesmeIceAktarimSonucDto` |
| `Program.cs` | DI kaydı |
| `Shared/Dto/BankaEkstre/BankaEkstreDtos.cs` | İstemci tarafı sonuç DTO'su |
| `Application/Services/BankaEkstreApi.cs` + arayüzü | `EslesmeleriIceAktarAsync`, `EslesmeSablonuAsync` |
| `Pages/BankaEkstre/Bolumler/OgrenilenEslesmelerBolumu.razor` | İçe aktarım kutusu, şablon indirme, sonuç raporu |

## Testler

`CatalogService.UnitTests/BankaEkstre/OgrenilenEslesmeIceAktarimServiceTests` — 19 test:
geçerli dosya 3 satır → 3 eklendi; ikinci kez aynı dosya → 0 eklendi / 3 atlandı; plan
dışı kod atlanıp raporlanıyor; hesap sahibi çekirdeğini kapsayan anahtar reddediliyor;
kolon sırası değiştirilmiş dosya okunuyor; farklı firmada aynı anahtar ayrı kayıt;
içe aktarılan eşleşme `KaynakKatman.GecmisOnay` ile çözülüyor; ayrıca ham unvanın yeniden
normalize edilmesi, boş yönün iki kayıt yazması, kısa anahtar / tanınmayan yön / dosya içi
tekrar reddi, başlıksız dosyanın hiç işlenmemesi ve şablonun kendi içe aktarımından geçmesi.

Sayılar: `CatalogService.UnitTests` 421 → **440**, `WebApp.UnitTests` **46** (değişmedi).
Tamamı geçiyor, mevcut testlerin hiçbiri değişmedi.

## Ne eksik kaldı

- **Ekran tarayıcıda denenmedi** (bUnit yok). Elle bakılacaklar: dosya seçici, şablon
  indirme, uzun hata listesinin görünümü, içe aktarım sonrası listenin yenilenmesi.
- **Onay diyaloğunda satır sayısı yok** — dosya sunucuda ayrıştırılmadan bilinemiyor
  (§67 ile aynı). Sayılar sonuç raporunda.
- **Geri alma yok.** Yanlış dosya aktarılırsa kayıtlar tek tek silinir; toplu geri alma
  (son içe aktarımı işaretleyip topluca silme) yazılmadı.
- **Rapor listeleri 100 satırla sınırlı** (`EnFazlaSorun`); bozuk bir dosyada geri kalan
  sebepler gösterilmiyor, sayaçlar tam.

# İşlem kategorileri + banka adı açılır listesi

İki ayrı sorun, aynı yerde: kurallar mekanizmasına göre (sabit kural / vergi kodu / kişi
yönlendirme / şablon) ayrılmıştı ama kullanıcı **muhasebe kategorisine** göre düşünüyor;
banka adı da serbest metin olduğu için aynı banka birden fazla yazımla giriliyordu.

**Eşleştirme mantığına dokunulmadı**: katman sırası, eşikler, algoritma ve desenler aynı.
Kategori yalnız etiket ve görünüm. Mevcut testlerin tamamı aynen geçiyor.

## 1. İşlem kategorisi

17 kategori seed'de (ad + varsayılan ana hesap grubu), tablo Tanımlar'dan yönetilebilir:

| Kategori | Ana grup | | Kategori | Ana grup |
|---|---|---|---|---|
| Hesaplar arası | 102 | | Kredi | 300 |
| Müşteri tahsilatı | 120 | | Kredi kartı | 309 |
| Tedarikçi ödemesi | 329 | | Ortaklar | 331 |
| Grup içi cari | 136 | | Diğer borç | 336 |
| Diğer alacak | 159 | | Vergi borcu | 360 |
| Personel iş avansı | 195 | | SGK | 361 |
| Personel maaş avansı | 196 | | KKEG | 689 |
| Banka gideri | 770 | | Araç/hizmet gideri | 740 |
| Finansman gideri | 780 | | | |

Dört tabloya nullable `IslemKategorisiId` eklendi: `SabitKural`, `VergiKoduEslemesi`,
`KisiYonlendirme`, `AciklamaSablonu`. Yabancı anahtar `SetNull` — kategori silinince kural
kalır, yalnız etiketi düşer.

**Mevcut kayıtlara kategori atanıyor** (yalnız boş olanlara; kullanıcının seçimi ezilmez).
Kod taşıyan kayıtlar kategorilerini **hesap kodunun ana grubundan** alıyor; açıklama
şablonlarının kodu olmadığı için onların eşlemesi seed'de elle yazılı.

## 2. Kategoriler görünümü

"Bu bankanın kuralları" sekmesinde, tek liste + üç kolon: kategori adı · hesap kodu ·
kural sayısı. Üstte tek satır özet (`Vakıfbank · 13 / 17 kategori tanımlı`). Tanımlı
satırlar tamamen sade; **yalnız kuralsız kategoriler** kırmızı zeminde ve sayı yerine
`yok` yazıyor — amaç yeni banka eklerken eksikleri kontrol listesi gibi görmek.

Satıra tıklanınca accordion açılıyor: kategorideki bütün kurallar tek listede, mekanizma
küçük bir etiket (`sabit kural`, `şablon`, `vergi kodu`, `kişi`). Kural buradan
düzenlenebiliyor; sabit kural ve şablon aynı sekmedeki formda açılıyor, vergi kodu ve
kişi yönlendirmesi Tanımlar ekranında olduğu için oraya yönlendiriliyor.

## 3. Onay ekranı

Satırın kategorisi küçük bir etiket olarak görünüyor ve üstte kategori filtresi var
(yalnız o ekstrede geçen kategoriler, yanında satır sayısı). Kategori **satıra
yazılmıyor**: önerilen/onaylanan hesap kodunun ana grubundan okunuyor — kullanıcı kodu
düzeltince etiket de düzeliyor.

## 4. Banka adı açılır liste + birleştirme

Banka adı alanı artık açılır liste; gerçekten yeni bir banka "Yeni banka ekle" adımından
geçiyor (serbest yazım varsayılan değil). Tanımlar > Banka hesapları'na **birleştirme**
eklendi: aynı bankanın farklı yazımları seçilip tek ada indiriliyor, onay adımında kaç
hesabın etkileneceği yazıyor. Toplu içe aktarımda tanınmayan banka adı satırı düşürmüyor,
uyarı satırı üretiyor.

## Uç noktalar

| Uç | İş |
|---|---|
| `GET /catalog/banka-ekstre/islem-kategorileri` | Kategori listesi (form açılır listeleri) |
| `GET /catalog/banka-ekstre/islem-kategorileri/kapsam?parserTipi=` | Kategoriler görünümü (özet + kurallar) |
| `POST/PUT/DELETE /catalog/banka-ekstre/islem-kategorileri` | Kategori yönetimi |
| `GET /catalog/banka-ekstre/banka-hesaplari/banka-adlari` | Banka adları + hesap sayıları |
| `POST /catalog/banka-ekstre/banka-hesaplari/banka-adi-birlestir` | Yazımları tek ada indirir |
| `GET /catalog/banka-ekstre/ekstre/{id}/satirlar?kategoriId=` | Kategori filtresi (mevcut uca parametre) |

## Veritabanı

`Migrations/20260825212831_BankaOtomasyonIslemKategorisi` — yeni `catalog.EkstreIslemKategorileri`
tablosu (Ad tekil), dört tabloya nullable `IslemKategorisiId` + `SetNull` yabancı anahtar.
Migration üretildi ve uygulandı; `has-pending-model-changes` temiz.

## Testler

| Test | Ne doğruluyor |
|---|---|
| `CatalogService.UnitTests/BankaEkstre/IslemKategorisiTests` (16) | 17 kategori tohumlanıyor ve ikinci seed tekrar etmiyor; mevcut kurallara kategori atanıyor, kullanıcının seçimi ezilmiyor; kapsam görünümü bankaya göre süzüyor ve kategorisiz kuralları sayıyor; ad tekilliği; kategori silinince kural kalıyor; satır etiketi ana gruptan okunuyor (195/196 ayrı); **kategori tablosu boşaltılınca eşleştirme sonucu değişmiyor**; kategori filtresi |
| `CatalogService.UnitTests/BankaEkstre/BankaAdiYonetimiTests` (6) | Ad listesi pasif hesapları da sayıyor; yazımlar tek ada iniyor ve yalnız ad değişiyor; hedefin kendisi "etkilenen" sayılmıyor; büyük/küçük harf farkı birleşiyor; boş hedef/seçim reddediliyor; başka firmanın hesabına dokunulmuyor |
| `CatalogService.UnitTests/BankaEkstre/BankaHesabiIceAktarimServiceTests` (+2) | Tanınmayan banka adı satırı düşürmüyor, bir kez uyarıyor; bilinen ad uyarı üretmiyor |
| `WebApp.UnitTests/BankaEkstre/KategoriGorunumuTests` (4) | Kod kolonu (`195 · 196`), kuralsız kategorinin `yok` gösterimi, kodsuz şablon satırı |

Sayılar: `CatalogService.UnitTests` 470 → **494**, `WebApp.UnitTests` 46 → **50**.
Tamamı geçiyor, mevcut testlerin hiçbiri değişmedi.

## Ne eksik kaldı

- **Ekranlar tarayıcıda denenmedi** (bUnit yok). Elle bakılacaklar: accordion açılışı,
  kırmızı eksik satırlar, banka adı açılır listesi + "Yeni banka ekle" adımı, birleştirme
  onayı, onay ekranındaki kategori filtresi.
- **Kategori satıra yazılmıyor**, hesap kodunun ana grubundan okunuyor. Aynı ana grupta iki
  kategori tanımlanırsa sırası küçük olan kazanır; bu bilinçli bir sadeleştirme.
- **Sunucu tarafı banka adını hâlâ serbest kabul ediyor.** "Açılır liste + ayrı adım"
  kuralı ekranda; API'ye kısıt konmadı — toplu içe aktarım ve eski istemciler kırılmasın.
- Kategorisi olmayan kurallar görünümde ayrı bir satırda listelenmiyor, yalnız sayısı
  özet satırında yazıyor.

---

# Kural grubu önceliği + çoklu ana grup

Sabit kural bir ana grup belirlediğinde (`MAAŞ AVANSI → 196`), grup içindeki alt hesap
araması diğer gruplardaki aynı isimli kayıtları da **eşit aday** sayıyor ve satır
gereksiz yere onaya düşüyordu. Gerçek örnek — `MAAŞ AVANSI … ÖMER CAN DİZDAR hesabına
giden FAST ödemesi`; kişi planda üç kez var:

```
195 01 O09       Ömer Can Dizdar     (iş avansı)
196 03 25 O04    Ömer Can Dizdar     (maaş avansı)   ← kuralın grubu
335 01 O09       Ömer Can Dizdar     (personele borçlar)
```

## 1. Kararı kuralın grubu veriyor

Kural bir ana grup belirlediyse **o gruptaki aday sayısı** belirleyici:

| Kuralın grubunda | Sonuç |
|---|---|
| tam **bir** aday | **Otomatik.** Diğer gruplardaki karşılıklar alternatif olarak saklanır, engellemez |
| **sıfır** aday | Onaya düşer; diğer gruplardaki karşılıklar aday listelenir |
| **iki veya daha fazla** | Onaya düşer; hepsi aday listelenir |

Tek kelimelik isimde (`İlyas`) hiçbir zaman otomatik seçim yapılmıyor — bu kural değişmedi.

## 2. Kural birden fazla ana grup kapsayabiliyor

`SabitKural.AnaGruplar` (yeni, nullable): virgülle ayrılmış grup listesi (`195, 196`).
Doluysa alt hesap araması bu grupların **tamamında** yapılır ve sayım toplamı üzerinden
karar verilir. Boşsa küme, `HesapKodu`'nun tek ana grubudur — eski davranış aynen.

Alan yalnız "kural yalnız ana grubu belirliyor" işaretliyken doldurulabiliyor; başka bir
kuralda yazılırsa sessizce yok sayılmıyor, hata veriliyor. Çoklu gruplu kuralda aday
bulunamazsa **kod önerilmiyor**: 195 mi 196 mı bilinmiyorsa birini yazmak kullanıcıyı
yanlış gruba yönlendirirdi.

Seed: `İş Avansı → 195` ve `Maaş Avansı → 196` **tek gruplu kaldı**, genel `Avans` kuralı
`195, 196` oldu. Sıra numaraları zaten doğruydu (İş Avansı 10, İş Avans 20, Masraf Ödemesi
30, Maaş Avansı 40, genel Avans 50) — dar ifadeler genelden önce; test bunu sabitliyor.
Çoklu gruptan önce kurulmuş veritabanlarındaki `Avans` satırı için **dar** bir tek seferlik
yükseltme var: kayıt hâlâ seed'in bıraktığı hâldeyse (kod 196, liste boş) listeye
`195, 196` yazılır, kullanıcı düzenlediyse dokunulmaz.

## Veritabanı

`Migrations/20260827172923_BankaOtomasyonKuralAnaGruplari` — `catalog.EkstreSabitKurallar`
tablosuna nullable `AnaGruplar nvarchar(200)`. Migration üretildi ve **uygulandı**.

## Arayüz

Sabit Kurallar bölümüne "Ana gruplar" alanı eklendi (yalnız "alt hesap gerekli" açıkken
etkin); listede hesap kodu kolonu çoklu gruplu kuralda grup listesini gösteriyor. **Sıra
kolonu zaten düzenlenebilirdi** (form içindeki `Sıra` sayısal alanı) — doğrulandı, değişmedi.

## Gerçek dosyayla ölçüm

Depo kökündeki gerçek Vakıfbank ekstresi (287 satır), fikstür planına dört senaryonun
gerçek ORKA kayıtları eklenerek, önce/sonra:

```
Otomatik      62 → 63
Onay bekliyor 102 → 101
Çözülemedi    123 → 123
```

Fark tam olarak **bir satır** — düzeltmenin hedefi olan satırın kendisi. Dört senaryonun
dördü de bu dosyanın 286–289. satırlarında duruyor ve hepsi beklendiği gibi davranıyor:

| Satır | Kural grubu | Grup içi aday | Sonuç |
|---|---|---|---|
| `ÖMER CAN DİZDAR` | 196 | 1 (`196 03 25 O04`) | **Otomatik** → `196 03 25 O04`; `195 01 O09` alternatif |
| `emirhan özer` | 196 | 2 (`196 03 25 E01`, `196 IU 77`) | Onaya düşüyor, ikisi de aday |
| `ABDULKADİR SAYICI` | 195 | 0 (yalnız `331 02` var) | Onaya düşüyor, `331 02` aday |
| `EMİRHAN ÖZDEMİR` (2 satır) | 196 | 0 (planda yok) | Onaya düşüyor, öneri yok |

Regresyon: ad + soyadı geçen diğer satırlar eskisi gibi otomatik çözülüyor
(`Mesut Aktaş` → `195 01 M05`, `İlyas Ömeroğlu` → `195 01 H13`, `Eda Budak` → `195 01 E03`,
`Dilara Kaya` → `195 01 D06`), `dilara sager` ve tek kelimelik `İlyas` satırları hâlâ
öneri almıyor.

**Ofis planıyla ölçüm ayrıca yapılmalı:** depoda 88 kayıtlık fikstür var, gerçek plan
6.128 kayıt. Mekanizma plandan bağımsız ama toplam sayılar ancak orada anlamlı.

## Testler

| Test | Ne doğruluyor |
|---|---|
| `CatalogService.UnitTests/BankaEkstre/KuralGrubuOnceligiTests` (21) | Dört senaryo (birim + gerçek dosya uçtan uca); çoklu grupta toplam tek aday otomatik, birden fazlası onaya; çoklu grupta aday yoksa kod önerilmiyor; kural kümesi dışındaki grup sayıma girmiyor; tek kelimelik isim otomatik değil; seed sırası ve `195, 196`; tek gruplu eski kaydın yükseltilmesi ve kullanıcı düzenlemesinin korunması; ana grup listesinin normalize edilmesi ve geçersiz kullanımın reddi |

Sayı: `CatalogService.UnitTests` 494 → **515**. Tamamı geçiyor; **mevcut testlerin hiçbiri
değiştirilmedi**. `GercekHesapPlani` fikstürüne dört senaryonun gerçek ORKA kayıtları
eklendi (196 grubu daha önce fikstürde hiç yoktu).

## Ne eksik kaldı

- **Ekranlar tarayıcıda denenmedi:** "Ana gruplar" alanının kilidi, listedeki grup
  gösterimi. Sunucu tarafı testli.
- **Ana grup listesi hesap planına karşı doğrulanmıyor.** Gruplar (195/196) plan kaydı
  değil, kod öneki; var olmayan bir grup yazılırsa kural o grupta hiç aday bulamaz —
  hata vermez, sessizce boş döner.
- **Çoklu grup yalnız genel `Avans` kuralında kullanılıyor.** Başka kurallara gerekirse
  arayüzden eklenir; kod değişmez.

---

# Bordro hesaplayıcısı Hesaplamalar'ın altına taşındı

Sayfa `/payroll-calculator` adresinde, **girişin dışında**, uygulamanın menüsü ve
kullanıcısı olmayan ayrı bir kabukta duruyordu. Artık uygulamanın içinde, kimlik
doğrulamasının arkasında.

## Anonim erişim nasıl kurulmuştu (ve nasıl kapatıldı)

Tek bir bayrak değildi; beş ayrı yerde birden. Tespitlerin tamamı **KARARLAR §78**'de:

| Ne vardı | Ne oldu |
|---|---|
| `@page "/payroll-calculator"`, `[Authorize]` yok | Rota eski adres yönlendirmesine indi; içerik `[Authorize]`'lu sayfada |
| `@layout PublicPayrollLayout` | Kaldırıldı; standart `MainLayout` |
| `Pages/Payroll/Layout/PublicPayrollLayout.razor` (+ `.css`) | **Silindi** |
| `[Route("api/public/payroll")] [AllowAnonymous]` | `[Route("api/catalog/payroll")] [Authorize]`; sınıf `PayrollController` |
| Ocelot'ta yetkisiz `/api/public/payroll/{everything}` rotası | **Üç yapılandırmadan da silindi** (`ocelot.json`, `.Docker.json`, `.Development.json`) |

En kritik ayrıntı: CatalogService'te global bir yetki politikası yok — korunan 39
controller'ın hepsi yetkiyi kendi `[Authorize]`'ıyla alıyor. `[AllowAnonymous]`'u sadece
silmek controller'ı **herkese açık bırakırdı**; yerine açıkça `[Authorize]` kondu.

Anonim erişime özel bir CORS istisnası, ayrı bir HttpClient ya da nginx kuralı **yoktu**
(arandı). `PayrollApiService` zaten token gönderen `ApiGatewayCorridor` istemcisini
kullanıyordu — token isteniyor değildi, o kadar. Bu yüzden DI'ya dokunulmadı, yalnız
temel yol `/api/public/payroll` → `/catalog/payroll` oldu.

## Yeni yapı

```
Menü:  Hesaplamalar (/hesaplamalar)
         └── Bordro Hesaplaması (/hesaplamalar/bordro)
```

`Pages/Hesaplamalar/`
- `HesaplamaSekmesi.cs` — sekme **kayıt listesi**: slug, başlık, ikon, bileşen tipi
- `HesaplamalarPage.razor` — `@page "/hesaplamalar"` + `@page "/hesaplamalar/{Slug}"`,
  `[Authorize]`; şeridi listeden üretir, aktif sekmeyi `<DynamicComponent>` ile basar
- `EskiRotaYonlendirme.razor` — `/payroll-calculator` → `/hesaplamalar/bordro`
- `Bordro/` — taşınan bordro modülünün tamamı

**Yeni sekme eklemek:** bir Razor bileşeni yazın (kendi `@page`'i olmasın) ve
`HesaplamaSekmesi.Hepsi` listesine bir satır ekleyin. Sayfa iskeleti değişmez
(KARARLAR §79). Banka Otomasyon'un şeridi sekmeleri elle yazıyor; burada büyüme
beklendiği için kalıp bir adım ileri taşındı.

`/hesaplamalar` kökü ilk sekmeye `replace: true` ile yönleniyor — aynı ekranın iki adresi
olmasın. Tanınmayan slug hata vermiyor, şerit çizilip "sekme bulunamadı" yazıyor.

## Taşınan dosyalar

`Pages/Payroll/` → `Pages/Hesaplamalar/Bordro/` (klasör tamamen kalktı):

| Eski | Yeni |
|---|---|
| `Payroll/Page/PayrollCalculator.razor(.css)` | `Hesaplamalar/Bordro/BordroHesaplamasi.razor(.css)` |
| `Payroll/Component/` (3 bileşen) | `Hesaplamalar/Bordro/Component/` |
| `Payroll/Model/` (11 dosya) | `Hesaplamalar/Bordro/Model/` |
| `Payroll/Services/` (`IPayrollApiService`, `PayrollApiService`) | `Hesaplamalar/Bordro/Services/` |
| `Payroll/PayrollDisclaimerTexts.cs` | `Hesaplamalar/Bordro/PayrollDisclaimerTexts.cs` |
| `Payroll/Layout/PublicPayrollLayout.razor(.css)` | **silindi** |

Ad alanı `WebApp.Pages.Payroll.*` → `WebApp.Pages.Hesaplamalar.Bordro.*` (17 dosya).
Sınıf adları ve **hesaplama mantığı değişmedi**; taşınan bileşenden yalnız `@layout`,
`@page` ve `<PageTitle>` satırları çıkarıldı (başlığı artık sayfa iskeleti veriyor).

Sunucu tarafında `Features/Payroll/` altındaki komut/sorgu/servis/entity katmanlarının
**hiçbirine dokunulmadı**; yalnız controller'ın rotası, yetki bayrağı ve adı değişti.

## Testler

`CatalogService.UnitTests` **515**, `WebApp.UnitTests` **50** — ikisi de tamamen geçiyor,
**hiçbir test değiştirilmedi**. WebApp, CatalogService.Api ve Web.ApiGateway temiz
derleniyor; üç ocelot yapılandırması da geçerli JSON ve artık `public` rota içermiyor.

## Ne eksik kaldı

- **Tarayıcıda denenmedi** (bUnit yok). Elle bakılacaklar: menüdeki Hesaplamalar başlığı
  ve alt sekme, `/hesaplamalar` → `/hesaplamalar/bordro` yönlendirmesi, eski
  `/payroll-calculator` yer iminin çalışması, bordro ekranının `MainLayout` içinde
  bozulmadan görünmesi (eski kabuk tam sayfaydı).
- **Bordro CSS'i taşınırken sadeleştirilmedi.** `BordroHesaplamasi.razor.css` hâlâ
  `html, body { overflow: hidden }` ile başlıyor; Blazor CSS izolasyonu bunu bileşene
  kapsadığı için etkisiz (bu yüzden dokunulmadı), ama tam sayfa kabuktan kalma ölü kod.
- **Bordro uçları için otomatik test yok.** Rota ve yetki değişikliği derleme + elle
  denemeyle doğrulanacak; `PayrollController` üzerinde entegrasyon testi bulunmuyor.
- **Kapsam dışı bırakılan yan tespit:** `ocelot.Development.json`'da genel
  `/catalog/{everything}` rotasının `AuthenticationOptions`'ı yok. Payroll'a özel değil,
  geliştirme yapılandırmasının geneli; üretim (`ocelot.json`) ve Docker yapılandırmasında
  Bearer var.

---

# Finansman Gider Kısıtlaması sekmesi

Hesaplamalar sayfasının ikinci alt sekmesi: `/hesaplamalar/finansman-gider-kisitlamasi`.
TÜRMOB formundaki dokuz satır — kullanıcı dördünü girer, beşi sunucuda hesaplanır.
Kararlar ve gerekçeleri: **KARARLAR §80**.

## Nasıl çalışıyor

| # | Satır | Kaynak |
|---|---|---|
| 1 | Özsermaye tutarı | giriş (zorunlu, negatifse 0) |
| 2 | Yabancı kaynak toplamı (Aktif − Özsermaye) | giriş |
| 3 | Özsermayeyi aşan yabancı kaynak tutarı | `2 − 1` |
| 4 | Aşan kısmın yabancı kaynağa oranı | `3 ÷ 2` (yüzde) |
| 5 | Finansman gider tutarı (780, 660, 656 vb.) | giriş |
| 6 | Örtülü sermaye gideri (KKEG) / aynı kaynaktan finansman geliri | giriş |
| 7 | Hesaplamada dikkate alınacak finansman gideri | `5 − 6` (negatifse 0) |
| 8 | Aşan kısma isabet eden finansman gideri | `4 × 7` |
| 9 | **KKEG olacak finansman giderleri** | `8 × kısıtlama oranı` |

**Kenar kuralları:** 1. satır negatifse sıfır kabul edilir; 3. satır sıfır veya negatifse
kısıtlama yapılmaz (4–9 sıfır döner, ekranda gerekçe yazar; 3. satırın kendisi ham fark
olarak durur); 2. satır sıfırken 4. satırda bölme yapılmaz; 7. satır negatif çıkarsa sıfır
kabul edilir.

**Hesap sunucuda.** `FinansmanGiderKisitlamasiMotoru` saf fonksiyon: veritabanı bilmez,
oranı parametre alır. İstemci her değişiklikte 250 ms gecikmeyle `/hesapla`'yı çağırır
(Bordro sekmesiyle aynı). 8 ve 9. satırlar 4. satırın **yuvarlanmamış** oranıyla
hesaplanır; tutarlar sonuçta 2 haneye yuvarlanır.

## Kısıtlama oranı

Oranı (bugün %10) Cumhurbaşkanı Kararı belirliyor; koda gömülü değil, **yıl bazlı tabloda**
ve ekrandaki "Kısıtlama oranını düzenle" bölümünden düzenlenebiliyor: yıl ekle/değiştir/sil,
dayanak metni. Oran yüzde olarak saklanır (`10` = %10). Seçilen yılın oranı tanımlı değilse
hesap yapılmaz, ekranda "…oranı tanımlı değil, ekrandan tanımlayın" hatası çıkar.

Seed 2021–2026 için %10 yazar (kısıtlama 1/1/2021'de yürürlüğe girdi); yılı zaten kayıtlı
olan orana dokunmaz.

## Uç noktalar

`api/catalog/finansman-gider-kisitlamasi` (`[Authorize]`, gateway'in genel
`/catalog/{everything}` rotasından geçer — ayrı rota eklenmedi):

| Metot | Yol | İş |
|---|---|---|
| POST | `hesapla` | Dokuz satırı döner; oran yoksa 400 + açıklama |
| GET | `oranlar` | Tüm yılların oranları |
| GET | `oranlar/{yil}` | Tek yıl |
| PUT | `oranlar/{yil}` | Yıl bazlı upsert |
| DELETE | `oranlar/{yil}` | Yılın oranını siler |

## Veritabanı

`catalog.FinansmanKisitlamaOranlari` — `Yil` (benzersiz), `Oran decimal(18,4)` (yüzde),
`Dayanak`, `Not`, `GuncellenmeTarihi`. Firmadan/tenant'tan bağımsız ortak referans, query
filter yok. Migration: `20260827192040_AddFinansmanKisitlamaOranlari` (uygulandı,
`has-pending-model-changes` temiz).

## Değişen / eklenen dosyalar

**Sunucu (yeni):** `Features/FinansmanGiderKisitlamasi/` — `Domain/FinansmanKisitlamaOrani.cs`,
`Dtos/FinansmanGiderKisitlamasiDtos.cs`, `Services/FinansmanGiderKisitlamasiMotoru.cs`,
`Services/FinansmanKisitlamaOraniYokException.cs`,
`Services/IFinansmanGiderKisitlamasiService.cs` + `FinansmanGiderKisitlamasiService.cs`,
`Controllers/FinansmanGiderKisitlamasiController.cs`, `FinansmanGiderKisitlamasiSeed.cs`;
`Infrastructure/EntityConfigurations/FinansmanKisitlamaOraniEntityTypeConfiguration.cs`.

**Sunucu (değişen):** `CatalogContext` (DbSet + `ApplyConfiguration`), `Program.cs`
(DI + seed çağrısı), migration + snapshot.

**İstemci (yeni):** `Pages/Hesaplamalar/FinansmanGiderKisitlamasi/` —
`FinansmanKisitlamaHesabi.razor(.css)`, `Model/FinansmanKisitlamaModels.cs`,
`Services/IFinansmanKisitlamaApiService.cs` + `FinansmanKisitlamaApiService.cs`.

**İstemci (değişen):** `HesaplamaSekmesi.cs` (kayıt listesine bir satır),
`ServiceCollectionExtensions.cs` (DI), `MainLayout.razor` (alt menüye bir satır).
**`HesaplamalarPage.razor` değişmedi.**

## Testler

`CatalogService.UnitTests/FinansmanGiderKisitlamasi/FinansmanGiderKisitlamasiMotoruTests.cs`
— 10 test: normal senaryo, kısıtlama yok (aşmıyor / eşit), negatif özsermaye, yabancı kaynak
sıfır (iki varyant), finansman geliri giderden fazla, oran tanımsız hatası, oran parametresi
sonucu değiştiriyor, küsuratlı oranda yuvarlama.

`CatalogService.UnitTests` **525**, `WebApp.UnitTests` **50**, `Sovos.InvoiceWorker.Tests`
**1** — hepsi geçiyor, **hiçbir mevcut test değiştirilmedi**. CatalogService.Api ve WebApp
temiz derleniyor.

## Ne eksik kaldı

- **Tarayıcıda denenmedi** (bUnit yok). Elle bakılacaklar: sekme şeridinde yeni sekme,
  dokuz satırın anlık hesaplanması, oran panelinden ekleme/güncelleme/silme, Türkçe biçim.
- **Oran uçları için otomatik test yok.** Motor birim testli; CRUD ve controller
  doğrulamaları (yıl 2000–2100, oran 0–100) elle denenecek.
- **Dışa aktarım yok.** Bordro'daki gibi Excel/PDF çıktısı istenmedi, eklenmedi.
- **Beyannameye bağlanmadı.** Hesaplanan KKEG, Firma Kontrol'deki kurumlar vergisi
  beyanname ekranına otomatik taşınmıyor; kullanıcı tutarı kendisi giriyor.
- **Yerel veritabanına seed elle atıldı** (2021–2026, %10). Servis ilk açılışında seed zaten
  aynı satırları yazacak; iki kez yazmaz (yıl kayıtlıysa dokunmuyor).

---

# DBS ödemesi kayıt defterine düşmüyor

Bir satır yanlış çözülüyordu: `İŞ BANKASI DBS - BORUSANPRE - 879382 NO.LU ABONE / İŞ
BANKASI (… VADESİZ HESABINDAN … ŞUBESİ NEZDİNDEKİ …)` sistem `102 1 5 01` diyordu, doğrusu
`329 B15 Borusan Otomotiv Premium Kiralama`. Gerekçeler: **KARARLAR §81**.

## Ne değişti

**1. Koşul (c) DBS satırlarında kapalı.** Gövdede `DBS` ya da `ABONE` kelimesi geçiyorsa
"… VADESİZ HESABINDAN … ŞUBESİ NEZDİNDEKİ …" kalıbı banka kayıt defteri katmanını artık
açmıyor; satır cari katmanlarına düşüyor. Banka bu satırda yalnız aracı, para aboneye
(tedarikçiye) gidiyor.

Ölçüldü: bu satırda (a) tutmuyor (metinde "hesaplar arası" yok), (b) de tutmuyor
(`HesapSahibiElendi = false`). Katmanı yalnız (c) açıyordu, o yüzden **yalnız (c)** kapatıldı;
(a) ve (b) dokunulmadan duruyor.

**2. Abone adı kısaltmasıyla cari araması.** (c)'yi kapatmak tek başına satırı `329 B15`'e
götürmüyordu — çözülemedi kalıyordu: banka abone adını bitiştirip kısaltıyor
(`BORUSANPRE` = `BORUSAN` + `PRE`mium) ve benzersiz önek katmanı hesap adının metnin
token'ıyla **başlamasını** arıyor; burada ilişki ters. `CariOnekIndeksi.KisaltmaOnekiyleEslesenler`
bu ters yönü arıyor: yalnız DBS satırlarında, yalnız hesap adının ilk kelimesiyle, ilk kelime
en az 6 harfse ve **en son çare** olarak. Tek aday çıkarsa otomatik, birden fazlaysa satır
onaya düşer.

## `102 1 5 06` kontrolü

Kullanıcının defterindeki `102 1 5 06 İş Bankası, Dbs Tl - 3430904, Borusan` hesabı DBS
satırlarını **adı yüzünden çekmiyor**: eşleştirme hesap adına bakmıyor, yalnız `BankaAdi` ve
`EslestirmeAnahtarlari` metinde aranıyor. Hesap genel banka adı yarışına giriyor ve
`102 1 5 01`'in daha uzun anahtarına ("Türkiye İş Bankası") yeniliyor. Bu hesaba "Dbs" /
"Borusan" anahtarı tanımlansa bile DBS satırı kayıt defterine düşmüyor — (c) hiç açılmadığı
için anahtar aramasına sıra gelmiyor. Üçü de testte.

## Değişen dosyalar

| Dosya | Değişiklik |
|---|---|
| `Services/HesapEslestirici.cs` | `DbsOdemesiMi` (koşul c'nin önüne), `DbsAboneAramasi` + `DbsAboneAdi` (önek katmanının son çaresi) |
| `Services/CariOnekIndeksi.cs` | `KisaltmaOnekiyleEslesenler` — ters yönde, dar önek araması |
| `CatalogService.UnitTests/BankaEkstre/GercekHesapPlani.cs` | `329 B15 Borusan Otomotiv Premium Kiralama` carisi + `102 1 5 06` DBS banka hesabı |
| `CatalogService.UnitTests/BankaEkstre/BankaKayitDefteriTests.cs` | Gerçek DBS açıklaması + 4 test |
| `CatalogService.UnitTests/BankaEkstre/Tur2GercekVeriTests.cs` | Gerçek dosyada uçtan uca 2 test |

Veritabanı, DTO ve istemci değişmedi; yeni `KaynakKatman` değeri açılmadı (eşleşme yine
`BenzersizOnek`).

## Testler

Yeni 6 test:

- DBS satırı kayıt defteri katmanını açmıyor (koşul (c) kapalı).
- Aynı gövdeden `DBS`/`ABONE` kelimeleri çıkarılınca katman **yine açılıyor** — farkı
  yaratanın bu iki kelime olduğunun kanıtı.
- DBS hesabına "Dbs, Borusan" anahtarı tanımlansa bile satır kayıt defterine düşmüyor.
- DBS hesabı defterdeyken normal `İŞ BANKASI (…)` satırı hâlâ `102 1 5 01`'e gidiyor.
- Gerçek dosya, uçtan uca: DBS satırı → `329 B15`, otomatik, katman kayıt defteri değil.
- Gerçek dosya: `HESAPLAR ARASI E.F.T. VAKIFBANK/DENİZBANK`, `İŞ BANKASI (…)` ve
  `DENİZBANK HESABINA` satırları eskisi gibi kayıt defterinden çözülüyor.

`CatalogService.UnitTests` **531**, `WebApp.UnitTests` **50** — hepsi geçiyor, mevcut
testlerin hiçbiri değiştirilmedi. Gerçek dosyadaki diğer üç "ABONE" satırı (Superonline,
Türk Telekom) eski eşleşmelerini koruyor.

## Ne eksik kaldı

- **Tek örnekle çalışıldı.** Dosyada bir DBS satırı var; başka bankaların DBS gövdeleri
  ("DBS ÖDEMESİ - …" gibi) farklı yazılmış olabilir. `DBS` kelimesini izleyen token kuralı
  o zaman gözden geçirilmeli.
- **Kısaltma eşleşmesi yalnız DBS satırlarında açık.** Aynı sorun başka satır tiplerinde de
  varsa (banka kısaltılmış satıcı adı yazıyorsa) kapsam genişletilmedi — bilerek dar
  bırakıldı.
- **`DBS` / `ABONE` kodda sabit**, yönetilebilir tabloda değil; `BankalarArasiIfadeleri` ile
  aynı yerde duruyor.

---

# Düzeltilmiş ekstre: ORKA formatı ve satır kaybı

İki sorun vardı: çıktı bankanın 17 kolonlu yapısını + künye bloğunu koruyordu (ORKA
okumuyor) ve dosyada satırların yalnız bir kısmı görünüyordu. Gerekçeler: **KARARLAR §82**.

## Yeni format

```
Tarih | Açıklama | Giren | Çıkan
```

- Başlık 1. satırda, veri 2. satırdan; **künye bloğu yok**.
- **Tarih** — işlem tarihi, gerçek tarih hücresi, `dd.MM.yyyy` biçimi.
- **Açıklama** — üretilen açıklama, 50 karakterde kırpılı.
- **Giren / Çıkan** — yönüne göre biri dolu, diğeri boş. Tutarlar **sayısal hücre**
  (`#,##0.00` biçimi); metin hücreyi ORKA yanlış ayrıştırabiliyor.

Dosya artık orijinalin kopyası değil, satırlardan **sıfırdan** yazılıyor. Bunun yan
faydası: kaynak dosya saklanmamış ya da açıklama kolonu belirlenememiş olsa bile düzeltilmiş
ekstre üretiliyor (eski sürüm bu iki durumda hata veriyordu).

## Satır kaybının nedeni

Satır döngüsü suçsuzdu: gerçek dosyadaki 287 satırın hepsi işleniyor ve üretilen xlsx'e
yazılıyordu. Sorun **kopyala-kaydet** yönteminin kendisiydi — kaynak dosyayı ClosedXML ile
açıp değiştirmeden kaydetmek bile dosyayı bozuyor: üretilen dosya ClosedXML'in kendisiyle
bile açılamıyor (`LoadStyle` → `ArgumentOutOfRangeException`), boyutu 48 KB'den 42 KB'ye
düşüyor. Bozuk dosyayı okuyan taraf onarma moduna girip içeriğin bir kısmını düşürüyor;
kullanıcının gördüğü 17 satır bu.

Yeni yöntemde kaynak dosya hiç açılmıyor. Testler üretilen dosyayı ClosedXML ile okuyor —
eski çıktıda bu mümkün değildi, yani regresyon artık otomatik yakalanır.

## Hizalama garantisi

Robot kod listesini ORKA gridine satır sırasına göre yazıyor; iki çıktının satır sayısı
veya sırası ayrışırsa kodlar yanlış satırlara gider. Filtre artık iki yerde ayrı yazılmıyor,
ikisi de `OrkayaGidenSatirlar` üzerinden geçiyor: dosyadaki sıra (`SiraNo`) + "diğer
bankada" işaretli satırların düşürülmesi.

## Değişen dosyalar

| Dosya | Değişiklik |
|---|---|
| `Services/EkstreService.cs` | `DuzeltilmisEkstreAsync` baştan yazıldı (dört kolon, sıfırdan üretim); `OrkayaGidenSatirlar` ortak yardımcısı; `DisaAktarAsync` aynı yardımcıyı kullanıyor; `DuzeltilmisEkstreHazir` artık her zaman `true` |
| `CatalogService.UnitTests/BankaEkstre/EkstreServiceTests.cs` | Eski 17 kolon testi yeni formatın testiyle değişti + kaynak dosyasız üretim testi |
| `CatalogService.UnitTests/BankaEkstre/Tur2GercekVeriTests.cs` | Gerçek dosyada hizalama testleri (287 satır) |

Veritabanı, DTO ve istemci değişmedi; indirme düğmesi ve uç noktası aynı.

## Testler

- **Format**: dört kolon, başlık 1. satırda, veri 2'den, künye yok; tarih hücresi
  `dd.MM.yyyy`; giren satırında yalnız Giren dolu ve **sayısal** hücre, çıkan satırında
  tersi; başlık + veri dışında satır yok.
- **Kaynak dosyasız üretim**: `DosyaIcerik` ve `AciklamaKolonu` silinse de dosya üretiliyor.
- **Hizalama (gerçek dosya, 287 satır)**: düzeltilmiş ekstrenin veri satırı sayısı = kod
  listesi satır sayısı = 287; her i için tarih, açıklama ve tutar iki çıktıda birebir aynı
  ve sıra dosyadaki sıra.
- **"Diğer bankada"**: işaretli satır iki çıktıdan **birden** düşüyor, kalan satırlar hizalı.

`CatalogService.UnitTests` **534**, `WebApp.UnitTests` **50** — hepsi geçiyor.

## Ne eksik kaldı

- **ORKA'da denenmedi.** Format kullanıcının tarifine göre yazıldı; gerçek Veri Transferi
  ekranıyla doğrulama ofiste yapılacak.
- **Sayfa adı sabit** ("Ekstre"); ORKA sayfa adına bakıyorsa ayarlanması gerekebilir.
- **Kaynak dosya hâlâ saklanıyor** (`DosyaIcerik`). Düzeltilmiş ekstre artık kullanmıyor ama
  yeniden işleme/inceleme için duruyor; temizlenmedi.

---

# Üç yeni banka: İş Bankası, Akbank, Ziraat

Modül dört bankayı okuyor. Eşleştirme mantığı — katman sırası, eşikler (0.85 / 0.05 / 0.40),
benzersiz önek algoritması — **değişmedi**; eklenen şey üç ayrıştırıcı, onların yapılandırma
satırları ve dosya biçimlerini karşılayan ortak bir okuma katmanı.

## Dosya yapıları (gerçek 7 aylık ekstrelerden ölçüldü)

| Banka | Biçim | Başlık satırı | Yön nereden | İşlem tipi |
|---|---|---|---|---|
| Vakıfbank | xlsx | 7 | B/A kolonu | var |
| İş Bankası | **eski .xls** | 16 | tutarın işareti | var (11 tip) |
| Akbank | xlsx | 10 | Borç/Alacak kolonu (+ işaretle çapraz doğrulama) | **yok** |
| Ziraat | xlsx (**bozuk styles.xml**) | 12 | tutarın işareti | **yok** |

Üç dosyanın üç ayrı hastalığı tek kütüphaneyle geçilmiyordu; dosya artık imzasına bakan bir
**okuyucu zinciriyle** açılıyor: OLE2 → NPOI/HSSF, zip → ClosedXML → NPOI/XSSF → ham XML.
Yedek yola düşülürse hangi okuyucunun neden başarısız olduğu uyarıya yazılır. Ayrıntı ve
gerekçe: KARARLAR §85.

İş Bankası'nda tarih hücresi `26/08/2026-14:58:47` — saat **tireyle** ayrılmış; boşluk
bekleyen bir ayrıştırıcı bu kolonu hiç okuyamaz.

## Yapılandırma satırları (seed)

| Banka | Şablon | Unvan deseni | Sabit kural |
|---|---|---|---|
| Vakıfbank | 17 | 9 | 13 |
| İş Bankası | 11 | 3 | 17 |
| Akbank | 9 | 3 | 5 |
| Ziraat | 5 | 3 | 3 |

Vakıfbank satırları aynen korundu (test: `Vakifbank_satirlari_degismedi`).

Yeni kategoriler: **Menkul kıymet (118)** ve **Alınan çekler (101)** — toplam 19.
Diğerleri (780 finansman gideri, 361 SGK, 309 kredi kartı, 300 kredi) listede zaten vardı,
bu turda kural kazandılar.

İki yeni yer tutucu: `{YON}` (aynı işlem tipi hem gelen hem giden olabiliyor) ve `{KREDI}`
(kredi hesap numarası). Şablon tablosuna yön kolonu eklenmedi — KARARLAR §87.

## Eşleştirmeye dokunulmayan yerler

- Katman sırası, eşikler ve ORKA kod formatı (boşluklu) aynı.
- Akbank/Ziraat'te **uydurma işlem tipi türetilmedi**; şablon ve kural açıklamadan eşleşiyor
  (KARARLAR §86). Uydurulsaydı unvansız satırların öğrenme anahtarı ilgisiz satırları da
  çözerdi.
- Bankalar arası satırlara (hesap açılışı, virman, hesaplar arası EFT) **açıklama kuralı
  yazılmadı**: açıklama kuralları tüm katmanlardan önce çalışıp satırı kapatıyor, karşı
  hesabı banka kayıt defteri bulmalı.
- DBS satırları bankalar arası **değil**, tedarikçi ödemesi (§81 ile aynı karar, bu kez
  Akbank tarafında).

## Değişen dosyalar

| Dosya | Değişiklik |
|---|---|
| `Services/Parsing/EkstreTablosu.cs` | **yeni** — okuyucudan bağımsız tablo modeli + tarih/tutar okuma |
| `Services/Parsing/EkstreTabloOkuyucu.cs` | **yeni** — imzaya bakan okuyucu zinciri |
| `Services/Parsing/HamXlsxOkuyucu.cs` | **yeni** — zip içindeki XML'i doğrudan okuyan yedek yol |
| `Services/Parsing/TabloBaslik.cs` | **yeni** — başlığı isimle bulma, bulunamazsa sabit indeks + uyarı |
| `Services/Parsing/TabloParserTemeli.cs` | **yeni** — ortak ayrıştırma iskeleti |
| `Services/Parsing/IsBankasiVadesizParser.cs` | **yeni** |
| `Services/Parsing/AkbankVadesizParser.cs` | **yeni** |
| `Services/Parsing/ZiraatVadesizParser.cs` | **yeni** |
| `Services/Parsing/IEkstreParser.cs` | `AyrilanSatir.Referans` alanı |
| `Services/Normalizasyon.cs` | `KrediAnahtar` İş Bankası yazımını da tanıyor; IBAN çıkarımı yıldızla ayrılmış alanlarda IBAN'ı artık kaçırmıyor |
| `Services/AciklamaUretici.cs` | `{YON}` + `{KREDI}` yer tutucuları; şablonsuz satırda taban sırası işlem tipi → unvan → banka açıklaması |
| `BankaEkstreSeed.cs` | Dört banka; her bankanın satırları kendi metodunda, ekleme mantığı ortak |
| `Domain/EkstreSatiri.cs` + entity config | `Referans` kolonu |
| `Services/EkstreService.cs` | Referans satıra yazılıyor |
| `Program.cs` | Üç yeni `IEkstreParser` kaydı |
| `Migrations/…_BankaEkstreSatirReferansi` | Tek kolon, nullable |
| `CatalogService.Api.csproj` | NPOI 2.7.1 (eski .xls için) |

## Testler

Yeni: `UcBankaTestOrtami`, `IsBankasiParserTests`, `AkbankParserTests`, `ZiraatParserTests`,
`UcBankaSeedTests`, `TabloDegerTests`. Ham açıklamalar gerçek ekstrelerden birebir.

Kapsanan sekiz madde: başlığın isimle bulunması (uyarısız), satırların ayrışması, tarih ve
yön (Akbank'ta B/A ile işaretin çapraz doğrulanması), her bankadan en az üç gerçek satırda
unvan çıkarma, İş Bankası "Havale" satırında unvanın sondan alınması, Akbank DBS satırının
banka kayıt defterine düşmeyip cari katmanına gitmesi, Ziraat'in bozuk `styles.xml`'ine
rağmen okunması ve İş Bankası'nın `.xls` biçiminin okunması.

`CatalogService.UnitTests` **600** (öncesi 541), `WebApp.UnitTests` **62** — hepsi geçiyor.

## Ne eksik kaldı

- **Gerçek dosyalarla satır sayıları doğrulanamadı.** Vakıfbank ekstresinin aksine üç
  bankanın dosyaları depoda yok; ölçülen 418 / 186 / 356 satır bu yüzden test edilemedi.
  Testler yapıyı (başlık, kolon yerleşimi, dosya biçimi, atlanan satır) sınıyor. Dosyalar
  depoya konursa `VakifbankGercekDosyaTests` kalıbıyla üç test daha yazılmalı.
- **Ziraat alt hesap numaraları** (`62286065-5010` → vadeli kasa, `-5022` → günlük kazanan)
  firmaya özel; seed'e yazılamaz. Tanımlar > Banka hesapları'nda ilgili hesabın
  **eşleştirme anahtarları** alanına girilmeli, yoksa 356 satırın 173'ü onaya düşer.
- **Muavin kodları PKF Aday ölçümünden** (`102 1 5 04`, `102 1 5 07`, `102 1 7 06`,
  `770 03 005`). Hesap planı farklı olan firmada Tanımlar'dan düzeltilmeli; seed mevcut
  kayıtların üzerine yazmaz.
- **Mükerrer yükleme elemesi yok.** Referans saklanıyor ama okunmuyor (KARARLAR §88).
- **Gerçek ekstrelerle uçtan uca çalıştırılmadı**; kaç satırın şablon/kural eşleşmesi
  bulduğu ofiste ölçülecek.

---

# Birikmiş işler turu: Beyannameler, Anasayfa, Firma Bilgileri, Tema

`birikmis-isler-beyanname-anasayfa-firma.md`'deki dört bölümün tamamı.

## 1. Beyannameler → Takip + Özet

`Beyanname Takip` ve `Beyannameler` ayrı menü satırlarıydı; artık tek üst sayfanın
sekmeleri:

```
Beyannameler  (/beyannameler)
  ├── Takip   (/beyannameler/takip)  — eski ekran, içeriği değişmedi
  └── Özet    (/beyannameler/ozet)   — yeni: firma × beyanname türü matrisi
```

Sekmeler `BeyannameSekmesi.Hepsi` listesinden üretiliyor (Hesaplamalar'daki kalıp,
KARARLAR §79): yeni sekme = bir bileşen + listeye bir satır. Eski `/beyanname-takip`
adresi yönlendirme olarak duruyor.

**Özet matrisi.** Satır firma, kolon beyanname türü, kesişimde durum; satır sonunda firma
toplamı, sütun sonunda tür toplamı — kullanıcının Excel'de elle tuttuğu tablonun
karşılığı. Kolonlar **sabit yazılmadı**, `catalog.BeyannameTurleri` tanımlarından geliyor
(kolon başlığında tür adı, altında vergi kodu). Hücre durumu dört değerli: yok /
hazırlandı / onaylandı / ödendi — her biri renk **ve** işaret taşıyor. Hücreye tıklamak
beyannamenin detayını ve belgelerini açıyor. Tanımlarda karşılığı olmayan bir tür varsa
kayıt sessizce düşmüyor, ekran uyarıyor.

**PDF belgeler.** Her beyannameye tahakkuk, beyanname ve (yalnız ödendi işaretliyse)
dekont bağlanabiliyor. Matriste ve takip listesinde üç küçük PDF ikonu: belge varsa dolu,
yoksa soluk. Tıklanınca dosya **tarayıcı içinde** açılıyor (indirme zorunlu değil).
Saklama repodaki mevcut altyapı: dosya FileApiService'te, kayıtta yalnız `FileId` +
metadata (KARARLAR §91). Yalnız PDF, en fazla 20 MB — doğrulama hem istemcide hem
sunucuda.

## 2. Anasayfa

Giriş sonrası varsayılan rota artık `/anasayfa` (eskiden doğrudan Takvim açılıyordu).
Dört kart, hepsi tıklanabilir:

| Kart | Ne gösteriyor | Nereye götürüyor |
|---|---|---|
| Bu ay bekleyen beyanname | Ödemesi tamamlanmamış kayıt sayısı + toplam vergi | Beyannameler > Özet |
| Onay bekleyen ekstre satırı | Firma bazlı sayaç, en çok bekleyen üstte | Banka Otomasyon |
| Yaklaşan son ödeme tarihleri | 15 günlük pencere; gecikmişler kırmızı | Beyannameler > Takip |
| Son kullanılan firmalar | Tarayıcıda tutulan kısayollar | Firma Bilgileri |

Sayıların hepsi mevcut servislerden geliyor; anasayfa kendi hesabını yapmıyor
(KARARLAR §95). Veri yoksa kart boş kalmıyor, ne olduğunu yazıyor.

## 3. Firma Bilgileri

`Yönetim > Firmalarım` satırındaki yeni düğme →
`/yonetim/firmalar/{firmaId}/bilgiler`. Dört bölüm, **her biri ayrı kaydediliyor**:

- **Sicil** — unvan, VKN, vergi dairesi, ticaret sicil no, MERSİS, kuruluş tarihi, adres,
  NACE, e-posta, telefon, sermaye. `catalog.Firmalar`'daki alanlar oraya, yeni alanlar
  modülün kendi tablosuna yazılıyor; kopyalanmadı (KARARLAR §93).
- **Ortaklık** — ad, TCKN/VKN, pay tutarı, pay oranı, başlangıç. Tablo bütün olarak
  kaydediliyor (ekrandan silinen satır sunucuda da siliniyor). Toplam pay oranı %100
  değilse **uyarı** var, kayıt engeli yok.
- **İmza yetkilileri** — ad, TCKN, görev, temsil şekli (münferit/müşterek), yetki
  başlangıç/bitiş. Süresi dolmuş yetkili silinmiyor, görsel olarak ayrılıyor; "süresi
  doldu" kararı sunucuda veriliyor.
- **Belgeler** — imza sirküleri, vergi levhası, faaliyet belgesi, ticaret sicil gazetesi.
  1. bölümdeki PDF altyapısının aynısı, tek farkla: aynı türden **birden çok** belge
  olabiliyor (vergi levhası her yıl yenileniyor).

Kapsam Banka Otomasyon'daki mekanizmanın aynısı: `?firmaId=`, her sorguda görünür
(KARARLAR §94).

## 4. Sol menü teması

Sol menü koyu (`#1f2733`), metin açık, seçili satır hem renkle hem sol kenar çubuğuyla
belirgin. Başlık ve içerik alanı açık kaldı. Bütün renk değerleri `app.css`'te tek bir
`:root` bloğunda; kontrast oranları yorumda yazılı (KARARLAR §96).

## Değişen ve eklenen dosyalar

| Alan | Dosya |
|---|---|
| Beyanname (API) | `Features/Declarations/Entities/BeyannameTuru.cs`, `BeyannameEk.cs`; `Dtos/BeyannameOzetDtos.cs`, `BeyannameEkDtos.cs`; `Services/BeyannameTuruEsleyici.cs`, `BeyannameOzetKurucu.cs`, `BeyannameOzetService.cs`, `BeyannameEkService.cs`, `BeyannameKuralException.cs`; `Controllers/BeyannameOzetController.cs`; `BeyannameTuruSeed.cs` |
| Beyanname (istemci) | `Pages/Beyannameler/*` (üst sayfa, sekme kaydı, Özet sekmesi, eski rota), `Pages/Beyannameler/Components/*`, `Shared/Components/PdfGoruntuleyiciDialog.razor`, `Application/Services/BeyannameOzetApiService.cs` |
| Firma Bilgileri (API) | `Features/FirmaBilgileri/*` (domain, dto, servis, controller) |
| Firma Bilgileri (istemci) | `Pages/Yonetim/FirmaBilgileri/*`, `Application/Services/FirmaBilgiApiClient.cs` |
| Anasayfa | `Features/Anasayfa/*` (API), `Pages/Anasayfa/AnasayfaPage.razor`, `Application/Services/AnasayfaApiClient.cs` (+ `SonFirmalarStore`) |
| Tema | `wwwroot/css/app.css` (koyu menü bloğu), `wwwroot/index.html` (sürüm) |
| Ortak | `CatalogContext` (7 yeni DbSet), iki yeni entity configuration dosyası, `Program.cs` (DI + seed), `MainLayout.razor` (menü), `LoginPage`/`FirmSelectDialog` (varsayılan rota), `DeclarationFollow.razor` (rota kaldırıldı + Belgeler kolonu), `Yonetim/Firmalar.razor` (Bilgiler düğmesi) |

Migration: `BeyannameTurleriVeEkleri`, `FirmaBilgileri` — ikisi de üretildi ve uygulandı.

## Testler

`CatalogService.UnitTests` **682** (öncesi 600), `WebApp.UnitTests` **62** — hepsi geçiyor.
Yeni testler:

- `Beyannameler/BeyannameTuruEsleyiciTests` — yazım çeşitleri, Türkçe harf tuzağı, kod
  eşleşmesi, tanınmayan tür
- `Beyannameler/BeyannameOzetKurucuTests` — kolon üretimi, durum türetimi, aynı hücrede
  iki kayıt, satır/sütun toplamları, eşleşmeyen tür raporu
- `Beyannameler/BeyannameEkServiceTests` — PDF/boyut doğrulaması, dekontun ödeme şartı,
  aynı türden ikinci belgenin eskisinin yerine geçmesi, seed
- `FirmaBilgileri/FirmaBilgiServiceTests` — kapsam izolasyonu, sicil çift tablo yazımı,
  pay oranı uyarısı, süresi dolmuş yetkili, belge kuralları
- `Anasayfa/AnasayfaOzetKurucuTests` — bekleyen sayımı, firma sıralaması ve kırpma,
  yaklaşan ödemelerin sıralanması ve gün hesabı

## Ne eksik kaldı

- **Ekranlar tarayıcıda denenmedi.** Sunucu tarafı testli, istemci derleniyor; görsel
  doğrulama (koyu menü kontrastı, matrisin geniş ekranda görünümü, PDF iframe'i) ofiste
  yapılacak.
- **Beyanname tablosunun kapsamı** hâlâ filtresiz (KARARLAR §92); bu turda bilerek
  değiştirilmedi.
- **Tür tanımları için ekran yok**: `catalog.BeyannameTurleri` seed ile doluyor, yeni tür
  eklemek için şimdilik kayıt eklemek gerekiyor. Tanımlar ekranı istenirse ayrı bir iş.
- **Ortaklık ve imza yetkilisi geçmişi tutulmuyor**: kayıt güncelleniyor, eski hâli
  saklanmıyor. Pay devri geçmişi gerekirse ayrı bir tablo ister.
