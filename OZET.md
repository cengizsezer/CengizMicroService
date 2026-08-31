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

Alt menü de koyu: zemin bir tık daha koyu (`#18202b`), kutu/kart görünümü yok, solda ince
dikey çizgi + girinti var. Metin soluk (`#9aa7b8`), hover'da açılıyor, seçili satırda metin
beyaz ve sol çizgi vurgulu. Radzen'in ikinci/üçüncü seviye token'ları `.app-sidebar`
üzerinde aynı değişkenlere bağlandı; ham renk kodu kurallara dağılmadı (KARARLAR §97).
Menüdeki hiçbir satır gerçekten devre dışı değil — soluk görünen satırlar Radzen'in ikinci
seviye gri metin rengiydi; yetki kontrolü satırı çizmemekle yapılıyor.

## 5. Beyanname türleri: tek kaynak + Tanımlar ekranı

Türler yalnız `catalog.BeyannameTurleri` tablosundan okunuyor. Takip sekmesindeki sabit
liste kaldırıldı; Takip filtresi, yeni/düzenle formu ve Özet matrisi aynı kaynağı görüyor.
Yeni **Tanımlar** sekmesi (`/beyannameler/tanimlar`) tür ekleme/düzenleme ve pasife alma
sağlıyor; "Varsayılanları yükle" düğmesi kurulu veritabanlarında eksik standart tanımları
tamamlıyor.

Açılış seed'leri artık adım adım yalıtık (`Infrastructure/Seeding/SeedAdimi.cs`): bir seed
patlayınca sonrakiler de atlanıyordu, tablonun yayında boş kalma sebebi buydu. Her adım
kendi `try/catch`'inde ve adıyla loglanıyor (KARARLAR §98).

## 6. Firma: oturum bağlamı değil, veri boyutu

Banka Otomasyon'daki firma seçim ekranı ve modül içi firma bağlamı **kaldırıldı** — bir
önceki turun bilinçli olarak geri alınması (KARARLAR §99). Kullanıcı tek oturumla sorumlu
olduğu bütün firmaları yönetiyor; her işlem için firmaya girmiyor.

- **Aktar** tüm firmaların banka hesaplarını gösteriyor; her kartta firma adı. Ekstre bir
  banka hesabına yükleniyor, hesap firmayı belirliyor — ayrıca sorulmuyor.
- **Tanımlar** sayfa geneli tek firma filtresi taşıyor; banka hesapları, öğrenilen
  eşleşmeler ve kişi yönlendirmeleri listelerinde firma kolonu var. Kayıt eklerken firma
  formda seçiliyor (filtreden ayrı bir alan).
- **Onay ekranı** firmasını ekstreden alıyor; başlıkta firma adı yazıyor.
- **Anasayfa** bütün firmaların özetini kırpmadan listeliyor.
- Okuma kapsamsız olabiliyor ("tüm firmalar"); **yazma firma zorunlu** — 400 ve
  `SaveChangesAsync` ile iki kez korunuyor. Yıkıcı ve firma başına anlamlı işlemler
  "tüm firmalar" görünümünde kapalı, sebebi ekranda yazılı.
- **Muhasebe kapsam dışı**: hesap planı `TenantNo` kapsamlı, `FirmaId` değil; bu turda
  hiç değiştirilmedi.

## Değişen ve eklenen dosyalar

| Alan | Dosya |
|---|---|
| Beyanname (API) | `Features/Declarations/Entities/BeyannameTuru.cs`, `BeyannameEk.cs`; `Dtos/BeyannameOzetDtos.cs`, `BeyannameEkDtos.cs`; `Services/BeyannameTuruEsleyici.cs`, `BeyannameOzetKurucu.cs`, `BeyannameOzetService.cs`, `BeyannameEkService.cs`, `BeyannameKuralException.cs`; `Controllers/BeyannameOzetController.cs`; `BeyannameTuruSeed.cs` |
| Beyanname (istemci) | `Pages/Beyannameler/*` (üst sayfa, sekme kaydı, Özet sekmesi, eski rota), `Pages/Beyannameler/Components/*`, `Shared/Components/PdfGoruntuleyiciDialog.razor`, `Application/Services/BeyannameOzetApiService.cs` |
| Firma Bilgileri (API) | `Features/FirmaBilgileri/*` (domain, dto, servis, controller) |
| Firma Bilgileri (istemci) | `Pages/Yonetim/FirmaBilgileri/*`, `Application/Services/FirmaBilgiApiClient.cs` |
| Anasayfa | `Features/Anasayfa/*` (API), `Pages/Anasayfa/AnasayfaPage.razor`, `Application/Services/AnasayfaApiClient.cs` (+ `SonFirmalarStore`) |
| Tema | `wwwroot/css/app.css` (koyu menü + alt menü bloğu), `wwwroot/index.html` (sürüm artırıldı, sürümsüz ikinci `app.css` bağlantısı kaldırıldı) |
| Beyanname türleri (API) | `Features/Declarations/Services/BeyannameTuruService.cs`, `Controllers/BeyannameTurleriController.cs` (yeni); `BeyannameTuruSeed.cs`, `BeyannameOzetService.cs`, `BeyannameOzetController.cs`, `Dtos/BeyannameOzetDtos.cs` (güncellendi); `Infrastructure/Seeding/SeedAdimi.cs` (yeni), `Program.cs` (seed yalıtımı + DI) |
| Firma kapsamı (API) | `Features/BankaEkstre/Kapsam/*` — `BankaFirmaFiltresi` (okuma/yazma ayrımı + firma adı doldurma), yeni `FirmaKapsamiSorgu`, `FirmaAdlari`, `FirmaKapsamiGerekmez`; beş servisin kapsam sorgusu; `Dtos/BankaEkstreDtos.cs` (FirmaId/FirmaAdi + `IFirmaliSatir`); `Anasayfa/Services/AnasayfaOzetKurucu.cs` (kırpma kalktı) |
| Firma kapsamı (istemci) | `Application/Services/BankaEkstreApi.cs` + arayüzü (53 metotta `firmaId` parametresi), yeni `FirmaSecenekleri.cs`; yeni `Bolumler/FirmaFiltresi.razor` ve `Bolumler/FirmaSecici.razor`; `AktarPage`, `TanimlarPage`, `EkstreOnayPage`, `EskiRotaYonlendirme`, on bir `Bolumler/*`, `AnasayfaPage`. **Silinen:** `FirmaSecimPage.razor`, `BankaOtomasyonOturumu.cs`, `IBankaOtomasyonOturumu.cs` |
| Beyanname türleri (istemci) | `Pages/Beyannameler/BeyannameTurleriTab.razor` (yeni), `Application/Services/BeyannameTuruApiService.cs` + arayüzü (yeni); `BeyannameSekmesi.cs`, `BeyannameOzetTab.razor`, `DeclarationFollow.razor`, `DeclarationFormDialog.razor`, `Shared/Dto/DeclarationFollow/BeyannameOzetDtos.cs`, `MainLayout.razor` (güncellendi) |
| Ortak | `CatalogContext` (7 yeni DbSet), iki yeni entity configuration dosyası, `Program.cs` (DI + seed), `MainLayout.razor` (menü), `LoginPage`/`FirmSelectDialog` (varsayılan rota), `DeclarationFollow.razor` (rota kaldırıldı + Belgeler kolonu), `Yonetim/Firmalar.razor` (Bilgiler düğmesi) |

Migration: `BeyannameTurleriVeEkleri`, `FirmaBilgileri` — ikisi de üretildi ve uygulandı.

## Testler

`CatalogService.UnitTests` **696** (öncesi 682), `WebApp.UnitTests` **60** — hepsi geçiyor.
Yeni testler:

- `BankaEkstre/TumFirmalarKapsamiTests` — tek firma kapsamında izolasyon (eski davranış
  aynen), kapsamsız okumada bütün firmalar, her satırın kendi firmasını taşıması, kapsamsız
  yazmanın reddi, silmenin komşu firmaya dokunmaması
- `WebApp.UnitTests/BankaEkstre/FirmaKapsamiIstektenGelirTests` — çağıranın verdiği firmanın
  adrese yansıması, "tüm firmalar"da `firmaId`'nin hiç gönderilmemesi, ardışık çağrıların
  birbirinin firmasını taşımaması

- `Beyannameler/BeyannameTuruServiceTests` — tanım ekleme/güncelleme, benzersiz `Deger`,
  boş alan reddi, pasif tanımın listede çıkmaması, varsayılanların elle yüklenmesi ve
  kullanıcının düzenlediği adın korunması

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

- **Kaldırılan/değişen testler:** `BankaOtomasyonOturumuTests` (7 test) silindi — sınadığı
  oturum tipi kaldırıldı; yerine `FirmaKapsamiIstektenGelirTests` (5 test) geldi.
  `AnasayfaOzetKurucuTests.Toplam_listelenmeyen_firmalari_da_kapsar` kırpmayı doğruluyordu;
  kırpma kalktığı için `Butun_firmalar_listelenir_ve_toplam_hepsini_kapsar` oldu. Firma
  izolasyonu testlerinin hiçbiri değişmedi.
- **Ekranlar tarayıcıda denenmedi.** Sunucu tarafı testli, istemci derleniyor; görsel
  doğrulama (koyu menü kontrastı, matrisin geniş ekranda görünümü, PDF iframe'i) ofiste
  yapılacak.
- **Beyanname tablosunun kapsamı** hâlâ filtresiz (KARARLAR §92); bu turda bilerek
  değiştirilmedi.
- **Vergi Ödeme ekranlarındaki tür listeleri hâlâ sabit**: `TaxPayments.razor` ve
  `CreateOrUpdateTaxPayment.razor` kendi `List<string>`'lerini taşıyor (ve `GECICI`/`GECİCİ`
  yazımları tutarsız). Beyanname sekmeleri tanım tablosuna geçti; Vergi Ödeme ayrı bir modül
  olduğu için bu turda bilerek elle tutulmadı.
- **Ortaklık ve imza yetkilisi geçmişi tutulmuyor**: kayıt güncelleniyor, eski hâli
  saklanmıyor. Pay devri geçmişi gerekirse ayrı bir tablo ister.

---

# SignalR ajan hub'ı (A adımı): sunucu tarafı

Kullanıcı dijitalmasraf.com'dan "ORKA'ya aktar" dediğinde işi ofisteki banka
bilgisayarında çalışan **PkfRobot** yapacak. Sunucu o makineye uzanamıyor (NAT
arkasında, sabit adresi yok), o yüzden **bağlantıyı ajan kuruyor**: SignalR ile
ters yön. Ajan bağlanıp açık tutuyor, iş emri bu kanaldan geriye doğru gidecek.

Bu tur **yalnız sunucu tarafı**: hub, bellekteki ajan listesi, durum ucu, nginx
bloğu ve bağlantıyı kanıtlayan asgari test istemcisi. Windows ajanı (B adımı) ve
Blazor durum göstergesi (C adımı) ayrı turlarda. Bu turda ajan kodu yazılmadı.

Kararlar ve gerekçeleri: **KARARLAR.md §100–103**.

## Nasıl çalışıyor

1. Ajan `wss://dijitalmasraf.com/agenthub` adresine **JWT ile** bağlanır. Hub
   `[Authorize]`; token yoksa ya da geçersizse el sıkışma 401 ile biter, bağlantı
   hiç kurulmaz.
2. Ajan `Kaydol(AjanKaydiIstegi)` çağırır: `MakineId`, `MakineAdi`, `AjanSurumu`,
   `IsletimSistemi`, `OrkaCalisiyorMu`. Sunucu `KayitSonucu` döner:
   `Kabul`, `Mesaj`, `SunucuSurumu`, `AsgariAjanSurumu`.
3. **Sürüm kontrolü kayıttan önce.** Ajan sürümü yapılandırmadaki asgari sürümün
   altındaysa kayıt reddedilir ve mesaj "Lütfen PkfRobot'u güncelleyin" der.
   Reddedilen ajan depoya hiç girmez.
4. Kabul edilen kayıt **bellekte** durur: `ConnectionId`, `MakineId`, `MakineAdi`,
   `AjanSurumu`, `KullaniciId`, `BaglantiZamani`, `SonKalpAtisi`, `OrkaCalisiyorMu`.
   Kaydın sahibi **token'daki** kullanıcı — istekle gelen `MakineId`'ye
   güvenilmiyor.
5. Ajan `KalpAtisi()` ile canlı olduğunu bildirir; `SonKalpAtisi` güncellenir.
   `KalpAtisiZamanAsimiSaniye` boyunca atış gelmezse kayıt listeden düşer.
6. Bağlantı koparsa (`OnDisconnectedAsync`) kayıt silinir. Aynı `MakineId` ile
   ikinci bir bağlantı gelirse **eski soket kapatılır** ve listede tek kayıt kalır.

## Uçlar

| Uç | Yol | Nereden geçiyor |
|---|---|---|
| Hub | `/agenthub` — `Kaydol`, `KalpAtisi` | **nginx → doğrudan `c_catalogservice:5004`** (Ocelot baypas) |
| Durum | `GET /api/catalog/agent/baglilar` | Ocelot: dışarıdan `https://dijitalmasraf.com/catalog/agent/baglilar` |

