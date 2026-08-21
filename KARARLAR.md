# KARARLAR — Banka Ekstresi İşleme Modülü

Prompt'ta açıkça yazmayan noktalarda alınan kararlar ve gerekçeleri.
Kural: belirsizse en muhafazakâr seçenek, mimari belirsizse repodaki benzer koda bak.

## 1. Modül nereye kondu

**Karar:** `CatalogService.Api/Features/BankaEkstre` — yeni mikroservis açılmadı.
**Neden:** Muhasebe, SMMM Takip, Firma Kontrol, Ticaret Sicil gibi son modüllerin
hepsi CatalogService içinde dikey dilim. Aynı `CatalogContext`, aynı `catalog` şeması.

## 2. Rota öneki ve gateway

**Karar:** Prompt'taki `/api/banka-hesaplari`, `/api/ekstre/...` yerine
`api/catalog/banka-ekstre/...` kullanıldı. **Ocelot yapılandırması değişmedi.**
**Neden:** Gateway tek route ile çalışıyor: `/catalog/{everything}` → `/api/catalog/{everything}`.
`api/catalog` dışındaki bir rota gateway'den geçmez, yeni route eklemeyi gerektirirdi.
Blazor istemcisi de `/catalog/banka-ekstre/...` çağırıyor.

Karşılıklar:

| Prompt | Gerçekleşen |
|---|---|
| `POST /api/banka-hesaplari` | `POST api/catalog/banka-ekstre/banka-hesaplari` |
| `GET /api/banka-hesaplari` | `GET  api/catalog/banka-ekstre/banka-hesaplari` |
| `POST /api/ekstre/yukle` | `POST api/catalog/banka-ekstre/ekstre/yukle` |
| `GET /api/ekstre/{id}/satirlar` | `GET  api/catalog/banka-ekstre/ekstre/{id}/satirlar` |
| `PUT /api/ekstre/satir/{id}/onayla` | `PUT  api/catalog/banka-ekstre/ekstre/satir/{id}/onayla` |
| `PUT /api/ekstre/satir/{id}/diger-bankada` | `PUT  api/catalog/banka-ekstre/ekstre/satir/{id}/diger-bankada` |
| `POST /api/ekstre/{id}/disa-aktar` | `POST api/catalog/banka-ekstre/ekstre/{id}/disa-aktar` |
| `POST /api/hesap-plani/ice-aktar` | `POST api/catalog/banka-ekstre/hesap-plani/ice-aktar` |

## 3. Hesap planı neden ayrı tablo

**Karar:** `EkstreHesapPlani` tablosu, Muhasebe modülünün `HesapPlanlari` tablosundan ayrı.
**Neden:** Muhasebe hesap planı ağaç yapılı, tenant bazlı, kodları noktalı ve tamamen sayısal
(`102.01.01.0046`). ORKA kodları boşluklu ve harf içeriyor (`120 D22`, `329 P27`). İkisini
birleştirmek ya ORKA formatını bozardı ya mevcut Muhasebe kurallarını. Prompt'un domain
modelinde de ayrı bir `HesapPlaniKaydi` var; ona uyuldu.

## 4. Tenant (firma) izolasyonu

**Karar:** `BankaHesabi`, `EkstreYukleme`, `OgrenmeKaydi`, `HesapPlaniKaydi` → `TenantEntity`
+ query filter. `EkstreSatiri` bağlı olduğu yüklemeden izole olur (Muhasebe'de `FisSatir`
aynı yaklaşım). Şablon / desen / sabit kural tabloları **global** — tenant'a bağlı değil.
**Neden:** Hesap planı ve cari kodları firmaya özel; sızması veri kazası olur. Şablon ve
desenler banka bazlı referans içerik (SmmmTakip, TicaretSicil, MevzuatNotlari ile aynı
mantık), her firmaya kopyalanması gereksiz.

## 5. Şablon/desen/kural tabloları veritabanında

**Karar:** `EkstreAciklamaSablonlari`, `EkstreUnvanDesenleri`, `EkstreSabitKurallar` —
üçü de tablo, seed ile Vakıfbank satırları doldu. Seed **satır bazında idempotent**:
var olan kaydın üzerine yazmaz (kullanıcı düzenlemesi korunur).
**Neden:** Prompt: "kod değişmesin". Seed'in üzerine yazması bu vaadi bozardı.

## 6. Unvan desenleri büyük/küçük harf **duyarlı** çalışır

