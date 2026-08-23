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

## 33. Banka hesabı içe aktarımı dosyada olmayanı pasife çekmiyor

**Karar:** Hesap planı içe aktarımı dosyada olmayan kodları pasife çekiyor (§29); banka
hesabı içe aktarımı **çekmiyor**, dosyada olmayan hesaba hiç dokunmuyor.
**Neden:** Hesap planı dosyası ORKA'nın tam listesidir — orada olmayan kod gerçekten
kapanmıştır. Banka hesabı dosyası ise kullanıcının elle hazırladığı bir liste; bir
bankayı bilerek dışarıda bırakmış olabilir. Pasife çekmek, o hesabın günlük ekrandaki
sekmesini ve ekstre yüklemesini sessizce kaybettirirdi.

## 34. İçe aktarım `Aktif` ve katman bayraklarına dokunmuyor

**Karar:** Mevcut hesap güncellenirken `Aktif`, `IbanKatmaniAktif`, `VknKatmaniAktif`
korunuyor; yalnız yeni kayıtta `Aktif = true` ile açılıyor. Boş `Parser Tipi` hücresi de
mevcut ayrıştırıcıyı **silmiyor**.
**Neden:** Bu alanların hiçbiri dosya formatında yok (prompt'taki kolon listesi). Dosyada
karşılığı olmayan bir alanı içe aktarımın varsayılana döndürmesi, ekranda bilerek
kapatılmış bir hesabı veya çalışan bir ayrıştırıcı tanımını sessizce geri açardı.

## 35. `Hesap Adı` için yeni alan

**Karar:** `BankaHesabi.HesapAdi` (nullable, 200) eklendi; migration
`20260821082234_AddBankaHesabiIceAktarim`. Tekli CRUD formunda da isteğe bağlı alan olarak
duruyor, liste banka adının yanında gösteriyor.
**Neden:** Dosya formatında `Hesap Adı` zorunlu kolon ama entity'de karşılığı yoktu —
`BankaAdi` bankanın adı ("Vakıfbank"), hesabın ORKA'daki adı değil ("VAKIFBANK VADESIZ
TL"). Kolonu okuyup atmak, kullanıcının dosyaya yazdığı bilgiyi kaybetmek olurdu. Eski
kayıtlarda karşılığı olmadığı için nullable; tekli CRUD zorunlu tutmuyor.

Aynı migration `(TenantNo, OrkaHesapKodu)` benzersiz index'ini de ekliyor: içe aktarımın
upsert anahtarı bu ikili ve tekillik şimdiye kadar yalnız servis katmanında kontrol
ediliyordu.

## 36. Rapor sözleşmesi: `{ field, message }` + satır numarası

**Karar:** İçe aktarım sonucu iki liste taşıyor (`Hatalar`, `Uyarilar`), her ikisi de
`{ SatirNo, Field, Message }`. Hesap planındaki düz `List<string> Uyarilar` kalıbı
kopyalanmadı.
**Neden:** Prompt "her hatalı satır için satır numarası + sebep" ve "mevcut
`{ field, message }` sözleşmesine uy" diyor. Alan adları CRUD'un hata gövdesiyle aynı
kaldığı için istemci tarafında ayrı bir okuma mantığı gerekmedi; `SatirNo` yalnız
kullanıcının dosyada satırı bulabilmesi için eklendi. Listeler 100 kayıtla sınırlı —
bozuk bir dosya ekranı doldurmasın.


## 37. Eşleştirme anahtarları ayrı tablo değil, virgüllü liste

**Karar:** `BankaHesabi.EslestirmeAnahtarlari` — nullable, virgülle ayrılmış tek metin
alanı (300 karakter). Ayrı bir `EkstreHesapAnahtarlari` tablosu açılmadı.
**Neden:** Hesap başına birkaç anahtar var, sorgu ihtiyacı yok (eşleştirme zaten tüm
hesapları belleğe alıyor — `EslestirmeVerisi.BankaHesaplari`), ekranda tek satırlık bir
alan olarak düzenleniyor ve toplu içe aktarımda tek hücreye sığıyor. Ayrı tablo, CRUD'a
ve içe aktarıma birer birleştirme adımı eklemekten başka bir şey getirmezdi.

Eşleşme `Contains` ile değil `Normalizasyon.IfadeVarMi` ile aranıyor: `"TEB"` anahtarı
`"OTEBANK"` içinde de geçiyordu, tam kelime sınırı şart. Mevcut banka adı eşlemesi de
aynı yardımcıya taşındı — iki farklı eşleşme kuralı bırakmanın anlamı yok.

## 38. Anahtar önerisi formda üretilir, kaydederken zorlanmaz

**Karar:** Hesap adından anahtar önerisi `GET .../banka-hesaplari/anahtar-onerisi` ile
alınıp forma yazılıyor. `CreateAsync` alanı boş bulursa **doldurmuyor**.
**Neden:** Prompt "öneri üret ve forma doldur (kullanıcı düzenleyebilsin)" diyor. Sunucu
kaydederken de doldursaydı, kullanıcının bilerek boşalttığı alan sessizce geri gelirdi —
öneri kuralı yanlış anahtar üretebilir ve yanlış anahtar, anahtarsızlıktan kötüdür
(anahtarsızlık satırı onaya düşürür, yanlış anahtar yanlış hesaba yazdırır).

Öneri mantığı yine de sunucuda (`EslestirmeAnahtari.Oner`): Blazor'a kopyalansaydı iki
yerde ayrı ayrı bakımı gerekirdi ve birim testi istemci projesine düşerdi.

## 39. Banka adı denetimi uyarır, engellemez

**Karar:** 25 karakterden uzun veya virgül/tire içeren banka adı kaydediliyor, yalnız
kaydetme sonrası uyarı bildirimi çıkıyor. Denetim istemcide
(`BankaAdiDenetimi`, `WebApp.Shared.Dto.BankaEkstre`).
**Neden:** Prompt "engelleme, sadece uyar" diyor. Sunucuya konsaydı ya hata olurdu
(engelleme) ya da DTO'ya yeni bir "uyarılar" alanı eklemek gerekirdi; alan yalnız formu
ilgilendiren bir biçim tavsiyesi. `"İş Bankası"` gibi meşru adlar da sınırın altında
kaldığı için uyarı gürültü yapmıyor.

## 40. Ayrıştırıcı nullable; "yok" tek bir değerle anlatılıyor

**Karar:** `BankaHesabi.ParserTipi` `string?` oldu ve migration eski boş metinleri
`NULL`'a çevirdi. DTO'da alan `string` (boş metin) olarak kaldı.
**Neden:** İçe aktarım ayrıştırıcısız hesabı zaten boş metinle yazıyordu, tekli CRUD ise
zorunlu tutuyordu — aynı durum iki yerde iki farklı biçimde duruyordu. Veritabanında tek
bir "yok" değeri olsun diye NULL seçildi; DTO'nun boş metni koruması istemci sözleşmesini
(dropdown `ValueProperty="Tip"`, boş seçenek) değiştirmeden bıraktı.

İşleme ekranı ayrıştırıcısız hesabı hiç göstermiyor: boş kartı sürükle-bırak alanıyla
göstermek, kullanıcıyı hata ile biteceği belli bir işleme davet ederdi. Hesap kayıt
defterindeki yerini koruyor.

## 41. Belirsiz banka eşleşmesinde kod önerilmiyor

**Karar:** Aynı bankanın birden fazla hesabı açıklamaya uyuyorsa satır onaya düşüyor ve
`OnerilenHesapKodu` **boş** bırakılıyor; adaylar `Adaylar` listesinde gidiyor.
**Neden:** Prompt "Rastgele veya 'ilk bulunan' seçme" diyor. Onay ekranı adayları zaten
`SecilebilirAdaylar()` ile listeliyor ve `Alt+1..9` ile seçtiriyor, dolayısıyla boş öneri
kullanıcıyı yavaşlatmıyor. Dolu bir öneri ise Enter'a basmayı davet ederdi — yanlış banka
hesabına atılan kaydı fark etmek zor.

Aile ayrımındaki (`AileOnayaDusur`) davranış bilerek farklı: orada tüm adaylar öğrenilmiş
kayıtlardan geliyor ve güven 1.0; burada hangi hesabın kastedildiğine dair hiçbir kanıt yok.

---

# Tur 2 — gerçek veri düzeltmeleri

## 42. Benzersiz önek katmanı: "başlıyor", "içeriyor" değil

**Karar:** Yeni bir katman (`CariOnekIndeksi`, `KaynakKatman.BenzersizOnek`) desen tabanlı
unvan benzerliğinden **önce** çalışıyor. Açıklamanın token dizileri n=4'ten n=2'ye
dolaşılıyor ve hesap adı çekirdeği o diziyle **başlayan** cariler aranıyor.

**Neden "başlıyor":** ORKA hesap adlarını 50 karakterde kesiyor — 6.128 kaydın 914'ü
48–50 karakter ve son kelimesi ortasından kopmuş
(`120 B62 "Baycan Elektrik Müteahhitlik Sanayi Ve Ticaret Ano"`). Açıklamada
"… MÜTEAHHİTLİK SANAYİ VE TİCARET ANONİM" yazdığı için bitişik alt metin eşleşmesi
tutmuyor; önek eşleşmesi kesilmeden etkilenmiyor.

**Ölçüm (gerçek dosya, 87 cari satırı):**

| Yöntem | Otomatik çözülen | Doğru | Yanlış | İsabet |
|---|---|---|---|---|
| Bitişik alt metin + en uzun | 69 | 60 | 9 | %87 |
| **Benzersiz önek (+ alt metin yedeği)** | **57** | **56** | **1** | **%98** |

Daha az satır çözüyor ama neredeyse hiç yanlış yapmıyor. **Bu değiş tokuş bilinçli** —
muhasebede yanlış kayıt, onaya düşen satırdan pahalıdır. Kapsamı artırmak için
gevşetilmemeli.

**İndeks kuralları** (`CariOnekIndeksi.Kur`):
- Yalnız cari grupları: `120, 329, 136, 159, 195, 196, 320, 331, 336`. Gider hesapları
  girmiyor — planda `622 0 03 00 PKF ADAY BAĞIMSIZ DENETİM`, `740 0 BAĞIMSIZ DENETİM`
  gibi firmanın kendi adını taşıyan kayıtlar var, girseler her satır onlara eşleşirdi.
- Adında `BANKASI / BANKA / BANK / FİNANS / KATILIM` geçen cariler çıkarılıyor:
  "ZİRAAT BANKASI" metni `320 1 10011 ZİRAAT BANK` carisiyle eşleşip **16 satırı yanlış
  çözüyordu**. Bankalar banka kayıt defteri katmanının işi.
  *Yan etki:* "Pardus Portföy … Katılım … Fonu" gibi meşru cariler de indekse girmiyor.
  Bilinçli kabul: eleme yalnız aday **çıkarır**, yanlış aday **eklemez**; o satırlar
  benzerlik katmanına düşüyor.
- Hesap sahibinin tüm yazımlarını taşıyan cariler çıkarılıyor (§45).
- Çekirdeği 6 karakterden kısa hesaplar girmiyor. (8 de olur; **12 yapılırsa isabet
  çöküyor — kullanılmamalı.**)
- İndeks firma bazlı ve **yükleme başına bir kez** kuruluyor
  (`EslestirmeVerisi.OnekIndeksi`, tembel), satır başına değil.

**Katman sırası:** geçmiş onay → banka kayıt defteri → vergi/plaka → sabit kural →
**benzersiz önek** → desen tabanlı unvan benzerliği.

## 43. Adaylar tüm n seviyelerinden birleştiriliyor

**Karar:** Bir n seviyesinde durulmuyor; n=4'ten n=2'ye tüm n-gram'ların sonuçları tek
kümede toplanıyor, uzun n-gram'dan gelen liste başına yazılıyor. Küme tek hesaba inerse
otomatik, inmezse satır **tüm adaylarla** onaya düşüyor.

**Neden:** `"KEMAL GÜLMAN VK POLAT GÜLMAN PARK PLAZA 19.KAT D43 (…)"` satırında n=3'teki
`PARK PLAZA KAT` tek sonuç veriyor (`329 P27`), ama n=2'deki `KEMAL GULMAN` ve
`POLAT GULMAN` de birer sonuç veriyor. Uzun n-gram'ın tek sonucunu kabul etmek, karşı
tarafı üç adaydan biri olan satırı yanlış cariye otomatik yazmak olurdu.

## 44. Tek kelimelik unvanda n=1, yalnız desen çıktısında

**Karar:** Ham açıklamada n≥2 aranıyor. **Desenle çıkarılmış unvan tek kelimeden
ibaretse** (`Belbim`, `Superonline`, `Turknet`) ve en az 4 karakterse, o kelimeyle n=1
araması yapılıyor — ve yalnız **tek** sonuç kabul ediliyor.

**Neden:** Ham açıklamada tek kelime aramak gürültü üretir (`FATURA`, `ABONE`, `TAKAS`
her şeye eşleşir). Desenin çıkardığı kelime gürültü değil: bir desen onu karşı tarafın
adı olarak yakalamış. Bu satırlar başka türlü hiç çözülemiyordu — benzerlik katmanında
`"BELBIM"` ↔ `"BELBIM ELEKTRONIK PARA ODEME"` skoru 0.21, yani §48'in eşiğinin altında.

Çok kelimeli unvanda **ham açıklama** kullanılıyor: satırın kalanında geçen diğer cariler
de aday olsun (§43'teki Kemal/Polat/Park Plaza satırı).

## 45. Hesap sahibi çoklu yazım + kapsama kontrolü

**Karar:** `BankaHesabi.HesapSahibiTakmaAdlari` (satır satır, 1000 karakter) eklendi.
Eleme, ana unvan ve takma adların **herhangi birinin** çekirdeğine **kapsama** kontrolüyle
yapılıyor (`HesapSahibiKimligi`) — çekirdek eşitliğiyle değil.

**Neden:** Bankalar aynı firmayı çok farklı yazıyor; gerçek dosyada altı yazım sayıldı.
Beşi kapsama kontrolüne takılıyor (`ADAY BAĞIMSIZ DENETİM` ⊂ `PKF ADAY BAĞIMSIZ DENETİM`),
biri takılmıyor: `ADAY BAĞIMSIZ DENETİM VE SMMM A.Ş.` → çekirdek
`ADAY BAGIMSIZ DENETIM SMMM`. O yüzden hem kapsama hem takma ad listesi gerekli.

Kapsamaya girecek en kısa çekirdek 6 karakter: `"ADAY"` gibi kısa bir yazım tüm carileri
elerdi.

**Öneri üretimi:** `GET .../banka-hesaplari/{id}/hesap-sahibi-onerileri` yüklenmiş
ekstrelerin **çıkarılan unvanlarını** tarıyor, tanımlı çekirdeklerden biriyle en az iki
ardışık kelime paylaşan ama kapsamaya takılmayanları döndürüyor. Ham açıklamayı baştan
taramak yerine çıkarılan unvanlar kullanılıyor: bankanın firmayı nasıl yazdığı zaten orada.

## 46. Hesap sahibinin adı token dizisinden çıkarılıyor

**Karar:** Benzersiz önek katmanı, ham açıklamanın token dizisinden hesap sahibinin adına
denk gelen bölümleri **çıkarıyor** ve kalan parçaları ayrı ayrı geziyor
(`HesapSahibiKimligi.Parcala`). n-gram'lar parça sınırını aşmıyor.

**Neden:** Ölçülen 287 satırın 268'inde açıklamada firmanın kendi unvanı geçiyor.
Çıkarılmazsa `BAGIMSIZ DENETIM` dizisi `120 B58 Bağımsız Denetim Derneği` gibi **başka**
bir cariye eşleşiyor. Parçaların birleştirilmemesi de şart: çıkarılan adın iki yanındaki
kelimeler yan yana gelirse gerçekte açıklamada olmayan bir dizi üretilir.

## 47. Yön kuralı yalnız adlar birebir aynıyken çalışır

**Karar:** Aday listesindeki hesapların **hesap adı çekirdeği aynı** ve fark yalnız ana
gruptaysa yön karar veriyor: çıkan → `329`/`320`, giren → `120`/`159`
(`CariOnekIndeksi.YonleCoz`). Adlar farklıysa yön hiçbir şey yapmıyor.

**Neden:** Onaya düşen satırların büyük kısmı gerçek belirsizlik değil, aynı carinin iki
grup altındaki kopyası: Zafer Genç, Burak Günel, Yurtiçi Kargo, Aras Kargo, Ufuk Çolak —
hepsi `159` + `329` çifti, adlar birebir aynı. Gerçek belirsizlik adların **farklı**
olduğu durumdur (Park Plaza Aidat/Elektrik/19. Kat, Pardus fonları, Cms Jant / Cms Jant
Makina) ve onaya düşmeye devam ediyor.

## 48. 0.40 altındaki benzerlik önerisi hiç gösterilmiyor

**Karar:** `HesapEslestirici.EnAzOneriEsigi = 0.40m`. Unvan benzerliği katmanında en iyi
aday bunun altındaysa satır `Çözülemedi` oluyor, kod kutusu boş kalıyor, aday listesi de
üretilmiyor.

**Neden:** Ölçümde `Superonline Tahsilatı` satırına **0.20 skorla**
`329 A33 Adobe Systems Ireland`, `Turknet Tahsilatı` satırına 0.21 ile `329 N21 Novatek`
öneriliyordu. Alakasız öneri boş kutudan **kötüdür**: kullanıcı yanlışlıkla onaylar ve
sistem onu öğrenir.

## 49. Belirsizlik kararı öğreniliyor — aday kümesi özetiyle birlikte

**Karar:** Yeni anahtar tipi `AnahtarTipi.Belirsizlik`. Anahtar belirsizliği üreten
n-gram(lar), değer seçilen hesap kodu. `HesapEslesmesi.AdayKumesiOzeti` aday kod
listesinin SHA-256 özetini tutuyor; kayıt yalnız **aynı küme** tekrar geldiğinde
uygulanıyor.

**Neden:** Aksi hâlde yeni açılan bir Park Plaza hesabı hiç görünmez olurdu — eski karar
sessizce uygulanır ve satır bir daha sorulmazdı.

**Belirsizlikten gelen onay, sade unvan çekirdeği anahtarı yazmıyor.** Yazsaydı o kayıt
(aday kümesi denetimi olmayan geçmiş onay katmanı) belirsizlik kaydından **önce** çalışır
ve güvenlik kaydını devre dışı bırakırdı. Global kimlik kaydı (`KimlikKaydi`) yine
yazılıyor: bir unvanın kim olduğu belirsizlikten bağımsız.

Kayıtlar Tanımlar > Öğrenilen Eşleşmeler ekranında "belirsizlik" rozetiyle görünüyor,
düzenlenebiliyor ve silinebiliyor.

## 50. Aile ayrımı: tam küme üzerinden ve taraflıysa hiç yapılmıyor

**Karar:** `AileyiAyikla` iki noktada sıkılaştırıldı:
1. Karar **kırpılmamış** aday kümesiyle veriliyor (ekranda ilk 8 gösteriliyor).
2. Bir üyenin ayırt edici kelimesi hiç yoksa (adı diğerlerinin ortak çekirdeğinden ibaret)
   ayrım **hiç yapılmıyor**.

**Neden (1):** 37 üyeli Pardus ailesinde ilk 8 üzerinden karar verilince "tek üye uydu"
sanılıyor ve `PARDUS PORTFÖY MARMARA HİSSE SENEDİ SERBEST FON` satırı
`120 F07 Pardus Portföy Para Piyasası` fonuna otomatik yazılıyordu — ölçümde 13 satır.
**Neden (2):** `Cms Jant` / `Cms Jant Makina` ailesinde `Cms Jant`'ın ayırt edici kelimesi
yok, yani hiçbir zaman kazanamaz; `MAKINA` kelimesine bakıp karar vermek taraflı olur.

## 51. Alt metin yedeğinde hesabın ilk kelimesi de metinde geçmeli

**Karar:** Önek hiç tutmadığında devreye giren bitişik alt metin yedeğinde, eşleşen
hesabın **ilk kelimesi** de metinde geçmiş olmalı (`IcerenlerGuvenli`). İstisna: tek
kelimelik unvan aramasında (§44) bu şart aranmıyor — orada metin zaten o tek kelimeden
ibaret ve `"Superonline"` ile `329 T06 Turkcell Superonlıne` başka türlü eşleşemez.

**Neden:** Yedek katman, açıklamanın unvanın **önüne** bir şey eklediği durum için var
(`"NAOSKZ NAOS İSTANBUL KOZMETİK"` → `Naos İstanbul Kozmetik`). Tersi geçerli değil:
`"… SAĞLAMOĞLU YETKİLİ MÜESSESE ANONİM ŞİRKETİ hesabından …"` metni
`120 H30 Hakan Yetkili Müessese` adının ortasına oturuyor ve satırı yanlış cariye
çözüyordu.

## 52. Açıklamanın sonundaki satıcı adı ayrı bir desen

**Karar:** Yeni unvan deseni **sıra 5**'te (diğerlerinden önce), sabiti
`BankaEkstreSeed.TahsilatDeseni`. Yakalama rakam ve iki nokta içermiyor, bu yüzden
`Abone No:22912623` / `Fatura No:…` alanlarına giremiyor; kuyruktaki genel ekler
(`Temsilci`, `Bayi`, `Abone`, `Fatura`, `Ses/Data/ICT`) yakalamanın dışında bırakılıyor.

| Açıklama kuyruğu | Çıkan unvan | Hesap |
|---|---|---|
| `…,Belbim Temsilci Tahsilatı` | Belbim | `329 B43` |
| `…Tutar:2.764,90  SuperonlineTahsilatı` | Superonline | `329 T06` |
| `…Tarihi:19.08.2026 Türk Telekom Ses/Data/ICT Tahsilatı` | Türk Telekom | `329 T01` |
| `…Son Ödeme Tarihi:23.07.2026 Turknet Tahsilatı` | Turknet | `329 T61` |

**Neden önce:** Aynı metinde `Ad Soyad/Unvan:` alanı da var ve mevcut desenler oraya
takılıyordu.

## 53. "Ad Soyad/Unvan:" alanları hiç unvan kaynağı değil

**Karar:** Yakalama bu etiketlerden birinin hemen ardından başlıyorsa desen atlanıyor
(`UnvanCikarici.UnvanAlanindanMi`): `AD SOYAD UNVAN`, `ADI UNVANI`, `SOYADI UNVANI`.
Karşılaştırma normalize metinde, yakalamanın önündeki 40 karakter içinde.

**Neden:** O alan hesap sahibinin kendi unvanı, karşı taraf değil — ve bazen maskeli
(`PK* AD** BA****** DE*****`), yani hesap sahibi elemesine (§45) takılmıyor. Etiketin
kendisini engellemek maskelemeden bağımsız çalışıyor.

## 54. Vergi tahsilatı: yönetilebilir eşleme tablosu + plaka anahtarı

**Karar:** Yeni **global** tablo `EkstreVergiKodlari` (`VergiKoduEslemesi`): vergi kodu
ve/veya anahtar kelime → ORKA hesap kodu. Yeni katman (`KaynakKatman.VergiPlaka`) sabit
kuraldan önce çalışıyor. Bu satırlarda **unvan hiç çıkarılmıyor**.

**Neden tablo:** Karşı hesap metnin içeriğine göre değişiyor; gerçek dosyadaki 5 vergi
satırı **dört farklı hesaba** gitmiş. Tek kural yetmiyor.

**Neden global:** Vergi kodları (`0040` damga, `0033` kurum geçici) firmadan firmaya
değişmez — `SabitKural` ile aynı yaklaşım. Firmaya özel kırılım gerekiyorsa satır
Tanımlar ekranından düzenlenir.

**Neden unvan çıkarılmıyor:** Açıklamadaki `Soyadi/Unvani :PKF ADAY …` hesap sahibinin
kendi unvanı. Çıkarılsaydı satır cari katmanlarına düşerdi.

Tohumlanan satırlar: `9085 / TRAFİK CEZ → 689 9 1`, `0040 / DAMGA → 360 01 004`,
`0033 / BEYANNAME → 770 04 001`. Eşlemesi olmayan kod (ölçümde `0010/KURUMLAR V.`)
onaya düşüyor — tahmin edilmiyor.

**Plaka anahtarı:** Metindeki plaka (`Plaka:34MRP081`, `34MRP471 Nolu plakanın`) hesap
planında adında o plakayı taşıyan hesapları aday yapıyor; karşılaştırmada boşluklar
temizleniyor (planda `740 99 01 01 09 — 34 Mrp 081 …`, metinde `34MRP081`). Plaka **tek
başına karar vermiyor**: aynı plakanın birden fazla hesabı olabildiği için adayları
daraltıyor ve satır onaya düşüyor. Aynı mantık HGS/otoyol yükleme satırlarında da
geçerli; alt hesabı kullanıcıdan beklenen kurallarda (personel/iş avansı) plaka aranmıyor.

## 55. Açıklama şablonu önce ham açıklamada aranıyor

**Karar:** `AciklamaUretici.SablonBul` önce ham açıklamayı tarıyor (yalnız `Icerir` /
`Regex` şablonları), sonra işlem tipini. Karşılaştırma `Normalizasyon.KisaltmaNormalize`
üzerinden: nokta siliniyor, `E.F.T.` ile `EFT` aynı biçime iniyor.

**Neden:** `"HESAPLAR ARASI E.F.T. VAKIFBANK/DENİZBANK …"` satırının işlem tipi
`"Gelen EFT Otomatik Yatan"`; genel şablona düşüyor ve üretilen açıklama karşı bankayı
hiç yazmıyordu. Açıklamada geçen ifade işlem tipinden daha belirleyici.

## 56. Onay ekranında en fazla 8 aday gösteriliyor

**Karar:** Aday listesi ekranda 8 ile sınırlı (`EnFazlaAday`), ama **karar ve aday kümesi
özeti tam küme üzerinden** hesaplanıyor.

**Neden:** 37 üyeli Pardus ailesini tek satırda listelemek ekranı kullanılmaz yapardı;
kullanıcı listede yoksa kodu doğrudan kutuya yazıyor. Kararın tam küme üzerinden
verilmesi §50'nin şartı; özetin tam küme üzerinden alınması §49'un.

## 57. Banka kayıt defteri katmanı iki koşuldan biriyle açılır

**Karar:** Katman artık "açıklamada banka adı geçiyor" diye tetiklenmiyor. En az biri
sağlanmalı:

- **(a)** Metinde (ham açıklama **veya** işlem tipi) bankalar arası ifadesi var:
  `hesaplar arası`, `hesaplararası`, `virman`, `süpürme`.
- **(b)** Çıkarılan karşı taraf hesap sahibinin kendisi: en az bir desen sahibin unvanını
  yakalamış **ve** geriye gerçek bir firma kalmamış (hiç unvan yok, ya da kalan yakalama
  bir banka adı).

İkisi de tutmazsa katman atlanıyor, satır cari katmanlarına düşüyor; orada da çözülemezse
onaya gidiyor — yanlış çözmektense soruyor.

**Neden:** Eski tetikleyici (`Sablon.BankalarArasi || HesapSahibiElendi`) müşteri
ödemelerinde de çalışıyordu, çünkü o satırlarda **gönderenin bankası** yazıyor. Ölçüm:
87 cari satırının **59'unda** açıklamada banka adı geçiyor —
`BAYCAN A.Ş. CARİ HESAP ÖDEME/TÜRKİYE CUMHURİYETİ ZİRAAT BANKASI …`,
`NAOSKZ NAOS İSTANBUL KOZMETİK…/TÜRKİYE GARANTİ BANKASI …`, tüm personel masraf
ödemeleri. Bunların hepsi cari katmanlarına gitmeli.

Ölçülen iki yanlış otomatik eşleşme bu yolla düzeldi:

| Satır | Eskiden | Şimdi |
|---|---|---|
| `MARBAŞ MENKUL DEĞERLER ÖDEME (… Akbank T.A.Ş. MARBAŞ MENKUL DEĞERLER ANONİM ŞTİ. hesabından …)` | `102 1 4 01 Akbank` | `120 M40 Marbaş Menkul Değerler` |
| `PKF BAĞIMSIZ DENETİM FİRMASI ÖDEMESİ (… Türkiye İş Bankası A.Ş. DEMET DÖVİZ … hesabından …)` | `102 1 5 01 İş Bankası` | `120 D50 Demet Döviz Yetkili Müessese` |

**(a) neden hem açıklamada hem işlem tipinde aranıyor:** Aynı bilgi bazen açıklamada
(`HESAPLAR ARASI E.F.T. VAKIFBANK/DENİZBANK …`), bazen yalnız işlem tipinde
(`Virman`, `Otomatik Süpürme İşlemleri Virman`) duruyor. Karşılaştırma
`Normalizasyon.KisaltmaNormalize` ile, tam kelime sınırıyla; `HESAPLARARASI` bitişik
yazımı ayrı ifade olarak aranıyor.

**(b)'de "kalan yakalama banka adıysa" istisnası neden var:** Bankalar arası ifadesi
taşımayan gerçek self-transferlerde parantez öncesi serbest metin unvan sanılıyor —
`"İŞ BANKASI  (PKF ADAY … VADESİZ HESABINDAN … NO'LU PKF ADAY … HESABINA …)"`,
`"DENİZBANK HESABINA (…)"`. Bunlar karşı taraf değil, transferin gittiği bankadır.
Tespit iki aşamalı: önce genel banka kelimeleri (`… BANKASI`, `… BANK`), sonra **kayıt
defterinde tanımlı** banka adları ve eşleştirme anahtarları — `DENİZBANK HESABINA`
yazımında genel kelime yok, ayırt eden şey bankanın kayıt defterinde bulunması.

Ölçüm: 48 bankalar arası satırın 42'si (a) ile, kalan 6'sı (b) ile yakalanıyor. Fikstür
planıyla katmanın çözdüğü satır sayısı 22'den 20'ye indi; ikisi de yanlış eşleşmeydi.

**Genişletme kuralı da daraldı:** "Aynı bankanın tüm hesapları aday olsun" genişletmesi
(açıklamada hiç banka adı geçmeyen `Hesaplararası Virman` satırları için) yalnız **(a)**
ile açılan satırlarda yapılıyor. (b) ile açılanlarda yapılsaydı karşı tarafı gerçek bir
cari olan satırlar cari eşleştirmesine hiç gidemeden banka adaylarıyla onaya düşerdi.

---

# Tur 3 — kişi eşleştirmesi

## 58. Kural grubu içindeki alt hesap araması: benzersiz önek, benzerlik değil

**Karar:** Sabit kural yalnız ana grubu veriyorsa (`AltHesapGerekli`) grup içindeki kişi
muavini artık **önek eşleşmesiyle** aranıyor: çıkarılan isim, hesap adının **token
sınırında biten öneki** olmalı. Benzerlik skoru (`Benzerlik.Oran`) bu aramada hiç
kullanılmıyor.

Karar tablosu:

| Durum | Sonuç |
|---|---|
| Ad + soyad (≥2 kelime), grup içinde tek eşleşme, başka grupta karşılık yok | **Otomatik** (güven 0.95) |
| Birden fazla eşleşme | Onaya düşer, **hepsi** aday listelenir |
| Tek kelimelik isim (`İlyas`) | **Hiçbir zaman otomatik değil** |
| Hiç eşleşme yok | Alt hesap **boş**; yalnız ana grup (`195`) önerilir |

**Neden:** Kural ana grubu doğru belirliyordu ama grup içi arama difflib benzerliğiyle
yapılıyor ve **yanlış kişiyi** seçiyordu. Gerçek dosyadan ölçülen üç satır:

```
"ABDULKADİR SAYICI Masraf Ödemesi Arta Tekmer"
   önce → 195 01 A20  Abdülkadir Yılmaz  (0.65)
"dilara sager masraf ödemesi"
   önce → 195 01 D06  Dilara Kaya        (0.67)
"… Akbank T.A.Ş. İlyas hesabına giden FAST ödemesi"   (soyad yok)
   önce → 195 01 I02  İlyas Yücel        (0.45)   — planda İlyas Ömeroğlu da var
```

Satırlar onay kuyruğunda olduğu için kayıt bozulmuyordu; **tehlike onay anında**: kutuda
hazır duran yanlış kişi ONAYLA'ya basarken kolayca gözden kaçıyor. "Yakın isimli başka
kişi" önerisi boş kutudan kötüdür — bu, §48'in (0.40 eşiği) kişi muavinlerindeki karşılığı.

**Tek kelimelik isim neden hiç otomatik değil:** Tek başına `ABDULKADIR` planda yalnız
`Abdülkadir Yılmaz`'ı tutsa bile o kişi olduğunu göstermez; ölçülen dosyada aynı adın iki
sahibi var (`İlyas Ömeroğlu`, `İlyas Yücel`). Bir eşleşme çıksa da satır onaya düşer.

**Arama neden ön indeksten geçiyor:** Gerçek plan 6.000+ kayıt. `HesapPlaniIndeksi`
ilk kelimeyle ikili arama yapıp bitişik bloğu veriyor; her avans satırında tüm plan
taranmıyor (§25c ile aynı gerekçe).

## 59. Kural ana grubu tek başına kilitlemiyor

**Karar:** Kural bir ana grup önerdiğinde, aynı ismin **başka cari gruplarındaki birebir
eşleşmeleri** de aday olarak gösteriliyor ve satır onaya düşüyor. Kural grubundaki adaylar
listenin başında duruyor; kod kutusunda kuralın ana grubu (`195`) kalıyor.

**Neden:** `ABDULKADİR SAYICI` hesap planında **gerçekten var** — ama `331 02` (ortaklara
borçlar) altında. Kural `195`'e kilitlediği için kişi hiç bulunamıyordu ve kullanıcı kodu
elle aramak zorundaydı.

**Neden önek değil, tam eşitlik:** Kural grubunun dışına çıkmak ancak isim **aynen**
tutuyorsa meşru. Önek yeterli sayılsaydı her avans satırına ilgisiz cariler eklenirdi
(`ALİ` → `Ali Rıza Tekstil A.Ş.`). Arama uzayı `CariOnekIndeksi.CariGruplari`
(120/329/136/159/195/196/320/331/336) ile sınırlı: gider hesapları aday olmamalı.

**Yan etki — aday listesi artık tek elemanlıyken de saklanıyor.** `AdaylariYaz` eskiden
yalnız birden fazla adayı JSON'a yazıyordu; tek aday (`331 02`) kayboluyor ve onay ekranında
hiç görünmüyordu. Eşik `Count == 0` oldu.

## 60. Kişi yönlendirme tablosu: sabit kuraldan önce çalışan yeni katman

**Karar:** `EkstreKisiYonlendirmeleri` (firma bazlı) tablosu eklendi:
`IsimCekirdegi`, `Isim`, `Yon` (Giren/Çıkan/Farketmez), `HesapKodu`, `HesapAdi`,
`Aciklama`, `Aktif`. Katman **tüm katmanlardan önce** — sabit kuraldan da önce — çalışıyor
ve tutarsa satır güven 1.0 ile otomatik çözülüyor (`KaynakKatman.KisiYonlendirme = 10`).

**Neden koda gömülmedi:** Sabit kural işlemin **niteliğini** biliyor ("masraf ödemesi"),
kişinin **ne olduğunu** bilmiyor. Ortak ve yöneticiler için aynı ifade `331`'e gitmeli,
personel avansına değil. Kimin ortak olduğu firmaya özel ve zamanla değişir; kullanıcı
kendi tanımlamalı.

**Neden sabit kuraldan önce:** Sonra çalışsaydı hiç sıra gelmezdi — kural açıklama
kapsamlı ve Katman 0'da her `masraf ödemesi` satırını kapıyor.

**Neden firma bazlı (vergi kodlarının aksine):** Vergi kodu (`0040` = damga) her firmada
aynıdır; kimin ortak olduğu değildir. `TenantNo` + query filter, `BankaHesabi` ile aynı
kalıp. Prompt'taki `FirmaId` alanı bu depoda `TenantNo` karşılığıyla uygulandı — modülün
tamamı öyle ölçekleniyor.

**Yön neden ayrı bir enum (`YonlendirmeYonu`):** Yönlendirme "iki yönde de aynı hesap"
diyebilmeli; ekstre satırının yönü ise her zaman kesin. Aynı kişi için giden ödeme `331`,
gelen tahsilat başka bir hesap olabilir. Yönü belirtilmiş kayıt `Farketmez` kaydını yener.

**İsim iki yerde aranıyor:** önce çıkarılan unvanın çekirdeğiyle **tam eşitlik**, tutmazsa
ham açıklamanın çekirdeğinde **tam kelime dizisi** olarak (`Normalizasyon.IfadeVarMi`).
İkincisi şart: ölçülen dosyada aynı kişi bir satırda desenin yakaladığı yerde
(`… A.Ş. ABDULKADİR SAYICI hesabına …`), başka bir satırda açıklamanın başında
(`ABDULKADİR SAYICI Masraf Ödemesi Arta Tekmer`) geçiyor. Benzerlik **hiç** kullanılmıyor —
`Abdülkadir Şahin` tanımlıyken `Abdulkadir Sayıcı` satırı tutmamalı.

**Denetimler:** hesap kodu planda yoksa kaydedilmiyor (yönlendirme bir daha sorulmadan
uygulanacak; yanlış kod her ay sessizce yanlış hesaba yazardı), aynı isim + yön için ikinci
kayıt reddediliyor (hangisinin uygulandığı kayıt sırasına kalmasın). Plan hiç yüklenmemişse
kod denetimi atlanıyor, kurulum sırası bozulmasın.

**Onay ekranından kısayol:** Satır onaylanırken "bu kişiyi hep bu hesaba yönlendir"
seçilirse kayıt otomatik oluşuyor; yön o satırın yönünden geliyor, isim satırın çıkarılan
unvanından. Unvan okunamamışsa kayıt yazılmıyor ve uyarı dönüyor — sessizce boş isimli bir
yönlendirme oluşturmaktansa kullanıcıya söylemek gerek.

## 61. Analiz dökümü ayrı bir dışa aktarım; ORKA kısıtı korundu

**Karar:** Yeni uç nokta `POST …/ekstre/{id}/analiz-dokumu` ve "Analiz için dışa aktar"
düğmesi. Durumu ne olursa olsun **tüm satırları** xlsx olarak veriyor:
`SiraNo | Tarih | Yon | Tutar | HamAciklama | UretilenAciklama | OnerilenHesapKodu |
OnerilenHesapAdi | GuvenSkoru | KaynakKatman | Durum | AdaySayisi`.

"Kod listesi" ve "Düzeltilmiş ekstre" onay bekleyen/çözülemeyen satır varken **400 dönmeye
devam ediyor** — eksik listeyle ORKA'ya gitmenin anlamı yok (§28).

**Neden ayrı uç nokta:** İki dosyanın amacı farklı. ORKA'ya giden çıktı eksiksiz olmalı;
analiz dökümü ise tam olarak **eksik satırları incelemek** için var. Aynı uç noktaya
"zorla" bayrağı eklenseydi kısıt kazara atlanabilirdi.

Düğme adı ve yanındaki açıklama dosyanın ORKA'ya yüklenmediğini açıkça söylüyor; dosya adı
da `…-analiz.xlsx` (ORKA'ya giden `…-duzeltilmis.xlsx`).