İkisi de `[Authorize]`. Durum ucu sıradan bir HTTP isteği olduğu için gateway'den
geçmeye devam ediyor; **`ocelot.json`'a dokunulmadı** — mevcut
`/catalog/{everything}` → `/api/catalog/{everything}` kuralı yeni ucu zaten
kapsıyor.

Ajanların hiçbiri bağlı değilken uç boş liste (`[]`) döner.

## Yapılandırma

`Configurations/appsettings*.json` içinde yeni `AgentHub` bölümü:

```json
"AgentHub": {
  "SunucuSurumu": "1.0.0",
  "AsgariAjanSurumu": "1.0.0",
  "KalpAtisiZamanAsimiSaniye": 90
}
```

| Anahtar | Ne işe yarar |
|---|---|
| `SunucuSurumu` | Ajana bildirilen sunucu sözleşme sürümü (ajan kendi ekranında gösterir) |
| `AsgariAjanSurumu` | Bu sürümün altındaki ajanlar reddedilir. Ortam değişkeniyle de verilebilir: `AgentHub__AsgariAjanSurumu=1.2.0` |
| `KalpAtisiZamanAsimiSaniye` | Bu kadar süre atış gelmeyen kayıt "bağlı" sayılmaz |

Docker'da compose'a **yeni servis ya da port eklenmedi**; hub mevcut
`c_catalogservice` container'ının içinde yaşıyor.

## Nginx: `/agenthub` yolu

**Uygulandı — repoda duruyor.** `Nginx/conf.d/dijitalmasraf.conf` dosyasına iki
ekleme yapıldı; elle yapıştırılacak bir şey kalmadı (`deploy/nginx-agenthub.conf`
kaldırıldı, artık kaynağın kendisi conf dosyası).

1. Dosyanın **en başında**, server bloklarının dışında `map $http_upgrade
   $connection_upgrade` (WebSocket el sıkışmasında `Connection: upgrade`, aksi
   halde `close`). `conf.d/*.conf` doğrudan `http` bağlamına dahil edildiği için
   `map` burada geçerli; `nginx.conf`'a dokunulmadı.
2. `dijitalmasraf.com` 443 server bloğunda, `location /catalog/`'un hemen
   altında `location /agenthub` — hedef **değişkene alınmış** olarak
   `resolver 127.0.0.11` + `set $agenthub_upstream http://catalogservice.api:5004;`
   + `proxy_pass $agenthub_upstream;` (URI'siz), 3600 sn timeout,
   `proxy_buffering off`.

> ⚠️ **nginx'te konteyner adını doğrudan `proxy_pass`'e yazmayın.** nginx,
> `proxy_pass`'teki sabit host adını **başlangıçta** çözer. Hedef container
> nginx'ten sonra ayağa kalkarsa çözemez ve
> `[emerg] host not found in upstream "catalogservice.api"` ile **hiç
> başlamaz** — yalnız o yol değil, **tüm site düşer**. Mevcut blokların hepsi
> `web.apigateway` kullandığı için bu risk şimdiye kadar görünmedi; `/agenthub`
> gateway'i baypas ettiği için ilk kez burada patladı.
>
> Çözüm: host adını bir değişkene alın ve Docker'ın gömülü DNS'ini
> (`127.0.0.11`) resolver olarak tanımlayın. `proxy_pass` değişken içerdiğinde
> nginx çözümlemeyi **istek anına** erteler; başlangıçta hata vermez, container
> geç kalırsa yalnız o istek 502 olur. Değişkenli `proxy_pass`'te URI kısmı
> yazılmadığı için istek URI'si olduğu gibi aktarılır — SignalR el sıkışması
> (`/agenthub/negotiate?...`) bozulmaz. Bundan sonra gateway dışı bir container'a
> doğrudan proxy yazan **her yeni blok** aynı kalıbı kullanmalı.

> **Aynı bloğu ikinci kez eklemeyin.** nginx tekrar eden `location`'ı
> `duplicate location` hatasıyla reddeder ve o an ayağa kalkmaz. Aynısı `map`
> için de geçerli — `nginx.conf`'a ikinci bir `map $http_upgrade` yazmayın.

Hedef adres **`catalogservice.api:5004`** (`net_backendservices` ağındaki servis
adı), `c_catalogservice` değil. Ocelot baypas ediliyor; durum ucu
(`/catalog/agent/baglilar`) gateway'den geçmeye devam ediyor, `ocelot.json`'a
dokunulmadı.

### Yayınlama (sunucuda elle)

Conf dosyaları imaja gömülü (bind mount değil), yani **imajın yeniden derlenmesi
gerekiyor**:

```bash
docker compose build nginx.public
docker compose up -d nginx.public
docker exec c_nginx_public nginx -t
```

`nginx -t` hata verirse **reload etmeyin** — hatalı yapılandırmayla reload site
tamamen düşer. Hata varsa önceki imajla geri dönün.

Yapılandırma yerelde gerçekten doğrulandı: Dockerfile'daki `dos2unix` adımı
taklit edilip `nginx -t` bir tek kullanımlık `nginx` container'ında çalıştırıldı
(upstream adları ve sertifikalar sahte) → `syntax is ok / test is successful`.

### Doğrulama

```bash
curl -i -X POST "https://dijitalmasraf.com/agenthub/negotiate?negotiateVersion=1"
```

- **401** → doğru: yol açık, hub yetki bekliyor.
- **404 / 502** → nginx bloğu yerine oturmamış ya da `catalogservice.api`
  erişilemiyor.

Token'lı hâli 200 dönmeli; durum ucu ajan yokken boş liste (`[]`) dönmeli:

```bash
curl -i -X POST "https://dijitalmasraf.com/agenthub/negotiate?negotiateVersion=1&access_token=$TOKEN"
curl -s -H "Authorization: Bearer $TOKEN" https://dijitalmasraf.com/catalog/agent/baglilar
```

Son adım: `tools/AgentHubTestClient` yayındaki adrese karşı bağlanmalı.
**Prod test komutu** (`--durum-yolu` şart, aşağıdaki nota bakın):

```bash
dotnet run --project tools/AgentHubTestClient -- --api https://dijitalmasraf.com \
    --hub https://dijitalmasraf.com/agenthub --durum-yolu /catalog/agent/baglilar \
    --token <jwt>
```

Bu komut yayında çalıştırıldı: hub bağlantısı kuruldu, eski sürüm reddedildi,
geçerli sürümle kayıt kabul edildi. Tek kırılan adım durum ucuydu — istemci
`/api/catalog/agent/baglilar` deniyor ve 404 alıyordu. Dışarıda nginx `/catalog/`
önekini gateway'e veriyor, `/api/catalog/`'a çeviren **gateway** oluyor; yani
dışarıdan doğru yol `/catalog/agent/baglilar`. Bunun için `--durum-yolu`
eklendi. Varsayılanı `/api/catalog/agent/baglilar` olarak kaldı — yerelde
CatalogService'e doğrudan gidildiği için orada `/api/...` doğru yol.


## Test istemcisi

`tools/AgentHubTestClient` — ajanın kendisi değil, **bağlantıyı kanıtlayan asgari
istemci**. FlaUI, ORKA, iş kuyruğu yok.

```bash
# CatalogService ayakta olsun (yerelde 5004)
dotnet run --project tools/AgentHubTestClient -- --api http://localhost:5004

# yayındaki sunucuya karşı — durum ucunun yolu nginx yüzünden farklı
dotnet run --project tools/AgentHubTestClient -- --api https://dijitalmasraf.com \
    --hub https://dijitalmasraf.com/agenthub --durum-yolu /catalog/agent/baglilar \
    --token <jwt>
```

`--durum-yolu` yalnız durum ucunun yolunu değiştirir (varsayılan
`/api/catalog/agent/baglilar`); hub adresi ondan bağımsız, `--hub` ile veriliyor.

Token verilmezse istemci Development imza anahtarıyla kendi token'ını üretir —
doğrulama için IdentityService'i ayağa kaldırmak gerekmiyor. Çalıştığında adımları
sırayla sınıyor ve başarısızlıkta 1 ile çıkıyor.

> **Bu bölüm o günkü hâli anlatıyor: sekiz adım, tek token.** Ajan kimliği
> turundan sonra istemci **iki token** taşıyor (hub yalnız ajan token'ını, durum
> ucu yalnız kullanıcı token'ını kabul ediyor) ve **on adım** sınıyor. Güncel
> kullanım, bayraklar ve gerçek çıktı için "Ajan Kimliği — uzun ömürlü anahtar"
> bölümündeki *Test istemcisi (güncel)* başlığına bakın.

Ayrıca elle: ajan yokken `GET /api/catalog/agent/baglilar` (yayında
`GET /catalog/agent/baglilar`) → `[]`;
`?access_token=` sorgu parametresi yalnız `/agenthub` yolunda kabul ediliyor
(sıradan uçta hâlâ 401), Bearer başlığıyla 200.

`AgentHubTestClient` çözüme (`.sln`) **eklenmedi** — `src/Robot.Agent` gibi ayrı
duruyor, `dotnet run --project` ile çalışıyor. CI docker-compose üzerinden derliyor,
bu proje derlemeye girmiyor.

## Değişen ve eklenen dosyalar

| Yer | Dosyalar |
|---|---|
| Hub (yeni) | `Features/Ajanlar/AgentHub.cs`, `AgentHubAyarlari.cs`, `Domain/AjanKaydi.cs`, `Dtos/AjanDtos.cs`, `Services/IAjanDeposu.cs`, `Services/AjanDeposu.cs`, `Services/SurumKontrolu.cs`, `Controllers/AgentController.cs` |
| Program | `Program.cs` — `AddSignalR()`, `MapHub<AgentHub>("/agenthub")`, `IAjanDeposu` + `TimeProvider` DI, `AgentHubAyarlari` bağlama, JwtBearer'a `OnMessageReceived` (yalnız hub yolunda `?access_token=`) |
| Yapılandırma | `Configurations/appsettings.json`, `appsettings.Development.json`, `appsettings.Docker.json` — `AgentHub` bölümü |
| Dağıtım | `deploy/nginx-agenthub.conf` (yeni) |
| Test istemcisi | `tools/AgentHubTestClient/AgentHubTestClient.csproj`, `Program.cs` (yeni) |
| Testler | `CatalogService.UnitTests/Ajanlar/*` (yeni) |

**Değişmeyenler:** `ocelot.json` (üç ortam), `docker-compose.yml`,
`docker-compose.override.yml`, `Nginx/conf.d/dijitalmasraf.conf`,
`Nginx/nginx.conf`, migration yok (veritabanına dokunulmadı).

## Testler

`CatalogService.UnitTests` **738** (öncesi 696) — hepsi geçiyor. Yeni 42 test:

- `Ajanlar/AjanDeposuTests` — kayıt listede görünüyor, aynı makinenin ikinci
  bağlantısı eskisini düşürüyor, aynı bağlantının ikinci kaydı düşürme sayılmıyor,
  kopan bağlantı siliniyor, **düşürülen soketin kopuş bildirimi yerine geçen kaydı
  silmiyor**, kalp atışı son atışı ilerletiyor, atışı kesilen kayıt listeden
  düşüyor, atışını sürdüren ajan eşik aşılsa da kalıyor
- `Ajanlar/AgentHubTests` — geçerli sürüm kabul + depoda görünme, eski sürüm reddi
  (mesajda asgari sürüm ve "güncelleyin"), okunamayan sürüm reddi, boş `MakineId`
  reddi, sonucun her hâlde sunucu/asgari sürümü taşıması, sahibin token'dan
  gelmesi, ikinci bağlantının eski soketi kapatması, kalp atışı, kopuşta silinme,
  `[Authorize]` ve `/agenthub` yolu
- `Ajanlar/SurumKontroluTests` — eşit/yeni sürüm geçiyor, `1.10.0 ≥ 1.9.0`
  (metin karşılaştırmasının yanılacağı yer), asgarinin altı reddediliyor,
  okunamayan ajan sürümü reddediliyor, **bozuk asgari ayar kimseyi dışarıda
  bırakmıyor**
- `Ajanlar/AgentControllerTests` — boş liste, bütün alanların dönmesi, atışı
  kesilen ajanın listede çıkmaması, `[Authorize]`, rota ön ekinin Ocelot kuralına
  uyması

Zamana bağlı testler sahte saatle (`SahteSaat : TimeProvider`) yazıldı: 90
saniyelik eşiği 90 saniye uyuyarak sınayan bir test, süre uzadıkça ilk atılacak
testtir.

## Ne eksik kaldı

- **Nginx bloğu sunucuya uygulanmadı.** Erişim yok; yukarıdaki talimat elle
  uygulanacak. Uygulanana kadar hub yalnız yerelde ve container ağı içinden
  erişilebilir.
- **Ajanın kendisi yazılmadı (B adımı).** `src/Robot.Agent` (PkfRobot) hâlâ
  SignalR bilmiyor; `Kaydol`/`KalpAtisi` çağıran bağlantı yönetimi, yeniden
  bağlanma ve `MakineId`'nin yerelde saklanması o turda gelecek.
- **Blazor durum göstergesi yok (C adımı).** `baglilar` ucu hazır, ekran yok.
  (Sonraki turda **Yönetim → Ajanlar** ekranı geldi; bağlı ajanları da orada
  gösteriyor.)
- **İş emri gönderimi yok.** Bu turda hub'ın ajana bir şey göndermesi
  tasarlanmadı; kanal açık, sözleşme (hangi metot, hangi paket) B adımının konusu.
- **Ajan listesi tek container'a özgü.** CatalogService birden fazla kopya olarak
  çalıştırılırsa her kopya kendi listesini tutar; ajan yalnız bağlandığı kopyadan
  görünür. Tek makine senaryosunda sorun değil, ölçeklenirse backplane
  (Redis) ya da kalıcı kayıt gerekir.
- **Kimlik yalnız saklanıyor, henüz bir kural kurmuyor.** Sahip alanı kayıtta
  duruyor ama "kim hangi makineye iş gönderebilir" kontrolü yazılmadı — iş emri
  gelmeden dayanacağı bir şey yok. (Bu alan sonraki turda `AjanId` oldu; bkz.
  "Ajan Kimliği — uzun ömürlü anahtar".)

# Ajan Kimliği — uzun ömürlü anahtar

## Sorun

Kullanıcı token'ı **20 dakika** ömürlü (prod'da ölçüldü: `nbf`/`exp` farkı 1200
saniye). Ajan (PkfRobot) ofisteki makinede günlerce bağlı kalacak; kullanıcı
token'ıyla bağlansa yirmi dakikada bir düşerdi. Ayrıca o makine fiziksel olarak
erişilebilir bir yerde — orada kullanıcı parolası ya da uzun ömürlü kullanıcı
token'ı tutmak, bir insanın bütün yetkisini masaüstüne bırakmak demek.

## Çözüm

Ajana özel, kullanıcıdan bağımsız bir kimlik: sunucuda anahtar üretilir, **bir
kez** gösterilir, ajan saklar. Ajan bu anahtarı 8 saatlik bir *ajan token'ına*
çevirip hub'a onunla bağlanır. Anahtar iptal edilebilir.

Neden böyle olduğu (ve elenen alternatifler) `KARARLAR.md` §104–§108'de.

## Veritabanı: `Ajanlar` (IdentityService)

Migration: `20260829232848_AjanKimligi` — yalnız yeni tablo, başka hiçbir tabloya
dokunmuyor. Yerel geliştirme veritabanına uygulandı;
`dotnet ef migrations has-pending-model-changes` → *No changes*.

| Sütun | Not |
|---|---|
| `Id` | |
| `Ad` | Kullanıcının verdiği ad: "Ofis Banka PC" |
| `AnahtarHash` | Ham anahtar **saklanmıyor**; PBKDF2 (`IPasswordHasher<Ajan>`) |
| `AnahtarOnEki` | İlk 8 karakter — listede tanımak ve hash adaylarını daraltmak için (tekil değil) |
| `OlusturanKullaniciId` | |
| `OlusturmaZamani` / `SonKullanim` / `GecerlilikBitisi` | Hepsi **UTC**; `GecerlilikBitisi` null ise süresiz |
| `Aktif` / `IptalZamani` / `IptalNedeni` | |

## Uçlar

Gateway ve nginx yapılandırması **değişmedi** — adresler mevcut kuralların
altına yerleştirildi (§108).

| Uç | Yetki | İş |
|---|---|---|
| `POST /auth/agent/token` | anonim + hız sınırı | `{ AjanAnahtari }` → ajan token'ı (8 saat) |
| `GET /auth/admin/agents` | `role: Admin` | Ajan listesi (ham anahtar dönmez) |
| `POST /auth/admin/agents` | `role: Admin` | Yeni ajan; **ham anahtar yalnız bu yanıtta** |
| `POST /auth/admin/agents/{id}/iptal` | `role: Admin` | `{ Neden }` |
| `GET /catalog/agent/baglilar` | kullanıcı token'ı | Bağlı ajanlar (ajan token'ıyla **403**) |
| `POST /catalog/agent/{ajanId}/dusur` | `role: Admin` | Açık bağlantıyı düşürür |