**Karar:** `RegexOptions.IgnoreCase` kullanılmadı.
**Neden:** Ölçülen desenler `[A-ZÇĞİÖŞÜ]` gibi büyük harf sınıflarına dayanıyor;
IgnoreCase bu sınıfları küçük harfe de açar ve ölçülen kapsama (120/72/32/12/6 satır)
geçersiz olurdu. Şablon ve sabit kural eşleşmeleri ise büyük/küçük harf duyarsız
(orada işlem tipi metni eşleşiyor, desen sınıfı değil).

## 7. Yeni katman: VKN

**Karar:** `KaynakKatman` enum'una `Vkn` eklendi; sıra IBAN → VKN → geçmiş onay.
**Neden:** Prompt'un domain modelinde `AnahtarTipi = Vkn` var ama katman listesinde yok.
VKN de IBAN gibi kesin bir anahtar; öğrenme onu yazıyorsa okunmalı. IBAN'dan sonra,
açıklama hash'inden önce konuldu (daha spesifik anahtar önce).

## 8. Çözülemedi ↔ OnayBekliyor ayrımı

**Karar:**
- Hiç aday üretilemedi (unvan yok veya hesap planı boş) → `Cozulemedi`
- Aday var ama skor < 0.85 **veya** ikinci aday 0.05 içinde → `OnayBekliyor` (öneri gösterilir)

**Neden:** Prompt §6 "hiçbiri tutmazsa onaya düşer", §7 "çözülemezse Cozulemedi" diyor.
İkisi de onay kuyruğuna girer; ayrım kullanıcıya "burada hiç fikrim yok" ile "şu adayı
öneriyorum" arasındaki farkı gösterir. Onay ekranında ikisi de aynı listede.

## 9. Şablonu olmayan işlem tipi

**Karar:** Şablon bulunamazsa açıklama = `Title Case(İşlem Tipi)` (+ varsa `- Unvan`),
50 karaktere kırpılır. Satır yine katmanlardan geçer.
**Neden:** Boş açıklama ORKA'ya boş gider. Uydurma yapılmıyor; bankanın kendi metni
düzenleniyor. Bilinmeyen işlem tipi zaten çoğunlukla onaya düşüyor, kullanıcı görüyor.

## 10. Title Case sınırı "harf olmayan her karakter"

**Karar:** `BaslikBicimi` kelimeyi boşlukla değil, harf/harf-olmayan sınırıyla bölüyor.
**Neden:** Boşlukla bölen sürüm `A.Ş.` → `A.ş.` üretiyordu. Şimdi `A.Ş.` korunuyor,
`PKF` → `Pkf`, `34 ABC 123` → `34 Abc 123`. Kültür `tr-TR` (İ→i, I→ı).

## 11. Normalizasyonda nokta silinir, boşluğa çevrilmez

**Karar:** `UnvanNormalize` önce `.` karakterlerini siler, sonra kalan alfanümerik
olmayanı boşluğa çevirir.
**Neden:** `A.Ş.` boşluğa çevrilince `A` + `S` iki ayrı kelime oluyor ve gürültü
listesindeki `AS` ile eşleşmiyordu; unvan `DAGI GIYIM A S` kalıp gerçek eşleşmenin
skorunu 1.00'dan 0.71'e düşürüyordu (satır boşuna onaya düşüyordu). Nokta silinince
`AS` gürültü sayılıp atılıyor.

## 12. Tutar sayısal hücreden okunur

**Karar:** Excel hücresi sayısal ise `decimal` olarak doğrudan alınır; yalnız metin
hücrede tr-TR (`1.234,56`) ve invariant (`1,234.56`) biçimleri denenir.
**Neden:** Sayısal hücreyi `GetString()` ile okuyup tr-TR ile ayrıştırmak `12500.75`
değerini `1250075` yapıyordu — noktayı binlik ayracı sanıyor.

## 13. Yön tespiti

**Karar:** Öncelik tutarın işareti (negatif → çıkan). Tutar işaretsizse `B/A` kolonu
kullanılır (`B` = borç = çıkan, `A` = alacak = giren). Tutar her zaman **pozitif** saklanır.
**Neden:** Ölçüm tutarın işaretli geldiğini söylüyor, ama dosya sürümü değişirse B/A
yedek kalsın. Tutarın mutlak değerde saklanması `Yon` alanını tek doğruluk kaynağı yapar.

## 14. Sabit kural tablosunda muavin kırılımı yok

**Karar:** Seed'deki sabit kurallar ana hesap seviyesinde (`770`, `740`).
**Neden:** Muavin kırılımı (`770 01 05` gibi) firmadan firmaya değişir; uydurmak
"hesap kodunu uydurma" yasağını çiğnerdi. Arayüzden düzenlenebilir; tablo yapılandırılabilir.
`Vergi Tahsilatı` için sabit kural yazılmadı (360/368 kırılımı firmaya özel) — o satırlar
onaya düşer.

## 15. Dışa aktarım biçimi

**Karar:** `POST .../disa-aktar` JSON satır listesi döner (`OrkaSatirDto`); dosya üretilmez.
Her satırda karşı hesap kodu **ve** ekstresi işlenen banka hesabının ORKA kodu var.
**Neden:** Prompt "ORKA'ya aktarılacak satır listesi" diyor, dosya formatı belirtmiyor.
ORKA'nın beklediği dosya şeması bilinmediğinden en muhafazakâr çıktı: veriyi ver,
biçimi sonra ekle. Onay ekranı listeyi gösteriyor.

## 16. Banka hesabı silme

**Karar:** Ekstresi olan banka hesabı silinemez (409); pasife alınır.
**Neden:** Hesap aynı zamanda banka kayıt defteri; silinirse geçmiş yüklemelerin
bağı kopar. Muhasebe modülündeki "hareketi olan hesap silinemez" kuralıyla aynı çizgi.

## 17. Onay ekranında bilinmeyen kod reddedilir

**Karar:** Hesap planı doluysa, planda olmayan koda onay verilemez (400).
Plan hiç yüklenmemişse onaya izin verilir (ad boş kalır).
**Neden:** "Hesap kodunu uydurma" kuralının onay tarafındaki karşılığı. Plan henüz
yüklenmemişken sistemi tamamen kilitlemek de kullanışsız olurdu.

## 18. Öğrenmede farklı kod seçilirse sayaç sıfırlanır

**Karar:** Kullanıcı önerilenden farklı kod seçince `HesapKodu` ezilir ve
`KullanimSayisi = 1` olur.
**Neden:** Eski sayaç eski koda ait; yeni kodun güvenilirliğini temsil etmiyor.

## 19. `DigerBankada` öğrenme yazmaz

**Karar:** "Diğer bankada" işaretlemesi `OgrenmeKaydi` üretmez.
**Neden:** Bu bir hesap kararı değil, "bu satırı burada işleme" kararı. Öğrenilirse
aynı gönderici bir daha hiç eşleşmezdi.

## 20. Aday seçimi klavyeden `Alt+rakam`

**Karar:** Onay ekranında yakın adaylar ve yazdıkça çıkan öneri listesi `Alt+1..9`
ile seçilebilir. Öneri listesi açıksa rakam listeden, kapalıysa yakın aday
çiftinden seçer (1 = önerilen, 2 = ikinci aday). Odak kod kutusunda kalır.
**Neden:** Prompt §10 hem "fare gerektirmez" hem "iki aday da tıklanabilir/seçilebilir"
diyor. Adaylar yalnız `@onclick` ile seçilebiliyordu; klavye kullanıcısı ikinci adayı
elle yazmak zorunda kalıyordu. `↓`/`↑` satır değiştirmeye ayrılmış olduğundan aday
gezinmesi için kullanılamazdı, bu yüzden ayrı bir değiştirici tuş seçildi.

---

# Düzeltmeler turu (claude-code-prompt-banka-modulu-duzeltmeler.md)

Aşağıdaki kararlar ilk turun kararlarını **değiştiriyor**; çelişki olduğunda bu bölüm geçerli.

## 21. VKN ve IBAN katmanları silinmedi, banka bazlı bayrakla kapatıldı

**Karar:** `BankaHesabi`'ye `IbanKatmaniAktif` ve `VknKatmaniAktif` alanları eklendi;
ikisi de varsayılan **kapalı**. Katman kodu `HesapEslestirici` içinde duruyor, yalnız
bayrak açıksa okunuyor. Öğrenme de aynı kurala tabi: kapalı katman veri biriktirmiyor.
**Neden:** Prompt §1 "katmanı silme, banka bazlı bir bayrakla kapalı tut" diyor. Kapalı
katmanın sessizce veri biriktirmesi, sonradan açıldığında doğrulanmamış eşleşmelerin
güven 1.0 ile geçmesi demekti; bu yüzden yazma da bayrağa bağlandı.