Hız sınırı: IP başına dakikada 10 istek, aşınca **429** + `Retry-After`.
Başarısız her deneme loglanıyor (anahtar değil, öneki).

## Kimin token'ı nereye giriyor

| | Hub `/agenthub` | Durum ucu `/catalog/agent/baglilar` |
|---|---|---|
| Token yok | 401 | 401 |
| Kullanıcı token'ı | **reddedilir** | çalışır |
| Ajan token'ı | çalışır | **403** |

Ajan token'ı kullanıcı token'ıyla aynı imza/issuer/audience taşıyor; ayrım
claim'lerde: `sub = ajan-<id>`, `typ = agent`, `ajan_id = <id>`. Politika kararı
**`ajan_id`**'ye bakıyor (nedeni §106; `typ`'a güvenmemenin gerekçesi de orada).

## Yönetim ekranı

**Yönetim → Ajanlar** (`/yonetim/ajanlar`, yalnız Admin). Menü satırı da
Admin'e görünüyor.

- Liste: ad, anahtar öneki, durum (Aktif / İptal / Süresi doldu), **bağlı mı**
  (hub'daki listeyle `AjanId` üzerinden eşleştirilir), oluşturan, oluşturma, son
  kullanım, geçerlilik.
- İptal edilen ve süresi dolan satırlar soluk gösteriliyor.
- **Yeni Ajan** → ad (+ istenirse geçerlilik bitişi) → anahtar üretilir ve bir kez
  gösterilir: "Bu anahtarı şimdi kopyalayın, bir daha gösterilmeyecek."
- **İptal** → neden zorunlu. İptalden hemen sonra ekran
  `POST /catalog/agent/{id}/dusur` çağırıp açık bağlantıyı düşürüyor.

Hub okunamazsa liste yine geliyor, tepede uyarı çıkıyor ve "Bağlı" sütununun
güvenilir olmadığı söyleniyor — iki kaynak iki ayrı serviste.

## Anahtar nasıl üretilip ajana verilir (elle yapılacak)

1. Yayındaki panele **Admin** yetkisiyle girin.
2. **Yönetim → Ajanlar → Yeni Ajan**.
3. Ada makineyi tanıtan bir şey yazın: `Ofis Banka PC`. Geçerlilik bitişini boş
   bırakırsanız anahtar süresiz olur; bir tarih verirseniz o tarihte kendiliğinden
   geçersizleşir.
4. **Anahtarı üret** → ekranda `pkfr_…` ile başlayan anahtar çıkar.
   **Kopyala.** Bu ekran kapandıktan sonra anahtar hiçbir yerden okunamaz —
   sunucuda yalnız özeti var.
5. Anahtarı ofis makinesine taşıyın ve ajanın yapılandırmasına koyun. Sohbete,
   e-postaya, ortak dosyaya bırakmayın; bırakıldıysa ajanı iptal edip yenisini
   üretin.
6. Doğrulama — anahtarın gerçekten çalıştığını görmek için (bkz. Test istemcisi):

   ```bash
   dotnet run --project tools/AgentHubTestClient -- --api https://dijitalmasraf.com \
       --hub https://dijitalmasraf.com/agenthub \
       --durum-yolu /catalog/agent/baglilar \
       --token-ucu https://dijitalmasraf.com/auth/agent/token \
       --ajan-anahtari pkfr_... --token <kullanici-jwt>
   ```

7. **Ajanlar** ekranını yenileyin: satırda "Son kullanım" dolmuş olmalı.

Anahtar sızdıysa ya da makine değiştiyse: aynı ekrandan **İptal** → neden yazın.
Anahtar o an geçersizleşir ve ajanın açık bağlantısı düşürülür.

## Test istemcisi (güncel)

`tools/AgentHubTestClient` artık **iki token** taşıyor: hub yalnız ajan
token'ını, durum ucu yalnız kullanıcı token'ını kabul ettiği için.

```bash
# Yerelde: CatalogService 5004'te ayakta olsun. Hiçbir bayrak vermezseniz istemci
# Development imza anahtarıyla iki token'ı da kendi üretir; IdentityService
# gerekmez.
dotnet run --project tools/AgentHubTestClient -- --api http://localhost:5004

# Yayında: ajan token'ı gerçek anahtarla alınır, insan token'ı dışarıdan verilir.
dotnet run --project tools/AgentHubTestClient -- --api https://dijitalmasraf.com \
    --hub https://dijitalmasraf.com/agenthub \
    --durum-yolu /catalog/agent/baglilar \
    --token-ucu https://dijitalmasraf.com/auth/agent/token \
    --ajan-anahtari pkfr_... --token <kullanici-jwt>
```

Yeni bayraklar: `--ajan-anahtari` (anahtarı token'a çevirir), `--token-ucu`
(varsayılan `<api>/api/auth/agent/token`), `--ajan-token` (hazır ajan token'ı),
`--ajan-id` (yerelde üretilen ajan token'ının kimliği).

**Gerçek çıktı** (yerelde, `ASPNETCORE_ENVIRONMENT=Development`,
`http://localhost:5004`, gerçek CatalogService'e karşı):

```
Hub        : http://localhost:5004/agenthub
Durum ucu  : http://localhost:5004/api/catalog/agent/baglilar
Makine     : TEST-ISTEMCI (TEST-8be776fc)
Ajan token : yerelde üretildi
İnsan token: yerelde üretildi
------------------------------------------------------------------------
[ OK ] Durum ucu token'sız istekte 401 dönüyor
      (beklenen hata: HttpRequestException)
[ OK ] Token'sız hub bağlantısı reddediliyor
      (beklenen hata: HttpRequestException)
[ OK ] Hub kullanıcı token'ını kabul etmiyor
[ OK ] Durum ucu ajan token'ını kabul etmiyor
      sunucu mesajı: Ajan sürümü 0.0.1 desteklenmiyor; en az 1.0.0 gerekiyor.
                     Lütfen PkfRobot'u güncelleyin.
      sunucu 1.0.0 / asgari ajan 1.0.0
[ OK ] Eski sürümle kayıt reddediliyor, mesaj anlaşılır
[ OK ] Geçerli sürümle kayıt kabul ediliyor
      TEST-ISTEMCI / 1.0.0 / Microsoft Windows NT 10.0.26200.0 / ajan 9999 / ORKA: bilinmiyor
[ OK ] Ajan 'baglilar' ucunda görünüyor
[ OK ] Kalp atışı son atış zamanını ilerletiyor
[ OK ] Aynı MakineId ile ikinci bağlantı eskisini düşürüyor
[ OK ] Bağlantı kopunca ajan listeden siliniyor
------------------------------------------------------------------------
10 geçti, 0 kaldı. Hub doğrulandı.
```

(Adım adlarının hemen üstünde görünen girintili satırlar, o adımın kendi
çıktısı — konsol satır sırası böyle.)

## Değişen ve eklenen dosyalar

| Yer | Dosyalar |
|---|---|
| IdentityService — kayıt | `Domain/Entities/Ajan.cs`, `Domain/EntityConfigurations/AjanEntityTypeConfiguration.cs`, `Persistence/IdentityDbContext.cs` (DbSet + config), `Migrations/20260829232848_AjanKimligi.*` |
| IdentityService — servis | `Application/Services/Agent/AjanAnahtari.cs`, `AjanClaimleri.cs`, `AjanHizSiniri.cs`, `IAjanKimlikServisi.cs`, `AjanKimlikServisi.cs`, `Application/Models/Agent/AjanModelleri.cs` |
| IdentityService — uçlar | `Controllers/AgentAuthController.cs`, `Controllers/AdminAgentController.cs`, `Program.cs` (hasher + servis + `AddRateLimiter` + `UseRateLimiter`) |
| CatalogService | `Features/Ajanlar/AjanKimligi.cs` (yeni: claim'ler + politikalar), `AgentHub.cs`, `Controllers/AgentController.cs` (+ `dusur`), `Domain/AjanKaydi.cs`, `Dtos/AjanDtos.cs`, `Services/IAjanDeposu.cs` + `AjanDeposu.cs` (`AjanaGoreCikar`), `Program.cs` (`AddAuthorization(AjanPolitikalari.Ekle)`) |
| Blazor | `Pages/Yonetim/Ajanlar.razor`, `AjanOlusturDialog.razor`, `AjanIptalDialog.razor`, `Application/Services/Yonetim/IAjanApiClient.cs` + `AjanApiClient.cs`, `Shared/Dto/Yonetim/AjanDtos.cs`, `Layout/MainLayout.razor` (menü), `StartupExtensions/.../ServiceCollectionExtensions.cs` (DI) |
| Test istemcisi | `tools/AgentHubTestClient/Program.cs` |
| Testler | `Services/IdentityService/IdentityService.UnitTests/**` (yeni proje, `.sln`'e eklendi), `CatalogService.UnitTests/Ajanlar/AjanPolitikalariTests.cs` (yeni) + mevcut ajan testleri |

**Değişmeyenler:** `ocelot.json` / `ocelot.Development.json` / `ocelot.Docker.json`,
`Nginx/**`, `deploy/nginx-agenthub.conf`, `docker-compose*.yml`.

## Testler

| Proje | Önce | Sonra |
|---|---|---|
| `CatalogService.UnitTests` | 745 | **748** |
| `IdentityService.UnitTests` | — (yoktu) | **24** |
| `WebApp.UnitTests` | 60 | 60 |

Hepsi geçiyor. Yeni testler:

- `IdentityService.UnitTests/Ajanlar/AjanKimlikServisiTests` — anahtar üretimi ve
  yalnız hash'in saklanması, her anahtarın farklı olması, geçerli anahtarla token
  (claim'ler + 8 saatlik ömür), aynı imza/issuer/audience ile doğrulanabilirlik,
  geçersiz anahtar reddi, **öneki tutan ama gövdesi tutmayan anahtarın reddi**,
  iptal edilmiş anahtar, ikinci iptalin ilk kaydı bozmaması, olmayan ajanın iptal
  edilememesi, süresi dolmuş anahtar, süresiz anahtar, **aynı anahtarla iki kez
  token** (yeniden bağlanma), `SonKullanim` ilerlemesi, liste durumları, listenin
  ham anahtar döndürmemesi, boş adın reddi
- `IdentityService.UnitTests/Ajanlar/AjanAnahtariSizmiyorTests` — kabul/ret/iptal
  yollarının hiçbirinde ham anahtarın **ve hash'inin** log satırlarına
  (biçimlenmiş metin *ve* yapılandırılmış alanlar) girmediği
- `IdentityService.UnitTests/Ajanlar/AjanUclariTests` — uç adreslerinin gateway
  kurallarına uyması, token ucunun anonim olması ve hız sınırı taşıması, yönetim
  ucunun Admin istemesi
- `CatalogService.UnitTests/Ajanlar/AjanPolitikalariTests` — iki politikanın
  gerçek `IAuthorizationService` ile davranışı, kimliksiz isteğin ikisinden de
  geçememesi, ve **basılıp doğrulanmış** token'da `ajan_id`'nin adının değişmeden
  çıkması
- `CatalogService.UnitTests/Ajanlar/AgentControllerTests` — düşürme ucunun
  yetkisi, ajanın açık bağlantısını kapatması, başka ajana dokunmaması

## Ne eksik kaldı

- ~~**Ajanın kendisi hâlâ yazılmadı (B adımı).**~~ Sonraki turda yazıldı:
  `PkfRobot.exe --ajan` anahtarı okuyup token alıyor ve hub'a bağlanıyor.
  Bkz. "PkfRobot Ajan Bağlantısı (B adımı)".
- **Prod'da denenmedi.** Yeni uçlar ve ekran yayına çıkmadı; yukarıdaki adım adım
  yönerge ilk çalıştırmada izlenecek. Migration prod veritabanına
  uygulanmalı (servis açılışta `MigrateDbContext` ile kendisi uyguluyor).

  **Dağıtım sırası: önce IdentityService, sonra CatalogService.** CatalogService
  yeni hâliyle hub'a yalnız ajan token'ı alıyor; token'ı basan uç ise
  IdentityService'te. Ters sırada dağıtılırsa hub'a hiç kimse bağlanamayacağı bir
  aralık doğar. Bugün gerçek bir ajan bağlanmadığı için (B adımı yazılmadı) bu
  aralığın pratik bir bedeli yok, ama sıra yine de doğru olsun.
- **İptal yönetim ekranı dışından yapılırsa bağlantı hemen düşmez** — en geç ajan
  token'ının ömrü (8 saat) dolunca düşer. Gerekçe §107.
- **"Kim hangi makineye iş gönderebilir" kuralı hâlâ yok.** `AjanId` kayıtta
  duruyor ama iş emri olmadan dayanacağı bir şey yok.
- **Ajan listesi tek container'a özgü** (önceki turdan devam): CatalogService
  ölçeklenirse backplane gerekir.

# PkfRobot Ajan Bağlantısı (B adımı)

## Kapsam

Bu tur **yalnız bağlantı**: ajan hub'a bağlanıyor, kendini tanıtıyor, kalp atışı
gönderiyor, ORKA durumunu bildiriyor. **İş alma ve çalıştırma yok** — `GridDoldur`,
iş kuyruğu, ilerleme bildirimi C adımında. JSON adım motoruna dokunulmadı.

Kararlar ve elenen alternatifler `KARARLAR.md` §109–§113'te.

## Nereye yerleşti

`--ajan`, `--probe` / `--kalibre` / `--gorev` gibi ayrı bir **mod**. ORKA'ya
dokunmuyor, görev JSON'u istemiyor, `UIA3Automation` açmıyor.

```
PkfRobot.exe --ajan                    # bagla ve bagli kal (Ctrl+C ile dur)
PkfRobot.exe --anahtari-sifirla        # kayitli anahtari sil, yenisini sor
```

| Dosya | İş |
|---|---|
| `Ajan/AjanKimlikDeposu.cs` | Anahtar (DPAPI) + kalıcı `MakineId`, `%AppData%\PkfRobot` |
| `Ajan/AjanTokenSaglayici.cs` | Anahtar → 8 saatlik token; erken yenileme, 401/429 |
| `Ajan/HubBaglantisi.cs` | `IHubBaglantisi` + SignalR sarmalayıcı, `ws→http` çevirisi |
| `Ajan/AjanServisi.cs` | Bağlan, `Kaydol`, kalp atışı, ORKA bildirimi, yeniden bağlanma |
| `Ajan/GeriCekilme.cs` | 5s → 10s → 30s → 60s → 60s… |
| `Ajan/OrkaDurumu.cs` | `OrkaWinIceberg.64` süreci ayakta mı |
| `Ajan/AjanLog.cs` | Günlük log dosyası + `pkfr_`/JWT maskesi |
| `Ajan/AjanCalistirici.cs` | `--ajan` modunun giriş noktası, ilk kurulum sorusu |
| `Core/Hassas.cs` | Maskelenecek alan adları (`sifre`, `anahtar`, `token`, `agent`) |

## Ayarlar (`appsettings.json` > `Ajan`)

Kaynak dosya güncellendi — publish'te ezilmez.

```json
"Ajan": {
  "TokenUcu": "https://www.dijitalmasraf.com/auth/agent/token",
  "HubAdresi": "https://www.dijitalmasraf.com/agenthub",
  "KalpAtisiSaniye": 30,
  "TokenYenilemeEsigiDakika": 30,
  "LogSaklamaGun": 14,
  "OrkaSurecAdi": "OrkaWinIceberg.64"
}
```

`HubAdresi`'ne `wss://` de yazılabilir; istemci `https://`'e çeviriyor (SignalR
önce HTTP ile "negotiate" yapıyor). **Ajan anahtarı burada değil** — ilk
çalıştırmada sorulup `%AppData%\PkfRobot\agent.dat` içine şifreli yazılıyor.

## Diskte ne duruyor

```
%AppData%\PkfRobot\
  agent.dat                  ajan anahtari (DPAPI, CurrentUser) -- duz metin DEGIL
  makine.dat                 kalici GUID; MakineId = <makine adi>-<guid>
  logs\ajan-<tarih>.log      gunluk log, 14 gunden eskiler silinir
```

Publish klasörü değil, `%AppData%`: publish üzerine yazıldığında kaybolmasın
(§109).

## İlk kurulum — adım adım

### 1. Sunucudan anahtar al

1. Panele **Admin** yetkisiyle gir → **Yönetim → Ajanlar → Yeni Ajan**.
2. Ada makineyi tanıtan bir şey yaz: `Ofis Banka PC`.
3. **Anahtarı üret** → `pkfr_…` ile başlayan anahtar bir kez gösterilir.
   **Kopyala.** Bu ekran kapandıktan sonra hiçbir yerden okunamaz.

### 2. Publish'i ofis makinesine kopyala

Ev PC'sinde:

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

`bin\Release\net8.0-windows\win-x64\publish\` klasörünü olduğu gibi kopyala
(`.pdb` gerekmez). Ofiste .NET SDK kurulmasına gerek yok. Önerilen yer:

```
C:\PkfRobot\
```

Yeni sürüm gönderirken bu klasörün üzerine yazmak yeterli — anahtar ve makine
kimliği `%AppData%` altında olduğu için kaybolmaz.

### 3. Anahtarı bir kez gir

Ofis makinesinde bir komut satırı aç:

```
cd C:\PkfRobot
PkfRobot.exe --ajan
```

İlk çalıştırmada anahtarı sorar. **Yapıştır ve Enter.** Anahtar ekranda
yıldızla görünür, diske şifreli yazılır, bir daha sorulmaz. Ardından:

```
[BILGI] Ajan anahtari kaydedildi: C:\Users\...\AppData\Roaming\PkfRobot\agent.dat (DPAPI ile sifreli).
[BILGI] Ajan token'i alindi: Ofis Banka PC (#3), bitis ... UTC.
[BILGI] Hub'a baglanildi: BANKA-PC (BANKA-PC-<guid>), surum 1.0.0, ORKA: acik.
```

Panelde **Yönetim → Ajanlar** ekranını yenile: satırda **Bağlı** rozetini ve
makine adını görmelisin.

Anahtar yanlış girilirse ya da sonradan iptal edilirse ajan susmadan denemeye
devam etmez; şunu yazıp durur:

```
Ajan anahtari gecersiz veya iptal edilmis. Yonetim > Ajanlar ekranindan yeni
anahtar uretin ve PkfRobot.exe --ajan --anahtari-sifirla ile girin.
```

### 4. Windows açılışında kendiliğinden çalışsın

Ajan **oturum açık** çalışmalı (robotun geri kalanı UI Automation kullanıyor ve
kilitli oturumda çalışmıyor), o yüzden Windows servisi değil, **oturum açılışında
başlayan görev** doğru olan.

**Görev Zamanlayıcı ile (önerilen)** — yönetici komut satırında tek satır:

```
schtasks /Create /TN "PkfRobot Ajan" /TR "C:\PkfRobot\PkfRobot.exe --ajan" ^
  /SC ONLOGON /RL LIMITED /F
```

- `/SC ONLOGON` → kullanıcı oturum açınca başlar.
- `/RL LIMITED` → yönetici olarak çalıştırmaz. DPAPI anahtarı **kullanıcıya**
  bağlı olduğu için, ajanı anahtarı giren kullanıcıyla aynı hesapta çalıştır;
  başka bir hesapta `agent.dat` çözülemez ve anahtar yeniden sorulur.
- Görevin özelliklerinden **"Görev başarısız olursa yeniden başlat"**ı açmak
  iyi olur; ajan zaten kendi içinde sonsuz yeniden bağlanıyor, bu yalnız
  sürecin kendisi çökerse işe yarar.

**Alternatif (daha basit):** `PkfRobot.exe`'ye sağ tık → kısayol oluştur,
kısayolun hedefine ` --ajan` ekle ve kısayolu şuraya at:

```
%AppData%\Microsoft\Windows\Start Menu\Programs\Startup
```

### 5. Çalıştığını nereden anlarsın

- Panel: **Yönetim → Ajanlar** → satırda "Bağlı" rozeti, makine adı ve sürüm.
- Makinede: `%AppData%\PkfRobot\logs\ajan-<tarih>.log`

Bağlantı durumu, kalp atışı, token yenileme ve hatalar bu dosyaya düşüyor.
**Ajan anahtarı log'a hiç düşmüyor** — düşse bile `pkfr_***` olarak maskeleniyor.

### Sorun çıkarsa

| Belirti | Bak |
|---|---|
| "Ajan anahtari gecersiz veya iptal edilmis" | Panelde ajan iptal mi? Yeni anahtar üret, `--anahtari-sifirla` ile gir |
| "Sunucuya ulasilamiyor" tekrar ediyor | Ağ / `appsettings.json > Ajan.HubAdresi`; ajan kendiliğinden denemeye devam eder |
| "Sunucu kaydi reddetti … guncellenmeden" | Sürüm eski; yeni publish'i kopyala |
| Panelde görünmüyor ama log'da "Hub'a baglanildi" var | Panelde yanlış ajana bakıyor olabilirsin; log'daki `#<id>` ile satırı eşleştir |
| Anahtar soruluyor ama girmiştin | Ajan farklı bir Windows kullanıcısıyla çalışıyor; DPAPI kullanıcıya bağlı |

## Gerçek doğrulama (ev PC'si, yerel sunucular)

Ajan **gerçekten çalıştırıldı**: IdentityService (5005) ve CatalogService (5004)
yerelde ayağa kaldırıldı, anahtar gerçek yönetim ucundan
(`POST /api/auth/admin/agents`) üretildi, ajan `--ajan` ile başlatıldı.
Denemede kalp atışı aralığı 5 saniyeye çekildi (varsayılan 30) — beklemeler
kısalsın diye; başka hiçbir ayar değişmedi. ORKA yok, dolayısıyla `ORKA: kapali`.

**1) Bağlanma ve `baglilar` ucunda görünme**

```
[BILGI] Ajan anahtari kaydedildi: C:\Users\...\AppData\Roaming\PkfRobot\agent.dat (DPAPI ile sifreli).
[BILGI] PkfRobot ajan modu. Surum 1.0.0.
[BILGI] Makine  : DESKTOP-I290HP6 (DESKTOP-I290HP6-ad9e26be8a644d02b1a5f1141a3e7c46)
[BILGI] Ajan token'i alindi: Ev PC (B adimi) (#2), bitis 2026-08-30 08:38 UTC.
[BILGI] Hub'a baglanildi: DESKTOP-I290HP6 (...), surum 1.0.0, ORKA: kapali.
```

```
GET /api/catalog/agent/baglilar
[ { "makineId": "DESKTOP-I290HP6-ad9e26be8a644d02b1a5f1141a3e7c46",
    "makineAdi": "DESKTOP-I290HP6", "ajanSurumu": "1.0.0",
    "ajanId": "2", "orkaCalisiyorMu": false,
    "baglantiZamani": "...00:38:43", "sonKalpAtisi": "...00:39:08" } ]
```

Son kalp atışı bağlantı zamanından 25 saniye sonra: atışlar gerçekten gidiyor.

**2) Sunucu düşüp geri gelince kendiliğinden bağlanma** — CatalogService süreci
öldürüldü, sonra yeniden başlatıldı:

```
03:39:28 [UYARI] Baglanti hatasi: The 'InvokeCoreAsync' method cannot be called if the connection is not active
03:39:28 [BILGI] 5 sn sonra yeniden baglanilacak.
03:39:37 [UYARI] Baglanti hatasi: No connection could be made because the target machine actively refused it. (localhost:5004)
03:39:37 [BILGI] 10 sn sonra yeniden baglanilacak.
03:39:52 [UYARI] Baglanti hatasi: No connection could be made because the target machine actively refused it. (localhost:5004)
03:39:52 [BILGI] 30 sn sonra yeniden baglanilacak.
03:40:22 [BILGI] Hub'a baglanildi: DESKTOP-I290HP6 (...), surum 1.0.0, ORKA: kapali.
```

Geri çekilme 5 → 10 → 30 diye açıldı, sunucu gelince bağlandı. Ardından
`baglilar` **tek** kayıt döndü — `MakineId` sabit kaldığı için hayalet kayıt yok.

**3) Kapatılınca listeden düşme:** ajan süreci durduruldu, `baglilar` → `[]`.

**4) İkinci çalıştırma:** anahtar sorulmadı, `MakineId` birebir aynı geldi
(`DESKTOP-I290HP6-ad9e26be8a644d02b1a5f1141a3e7c46`).

**5) Anahtar sızıntısı:** çalıştırma sonrası hem log dosyası hem konsol çıktısı
gerçek anahtara karşı tarandı — anahtar yok, `pkfr_` hiç geçmiyor. `agent.dat`
düz metin değil.

**6) Publish:** `dotnet publish -c Release -r win-x64 --self-contained true
-p:PublishSingleFile=true` → tek `PkfRobot.exe` (~164 MB) + `appsettings.json` +
`gorevler\`. Exe'nin yanında ayrı DLL çıkmıyor; SignalR ve DPAPI paketleri de
gömülü. Yayınlanan exe `--yardim` ile çalıştırıldı, `--ajan` bölümü göründü.

> **Denenmeyen:** ORKA durum değişiminin gerçek ORKA ile bildirilmesi. Ev
> PC'sinde ORKA yok ve sahte bir süreçle taklit edilmedi — kural birim testinde
> sınandı, gerçeği ofiste görülecek.

## Testler

`src/Robot.Agent.UnitTests` (yeni proje, çözüme eklendi; `PkfRobot`'un kendisi
hâlâ çözümde değil, referansla derleniyor) — **49 test**, hepsi geçiyor.

| Konu | Ne sınanıyor |
|---|---|
| `AjanTokenSaglayiciTests` | İlk alma, taze token varken ağa çıkmama, **eşiğin altına inince yenileme**, eşik aşılmadan yenilememe, **401'de yeniden denememe ve mesajın anlaşılır olması**, **429'da `Retry-After` kadar bekleme**, başlık yoksa 60 sn, 500'ün geçici sayılması, anahtarın log'a düşmemesi |
| `AjanServisiTests` | Kendini tanıtma alanları, her turda kalp atışı, **ORKA durumunun yalnız değişimde bildirilmesi**, ORKA kapalıyken bağlı kalma, eski sürüm reddinin kalıcı sayılması ve döngünün durması, anahtar geçersizse durma, **geri çekilmenin 5→10→30 açılması**, başarılı bağlantıdan sonra **sıfırlanması** |
| `AjanKimlikDeposuTests` | Anahtarın yazılıp okunması, **diske düz metin yazılmaması**, silme, bozuk dosyanın `null` dönmesi, **`MakineId`'nin iki çalıştırmada aynı kalması**, farklı klasörde farklı olması |
| `AjanLogMaskesiTests` / `HassasTests` | `pkfr_` ve JWT maskeleme, sıradan metne dokunmama; alan adına göre maskeleme listesi |
| `AjanDosyaLogTests` | Günlük dosyaya yazma + maskeleme, 14 günden eskilerin silinmesi |
| `GeriCekilmeTests` | 5/10/30/60 ve 60'ta kalma, sıfırlama |
| `HubAdresiTests` | `wss://`→`https://`, `ws://`→`http://`; sürümün derlemeden okunması |

Çözümdeki diğer testler aynen geçiyor: CatalogService 748, IdentityService 24,
WebApp 60, Sovos 1.

## Değişen ve eklenen dosyalar

| Yer | Dosyalar |
|---|---|
| Ajan (yeni) | `src/Robot.Agent/Ajan/AjanKimlikDeposu.cs`, `AjanTokenSaglayici.cs`, `HubBaglantisi.cs`, `AjanServisi.cs`, `GeriCekilme.cs`, `OrkaDurumu.cs`, `AjanLog.cs`, `AjanCalistirici.cs` |
| Ajan (değişen) | `Program.cs` (`--ajan`, `--anahtari-sifirla`, yardım), `Config/RobotConfig.cs` (`AjanAyar`), `Core/AdimMotoru.cs` (maskeleme), `Core/Hassas.cs` (yeni), `appsettings.json` (`Ajan` bölümü), `PkfRobot.csproj` (SignalR + ProtectedData paketleri, `<Version>1.0.0</Version>`) |
| Testler | `src/Robot.Agent.UnitTests/**` (yeni), `SmartExpenseSystem.sln` |

**Değişmeyenler:** sunucu tarafı (CatalogService, IdentityService), gateway,
nginx, JSON adım motoru ve `gorevler\*.json`.

## Ne eksik kaldı

- **Ofiste denenmedi.** Ev PC'sinde ORKA yok; ORKA durum bildirimi yalnız birim
  testinde doğrulandı. İlk ofis çalıştırmasında ORKA açıkken/kapalıyken panelde
  durumun değiştiği görülmeli.
- ~~**İş alma yok (C adımı).**~~ Sonraki turlarda geldi: iş kuyruğu, ilerleme ve
  gerçek ORKA aktarımı için bkz. "İş Gönderme ve ORKA Aktarımı (C ve D adımları)".
- **Sürüm elle artırılıyor.** `AjanSurumu` derlemeden okunuyor ama `<Version>`'ı
  yükseltmek hâlâ insanın işi; sunucudaki asgari sürümü yükseltmeden önce yeni
  publish'in ofise gitmiş olması gerekiyor.
- **Kopuş en geç bir kalp atışı aralığı (30 sn) sonra fark ediliyor.** Sunucu
  tarafı kopuşu anında görüyor, bu gecikme yalnız yeniden bağlanmayı geciktiriyor.
- **Çözüm artık Linux'ta derlenmiyor** (§113): test projesi `.sln`'e eklendiği
  için `PkfRobot` da referansla derlemeye giriyor, o da `net8.0-windows`. CI
  docker-compose kullandığından etkilenmiyor.

# İş Gönderme ve ORKA Aktarımı (C ve D adımları)

C adımı iş akışını ORKA'ya dokunmadan uçtan uca kurdu; D adımı sahte işin yerine
gerçek ORKA aktarımını koydu. Bağlantı katmanı (B adımı) değişmedi.

Kararlar ve elenen alternatifler `KARARLAR.md` §114–§125'te.

## Akışın tamamı

```
Aktar ekrani                 CatalogService                  PkfRobot (ofis)
------------                 --------------                  ---------------
"ORKA'ya Aktar"  ── POST /catalog/agent/is ──▶ AjanIsleri (Bekliyor)
                                │  yuku SUNUCU kurar
                                │  ajan bagliysa ──── IsGonder ────▶ is alinir
                                                                      │
                             IsBasladi ◀──────────────────────────────┤
                                                                      │ 2 dosya indirilir
                                                                      │ on dogrulamalar
                             IsIlerleme ◀─────────────────────────────┤ ORKA surulur
  2 sn'de bir GET  ◀── durum ──┤                                      │ GridDoldur
                             IsBitti ◀────────────────────────────────┘ (Kaydet YOK)
```

## Veritabanı

| Migration | Ne |
|---|---|
| `20260830081607_AjanIsleri` | `catalog.AjanIsleri` — işin tamamı; kapsam `FirmaId` |
| `20260830085616_FirmaOrkaKodu` | `catalog.Firmalar.OrkaFirmaKodu` (nullable, 20) |

İkisi de yerel geliştirme veritabanına uygulandı;
`dotnet ef migrations has-pending-model-changes` → *No changes*.

`AjanIsleri` sütunları: `Id (Guid)`, `AjanId`, `FirmaId`, `IsTipi`, `Yuk (JSON)`,
`Durum`, `IlerlemeYuzde`, `IlerlemeMesaji`, `ToplamAdim`, `TamamlananAdim`,
`OlusturanKullaniciId`, `OlusturmaZamani`, `GonderimZamani`, `BaslamaZamani`,
`BitisZamani`, `SonIlerlemeZamani`, `HataMesaji`, `SonucOzeti (JSON)`,
`HataEkraniDosyaId`.

Durumlar: `Bekliyor → Gonderildi → Calisiyor → Tamamlandi | Basarisiz |
IptalEdildi | ZamanAsimi`.

## Uçlar

Gateway ve nginx yapılandırması **değişmedi**; hepsi `/catalog/{everything}`
kuralından geçiyor.

| Uç | Yetki | İş |
|---|---|---|
| `POST /catalog/agent/is` | insan | İş oluştur + gönder. Ajan meşgulse **409** |
| `GET /catalog/agent/is/{id}` | insan | Durum (ekran 2 sn'de bir yokluyor) |
| `GET /catalog/agent/isler` | insan | Liste (`firmaId`, `durum`, `ajanId` süzgeçli) |
| `POST /catalog/agent/is/{id}/iptal` | insan | İptal + ajana bildir |
| `GET /catalog/agent/is/{id}/ekstre` | **ajan** | Düzeltilmiş ekstre (xlsx) |
| `GET /catalog/agent/is/{id}/kod-listesi` | **ajan** | Karşı hesap kodları (JSON) |

Hub'a eklenen metotlar — sunucu→ajan: `IsGonder`, `IsIptal`; ajan→sunucu:
`IsBasladi`, `IsIlerleme`, `IsBitti`.

**Ajanın dosya uçları işine bağlı** (§119): iş kimliği token'daki ajana ait
değilse 404, iş bitmişse 409. Firma kapsamı istekten değil işten kuruluyor.

## Kimin token'ı nereye giriyor (güncel)

CatalogService'in **varsayılan** politikası artık "insan" (§118): politika
yazılmamış her `[Authorize]` ajan token'ını reddediyor.

| | Ajan token'ı | Kullanıcı token'ı |
|---|---|---|
| `/agenthub` | ✔ | ✘ 403 |
| `/catalog/agent/baglilar`, `/isler`, `/is/...` | ✘ 403 | ✔ |
| `/catalog/agent/is/{id}/ekstre` ve `/kod-listesi` | ✔ (kendi işi) | ✘ 403 |
| Diğer bütün catalog uçları | ✘ 403 | ✔ |

## Kurallar

- **Aynı ajana tek iş** — ikinci istek 409, çalışan iş yanıtta. Kural ajan
  tarafında bir kez daha var (§115).
- **Ajan bağlı değilse** iş `Bekliyor` kalıyor; ajan bağlanıp kaydı kabul
  edilince sıradan alınıyor. Sıradaki bekleyen iş, önceki iş bittiğinde ya da
  iptal edildiğinde de kendiliğinden gönderiliyor.
- **Yalnız kendi işini bildirebilir** — sahiplik sorgunun içinde, başka ajanın
  işi hiç yüklenmiyor.
- **Tekrarlanan bildirim zararsız** — yüzde geriye gitmiyor, bitmiş işin durumu
  bir daha değişmiyor.
- **Zaman aşımı** varsayılan 15 dk (`AgentHub:IsZamanAsimiDakika`), son ilerleme
  bildirimine bakıyor, okuma anında işaretleniyor.
- **Bağlantı koparsa** çalışan iş `Basarisiz` — mesajda "ORKA'da kaydedilmemiş
  giriş kalmış olabilir".
- **Kaydet'e ASLA basılmıyor.**

## Ekranlar

**Banka Otomasyon → Aktar:** çözülmüş her yükleme satırında **"ORKA'ya Aktar"**
düğmesi. Düğmenin rengi ve ipucu ajanın bağlı olup olmadığını söylüyor; bağlı
değilken de basılabiliyor (iş sıraya girer). Basınca durum kartı açılıyor:
ilerleme çubuğu, yüzde, mesaj, geçen süre, **İptal**. Bitince sonuç özeti —
başarılıysa *"175 satır yazıldı. ORKA'da kontrol edip Kaydet'e basın."*,
başarısızsa hata mesajı.

**Yönetim → Ajanlar:** ajan listesinin altına **Son işler** tablosu (tarih, tip,
ajan, durum, ilerleme, not).

## Ajan tarafı

`IIsCalistirici` arayüzü ve iki uygulaması:

| Tip | Ne yapar |
|---|---|
| `SahteAktarim` | ORKA'ya dokunmadan 10 adım, adım başına 1 sn (C adımı) |
| `OrkayaAktar` | Gerçek aktarım (D adımı) |

Bağlantı katmanı iş tipini tanımıyor; D adımında **yalnız** ikinci uygulama
eklendi.

### `OrkayaAktar` akışı

1. İş paketi çözülür (`EkstreYuklemeId`, `FirmaKodu`, `BankaHesabiOrkaKodu`,
   `SatirSayisi` — hepsini sunucu doldurdu)
2. İki dosya `%AppData%\PkfRobot\isler\{isId}\` altına indirilir
3. **Ön doğrulamalar** — biri tutmazsa ORKA'ya hiç dokunulmaz (§121)
4. `gorevler/orkaya-aktar.json` çalıştırılır; `GridDoldur` kod listesini yazar
5. Sonuç: `{ YazilanSatir, ToplamSatir, SureSaniye, KaydetBasilmadi: true }`
6. Başarılıysa klasör silinir; başarısızsa 7 gün durur (§124)

Hata durumunda ekran görüntüsü FileApiService'e yüklenir ve kimliği işe yazılır;
mesajın sonunda *"ORKA'da yarım kalmış giriş olabilir; KAYDETMEDEN ekranı
kapatın."*

### Yeni ayarlar (`appsettings.json > Ajan`)

```json
"IsUcuKoku": "https://www.dijitalmasraf.com/catalog/agent",
"DosyaYuklemeUcu": "https://www.dijitalmasraf.com/file/v1/uploads"
```

## Gerçek doğrulama (ev PC'si, yerel sunucular)

### C adımı — uçtan uca çalıştırıldı

IdentityService (5005) + CatalogService (5004) yerelde, anahtar gerçek yönetim
ucundan, ajan `--ajan` ile. Denemede kalp atışı 5 sn'ye çekildi (varsayılan 30).

**1) İş gönderildi ve çalıştı:**

```
HTTP 200 | İş ajana gönderildi.
isId: 9108e729-… | durum: Gönderildi
  Çalışıyor    %10   Sahte adim 1/10
  Çalışıyor    %20   Sahte adim 2/10
  …
  Tamamlandı   %100  Sahte adim 10/10
  ozet: {"Adim":10,"Sahte":true,"KaydetBasilmadi":true}
```

**2) Aynı ajana ikinci iş:**

```
HTTP 409 | Bu ajanda hâlâ süren bir iş var. Bitmesini bekleyin ya da iptal edin.
cakisan is: adb3a54b-… Çalışıyor
```

**3) İptal:** `HTTP 200 | durum: İptal edildi` — ajan log'unda
`[UYARI] Is iptal edildi: adb3a54b-…`

**4) Ajan kapalıyken iş:**

```
HTTP 200 | Ajan şu anda bağlı değil. İş sıraya alındı; ajan bağlanınca çalışacak.
isId: 28390e81-… | durum: Bekliyor
```

Ajan başlatıldı → iş kendiliğinden gönderildi, %10'dan %100'e ilerleyip
tamamlandı.

**5) Ajan iş ortasında öldürüldü:**

```
Başarısız    %30   Sahte adim 3/10
hata: Ajan bağlantısı koptu; iş yarım kaldı. ORKA'da kaydedilmemiş giriş
      kalmış olabilir, kaydetmeden kontrol edin.
```

### D adımı — sunucu tarafı çalıştırıldı, ORKA akışı çalıştırılmadı

**İş paketi doğrulamaları** (canlı, gerçek uçtan):

```
OrkayaAktar + olmayan ekstre  → HTTP 400 | Ekstre yüklemesi bulunamadı.
OrkayaAktar + ekstresiz istek → HTTP 400 | Aktarılacak ekstre seçilmedi.
```

**Yetki sınırları** (canlı):

```
ajan  -> /agent/isler                : 403
ajan  -> /agent/baglilar             : 403
insan -> /agent/is/<id>/ekstre       : 403
ajan  -> /agent/is/<baskasinin>/ekstre: 404
ajan  -> /api/catalog/banka-ekstre/ekstre : 403   (varsayilan politika)
insan -> /api/catalog/banka-ekstre/ekstre : 200
```

> **Çalıştırılmayan:** ORKA'nın kendisi. Ev PC'sinde ORKA yok ve sahte bir
> pencereyle taklit edilmedi. `GridDoldur`, giriş zinciri ve Veri Transferi
> koordinatları **ofiste** doğrulanacak — kontrol listesi aşağıda.

## Ofis doğrulama kontrol listesi (D adımı)

Sırayla, **test firmasında** ve önce **tek satırlık** bir ekstreyle.

### Hazırlık

- [ ] Firmanın ORKA kodu girildi mi? **Yönetim → Firmalarım → firma → ORKA Firma
      Kodu** (ör. `0001`). Boşsa iş oluşturulmaz, ekranda söylenir.
- [ ] Banka hesabının ORKA kodu dolu mu? (Banka Otomasyon → Tanımlar)
- [ ] Ofis makinesinde yeni publish var mı, `PkfRobot.exe --ajan` çalışıyor mu?
- [ ] Panelde **Yönetim → Ajanlar** satırında "Bağlı" rozeti görünüyor mu?
- [ ] `appsettings.json > Giris.Sifre` / `FirmaSifresi` dolu mu (ya da
      `ORKA_SIFRE` / `ORKA_FIRMA_SIFRE` ortam değişkenleri)?

### İlk çalıştırma

- [ ] Bir ekstre yükleyip **bütün satırları çözün** (onay bekleyen kalmasın).
      Kalırsa iş oluşturulmaz: *"… satır onay bekliyor"*.
- [ ] **ORKA'ya Aktar** → durum kartı açılmalı, yüzde ilerlemeli.
- [ ] Robot çalışırken makineyi kullanmayın: robot ORKA penceresini öne
      getiriyor ve tuşlar oraya gidiyor.

### Akış adım adım — nerede durursa ne bakılacak

| Yüzde | Beklenen | Durursa |
|---|---|---|
| %5 | ORKA açılıyor / zaten açık | `OrkaPath` doğru mu |
| %15 | Giriş + F7 + firma kodu + firma şifresi | `--probe` ile pencere başlıklarına bak |
| %25 | Veri Transferi ekranı | Modül gezinme tuş sayısı (`RIGHT×3`, `DOWN×1`) firmaya göre değişebilir |
| %35 | Dosya seçim diyaloğu, dosya yolu yazılıyor | Diyalog başlığı `Transfer Edilecek Excel` mi |
| %45 | Ekran doğrulaması | Hâlâ Veri Transferi ekranında mıyız |
| %50–95 | Kodlar yazılıyor | **Hemen durdurun**, aşağıya bakın |
| %100 | Bitti | — |

- [ ] **%50'den sonra ilk üç satırı gözle kontrol edin:** kodlar doğru satıra mı
      gidiyor? Kaymışsa **İptal**'e basın ve ORKA'yı **kaydetmeden** kapatın.
- [ ] Koordinatlar tutmuyorsa (`Tikla` yanlış yere basıyorsa)
      `PkfRobot.exe --kalibre` ile ölçüp `gorevler/orkaya-aktar.json` içindeki
      `X`/`Y` değerlerini düzeltin. Kod değişmez.
- [ ] Bitince ekranda *"… satır yazıldı. ORKA'da kontrol edip Kaydet'e basın."*
      yazmalı. **Kaydet'e robot basmaz; siz basarsınız.**
- [ ] ORKA'da satırları gözle geçirin, sonra Kaydet.

### Sonrasında

- [ ] Log: `%AppData%\PkfRobot\logs\ajan-<tarih>.log` ve `C:\RobotLog\<tarih>_orka…\`
      (adım adım + ekran görüntüleri).
- [ ] Başarısız olduysa iş klasörü `%AppData%\PkfRobot\isler\{isId}\` altında
      7 gün duruyor; indirilen iki dosya orada.
- [ ] Hata ekranının görüntüsü sunucuya yüklendi mi (iş kaydında
      `HataEkraniDosyaId`)?

### Ölçülecek/doğrulanacak değerler

- [ ] Modül gezinme: `RIGHT` sayısı 3 mü? (yetkiye göre değişir)
- [ ] Sol panel "Banka Ekstresi" oranı `X: 0.125, Y: 0.300`
- [ ] Dosya seç düğmesi oranı `X: 0.500, Y: 0.120`
- [ ] Grid ilk satır / karşı hesap kolonu oranı `X: 0.400, Y: 0.420`
- [ ] Grid'de ENTER bir alt satıra geçiyor mu? Geçmiyorsa `GridDoldur` içindeki
      tuş değişmeli (kod değişikliği gerekir, JSON yetmez).

## Testler

| Proje | Önce | Sonra |
|---|---|---|
| `CatalogService.UnitTests` | 748 | **777** |
| `PkfRobot.UnitTests` | 49 | **74** |
| `IdentityService.UnitTests` | 24 | 24 |
| `WebApp.UnitTests` | 60 | 60 |

Hepsi geçiyor. Yeni testler:

- `CatalogService.UnitTests/Ajanlar/AjanIsServisiTests` (21) — bağlı ajana
  gönderme, ajan yokken `Bekliyor`, bağlanınca gönderme, ikinci işin 409'u,
  başka ajanın işini güncelleyememe, tekrarlanan bildirimin zararsızlığı, geciken
  eski ilerlemenin çubuğu geri sarmaması, bitmiş işe gelen geç bildirim, zaman
  aşımı ve ilerleme geldikçe ertelenmesi, zaman aşımına uğrayan işin ajanı meşgul
  bırakmaması, bağlantı kopunca `Basarisiz`, iptalin ajana bildirilmesi, bitmiş
  işin iptal edilememesi, **iş bitince sıradakinin gönderilmesi**, firmasız iş,
  hedef ajanın seçilmesi/reddedilmesi, liste süzgeçleri
- `CatalogService.UnitTests/Ajanlar/OrkaAktarimYukuTests` (8) — yükün sunucuda
  kurulması, satır sayısının dışa aktarımdan gelmesi, ORKA firma/hesap kodu
  eksikse işin oluşmaması, çözülemeyen satır, aktarılacak satır yokluğu
- `PkfRobot.UnitTests/Ajan/IsCalistirmaTests` (7) — sahte işin 10 adımı, artan
  yüzdeler, aynı anda ikinci işin reddi, tanınmayan iş tipi, iptal, başka işin
  iptalinin etkisizliği, ajan kapanırken bildirimi
- `PkfRobot.UnitTests/Ajan/OrkayaAktarTests` (18) — satır sayısı uyuşmazlığı
  (kod listesi ve xlsx), boş kod/açıklama, boş liste, xlsx satır sayımı, bozuk
  xlsx, başarılı aktarımın özeti, ilerleme sırası, grid yüzdelerinin 50–95
  aralığında kalması, dosya indirilememesi, doğrulama tutmazsa ORKA'ya hiç
  dokunulmaması, hata ekranının yüklenmesi, bozuk iş paketi, **görev JSON'unda
  Kaydet adımı olmaması** ve ilerleme kilometre taşları

## Değişen ve eklenen dosyalar

| Yer | Dosyalar |
|---|---|
| CatalogService — iş | `Features/Ajanlar/Domain/AjanIsi.cs`, `Dtos/AjanIsDtos.cs`, `Services/IAjanIsServisi.cs` + `AjanIsServisi.cs`, `Services/AjanIsGondericisi.cs`, `Services/OrkaAktarimYuku.cs`, `Controllers/AgentIsController.cs`, `Controllers/AgentDosyaController.cs`, `Infrastructure/EntityConfigurations/AjanIsiEntityTypeConfiguration.cs` |
| CatalogService — değişen | `AgentHub.cs` (iş metotları), `AgentHubAyarlari.cs` (`IsZamanAsimiDakika`), `AjanKimligi.cs` (varsayılan politika), `Services/IAjanDeposu.cs` + `AjanDeposu.cs` (`AjanaGoreBul`), `Infrastructure/Context/CatalogContext.cs`, `Program.cs`, `Features/Firmalar/**` + `FirmaEntityTypeConfiguration` (`OrkaFirmaKodu`) |
| Ajan — iş | `Ajan/IsCalistirici.cs` (arayüz + sahte), `Ajan/IsDosyalari.cs`, `Ajan/OrkaSurucusu.cs`, `Ajan/OrkayaAktarCalistirici.cs`, `Core/GridDoldurVerisi.cs`, `gorevler/orkaya-aktar.json` |
| Ajan — değişen | `Ajan/HubBaglantisi.cs`, `Ajan/AjanServisi.cs`, `Ajan/AjanCalistirici.cs`, `Core/AdimMotoru.cs` (`GridDoldur` + adım geri çağrısı), `Config/Gorev.cs` (`Yuzde`), `Config/RobotConfig.cs`, `appsettings.json`, `PkfRobot.csproj` (ClosedXML) |
| Blazor | `Pages/BankaEkstre/AktarPage.razor`, `Pages/BankaEkstre/Bolumler/AjanIsKarti.razor` (yeni), `Pages/Yonetim/Ajanlar.razor`, `Pages/Yonetim/FirmaDialog.razor`, `Application/Services/Yonetim/AjanIsApi.cs` (yeni), `Shared/Dto/Yonetim/AjanIsDtos.cs` (yeni) + `Firma*Dto` |

**Değişmeyenler:** `ocelot*.json`, `Nginx/**`, `docker-compose*.yml`,
IdentityService (ajan kimliği tarafı), JSON adım motorunun mevcut adım tipleri.

## Ne eksik kaldı

- **ORKA akışı ofiste denenmedi.** Yukarıdaki kontrol listesi ilk çalıştırmada
  izlenecek; koordinatlar ve tuş sayıları orada ölçülecek.
- **Mükerrer aktarım kontrolü yok.** Aynı ekstre iki kez gönderilirse robot iki
  kez yazar. Sunucuda "bu yükleme zaten aktarıldı mı" kontrolü yok — Kaydet'e
  kullanıcı bastığı için bugün zararı sınırlı, ama canlı kullanımdan önce
  eklenmeli.
- **Grid'e yazılan doğrulanamıyor.** ORKA'nın gridi okunamadığı için güvence
  yazmadan önceki sayı kontrolleriyle sınırlı (§121). Gözle kontrol şart.
- **Tarayıcı yoklamayla güncelleniyor** (§117); SignalR sonra eklenebilir.
- **FileApiService ajan/insan ayrımı yapmıyor** (§123): ajan token'ı orada
  geçerli. Bu turda kapsam dışı bırakıldı.
- **Kuyruk tek tek ilerliyor:** ajan aynı anda tek iş yürüttüğü için bekleyenler
  sırayla gidiyor — bağlanmada, iş bitince ve iptalde bir sonraki gönderiliyor.
  Öncelik ya da sıra değiştirme yok; sıra oluşturma zamanına göre.


# Anasayfa — Firma Bilgi Paneli

## Kapsam

Uygulama açılınca sorumlu olunan firmaların künyesine anında erişilen bir ekran.
Aranan şey tek bir bilgi: *"Citadel'in vergi dairesi ne", "Progroup'ta kim imza
atabilir", "şu firmanın MERSİS no"*.

Yeni tablo açılmadı. Veriler Firma Bilgileri modülünün kendi tablolarından
geliyor; o modüle yalnız **mükellefiyet alanları** eklendi.

## Düzen

```
┌──────────────┬────────────────────────────────────────────┐
│ arama kutusu │  ALPHA AHŞAP SANAYİ A.Ş.                   │
│              │  ┌──────────────────────────────────────┐  │
│ ▸ ALPHA      │  │ Mükellefiyet            [Düzenle]    │  │
│   772147…    │  │ Sicil                   [Düzenle]    │  │
│ ▸ CİTADEL ⚠  │  │ Ortaklık (tablo + toplam)            │  │
│   728062…    │  │ İmza yetkilileri (tablo)             │  │
│ ▸ PROGROUP ⚠ │  │ Belgeler (chip'ler)                  │  │
└──────────────┴────────────────────────────────────────────┘
        Dönem özeti ────────────────────────────────
   [beyanname] [ekstre] [ödemeler] [son firmalar]   ← eskisi, kompakt
```

Kart ızgarası yapılmadı: on firma × beş bölüm ekrana sığmıyor (KARARLAR §127).
**Mevcut sayaç kartları kaldırılmadı**, panelin altına "Dönem özeti" başlığıyla
daha kompakt biçimde alındı — rakam 32px'ten 22px'e, kolon genişliği 300px'ten
240px'e indi.

## Uyarı göstergesi

Sol listede firma satırının sağında sarı ⚠. Kullanıcı firmaya tıklamadan sorunu
görebiliyor; simgenin üstüne gelince uyarı metni çıkıyor. Üç kural (hepsi
sunucuda, `FirmaPaneliKurucu`):

| Uyarı | Kural |
|---|---|
| İmza yetkisi bitiyor | Firmanın **en geç biten** yetkisine 60 günden az kaldıysa (ya da dolmuşsa). Süresiz yetkili varsa uyarı yok |
| Pay oranı tutmuyor | Ortak varsa ve toplam pay oranı %100 değilse (0,01 tolerans) — düzenleme ekranıyla **aynı hesap** |
| Eksik sicil alanı | Vergi dairesi, ticaret sicil no, MERSİS no, adres — biri boşsa |

Mükellefiyet alanları bilerek zorunlu listesinde **değil**: yeni eklendikleri için
her firmada boşlar ve panel ilk gün baştan aşağı uyarı gösterirdi (§128).

## Veri

**Yeni alanlar** (`catalog.FirmaSicilBilgileri`, hepsi nullable):

| Alan | Tip | Not |
|---|---|---|
| `MukellefiyetTurleri` | `nvarchar(300)` | Serbest metin: "Kurumlar, KDV, Muhtasar" |
| `EFatura` | `bit` | Üç hâl: var / yok / bilinmiyor (`null`) |
| `EDefter` | `bit` | Aynı |
| `IseBaslamaTarihi` | `datetime2` | `KurulusTarihi` ile aynı şey değil |

Migration: `20260830202237_AnasayfaFirmaPaneliMukellefiyet`. Yerelde uygulandı;
serviste `HostExtension` açılışta `Database.Migrate()` çağırdığı için yayında
kendiliğinden geçiyor.

Düzenleme tek yerde: **Yönetim → Firmalarım → firma → Bilgiler → Sicil bilgileri**.
Paneldeki her bölümün "Düzenle" bağlantısı oraya götürüyor; panel okuma odaklı.

## Uç

| Uç | Yetki | Döndürdüğü |
|---|---|---|
| `GET /api/catalog/anasayfa/firma-paneli?firmaId=` | insan | **Tüm** firmaların satırları (uyarılarıyla) + seçili firmanın ayrıntısı |

Tek çağrı, firma başına ayrı istek yok: sol listedeki uyarı bütün firmaların
ortak/yetkili kayıtlarını gerektiriyor, bunlar tek `IN (...)` sorgusuyla okunup
bellekte gruplanıyor (beş sorgu, on firma için de kırk firma için de).

Kapsam Banka Otomasyon'daki mekanizmanın aynısı: `?firmaId=` → `BankaFirmaFiltresi`
→ `IBankaFirmaKapsami`. Parametre yoksa sunucu **ilk firmayı** seçiyor (ilk açılışta
sağ panel boş kalmasın); tanınmayan firma değeri filtreden 400 dönüyor.

Rota `api/catalog/*` altında olduğu için **gateway değişmedi**.

## Ayrıntılar

- **TCKN/VKN maskeli**: `1234****901`, tıklanınca açılıyor. Maskeleme ekranda;
  aynı kullanıcı bu kimlikleri düzenleme ekranında zaten düz görüyor (§129).
- **Boş alan gizlenmiyor**, "—" yazıyor: eksik olduğunun görünmesi bu ekranın işi.
- **e-Fatura/e-Defter üç hâlli**: boş bırakılmış alan "yok" demek değil.
- **Belgeler**: var olan belge dolu PDF ikonuyla (tıklanınca mevcut PDF
  görüntüleyicide açılıyor), olmayan tür kesikli çerçeveli düğmeyle — tıklanınca
  Firma Bilgileri ekranına götürüyor.
- **Arama** istemcide süzüyor ve Türkçe harfe takılmıyor: "citadel" → CİTADEL,
  "sti" → ŞTİ., "ahsap" → AHŞAP. VKN aramasında yalnız rakamlar karşılaştırılıyor,
  araya konan boşluk/nokta bozmuyor.
- Yetki bitişi yaklaşan satır sarı zeminde, kalan gün sayısıyla; dolmuş olan
  daha koyu sarıda.

## Testler

| Proje | Önce | Sonra |
|---|---|---|
| `CatalogService.UnitTests` | 777 | **796** |
| `WebApp.UnitTests` | 60 | **68** |
| `PkfRobot.UnitTests` | 74 | 74 |
| `IdentityService.UnitTests` | 24 | 24 |

Hepsi geçiyor. Yeni testler:

- `CatalogService.UnitTests/Anasayfa/FirmaPaneliTests` (19) — listenin tüm
  firmaları döndürmesi, pasif firmanın düşmesi, firma seçilmemişse ilk firmanın
  gelmesi, firma yokken boş panel; üç uyarının doğru çıkması ve **çıkmaması
  gereken yerlerde çıkmaması** (künyesi tam firma, tam 60 gün eşiği, süresi
  dolmuşun yanında geçerli yetkili, süresiz yetkili, ortak yokluğu, boş
  mükellefiyet alanları); detayın seçili firmanın ortak/yetkili/belge
  kayıtlarından kurulması (kapsam izolasyonu), sicili olmayan firma, listede
  olmayan firmaId, kalan günün sunucuda hesaplanması
- `WebApp.UnitTests/Anasayfa/FirmaPaneliAramaTests` (8) — ad ve unvanla süzme,
  Türkçe harf duyarsızlığı, VKN ile süzme, ayıraçlı VKN, boş arama, uymayan
  arama, TCKN maskeleme biçimi

## Değişen ve eklenen dosyalar

| Yer | Dosyalar |
|---|---|
| CatalogService — yeni | `Features/Anasayfa/Dtos/FirmaPaneliDtos.cs`, `Features/Anasayfa/Services/FirmaPaneliKurucu.cs`, `Features/Anasayfa/Services/FirmaPaneliService.cs`, `Migrations/20260830202237_AnasayfaFirmaPaneliMukellefiyet.cs` |
| CatalogService — değişen | `Features/Anasayfa/Controllers/AnasayfaController.cs`, `Features/FirmaBilgileri/Domain/FirmaBilgileriEntities.cs`, `Features/FirmaBilgileri/Dtos/FirmaBilgileriDtos.cs`, `Features/FirmaBilgileri/Services/FirmaBilgiService.cs`, `Infrastructure/EntityConfigurations/FirmaBilgileriEntityTypeConfigurations.cs`, `Program.cs` |
| Blazor — yeni | `Pages/Anasayfa/FirmaPaneli.razor`, `Pages/Anasayfa/FirmaPaneliDetay.razor`, `Pages/Anasayfa/MaskeliKimlik.razor`, `Pages/Anasayfa/FirmaPaneliArama.cs`, `Shared/Dto/Anasayfa/FirmaPaneliDtos.cs` |
| Blazor — değişen | `Pages/Anasayfa/AnasayfaPage.razor`, `Pages/Yonetim/FirmaBilgileri/SicilBolumu.razor`, `Application/Services/AnasayfaApiClient.cs`, `Shared/Dto/FirmaBilgileri/FirmaBilgileriDtos.cs` |

**Değişmeyenler:** `ocelot*.json`, `Nginx/**`, `docker-compose*.yml`, Firma
Bilgileri ekranının kendi uçları ve tabloları (alan eklendi, mekanizma aynı).

## Ne eksik kaldı

- **Ekranda görülmedi.** Derleme, testler ve migration yerelde doğrulandı; panelin
  gerçek verideki görünümü (bilgi yoğunluğu, sütun genişlikleri, on firmalık
  listede kaydırma) tarayıcıda denenmedi.
- **Mükellefiyet türleri serbest metin.** Kod listesi yok; "Kurumlar" ile
  "kurumlar" ayrı yazılabiliyor. Raporlanacak bir alan hâline gelirse listeye
  çevrilmeli.
- **İmza uyarısı temsil şeklini gözetmiyor.** İki müşterek yetkiliden birinin
  süresi dolduysa firma fiilen imzalanamaz ama uyarı çıkmaz (§128'deki kural
  firmanın en geç biten yetkisine bakıyor).
- **Belge eklemek panelden yapılamıyor**; kesikli düğme Firma Bilgileri ekranına
  götürüyor. Ekran okuma odaklı kaldı.
- **Sayaçlar ayrı çağrıda.** Panel bir istek, dönem özeti bir istek. İkisi tek uca
  sıkıştırılsaydı firma değiştirmek bütün sayfayı yeniden yükletirdi (§127).

# Yönetim > Ajanlar — rol yerine izin

Ekran menüde görünmüyordu. **Yayın eksikliği değildi**: sayfa, uçları ve
testleriyle birlikte kodda duruyordu (A adımıyla geldi, `c16b2a7`,
2026-08-30). Sebep yetkiydi — dört ayrı yerde `Admin` rolü aranıyordu ve
`pkfadmin` kullanıcısının token'ında yalnız `role: pkf` var. Teşhisin ayrıntısı
KARARLAR §131'de.

## Ne değişti

| Yer | Önce | Sonra |
|---|---|---|
| Menü satırı (`MainLayout.razor`) | `<AuthorizeView Roles="Admin">` | `@if (canViewAjanlar)` → `AjanYonetimi.View` |
| Sayfa (`Ajanlar.razor`) | `[Authorize(Roles = "Admin")]` | `[Authorize]` + `PermissionService` kontrolü |
| Kayıt uçları | `api/auth/admin/agents`, `[Authorize(Roles="Admin")]` | `api/auth/agents`, izin politikaları |
| `/catalog/agent/{id}/dusur` | `YalnizInsan` + `Roles="Admin"` | `YonetimiDuzenle` (insan + `AjanYonetimi.Edit`) |

İki yeni izin, repodaki `Vehicle.View` / `BeyannameTakip.View` kalıbıyla aynı:

- `AjanYonetimi.View` — ekranı ve listeyi görme
- `AjanYonetimi.Edit` — anahtar üretme, iptal, bağlantı düşürme

İkisi de seed'de `pkf` rolüne bağlandı; Admin zaten "bütün izinleri Admin'e bağla"
döngüsünden alıyor. Yeni rol mekanizması kurulmadı, `pkf` rolüne Admin verilmedi,
gateway yapılandırması değişmedi (`/auth/{everything}` kuralı yeni yolu zaten
karşılıyor). Diğer sayfaların yetkilendirmesine dokunulmadı;
`/catalog/agent/baglilar` ve iş uçları `YalnizInsan` olarak kaldı — Banka
Otomasyon > Aktar ekranı da onları çağırıyor.

## Yayın notu

Değişiklik **iki tarafı birden** ilgilendiriyor, ikisi de yayınlanmadan ekran
açılmaz:

1. **IdentityService yeniden yayınlanmalı.** İzinler `IdentityContextSeed`'de
   üretiliyor ve seed servis açılışında koşuyor; yeni sürüm çalışmadan
   `AjanYonetimi.*` satırları veritabanına düşmez ve kimsenin token'ında
   görünmez. Migration gerekmiyor, tablo yapısı değişmedi.
2. **Blazor uygulaması yeniden yayınlanmalı.** Menü satırı ve sayfanın izin
   sorgusu istemci tarafında.

Sıra önemli değil ama ikisi de gerekiyor: eski Blazor + yeni Identity → menüde
hâlâ görünmez (satır `Roles="Admin"` içinde); yeni Blazor + eski Identity →
menü yine görünmez (token'da izin yok) ve `/auth/agents` 404 döner.

Kullanıcının **yeniden giriş yapması gerekiyor**: izinler token'a giriş anında
basılıyor, elindeki 20 dakikalık token'da `AjanYonetimi.*` yok.

## Doğrulama (yerel, gateway zinciri üstünden)

Consul host'ta çalışıyordu; CatalogService (:5004), IdentityService (:5005) ve
gateway (:5000) ayağa kaldırılıp gerçek `pkfadmin` girişiyle sınandı. Derlemeye
ve teste ek olarak uçlar gerçekten çağrıldı:

`pkfadmin` (token: `role: pkf`, Admin yok; `perm`: … `AjanYonetimi.View`,
`AjanYonetimi.Edit`)

| Çağrı | Sonuç |
|---|---|
| `GET /auth/agents` | 200, 4 kayıt |
| `POST /auth/agents` (anahtar üret) | 200, `pkfr_0VU6…`, oluşturan `pkfadmin` (#19) |
| `POST /auth/agents/1003/iptal` | 204, listede "İptal" |
| `GET /catalog/agent/baglilar` | 200 |
| `POST /catalog/agent/999/dusur` | 200 |
| `GET /catalog/agent/isler` | 200 |
| `GET /auth/admin/agents` (eski yol) | 404 |

`cengiz.sezer` (MaliIsler; ajan izni yok): `/auth/agents` GET ve POST 403,
`dusur` 403; `baglilar` 200 (bilerek açık).

Elle basılmış, yalnız `AjanYonetimi.View` taşıyan token: `GET /auth/agents` 200,
`POST /auth/agents` 403, `iptal` 403, `dusur` 403 — iki iznin ayrı durması
gerçekten işliyor.

Regresyon: `admin` kullanıcısı (rol `Admin`) seed döngüsünden iki izni de alıyor,
`GET /auth/agents` 200.

Birim testleri: IdentityService 28/28, CatalogService (Ajanlar) 85/85 geçti.

## Ne eksik kaldı

`/unauthorized` adresinde sayfa yok; izni olmayan kullanıcı 404 görüyor. Bu eksik
bu ekranla gelmedi — `DeclarationFollow`, `Vehicles`, `TaxPayments`,
`Companies`, `CalendarPage` de aynı adrese yolluyor. Düzeltilecekse hepsi
birlikte düzeltilmeli.

Ekranın tarayıcıdaki görünümü doğrulanmadı: menü satırı ve sayfa aynı `perm`
claim'ine bakıyor, o claim'in token'da olduğu yukarıda görüldü.



# PkfRobot — Windows arayüzü ve koordinat kalibrasyonu

## Kapsam

PkfRobot bugüne kadar yalnız konsol uygulamasıydı: koordinatlar ve yollar
`appsettings.json` ile `gorevler\*.json` içine Notepad ile yazılıyordu. Makine
değiştiğinde ya da ORKA'nın ekran düzeni bozulduğunda dosya düzenlemek
gerekiyordu.

Bu turda küçük bir masaüstü uygulaması eklendi. **Adım motoruna, görev JSON
şemasına ve ajanın bağlantı mantığına dokunulmadı**; eklenen şey arayüz ve ayar
yönetimi.

- Argümansız çalıştırma artık arayüzü açıyor. `--ajan`, `--gorev`, `--probe`,
  `--kalibre`, `--yardim` aynen duruyor.
- Pencere küçük (470×620), "her zaman üstte" seçeneği var, kapatma düğmesi
  sistem tepsisine indiriyor; çıkış tepsi menüsünden. Tepsi simgesi bağlantı
  durumuna göre renk değiştiriyor (yeşil bağlı, sarı bağlanıyor, kırmızı kopuk,
  gri kapalı).

## Ekranlar

**Durum** — hub bağlantısı, son kalp atışı (kaç saniye önce), ORKA açık mı,
çalışan işin tipi/ilerlemesi/mesajı, son beş işin özeti (tarih, sonuç, süre) ve
canlı log penceresi. "Baglan / Durdur" düğmesi ajanı arayüzden başlatıp
durduruyor; log ve görev logu klasörleri iki düğmeyle açılıyor.

**Ayarlar** — ORKA exe yolu, indirilen iş dosyaları klasörü, log klasörü
(hepsi gezginle seçiliyor, olmayan yol kırmızı/sarı uyarı veriyor); ORKA firma
kodu ve kullanıcı kodu; ORKA şifresi ve firma şifresi (yıldızlı, DPAPI ile ayrı
dosyada). "Yedekle / Geri yükle" ayarları tek dosyaya çıkarıp geri alıyor.

**Kalibrasyon** — görev JSON'larındaki her `Tikla` adımı için bir satır: ad,
mevcut değer, **Seç** ve **Dene**.

## Kalibrasyon — adım adım (ofiste yapılacak)

Aşağıdakiler ofiste, ORKA açıkken yapılır. Ev PC'sinde ORKA olmadığı için
ölçüm yapılamaz.

1. **ORKA'yı aç ve tam ekran yap.** Koordinatlar pencereye oranla saklanıyor
   (piksel değil). Tam ekran **şart değil** — oran pencerenin o anki ölçüsünden
   hesaplanıyor ve ölçüm engellenmiyor; ama pencereyi sonradan yeniden
   boyutlandırırsan oran kayar. Uygulama bunu uyarı olarak söylüyor.
2. **Kalibre edilecek ekrana ORKA içinde elle git.** Örneğin "Veri Transferi >
   Banka Ekstreleri". Robotun tıklayacağı nokta o an ekranda görünüyor olmalı.
3. **PkfRobot.exe'yi argümansız çalıştır**, "Kalibrasyon" sekmesine geç.
   Satırlar görev dosyalarından geliyor; hangi koordinat hangi görevde
   kullanılıyorsa o satır listede.
4. **İlgili satırda "Seç"e bas.** Pencere gizlenir, ORKA öne gelir ve ekranın
   üstünde ince bir şerit çıkar: "Hedefe tıklayın · İptal için Esc".
5. **ORKA'da hedefe tıkla.** Tıklama **ORKA'ya ulaşmaz** — yutuluyor, yani o
   düğmeye gerçekten basılmıyor, menü açılmıyor, kayıt değişmiyor. Vazgeçmek
   için Esc ya da sağ tık.
6. Nokta ORKA **ana** penceresine göre orana çevrilip kaydedilir. ORKA'nın alt
   pencerelerine (Veri Transferi ekranı, diyaloglar, firma şifresi popup'ı)
   tıklamak serbest — hepsi aynı süreç — ama oran her zaman ana pencereye göre
   hesaplanır, çünkü `Tikla` adımı da tıklamayı ana pencereye oranla uyguluyor.
   Başka bir uygulamaya tıklandıysa **kaydedilmez**. Ret mesajı üç şeyi yazar:
   hangi denetim tetiklendi, hangi pencereye tıklandı (başlık + süreç adı + pid),
   ne bekleniyordu. Aynı satır uygulamanın log ekranına da düşer.
7. **"Dene"ye bas.** Bu sefer ORKA'ya **gerçekten tıklanır** (önce onay sorar).
   Ardından ekran görüntüsü alınır ve tıklanan noktaya nişan çizilerek
   gösterilir. Nişan doğru yerdeyse koordinat tamam; değilse 4. adıma dön.
   Ölçümü gözle doğrulamanın başka yolu yok: ORKA'nın gridi UI Automation'a
   kapalı, robot yazdığı yeri ekrandan göremiyor.
8. Ölçüm hem `%AppData%\PkfRobot\ayarlar.json` içine hem de görev JSON'una
   yazılır. "Görevlere uygula" düğmesi bunu elle de yapıyor; uygulama her
   açılışta zaten kendiliğinden yapıyor.
9. Yanlış ölçtüysen "Ölçümü sil" kayıtlı ölçümleri siler; görev
   dosyalarındaki değerler olduğu gibi kalır.

**Makine değiştirirken:** Ayarlar > "Yedekle" ile tek dosya al, yeni makinede
"Geri yükle". Kalibrasyon aynen gelir. **Şifreler yedeğe girmez** — DPAPI ile
şifrelenen değer başka makinede zaten çözülemez; yedeğe düz metin koymak
şifreleri bir dosyaya dökmek olurdu. Yeni makinede şifreler ve ajan anahtarı
elle girilir.

## Ayarlar nerede duruyor

| Dosya | İçerik |
|---|---|
| `%AppData%\PkfRobot\ayarlar.json` | Yollar, firma/kullanıcı kodu, kalibrasyon. Düz metin. |
| `%AppData%\PkfRobot\sifreler.dat` | ORKA ve firma şifresi. DPAPI (CurrentUser) ile şifreli. |
| `%AppData%\PkfRobot\agent.dat` | Ajan anahtarı (bu turda değişmedi). |
| `gorevler\*.json` | Koordinatların **çalışan** kopyası; motor buradan okuyor. |

Publish klasörü her yayında üzerine yazılıyor; asıl kopya bu yüzden
`%AppData%` altında. Uygulama her açılışta kayıtlı kalibrasyonu görev
dosyalarına geri yazıyor, yani yayın sonrası kalibrasyon kendiliğinden geliyor.

## Testler

Arayüz test edilmiyor; **kural** test ediliyor. PkfRobot birim testleri
115/115 geçiyor (önceki 74 aynen duruyor, 41 yeni):

- Mutlak koordinat → oran çevrimi (1920×1080, 1366×768, 2560×1440); oran →
  mutlak çevrimi tersini veriyor; ölçüsüz pencere reddediliyor; oran Türkçe
  locale'de de nokta ile yazılıyor.
- ORKA dışı pencereye tıklama reddediliyor; süreç okunamadığında da
  reddediliyor; ana pencerenin dışı reddediliyor; ORKA tam ekran değilken
  uyarı veriliyor ama kaydediliyor.
- Koordinat listesi görev JSON'larından türetiliyor; `Tikla` dışı adımlar
  girmiyor; bozuk bir dosya listeyi düşürmüyor; araya adım eklenmesi
  kalibrasyonu bozmuyor.
- Kalibrasyon görev dosyasına yazılıyor ve JSON'daki `"// KULLANIM"` gibi
  anahtarlar korunuyor; aynı değer yeniden yazılmıyor; adım açıklaması
  değiştiyse ölçüm **uygulanmıyor**; oran üç haneye yuvarlanıyor.
- Ayarlar kaydedilip okunuyor; bozuk ayar dosyası arayüzü kilitlemiyor;
  şifreler diskte düz metin durmuyor ve `ayarlar.json` içinde geçmiyor; yedek
  alınıp geri yüklenince ayarlar aynı; yedekte şifre yok.

## Değişen ve eklenen dosyalar

Yeni: `src/Robot.Agent/Ayarlar/` (OranDonusturucu, KoordinatSecimi,
KoordinatKesfi, KalibrasyonUygulama, RobotAyarlari, AyarDeposu, SifreDeposu,
AyarTanimlari) ve `src/Robot.Agent/Arayuz/` (AnaForm, DurumPaneli,
AyarlarPaneli, KalibrasyonPaneli, AjanKoprusu, IsIzleme, TiklamaYakalayici,
BilgiSeridi, EkranYakalama, OrkaPenceresi, Win32).

Değişen: `Program.cs` (argümansız → arayüz, `[STAThread]`),
`PkfRobot.csproj` (`UseWindowsForms`), `Ajan/AjanCalistirici.cs` (isteğe bağlı
kancalar), `Ajan/AjanServisi.cs` (yalnız okunan `SonKalpAtisi` damgası).

`Core/AdimMotoru.cs` ve `gorevler/*.json` **değişmedi**.

## Ne eksik kaldı

Arayüz ofiste çalıştırılmadı. Ev PC'sinde ORKA yok; tıklama yakalama, ORKA'yı
öne getirme, "Dene" ekran görüntüsü ve tepsi davranışı **ölçülemedi** —
derleme, publish ve birim testleri yapıldı. Kalibrasyonun ilk gerçek denemesi
ofiste yapılacak.

Ajan anahtarı hâlâ yalnız ilk bağlantı denemesinde soruluyor; Ayarlar
ekranında "anahtarı değiştir" düğmesi yok. Anahtar değiştirmek için
`PkfRobot.exe --anahtari-sifirla` duruyor.