Karar §7 (yeni katman: VKN) bu maddeyle geçersiz kaldı.

## 22. Banka kayıt defterinin IBAN kontrolü kaldı

**Karar:** "IBAN katmanını çıkar" kuralı yalnız **öğrenilmiş** IBAN eşleşmesini kapsıyor.
Bankalar arası hareketlerde `BankaBul`, kullanıcının Tanımlar'da kendi girdiği hesap
IBAN'ıyla eşleştirmeye devam ediyor.
**Neden:** Güvenilmez bulunan şey bankanın ekstredeki IBAN verisi ve ondan öğrenilen
eşleşme. Kendi hesaplarının IBAN'ı kullanıcının elle girdiği, doğrulanmış bir tanım;
kaldırmak Katman 2'yi (ölçümde en yüksek getirili katman) zayıflatırdı. Bu alan boş
bırakılabilir, o zaman yalnız banka adı metin eşlemesi çalışır.

## 23. Öğrenme tablosu ikiye bölündü; eski tablo düşürüldü

**Karar:** `EkstreOgrenmeKayitlari` migration ile **düşürüldü**. Yerine:

| Tablo | Kapsam | Alanlar |
|---|---|---|
| `EkstreKimlikKayitlari` | GLOBAL | `Anahtar`, `AnahtarTipi`, `NormalizeUnvan`, `KullanimSayisi`, `SonKullanim` |
| `EkstreHesapEslesmeleri` | FİRMA | `TenantNo`, `AnahtarCekirdek`, `AyirtEdiciEk`, `AnahtarTipi`, `HesapKodu`, `HesapAdi`, `Yon`, `KullanimSayisi`, `SonKullanim` |

**Neden:** Eski tablonun anahtarı ham açıklamanın hash'iydi; banka her satıra farklı sorgu
numarası, tarih ve tutar yazdığı için o anahtar **asla ikinci kez eşleşmiyordu**. Taşınacak
anlamlı veri yoktu, dönüştürmek yerine düşürüldü. Prompt'un alan listesine `AnahtarTipi`
eklendi ki kapalı IBAN/VKN katmanları açıldığında aynı tabloyu kullanabilsin.

## 24. Öğrenme anahtarı: unvan çekirdeği, gerekiyorsa ayırt edici ekle

**Karar:** `Normalizasyon.UnvanCekirdek` — `UnvanNormalize` + tek harfli token'ları at.
Unvan çıkarılamayan satırlarda anahtar `ISLEM:<normalize işlem tipi>`.
Aile tespit edilirse anahtar `çekirdek + AyirtEdiciEk`; aramada önce genişletilmiş
anahtar, tutmazsa sade çekirdek denenir.
**Neden:** Prompt §3 ve §5. Anahtarın **her zaman** çok parçalı olması, çoğu satırda
gereksiz kelime ekleyip anahtarın ikinci ay tutmamasına yol açardı.

## 25. Çıpa algoritması tüm grup taramasının yerini aldı

**Karar:** Unvan benzerliği katmanı artık yön → ana grup daraltmasından sonra normalize
unvanın **her token'ını sırayla çıpa** olarak deniyor: çıpayla başlayan hesapları getirip
kalan metinle (çıpa dahil) skorluyor, her hesap için en yüksek skoru tutuyor.
**Hiçbir çıpa aday getirmezse kod önerilmiyor**, satır `Cozulemedi` olarak onay kuyruğuna
düşüyor.
**Neden:** Prompt §4. Eski algoritma ilk harfle daraltıp tüm grubu tarıyordu ve alakasız
bir hesabı "en yakın" diye öneriyordu. Alakasız öneri, öneri yokluğundan daha kötü:
kullanıcı Enter'a basıp yanlış kodu öğretebiliyor.

`Cozulemedi` seçildi çünkü Karar §8'deki ayrım korunuyor: aday yoksa "burada hiç fikrim
yok", aday varsa "şu adayı öneriyorum". İkisi de aynı onay kuyruğunda.

## 25b. Aday sayısı eşiği kaldırıldı (gerçek hesap planıyla ölçüldü)

**Karar:** İlk sürümdeki `CipaAdayEsigi = 25` — "bir çıpa 25'ten fazla aday getiriyorsa
sonucunu yok say" kuralı — **kaldırıldı**. Çıpanın kaç aday getirdiğine bakılmıyor,
tüm token'lar deneniyor ve en yüksek skor alınıyor.

**Neden:** Kural, 6.127 kayıtlık gerçek hesap planında ölçüldü ve **zarar verdiği görüldü**.
Kalabalık çıpalar gürültü değil, meşru cari aileleri:

| Çıpa | Aday | Ne olduğu |
|---|---|---|
| `PKF` | 89 | Grup şirketleri |
| `PARDUS` | 101 | Portföy fonları |
| `ISTANBUL` | 126 | Aynı önekli gerçek cariler |

Eşik uygulanınca `PKF İstanbul YMM` skoru 0.95 → **0.48**, `İstanbul Portföy Yönetimi`
1.00 → **0.61** düşüyor ve satırlar alakasız ana hesaplara (`373`, `110`, `121 1`)
eşleşiyordu. Yani eşik, önlemeye çalıştığı hatanın ta kendisini üretiyordu.

Yanlış eşleşmeye karşı koruma zaten mevcut iki kuralda ve onlar gevşetilmedi:
**`OtomatikEsik = 0.85`** (düşük skor otomatik geçmez) ve **`AdayFarki = 0.05`**
(yakın ikinci aday varsa onaya düşer). Kalabalık bir çıpanın ürettiği alakasız aday
zaten bu iki filtreden geçemiyor; aday sayısına bakmak gereksiz ve zararlı bir üçüncü
filtreydi.

Regresyon testleri: `Kalabalik_cipa_elenmez_pkf_ailesi_dogru_cariye_gider` (89 PKF hesabı
arasından `120 P44`) ve `Kalabalik_cipa_elenmez_istanbul_portfoy_dogru_cariye_gider`
(126 İSTANBUL hesabı arasından `120 I61`), ikisi de 0.90 üzeri skorla.

## 25c. Eşik yerine ön indeks

**Karar:** Aday sayısını kısıtlamak yerine arama hızlandırıldı: `HesapPlaniIndeksi` —
ana grup başına, normalize hesap adına göre **ordinal sıralı dizi**. Çıpayla başlayan
hesaplar ikili aramayla (`AltSinir`) bulunan bitişik bir aralık. İndeks
`EslestirmeVerisi.Indeks` üzerinden **yükleme başına bir kez** kuruluyor.
**Neden:** Eşiğin tek meşru gerekçesi performanstı; 6.127 kaydı satır × token sayısı kadar
taramak gereksiz. Sıralı dizi + ikili arama seçildi çünkü token → hesap sözlüğü, çıpanın
hesap adının ilk kelimesinin **öneki** olduğu durumları (`PKF` → `PKFISTANBUL...`)
kaçırırdı; sıralı önek aralığı eski `StartsWith` davranışını birebir koruyor. Çıpa tek
token olduğundan (boşluk içermez) "adın öneki" ile "ilk kelimenin öneki" aynı şeydir.

## 26. Aile ayrımı ham açıklamada aranır

**Karar:** En iyi adayla 0.05 içindeki adaylar "aile" sayılır. Aile üyelerinin **ortak
olmayan** kelimeleri (Aidat, Elektrik, 19 Kat) ham banka açıklamasında tam token olarak
aranır. Tam bir üye bulunursa o seçilir ve anahtara ek olarak yazılır; sıfır veya birden
fazla üye bulunursa satır onaya düşer ve **tüm aile** aday olarak listelenir.
**Neden:** Prompt §5. "Birden fazla üye bulunursa" durumunda tahmin etmemek, iki adayın
0.05 içinde olması kadar belirsiz bir durum.

Onay ekranı artık iki adayla sınırlı değil; adaylar `EkstreSatirlari.Adaylar` alanında
JSON olarak duruyor (en fazla 8), `Alt+1..9` bu listeden seçiyor. `IkinciAday*` alanları
korundu — dışa aktarım ve mevcut testler onları kullanıyor.

## 27. Bilinmeyen hesap kodu artık reddedilmiyor

**Karar:** Hesap planında olmayan kodla onay **kabul ediliyor**; yanıtta uyarı dönüyor
("ORKA'da yeni açıldıysa hesap planını güncelleyin") ve **öğrenme kaydı yazılmıyor**.
**Neden:** Prompt §9. Karar §17 (bilinmeyen kod reddedilir) bununla geçersiz kaldı:
ORKA'da yeni açılmış bir cari için kullanıcıyı kilitlemek yerine, doğrulanmamış kodun
kalıcılaşmasını engellemek yetiyor. Öğrenilen eşleşme düzenleme ekranında ise bilinmeyen
kod hâlâ reddediliyor — orası doğrudan öğrenme tablosuna yazıyor.

## 28. Dışa aktarım iki parça; kaynak dosya saklanıyor

**Karar:**
1. `POST .../{id}/duzeltilmis-ekstre` → orijinal xlsx, açıklama kolonu `UretilenAciklama`
   ile değiştirilmiş (dosya indirilir).
2. `POST .../{id}/disa-aktar` → JSON kod listesi; `OrkaSatirDto.HesapKodu` alanı
   `KarsiHesapKodu` olarak yeniden adlandırıldı (PkfRobot'un `GridDoldur` sözleşmesi).
   `Aciklama` alanı robotun satır doğrulaması için duruyor.

Bunun için `EkstreYuklemeler`'e `DosyaIcerik` (varbinary(max)) ve `AciklamaKolonu`,
`EkstreSatirlari`'na `KaynakSatirNo` eklendi.
**Neden:** Prompt §10. "Orijinal ekstre yapısında" dosya üretmenin tek güvenilir yolu
kaynağı saklayıp açıklama hücrelerini üzerine yazmak; parse edilmiş alanlardan yeniden
üretmek dosyanın yapısını kaybettirirdi. Karar §15 (dosya üretilmez) bununla geçersiz.

Kaynak dosyası olmayan eski yüklemelerde yalnız kod listesi üretilir; ekran düğmeyi
devre dışı bırakır.

## 29. Hesap planı içe aktarımı pasife çekiyor

**Karar:** Dosyada olmayan mevcut kodlar silinmiyor, `Aktif = false` yapılıyor; sonuç
DTO'suna `Pasiflenen` sayacı eklendi. Ayrıca `HesapPlaniKaydi.SonGuncelleme` eklendi,
Tanımlar ekranı "son içe aktarım" bilgisini bunun en büyüğünden okuyor.
**Neden:** Prompt §8. Silmek geçmiş ekstre satırlarındaki kodun karşılığını kaybettirirdi.

## 30. Firma bağlamı: sayfa içi seçici yok

**Karar:** Yeni ekranlar `IAppSessionManager.FirmChanged` olayına abone oluyor ve firma
değişince kendilerini yeniliyor. Sayfa içine firma seçici konmadı.
**Neden:** Prompt §8. Uygulamanın üstündeki FİRMA DEĞİŞTİR bağlamı zaten `SelectTenant`
ile yeni JWT üretiyor (`tn` claim'i) ve `HttpCurrentTenant` bunu okuyor; ikinci bir
seçici iki doğruluk kaynağı yaratırdı.

Firma bazlı tablolarda sorgular EF global query filter'dan geçiyor (`TenantNo ==
CurrentTenantNo`); `EkstreSatiri` bağlı olduğu yüklemeden izole oluyor. `EkstreKimlikKayitlari`
ile şablon/desen/kural tablolarında filtre **yok** — kasıtlı, içerik global.

## 31. Menü ve sayfa yapısı

**Karar:** `Banka İşleme` → `İşleme` (`/banka-isleme`, günlük ana ekran) + `Tanımlar`
(`/banka-isleme/tanimlar`). Eski `/banka-isleme/hesaplar` ve `/banka-isleme/yukle`
rotaları kalktı; hesap CRUD'u Tanımlar'ın bir bölümü oldu (`Bolumler/` altında bileşen).
**Neden:** Prompt §8.

Kartlardaki "Dışa aktar" düğmesi onay ekranına götürüyor; iki parça da orada indiriliyor.
Ayrı bir indirme akışı kurmak, kullanıcıyı çıktı listesini görmeden dosya üretmeye
zorlardı.

## 32. İleri dönük alan

**Karar:** `EkstreSatiri.EslesenKarsiSatirId` (nullable) eklendi, dolduran mantık yok.
**Neden:** Prompt §11. İki firma da sistemde olduğu için Aday'dan SMMM'ye giden bir
transferin karşı bacağı diğer firmanın ekstresinde bulunabilir; grup içi çapraz doğrulama
ileride buradan yürüyecek.
