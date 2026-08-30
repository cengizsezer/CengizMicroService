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

## 62. Modül "Banka Otomasyon"; girişi firma seçim ekranı

**Karar:** Menüde `Banka İşleme` → **`Banka Otomasyon`**, alt menüde `İşleme` → **`Aktar`**.
Rotalar `/banka-otomasyon/...`. Modülün girişi artık firma listesi (`/banka-otomasyon`),
firma içi yapı iki sekme: **Aktar** (günlük iş + banka kapsülü) ve **Tanımlar** (firmanın
kurulumu).

| Eski | Yeni |
|---|---|
| `/banka-isleme` | `/banka-otomasyon` (firma listesi) |
| `/banka-isleme/firma-tanimlari`, `/banka-isleme/tanimlar` | `/banka-otomasyon/tanimlar` |
| `/banka-isleme/onay/{id}` | `/banka-otomasyon/onay/{id}` |

Eski rotalar `EskiRotaYonlendirme.razor` içinde duruyor ve `replace: true` ile yenisine
yönlendiriyor — kayıtlı bağlantılar ve yer imleri kırılmıyor, geri düğmesi de sonsuz
yönlendirmeye girmiyor.

**`/banka-isleme` kökü neden Aktar'a değil firma listesine gidiyor:** Yer imini açan
kullanıcının hangi firmada olduğu belirsiz. Sessizce bir firmanın Aktar ekranını açmak,
modülün baştan aşağı önlemeye çalıştığı riskin ta kendisi olurdu.

**Ekran kalıbı Raporlar'dan kopyalandı** (`/firmakontrol`): tablo + sağda `GİRİŞ` düğmesi,
satıra tıklamak da giriyor. Kendi tasarımı üretilmedi; iki ekran aynı işi yapıyor.

## 63. Seçilen firma gerçekten tenant bağlamını değiştiriyor — GEÇERSİZ (bkz. §68)

> Bu karar geri alındı. Kapsam tenant değil `catalog.Firmalar.Id`; modül tenant bağlamına
> hiç dokunmuyor. Aşağıdaki metin, kararın neden alındığını ve neyin değiştiğini
> anlaşılır kılmak için duruyor.

**Karar:** `GİRİŞ`, `IAppSessionManager.SelectFirmAsync` çağırıyor — yani yeni access token
üretiliyor ve `tn` claim'i o firmaya geçiyor. Bağlamın sahibi yeni bir scoped servis:
`BankaOtomasyonOturumu` (`IBankaOtomasyonOturumu`).

**Neden bu yol:** Sunucuda tenant `HttpCurrentTenant` ile önce JWT'nin `tn` claim'inden
okunuyor, başlık ancak claim yoksa devreye giriyor. Yani istemciden tenant'ı değiştirmenin
tek gerçek yolu token'ı yenilemek. Ekranda firma seçip isteği başlıkla yönlendirmeye
çalışmak sessizce çalışmazdı: ekran "PKF Aday" yazarken veri SMMM'ye yazılırdı.

**Sıra kritik:** Firma içi her ekran açılışta `BaglamiHazirlaAsync()` çağırıyor ve ancak
`null` dönmezse veri çekiyor. Böylece ekranın **ilk** isteği bile doğru firmaya gidiyor.
`null` dönerse (seçim yok) firma listesine yönlendiriliyor.

**Çakışma çözümü — sayfadaki seçim kazanır:** Modül ekranı açıkken üstteki genel
`FİRMA DEĞİŞTİR` kullanılırsa modül kendi firmasını geri uyguluyor ve bildirimle uyarıyor.
Üstünlük yalnız `Oturum.Aktif` iken geçerli; firma listesine dönüldüğünde bayrak kapanıyor,
modül dışında kullanıcının genel seçimine karışılmıyor.

Bu sırada `FirmChanged` iki kez tetikleniyor (önce gelen firma, sonra geri uygulanan).
Firma içi ekranlar olayı **seçili firmayla eşleşmiyorsa yok sayıyor**; aksi halde bir
anlığına yanlış firmanın verisi için istek atılırdı.

**Seçim oturum boyunca hatırlanıyor:** WASM'de scoped servis uygulama ömrü kadar yaşıyor,
sekme değişimlerinde seçim korunuyor. Sayfa yenilemesinde servis sıfırlandığı için seçim
`sessionStorage`'dan geri geliyor (`IBankaOtomasyonDeposu` — mantık tarayıcı deposuna
bağlanmadan test edilebilsin diye ayrı arayüz).

**Test:** İstemci tarafında `BankaOtomasyonOturumuTests` (sahte oturum yöneticisiyle:
girişin tenant'ı çevirdiği, sekme değişiminde tekrar sorulmadığı, yenilemede geri geldiği,
çakışmada sayfanın kazandığı, modül kapalıyken karışılmadığı). Sunucu tarafında
`FirmaTenantIzolasyonuTests`: **Aday seçiliyken yapılan hesap planı içe aktarımı Aday'ın
kayıtlarına yazılıyor**, SMMM'ninkiler bozulmuyor — iki firma aynı veritabanını paylaşıyor.

## 64. Firma seçim ekranının sayaçları: tenant filtresi tek yerde baypas — GEÇERSİZ (bkz. §69)

> Baypas tamamen kaldırıldı: global query filter kalkınca çok firmalı sayım sıradan bir
> sorgu oldu. Metin, baypasın neden var olduğunu göstermek için duruyor.

**Karar:** Yeni uç nokta `GET …/banka-ekstre/firmalar/ozet?tenantlar=201&tenantlar=106`,
tek servis `FirmaOzetService`. Global query filter `IgnoreQueryFilters()` ile atlanıyor ve
istenen her tenant için `{hesap planı sayısı, banka hesabı sayısı, onay bekleyen}` dönüyor.

**Neden baypas gerekti:** Ekran firmaya **girilmeden önce** açılıyor; token'da tek `tn`
claim'i var. Filtreye uyularak yalnız o an seçili firmanın sayıları okunabilirdi, diğer
satırlar boş kalırdı.

**Risk nasıl sınırlandı:** Baypas tek dosyada ve yalnız **adet** üretiminde; kayıt içeriği
hiç dönmüyor. Modülün geri kalan tüm sorguları izolasyonunu aynen koruyor. Hangi firmaların
sorulacağını istemci belirliyor ve listeyi login yanıtındaki **kendi** firmalarından
kuruyor; CatalogService token'da diğer firmaları göremediği için doğrulayamıyor. Sızabilecek
en fazla şey, bilinen bir firma numarasının kayıt adedi.

**"Onay bekleyen" tanımı:** Firmanın **tüm** bankaları ve **tüm** dönemleri toplanarak,
durumu `OnayBekliyor` veya `Cozulemedi` olan satır sayısı. Aktar ekranındaki banka rozeti
yalnız seçili dönemi sayar; iki sayı kasten farklı ve ekranın altında yazıyor. Firma
listesinde "bu firmada iş var mı?" sorusunun cevabı dönemden bağımsız olmalı.

Satırın kendi `TenantNo`'su yok (izolasyonu bağlı olduğu yüklemeden alıyor), bu yüzden
sayım `EkstreYuklemeler` üzerinden join ile yapılıyor.

## 65. Banka hesapları CRUD'u Tanımlar'a döndü

**Karar:** Tam CRUD (`Yeni hesap`, `Toplu İçe Aktar`, `Örnek şablon indir`, düzenle, sil)
Tanımlar ekranında, hesap planının hemen altında. Kapsüldeki
"Bu bankanın kuralları → Ayrıştırıcı ayarları" bölümü **kaldı** ama yalnız ayrıştırıcı ve
katman bayraklarını düzenliyor; her satırda tam düzenlemeye giden bağlantı var
(`/banka-otomasyon/tanimlar?hesap={id}`).

**Neden kapsülde olamaz:** (1) Banka hesabı tanımı bankaya değil **firmaya** ait bir kayıt —
ORKA kodu, IBAN, hesap tipi, eşleştirme anahtarları hep firmanın muhasebesinin verisi.
(2) Yeni bir banka eklenirken o bankanın sekmesi henüz yok; kapsülün içinden erişilemeyen
bir yerde dursaydı yeni banka hiç eklenemezdi.

Kapsülün "Yeni hesap" düğmeleri artık Tanımlar'a yönlendiriyor (`?yeniHesap=1` formu
açıyor). `BankaHesaplariBolumu`'nun `BankaFiltresi` parametresi ve `YeniHesapAc` metodu
kaldırıldı: bölüm artık tek yerde ve daima tüm hesapları listeliyor.

## 66. Banka adı otomatik tamamlamalı; yeni yazım uyarı veriyor

**Karar:** Form alanı `RadzenAutoComplete`, verisi mevcut hesapların banka adları. Serbest
yazma engellenmiyor (gerçekten yeni banka eklenebilmeli) ama listede olmayan yazım
`BankaAdiDenetimi.YeniBankaUyarisi` ile uyarı üretiyor.

**Neden gerekli:** Sorun görüntüsel değil. "Aynı banka önceliği" kuralı `BankaAdi` üzerinden
çalışıyor; aynı banka iki yazımla girilince (`İş Bankası` / `İŞ BANKASI`) sistem onları ayrı
bankalar sayıyor, sekme sayısı şişiyor (9 sekme, 8 banka) ve bankalar arası eşleştirme
bozuluyor.

**Karşılaştırma sekme şeridiyle birebir aynı** (`OrdinalIgnoreCase` + kırpma). Uyarı böylece
tam olarak "yeni bir sekme açılacak mı?" sorusunu yanıtlıyor. Türkçe sonucu bilinçli:
`ZIRAAT` ile `Ziraat` aynı sayılıyor, ama `İŞ BANKASI` ile `İş Bankası` **ayrı** — ordinal
karşılaştırma `ı` ile `I`'yı eşlemiyor ve sekme şeridi de tam bu yüzden ikiye bölünüyor.
Uyarının çıkması doğru: kullanıcının düzeltmesi gereken şey zaten bu.

**Eşleştirme mantığına dokunulmadı.** Gruplamayı kültür duyarlı hale getirmek tutarsızlığı
gizlerdi; görev kullanıcının düzeltebilmesini istiyor, sistemin örtmesini değil.

## 67. Yükleme ve içe aktarım onaylarında firma adı

**Karar:** Üç yerde dosya seçildikten sonra firma adıyla onay diyaloğu çıkıyor: ekstre
yükleme (Aktar), hesap planı içe aktarımı ve banka hesapları toplu içe aktarımı (Tanımlar).
Hesap silme de onaylı hale getirildi. Sonuç bildirimleri de firma adıyla başlıyor
(`PKF Aday · 287 satır okundu · …`).

**Prompt'taki "PKF Aday için 287 satır yüklenecek" neden birebir değil:** Satır sayısı dosya
**sunucuda ayrıştırılmadan** bilinemiyor; onay ise istek atılmadan önce sorulmalı. Onayda
firma adı + dosya adı, sonuç bildiriminde firma adı + okunan satır sayısı var. Kritik olan
yarı — hangi firmaya yazılacağı — onaydan önce ekranda.

## 68. Firma kapsamı tenant değil, `catalog.Firmalar.Id` — §62/§63/§64 düzeltildi

**Belirti:** Raporlar (`/firmakontrol`) 8 firmayı gerçek VKN'leriyle listeliyordu
(PKF Aday `0070511435`, PKF İstanbul SMMM `7300717173` …). Banka Otomasyon ise tek satır
gösteriyordu: `PKF Istanbul SMMM A.Ş / 1234567890`.

### Önce Raporlar incelendi

| Soru | Cevap |
|---|---|
| Firma listesi nereden geliyor? | `GET /catalog/firmalar` → `FirmalarController` → `FirmaService.GetAllAsync()` → **`catalog.Firmalar`** tablosu (`Aktif` olanlar). Tabloda tenant filtresi **yok**: firma listesi globaldir. |
| İstemci tarafı | `FirmaApiClient` → `MockFirmaKontrolService.EnsureFirmsLoadedAsync()` → `Pages/FirmaKontrol/Index.razor` |
| Bir firmaya girilince veri hangi alanla kapsamlanıyor? | **`Firma.Id` (int)**, rota parametresi olarak: `GET api/catalog/firma-kontrol/{firmaId:int}/maddeler`, `…/mizan`, `…/vergi`, `…/notlar`. VKN değil, ayrı bir tablo değil, token claim'i hiç değil. |
| Veri tablolarında karşılığı | `FirmaKontrolMadde.FirmaId`, `FirmaKontrolMizanSatir.FirmaId`, `MizanNotu.FirmaId`, `FirmaKontrolVergi.FirmaId` — hepsi `Firmalar`'a FK, `HasIndex(FirmaId)`. |
| Ekranda seçim | `Index.razor` satıra tıklayınca `/firmakontrol/{firma.Id}`; tenant'a **dokunulmuyor**. |

Yani Raporlar'ın mekanizması tek cümleyle: **firma listesi `catalog.Firmalar`'dan gelir,
veri `FirmaId` ile kapsamlanır ve `FirmaId` isteğin parametresidir.**

### Banka Otomasyon neden farklıydı

Modül listesini `IAppSessionManager.Firms`'ten kuruyordu; o da login yanıtındaki
**tenant**'lardan geliyor (`IdentityService` → `Tenants` tablosu). `pkfadmin` kullanıcısı
tek tenant'a bağlı: `FirmaNo = "500"`, `Ad = "PKF Istanbul SMMM A.Ş"`, `Vkn = "1234567890"`
(bkz. `IdentityContextSeed`). Ekrandaki tek satır buydu.

Veri de aynı yerden kapsamlanıyordu: beş tablo `TenantEntity`'den türüyor ve
`CatalogContext` bunlara `TenantNo == _tenant.CurrentTenantNo` global query filter'ı
uyguluyordu. Sonuç: **kullanıcının yönettiği sekiz firmanın banka verisi tek kovaya
("500") yazılıyordu.** PKF Aday'ın 6.128 satırlık hesap planı, 18 banka hesabı, öğrenilen
eşleşmeleri ve ekstre yüklemeleri şu anda orada duruyor.

§63'ün "seçilen firma gerçekten tenant bağlamını değiştiriyor" kararı doğru bir sorunu
(ekran bir firmayı yazarken isteğin başkasına gitmesi) çözüyordu, ama **yanlış eksende**:
tenant, kullanıcının kimliğine ait bir kavram; yönetilen firma sayısı ise tenant sayısından
bağımsız. Tek oturumla sekiz firma yöneten kullanıcıda ikisi hiç örtüşmüyor.

**Karar:** Modülün kapsamı `TenantNo` → **`FirmaId` (`catalog.Firmalar.Id`)**.
Raporlar'ın kullandığı kaynağın ve anahtarın aynısı.

| | Eski | Yeni |
|---|---|---|
| Firma listesi | login yanıtı (tenant'lar) | `GET /catalog/firmalar` (Raporlar ile aynı) |
| Kapsam alanı | `TenantNo` (string) | `FirmaId` (int) |
| Kapsam kaynağı | JWT `tn` claim'i | isteğin `?firmaId=` parametresi |
| Firmaya giriş | `SelectFirmAsync` → yeni token | seçim modül oturumunda tutulur, token'a dokunulmaz |
| Genel FİRMA DEĞİŞTİR çakışması | modül kendi tenant'ını geri uygular + uyarı | **çakışma yok**, ikisi farklı şeyler |

Etkilenen tablolar: `EkstreHesapPlani`, `EkstreBankaHesaplari` (hesap sahibi unvanları da
bu satırlarda), `EkstreYuklemeler`, `EkstreSatirlari` (kapsamını yüklemeden alır),
`EkstreHesapEslesmeleri`, `EkstreKisiYonlendirmeleri`.

`§62` (menü, rotalar, ekran kalıbı) geçerli. **`§63` ve `§64` bu kararla geçersizdir.**

## 69. Global query filter kaldırıldı; kapsam her sorguda görünür yazılıyor

**Karar:** Beş tablodaki `HasQueryFilter` **kaldırıldı**. Kapsam, scoped
`IBankaFirmaKapsami`'den okunup her sorguda açıkça yazılıyor
(`.Where(h => h.FirmaId == _kapsam.FirmaId)`); servislerin çoğunda tek bir
`private IQueryable<T> …` özelliğinde toplanıyor.

**Neden görünmez filtre değil:**

1. **Baypas ihtiyacını doğuruyordu.** Firma seçim ekranının sayaçları meşru biçimde çok
   firmalı bir sorgu; global filtre varken tek yolu `IgnoreQueryFilters()`'tı (§64). Filtre
   kalkınca o sorgu sıradan bir `WHERE FirmaId IN (…)` oldu ve **baypas tamamen silindi**.
2. Filtre görünmediği için hatalı olduğu da görünmüyordu: kapsamın token'dan geldiği
   `CatalogContext`'in bir satırında yazıyordu, modülün 6.500 satırlık kodunda değil.

Muhasebe ve gider modülleri tenant filtrelerini koruyor — onların kapsamı gerçekten
kullanıcının tenant'ı; değişen yalnız Banka Otomasyon.

**Kapsam nasıl geliyor:** `BankaFirmaFiltresi` (bir `IAsyncActionFilter`) isteğin
**sorgu dizesindeki** `firmaId`'sini okur, `catalog.Firmalar`'da var mı diye bakar ve
`IBankaFirmaKapsami`'ye yazar. Eksik/geçersiz/tanınmayan → **400**, sessiz varsayılan yok:
kapsamsız bir okuma "kayıt yok" gibi görünüp kullanıcıyı yanıltırdı.

Form gövdesi okunmuyor; istemci çok parçalı yüklemelerde de `?firmaId=`'yi sorgu dizesinde
gönderiyor. Sebebi pratik: filtre model bağlamadan önce çalışıyor ve 20 MB'lik gövdeyi
orada tamponlamanın anlamı yok.

**Yazma tarafında sessiz varsayılan da yok.** `TenantEntity`'de `SaveChangesAsync`
"boşsa istekten doldur" davranışı var; `FirmaKapsamliEntity` için **konmadı**:
`FirmaId <= 0` olan kayıt istisna ile reddediliyor. Kapsamı yazmayı unutan bir kod yolu,
kaydı yanlış firmaya değil hiçbir yere yazmalı.

**İstemci tarafında tek kaynak:** `?firmaId=` adreslere tek yerde ekleniyor
(`BankaEkstreApi.Adres`) ve değeri `IBankaOtomasyonOturumu.FirmaId`'den, yani ekranın
başlıkta gösterdiği firmadan okunuyor. Ekranda görünen firma ile isteğe giden firmanın
ayrışması böylece yapısal olarak imkânsız.

## 70. Hangi tablo firma bazlı, hangisi global

Düzeltme sırasında tablolar tek tek gözden geçirildi. Ölçüt: **kayıt firmanın
muhasebesine mi ait, bankanın yazım kalıbına mı?**

| Tablo | Kapsam | Gerekçe |
|---|---|---|
| `EkstreHesapPlani` | firma | ORKA hesap planı firmaya özel |
| `EkstreBankaHesaplari` | firma | ORKA kodu, IBAN, hesap sahibi unvanları hep firmanın |
| `EkstreYuklemeler` | firma | firmanın ekstresi |
| `EkstreSatirlari` | firma (yükleme üzerinden) | kendi alanı yok; Muhasebe'deki `FisSatir` ile aynı yaklaşım |
| `EkstreHesapEslesmeleri` | firma | aynı unvan her firmada farklı cari koduna gider |
| `EkstreKisiYonlendirmeleri` | firma | kimin ortak, kimin personel olduğu firmaya özel |
| `EkstreKimlikKayitlari` | **global** | bir unvanın *kim olduğu* her firmada aynı (§ mevcut karar) |
| `EkstreAciklamaSablonlari` | **global** | banka bazlı (`ParserTipi`); Vakıfbank'ın yazımı firmadan firmaya değişmez |
| `EkstreUnvanDesenleri` | **global** | aynı gerekçe |
| `EkstreSabitKurallar` | **global** | banka masrafı → 770 gibi kurallar bankaya ait |
| `EkstreVergiKodlari` | **global** | 0040 = damga, 0033 = kurum geçici; vergi kodları firmadan firmaya değişmez |

**Global kalanlara `FirmaId` eklenmedi.** Eklenseydi her firma sekiz bankanın desen ve
kural setini baştan kurmak zorunda kalırdı; modülün "yeni banka eklerken kod değişmez,
tabloya satır eklenir" kararı (§2, §37, §54) da anlamını yitirirdi.

**Ama kural tabloları hesap kodunu SEÇİLİ FİRMANIN planına karşı doğruluyor.**
`SabitKuralService` ve `VergiKoduService` bu yüzden kapsam alıyor — satırlarında `FirmaId`
yok, yalnız `YapilandirmaDogrulama.HesapKoduDogrulaAsync(db, firmaId, …)` çağrısı için.
Kod formatı ORKA'da firmadan firmaya değişiyor ve kullanıcı kuralı hangi firmadaysa
oradaki planla yazıyor.

## 71. Yanlış firmadaki veri taşınmadı; Tanımlar'a "Veri temizliği" eklendi

**Karar:** Migration (`BankaOtomasyonFirmaKapsami`) `FirmaId` kolonunu ekliyor,
`TenantNo`'yu düşürüyor ve **hiçbir satırı bir firmaya atamıyor**. Yanlış yerdeki veriyi
kullanıcı Tanımlar > **Veri temizliği** bölümünden siliyor, doğru firmaya yeniden yüklüyor.

**Neden otomatik taşıma yok:** Tenant ile firma arasında güvenilir bir eşleme yok.
Token'daki tenant `500 / "PKF Istanbul SMMM A.Ş" / VKN 1234567890`; `catalog.Firmalar`'da
bu VKN yok ve kayıtlar aslında **PKF Aday'a** ait. Bu bilgi yalnız kullanıcının kafasında.
VKN eşleştiren bir migration hiçbir satırı tutturamazdı; "hepsini şu firmaya yaz" diyen bir
migration ise veriyi doğru sandığı bir yere koyup hatayı görünmez yapardı.

**Eski satırlar neye atandı:** Düz `0` değil, **tenant başına tutarlı bir negatif sahte
kapsam** (`-(ABS(CHECKSUM(TenantNo)) % 1000000 + 1)`, beş tabloda aynı ifade). Sebebi
tekillik: eski şemanın `(TenantNo, Kod)` unique index'i düz sıfırda `(0, Kod)`'a çöker ve
iki tenant'ta aynı kod varsa yeni unique index migration'ı düşürürdü. Gerçek `Firma.Id`'ler
pozitif olduğu için bu satırlar hiçbir firmanın ekranında görünmüyor.

Ekrandaki karşılığı: "**Sahipsiz eski kayıtlar**" — ayrı bir sayaç listesi ve ayrı bir
temizle düğmesi. Olmasaydı o satırlar veritabanında sonsuza kadar erişilemez biçimde
kalırdı.

**`FirmaId`'ye FK kısıtı konmadı.** Sahipsiz satırlar `Firmalar`'da karşılığı olmayan
değerler taşıdığı için kısıt migration'ı düşürürdü. Veri temizlendikten sonra eklenebilir;
tablo eşlemelerinde not düşüldü.

**Temizlik ekranının kuralları:**

- Onaydan **önce** hangi tablodan kaç kayıt gideceği yazılıyor (hesap planı, banka hesabı,
  ekstre yüklemesi, ekstre satırı, öğrenilen eşleşme, kişi yönlendirmesi). Silinen geri
  gelmiyor; sayı göstermeden onay istemek kör imza olurdu.
- **Silinmeyenler ekranda ayrıca yazıyor**: açıklama şablonları, unvan desenleri, sabit
  kurallar, vergi kodları, kimlik kayıtları. Bunlar global (§70); bir firmanın temizliği
  diğerlerinin çalışan kurulumunu bozmamalı.
- Hesap sahibi unvanları ayrı bir tabloda değil `EkstreBankaHesaplari` satırlarında;
  hesaplar gidince onlar da gidiyor. Ekranda parantez içinde yazıyor.
- Silme sırası bağımlılıkları izliyor: satırlar → yüklemeler → banka hesapları
  (`EkstreYukleme.BankaHesabiId` FK'sı `Restrict`).
- Temizlikten sonra Tanımlar'ın tüm bölümleri tazeleniyor; ekranda silinmiş verinin
  durması kafa karıştırırdı.

## 72. Firma seçim ekranı ve modül oturumu sadeleşti

- **Liste artık Raporlar ile aynı kaynaktan**: `IFirmaApiClient.GetAllAsync()`. Kolonlar
  `Unvan` ve `VergiKimlikNo`; sayaçlar `FirmaId` ile eşleşiyor.
- **`BankaOtomasyonOturumu` tenant'a dokunmuyor.** `IAppSessionManager` bağımlılığı,
  `Aktif` bayrağı, `Uyari` olayı ve genel FİRMA DEĞİŞTİR çakışma çözümü kaldırıldı —
  §63'ün bütün makinesi. Kalan: seçili firma, `FirmaId`, `FirmaAdi`, `Degisti` olayı.
- **Seçim `sessionStorage`'da `FirmaId` olarak duruyor** (`BankaOtomasyon.FirmaId`).
  Sayfa yenilendiğinde `BaglamiHazirlaAsync` firmayı **kaynağından doğruluyor**: arada
  silinmiş/pasife alınmış bir firma için ekran açılmıyor, seçim temizleniyor.
- Firma içi ekranlar `IAppSessionManager.FirmChanged` yerine `Oturum.Degisti`'yi dinliyor.
  "Olay seçili firmaya ait mi?" kontrolüne gerek kalmadı; olay zaten yalnız modülden geliyor.
- Ekranda gösterilen ad tek yerden geliyor (`BankaOtomasyonOturumu.Ad`: unvan varsa o,
  yoksa kısa ad) ki başlık ile listedeki satır aynı şeyi yazsın.

**Testler:**

- Sunucu — `FirmaKapsamiTests`: aynı veritabanında iki firma; Aday'ın hesap planı
  aktarımı Aday'a yazılıyor, SMMM'ninki bozulmuyor; aynı kod iki firmada ayrı kayıt oluyor;
  **kapsamsız yazma istisna atıyor**; firma özeti girilmemiş firmaların sayaçlarını da
  döndürüyor; temizlik yalnız seçili firmayı siliyor, diğer firmaya ve global tablolara
  dokunmuyor.
- Sunucu — `EkstreServiceTests.Iki_firma_birbirinin_hesap_planini_ve_eslesmelerini_gormez`:
  uçtan uca yükleme + onay; aynı unvan iki firmada farklı koda çözülüyor, öğrenilen
  eşleşmeler ayrışıyor, kimlik kaydı (global) paylaşılıyor. Doğrulamalar artık açıkça
  `FirmaId` ile süzülüyor — "context ne görüyorsa odur" varsayımı gizli filtreyi sınamak
  demekti, artık gizli filtre yok.
- İstemci — `BankaOtomasyonOturumuTests`: seçim kapsam oluyor ve hatırlanıyor, sekme
  değişiminde firma kaynağına tekrar gidilmiyor, yenilemede depodan gelip kaynağından
  doğrulanıyor, artık tanımlı olmayan firma için bağlam hazırlanamıyor.

## 73. Öğrenilen eşleşmelerin toplu içe aktarımı

Öğrenme tablosu şimdiye kadar yalnız onay ekranından tek tek doluyordu. ORKA
yevmiyesinden çıkarılmış doğrulanmış eşleşmeler (PKF Aday'da 402 satır, 7 aylık geçmiş)
elle girilemez; bunlar kullanıcının geçmişte kendi verdiği kararların toplu hâlidir.

**Eşleştirme mantığına dokunulmadı.** Katman sırası, eşikler ve algoritma aynı; bu yalnız
yeni bir yazma yolu. İçe aktarılan kayıt `HesapEslesmeService.OgrenAsync`'in yazdığı
biçimin aynısıdır (`AnahtarTipi.UnvanCekirdek`, ayırt edici eksiz, sade çekirdek) ve
sonraki ekstrede geçmiş onay katmanından çözülür.

**Rota mevcut kalıbı izliyor:** `POST/GET api/catalog/banka-ekstre/eslesmeler/ice-aktar |
/sablon`. Görev metnindeki `banka-otomasyon/ogrenilen-eslesmeler` yolu ekranın adresidir
(`/banka-otomasyon/tanimlar`), API öneki değil — modülün tüm uçları `banka-ekstre`
altında ve gateway `/catalog/{everything}` route'undan geçiyor. Ayrı bir önek eklemek
gateway'i değiştirmeden çalışsa da modülün adres şemasını ikiye bölerdi.

**Anahtar tipi `UnvanCekirdek`, `Belirsizlik` değil.** Belirsizlik kaydı aday kümesinin
özetiyle birlikte anlamlıdır (§49); geçmişten türetilen satırda o küme yok. Özetsiz bir
belirsizlik kaydı hiç uygulanmaz, yazılması ölü satır üretirdi.

**Anahtar dosyadaki hâliyle değil, sistemin kendi normalizasyonundan geçirilerek yazılır.**
Dosyadaki değer zaten normalize görünse bile (`UnvanCekirdek`) yeniden üretilir: gürültü
kelimesi listesi veya Türkçe sadeleştirme ileride değişirse dosya ile tablo ayrışmasın.

**Mevcut kayıt ezilmez, satır atlanır.** Tekli düzenlemeden ve banka hesabı içe
aktarımından ayrılan tek nokta bu: kullanıcının onay ekranında verdiği karar, geçmişten
türetilen kayda göre önceliklidir. `Atlanan` (mevcut) ve `Hatali` (reddedilen) ayrı
sayılıyor — ikisi aynı sayaçta toplanırsa "402 satırın 380'i atlandı" raporu, dosyanın
bozuk mu yoksa zaten işlenmiş mi olduğunu söylemez.

**`Yon` boşsa "Farketmez" ve iki kayıt yazılır.** `HesapEslesmesi.Yon` yalnız
Giren/Çıkan tutuyor (ekstre satırının yönü her zaman kesindir) ve eşleştirici
`e.Yon == baglam.Yon` ile arıyor. Enum'a `Farketmez` eklemek eşleştirme mantığına
dokunmak olurdu; onun yerine satır iki yöne açılıyor. Bir yönde kullanıcının kararı
zaten varsa o yön korunur, yalnız boş yön yazılır ve satır uyarıyla raporlanır.
Sonuç raporunda `Eklenen` satır sayısı, `EklenenKayit` yazılan kayıt sayısıdır.

**Dosya içi tekrar denetimi yönleri kesişen satırlar için.** Aynı çekirdek Giren'de bir
koda, Çıkan'da başka bir koda gidebilir — bu meşru. Hata yalnız yönler kesişince veriliyor;
o durumda hangi kodun geçerli olduğu belirsiz ve sessizce "ilki kazandı" demek, dosyanın
402 satırında görülmeyecek bir yanlışı tabloya yazardı.

**Doğrulama satır bazlı** (hatalı satır dosyayı düşürmez): hesap kodu firmanın hesap
planında olmalı; anahtar en az 8 karakter (kısa çekirdek gelecekte alakasız satırları tek
cariye bağlar ve öğrenilen kayıt onaya bile düşmediği için hata sessiz kalır); anahtar
hesap sahibinin çekirdeklerinden birini kapsıyorsa reddedilir (firmanın kendi adı asla
karşı taraf olarak öğrenilmemeli — §46'nın aynı gerekçesi); yön tanınmalı.
Kaydedilen `HesapAdi` dosyadan değil hesap planından okunur; dosyadaki ad yalnız bilgi.

**Ayırt edici ekli kayıt "mevcut" saymaz, uyarı üretir.** `PARK PLAZA + AIDAT` ile sade
`PARK PLAZA` farklı anahtarlardır; eşleştirici önce genişletilmişi, tutmazsa sadeyi dener.
İçe aktarım sade anahtarı yazabilir ama kullanıcı aile ayrımı yapmışsa bunu görmeli.

**Migration yok:** yeni kolon/tablo eklenmedi, mevcut `EkstreHesapEslesmeleri` şeması
kullanılıyor.

**Onay kutusunda firma adı var, satır sayısı yok** (§67 ile aynı sebep): dosya sunucuda
ayrıştırılmadan satır sayısı bilinemiyor. Sayı sonuç raporunda ve bildirimde.

**Testler** (`OgrenilenEslesmeIceAktarimServiceTests`, 19 test): geçerli dosya 3 satır →
3 eklendi; ikinci kez aynı dosya → 0 eklendi / 3 atlandı; hesap planında olmayan kod
atlanıp raporlanıyor ve dosyanın kalanı işleniyor; hesap sahibi çekirdeğini kapsayan
anahtar reddediliyor; kolon sırası değişik ve Türkçe karaktersiz başlıklı dosya
okunuyor; farklı firmada aynı anahtar ayrı kayıt oluyor; içe aktarılan eşleşme
`HesapEslestirici`'de `KaynakKatman.GecmisOnay` ile çözülüyor; ham unvan yeniden
normalize ediliyor; boş yön iki kayıt yazıyor; şablon kendi içe aktarımından geçiyor.

## 74. İşlem kategorisi bir ETİKET; eşleştirmeye girmiyor

Kurallar mekanizmaya göre ayrılmıştı (sabit kural, vergi kodu, kişi yönlendirme, açıklama
şablonu); kullanıcı ise muhasebe kategorisine göre düşünüyor ve yeni banka eklerken
"hangi kategoriler eksik?" diye kontrol ediyor. Kategori bu iki bakışı bağlıyor.

**Katman sırası, eşikler, algoritma ve desenler kategoriden habersiz.** Kategori kuralın
üzerinde bir etiket; `HesapEslestirici` kategori tablosunu hiç okumuyor. Testte de
sabitlendi: kategori tablosu boşaltılınca aynı ekstrenin bütün satırları aynı kodu, aynı
katmanı ve aynı durumu veriyor.

**Tablo global, görünüm banka bazlı.** "Banka gideri" her firmada ve her bankada aynı
şeydir; hangi hesaba gittiği zaten kuralın kendi alanında. Görünüm ise ayrıştırıcıya göre
süzülüyor: sabit kurallar ve şablonlar bankaya bağlı, vergi kodları (global) ve kişi
yönlendirmeleri (firma) her bankada geçerli olduğu için hep listede.

**Satırın kategorisi satıra YAZILMIYOR**, önerilen (yoksa onaylanan) hesap kodunun ana
grubundan okunuyor (`KategoriCozucu`). Alternatifi `EkstreSatirlari`'na kolon eklemekti;
iki sakıncası vardı: (1) hangi katmanın çözdüğünü eşleştiriciden dışarı taşımak gerekirdi
— tam da dokunmamaya çalıştığımız yer, (2) kullanıcı kodu düzeltince etiket eskirdi.
Koddan türetince etiket her zaman satırın güncel kodunu anlatıyor ve kategori tablosu
değişince geçmiş satırlar da doğru etiketleniyor.

**Kural kategorileri de aynı yoldan atanıyor** (kod taşıyanlar için ana gruptan). Elle
yazılmış bir "kural → kategori" listesi tutulmadı: iki liste ayrışırsa aynı hesap kuralda
bir, satırda başka kategori gösterirdi. Açıklama şablonlarının kodu olmadığı için tek elle
yazılan eşleme onlarınki.

**Atama yalnız boş alanlara.** Seed mevcut kayda dokunmuyor (modülün genel kuralı);
kullanıcının ekrandan verdiği kategori kararı korunuyor.

**Kategori silinince kural silinmiyor** (`SetNull`). Kategori etiket olduğu için silinmesi
eşleştirmeyi değiştirmemeli; kural kategorisiz kalıp çalışmaya devam ediyor. Bellek içi
sağlayıcıda veritabanı kısıtı olmadığından servis alanı ayrıca boşaltıyor.

**Görünümde tek vurgu eksik olanda.** Tanımlı satırlar tamamen sade — renk, ikon, rozet
yok; kuralsız kategoriler kırmızı zeminde ve sayı yerine `yok`. Kapsama kutusu ve etiket
bulutu bilerek yazılmadı: onlar eksik olanı öne çıkarmak yerine gizliyordu.

## 75. Banka adı açılır liste; yanlış yazımlar birleştirmeyle düzeltiliyor

Otomatik tamamlama + uyarı yetmedi: gerçek veride `Vakıf Bank Eur`, `Vakıfbank Vadeli`,
`İŞ BANKASI` gibi yazımlar girilmiş ve 8 banka 11 sekmeye bölünmüştü. Bu yalnız görüntü
sorunu değil — "aynı banka önceliği" kuralı `BankaAdi` üzerinden çalıştığı için bankalar
arası eşleştirme de bozuluyor.

**Alan açılır liste, yeni banka ayrı adım.** Serbest yazım varsayılan olmaktan çıktı;
gerçekten yeni bir banka "Yeni banka ekle" düğmesi → ad girme → onay adımından geçiyor.
Düzenlenen hesabın adı listede yoksa seçeneklere ekleniyor: aksi hâlde eski yazımlı bir
hesabı düzenlerken alan boş görünür ve kullanıcı farkında olmadan bankayı değiştirirdi.

**Birleştirme şart**, çünkü yanlış yazımlar zaten girilmiş. Seçilen yazımlar tek ada
iniyor; yalnız `BankaAdi` alanı değişiyor, hesaplar/kodlar/ekstreler duruyor.
Karşılaştırma ordinal ve harf duyarsız — sekme şeridinin gruplaması da tam olarak böyle,
yani işlemin etkisi ekranda görülenle birebir aynı. Pasif hesaplar da sayılıyor ve
düzeltiliyor: yanlış yazımların bir kısmı pasif kayıtlarda duruyor.

**Sunucu tarafı serbest metni engellemiyor.** Kısıt API'ye konsaydı toplu içe aktarım ve
mevcut istemciler kırılırdı; içe aktarımda tanınmayan ad satırı düşürmüyor, uyarı
üretiyor (aynı ad dosyanın kalanında tekrar uyarmıyor). "Ayrı adımdan geç" kuralı ekranın
sorumluluğunda.

## 76. Kural grubu adayları önceliklendirir; başka gruptaki isim otomatiği engellemez

Alt hesap araması eskiden "kural grubunda tek eşleşme **ve** başka grupta hiç karşılık
yok" istiyordu. Gerçek planda bu neredeyse hiç tutmuyor: personelin çoğu hem maaş avansı
(196) hem iş avansı (195) altında kayıtlı, bazıları ayrıca 335'te. `ÖMER CAN DİZDAR`
maaş avansı satırı bu yüzden onaya düşüyordu — oysa kural zaten 196 demişti ve o grupta
**tek** aday vardı.

**Kararı kuralın grubu verir.** Grup içi sayım belirleyici: tam bir aday → otomatik,
sıfır ya da birden fazla → onaya düşer. Başka gruplardaki birebir karşılıklar aday olarak
saklanmaya devam ediyor (kullanıcı iş avansına yazmak isteyebilir) ama otomatik çözümü
engellemiyor.

**Neden güvenli?** Kural grubu kullanıcının kendi tanımı — "maaş avansı satırı 196'ya
gider" diyen o. Grup içinde tek aday varsa belirsizlik yok; belirsizlik olsaydı grup
içinde iki aday çıkardı ve satır zaten onaya düşerdi. Yakın isimli başka kişiye düşme
riski de değişmedi: arama hâlâ **benzersiz önek**, benzerlik değil (bkz. §-Tur 3), ve
tek kelimelik isim hiçbir zaman otomatik seçilmiyor.

Ölçüm: gerçek dosyada otomatik 62 → 63, onay 102 → 101; fark tam olarak hedef satır.

## 77. Çoklu ana grup ayrı alanda (`AnaGruplar`), hesap kodunun içinde değil

Genel `Avans` kuralı hem iş avansını hem maaş avansını kapsıyor; tek bir ana gruba
bağlanamaz. En kısa yol `HesapKodu` alanına virgüllü liste yazmaktı (`"195, 196"`) —
**yapılmadı**.

`HesapKodu` gerçek bir ORKA kodudur: hesap planına karşı doğrulanıyor, aday bulunamayan
satırda öneri olarak yazılıyor, dışa aktarıma ve ORKA'ya gidiyor, `Normalizasyon.AnaGrup`
ilk segmentini okuyor. İçine liste konsaydı kod normalizasyonu, ana grup çıkarımı ve dışa
aktarım aynı anda bozulurdu; ekrandaki kod kutusu da hesap planı otomatik tamamlamasına
bağlı ve virgüllü metinle çalışmaz.

**Ayrı nullable alan:** `AnaGruplar` (virgülle ayrılmış, 200 karakter). Boşsa küme,
`HesapKodu`'nun tek ana grubu — eski davranış aynen korunuyor, migration veri doldurmuyor.
Her parça `AnaGrup`'a indirgeniyor, yani kullanıcı `"195 01"` yazsa da kural çalışıyor.

**Yalnız `AltHesapGerekli` kurallarda geçerli.** Başka bir kuralda doldurulursa sessizce
yok sayılmıyor, hata veriliyor: sessiz yok sayma kullanıcının yazdığı şeyin neden hiçbir
etkisi olmadığını gizlerdi (§-aynı gerekçe geçersiz parser/regex denetimlerinde de var).

**Çoklu gruplu kuralda aday bulunamazsa kod önerilmiyor.** Tek gruplu kuralda kutuda ana
grup kalıyordu (195/196); çoklu grupta hangisinin kastedildiği bilinmiyor ve birini yazmak
kullanıcıyı yanlış gruba yönlendirirdi.

**Seed'de dar bir yükseltme var.** Seed kuralı mevcut kayıtların üzerine yazmamak; ama
çoklu gruptan önce kurulmuş veritabanlarındaki `Avans` satırı tek gruplu kalırsa kural
**eksik** çalışır (195 hiç taranmaz). Bu yüzden kayıt hâlâ seed'in bıraktığı hâldeyse
(Vakıfbank, kod `196`, liste boş, alt hesap bekleniyor) listeye `195, 196` yazılıyor;
kullanıcı kodu ya da listeyi değiştirdiyse kayda dokunulmuyor.

## 78. Bordro hesaplayıcısı: anonim erişim nasıl kurulmuştu (taşımadan önceki tespit)

Sayfa uygulamanın dışında, girişsiz açılan bir ada duruyordu. Anonim erişim **tek bir
bayrakla** değil, dört ayrı yerde birden kurulmuştu; taşımadan önce hepsi tek tek bulundu:

| # | Nerede | Ne yapıyordu |
|---|---|---|
| 1 | `WebApp/Pages/Payroll/Page/PayrollCalculator.razor` | `@page "/payroll-calculator"`, `@attribute [Authorize]` **yok** |
| 2 | Aynı dosyanın 1. satırı: `@layout PublicPayrollLayout` | Uygulamanın `MainLayout`'u yerine ayrı, menüsüz/kullanıcısız bir kabuk |
| 3 | `WebApp/Pages/Payroll/Layout/PublicPayrollLayout.razor` (+ `.css`) | Yalnız bu sayfa için yazılmış tam sayfa kabuk (PKF logosu + "Ücret Bordrosu" başlığı) |
| 4 | `CatalogService.Api/Features/Payroll/Controllers/PayrollPublicController.cs` | `[Route("api/public/payroll")]` + `[AllowAnonymous]` |
| 5 | `Web.ApiGateway/Configurations/ocelot.json` / `.Docker.json` / `.Development.json` | `/api/public/payroll/{everything}` için **AuthenticationOptions'sız** ayrı bir rota |

Kritik nokta: **`[AllowAnonymous]`'u kaldırmak tek başına hiçbir şey değiştirmezdi.**
CatalogService'te global bir yetki politikası (`FallbackPolicy`) yok; `AddAuthorization()`
çıplak çağrılıyor. Servisteki 39 controller'ın korunanları yetkiyi **kendi üzerlerindeki
açık `[Authorize]`** ile alıyor (BankaEkstre, Muhasebe, Jobs… hepsi böyle). Yani
`[AllowAnonymous]` silinseydi controller yetkisiz değil, **hâlâ herkese açık** kalırdı.
Bu yüzden yerine açıkça `[Authorize]` konuldu.

İkinci kritik nokta: gateway'deki ayrı rota, `[Authorize]` konsa bile **token'sız isteği
CatalogService'e kadar taşımaya devam ederdi** (401 dönerdi ama kapı açık kalırdı). Rota
üç yapılandırmadan da silindi; istek artık mevcut `/catalog/{everything}` rotasından
(Bearer'lı) geçiyor.

**Menüde kayıtlı değildi.** `MainLayout.razor`'daki `RadzenPanelMenu` içinde
`/payroll-calculator` geçmiyordu; sayfaya yalnız adresi bilerek giriliyordu.

**Anonim erişim için ayrıca bir CORS istisnası yoktu.** `Program.cs`'teki `"wasm"`
politikası tüm servis için tek ve zaten uygulamanın kendi kaynaklarını (localhost:2000,
dijitalmasraf.com) sayıyor — payroll'a özel bir giriş içermiyor, dokunulmadı.

**İstemci tarafında ayrı bir HttpClient yoktu.** `PayrollApiService`, DI'daki varsayılan
scoped `HttpClient`'ı alıyor; o da `ApiGatewayCorridor` (AuthTokenHandler + TenantHeader +
RefreshTokenCorridor). Yani token zaten gönderiliyordu, sadece **istenmiyordu**. Bu yüzden
taşımada DI'ya dokunmak gerekmedi, yalnız temel yol değişti.

**Nginx / index.html tarafında payroll'a özel bir kural yok** (arandı, bulunamadı).

Yan tespit, kapsam dışı bırakıldı: `ocelot.Development.json`'da genel
`/catalog/{everything}` rotasının da `AuthenticationOptions`'ı yok. Bu payroll'a özel
değil, geliştirme yapılandırmasının geneli; bu turda dokunulmadı ama **üretim
yapılandırmasında böyle olmadığı doğrulandı** (`ocelot.json` ve `.Docker.json`'da Bearer
var).

## 79. Hesaplamalar sekmeleri: kayıt listesi + `DynamicComponent`, elle yazılmış sekme şeridi değil

Banka Otomasyon'un sekme şeridi (`FirmaBasligi`) iki sekmeyi elle `<a>` etiketiyle
yazıyor; üçüncü sekme eklemek hem enum'a hem şeride hem de sayfa iskeletine dokunmayı
gerektirir. Hesaplamalar'ın büyümesi bekleniyor (bordro ilk sekme, başkaları gelecek),
bu yüzden kalıp bir adım ileri taşındı:

`HesaplamaSekmesi` bir **kayıt listesi** — slug, başlık, ikon ve bileşen tipi. Sayfa
iskeleti (`HesaplamalarPage`) listeyi dolaşıp şeridi üretiyor ve aktif sekmenin bileşenini
`<DynamicComponent>` ile basıyor. **Yeni sekme = yeni Razor bileşeni + listeye bir satır**;
sayfa dosyası hiç değişmiyor.

`/hesaplamalar` kökü ilk sekmeye `replace: true` ile yönleniyor. Kökte de aynı içeriği
basmak mümkündü ama o zaman aynı ekranın iki adresi olurdu; sekme bağlantısı, tarayıcı
yer imi ve "hangi sekmedeyim" vurgusu tek kanonik adres üzerinden yürüsün diye
yönlendirme seçildi. `replace: true` geri düğmesinin sonsuz döngü kurmasını engelliyor
(aynı gerekçe `BankaEkstre/EskiRotaYonlendirme`'de de var).

Tanınmayan slug hata vermiyor: şerit yine çiziliyor, içerik alanında "sekme bulunamadı"
yazıyor. Kırık bir yer imi kullanıcıyı 404'e değil, çalışan sekmelere düşürsün.

## 80. Finansman gider kısıtlaması: oran koda gömülü değil, yıl bazlı tabloda

9. satırdaki oranı (bugün **%10**) Cumhurbaşkanı Kararı belirliyor ve değişebiliyor.
Üç seçenek vardı: sabit (`const`), bordrodaki gibi bellekteki yapılandırma deposu
(`PayrollYearConfigStore`), ya da veritabanı tablosu. **Tablo seçildi** — çünkü oranın
*ekrandan düzenlenebilmesi* isteniyor; ilk ikisi yeni bir Cumhurbaşkanı Kararında
derleme + yayın gerektirirdi. Tablo `catalog.FinansmanKisitlamaOranlari`, `Yil`
benzersiz (yıl başına tek oran, `SmmmHadDegeri` ile aynı kalıp).

**Kapsam tenant değil, global.** Mevzuat oranı her firma için aynı; Ticaret Sicil /
Mevzuat Notları / SMMM Takip tablolarıyla aynı gerekçe (bkz. §70). Query filter yok.

**Oran yüzde olarak saklanıyor** (`10` = %10, `0,10` değil). Ekranda da yüzde girildiği
için dönüşüm tek yerde — motorun içinde — yapılıyor; iki temsil arasında gidip gelmek
yüzde/oran karışıklığının en sık kaynağı.

**Oran yoksa varsayılan uydurulmuyor.** Seçilen yılın kaydı yoksa hesap durur
(`FinansmanKisitlamaOraniYokException` → 400 + "…oranı tanımlı değil, ekrandan
tanımlayın"). Sessizce %10 varsaymak, oranın değiştiği bir yılda **yanlış bir KKEG**
üretir ve kullanıcı bunu ekranda göremezdi. Aynı gerekçeyle yıl açılır listesinde oranı
tanımsız yıllar da duruyor: kullanıcı eksiği hesap denerken görsün.

**Seed idempotent ve üzerine yazmıyor.** 2021–2026 için %10 ekleniyor (kısıtlama
1/1/2021'de yürürlüğe girdi, oran o gün bugün %10); yılı zaten kayıtlı olan orana
dokunulmuyor — kullanıcı ekrandan değiştirdiyse seed geri almasın (§73'teki seed kuralı).

### Hesap sunucuda ve saf bir motorda

`FinansmanGiderKisitlamasiMotoru` veritabanı bilmez: dört girdiyi ve **yılın oranını
parametre olarak** alır, dokuz satırı döner (`VergiHesaplamaMotoru` ile aynı kalıp).
Oranı servis okur, motor sorgu yapmaz. Kazanç: "oran tanımlı değil" hâli dahil bütün
kenar kuralları veritabanısız birim testlenebiliyor.

İstemci hesabı kendi yapmıyor, her değişiklikte (250 ms gecikmeyle) sunucuya soruyor —
Bordro sekmesiyle aynı yaklaşım. Aynı formülün iki yerde durması, birinin unutulduğu gün
ekranla beyannamenin ayrışması demekti.

### 3. satır sıfırlanmıyor, 4–9 sıfırlanıyor

Yabancı kaynak özsermayeyi aşmıyorsa kısıtlama yapılmaz: 4'ten 9'a kadar bütün satırlar
sıfır döner ve ekranda "yabancı kaynak özsermayeyi aşmıyor, gider kısıtlaması yapılmaz"
yazar. **3. satır ham fark olarak duruyor** (negatif görünür): kullanıcı özsermayenin
yabancı kaynağı ne kadar aştığını görsün; sıfırlamak bilgiyi silerdi, hesaba etkisi yok.

Diğer kenar kuralları: 1. satır negatifse sıfır kabul edilir (negatif özsermayeyle
hesap yapılmaz), 2. satır sıfırken 4. satırda bölme yapılmaz (sıfır kabul edilir),
7. satır negatif çıkarsa (finansman geliri giderden fazla) sıfır kabul edilir.

### Yuvarlama: 8 ve 9, ekrandaki iki haneli oranla değil tam oranla hesaplanıyor

4. satır ekranda %33,33 gösterilir ama 8. satır `3 ÷ 2`'nin **tam** değeriyle çarpılır;
yuvarlanmış oranla çarpmak 100.000 TL'lik bir giderde ~3 TL sapma üretiyordu. Tutarlar
yalnız sonuçta 2 haneye yuvarlanıyor (`MidpointRounding.AwayFromZero`, motorun geri
kalanıyla aynı).

### Türkçe biçim: hesaplanan satırlar sabit, giriş kutuları uygulamanın kültüründe

Uygulamanın kültürü kullanıcı tarafından değiştirilebiliyor (`CultureService`: tr-TR /
en-US). Hesaplanan satırlar ve yüzdeler bu yüzden ambient kültüre bırakılmadı, doğrudan
`tr-TR` ile biçimleniyor. `RadzenNumeric` kültür parametresi almıyor (7.1.4'te böyle bir
özellik yok), dolayısıyla **giriş kutuları** uygulamanın kültürünü izler; varsayılan
tr-TR olduğu için ikisi normalde aynı görünür.

### Sekme, sayfa iskeletine dokunmadan eklendi

§79'daki kalıp ilk kez sınandı: yeni bir Razor bileşeni + `HesaplamaSekmesi.Hepsi`
listesine bir satır. `HesaplamalarPage.razor` **değişmedi**. Bileşen adı klasör adıyla
çakışmasın diye `FinansmanKisitlamaHesabi` (klasör `FinansmanGiderKisitlamasi`) —
Bordro'daki `Bordro/BordroHesaplamasi` ile aynı adlandırma. `MainLayout`'taki
Hesaplamalar alt menüsüne de bir satır eklendi; menü sekmeleri elle sayıyor, oradaki
liste kayıt listesinden üretilmiyor.

## 81. DBS ödemesi bankalar arası transfer değil; banka yalnız aracı

Gerçek dosyadaki satır:

```
İŞ BANKASI DBS - BORUSANPRE - 879382 NO.LU ABONE / İŞ BANKASI (PKF ADAY BAĞIMSIZ
DENETİM ANONİM ŞİRKETİ VADESİZ HESABINDAN TÜRKİYE İŞ BANKASI A.Ş. - IBAN MERKEZ ŞUBE
ŞUBESİ NEZDİNDEKİ TR360006400000110083430904 NO'LU PKF ADAY BAĞIMSIZ DENETİM ANONİM
ŞİRKETİ HESABINA YAPILAN 8906612 SORGU NO'LU EFT)
```

sistem `102 1 5 01` diyordu, doğrusu `329 B15 Borusan Otomotiv Premium Kiralama`.

**Neden yanlış gidiyordu.** Gövde §-koşul (c)'nin kalıbının aynısı: "… VADESİZ HESABINDAN
… ŞUBESİ NEZDİNDEKİ …" + banka adlı unvan. Ölçüldü: bu satırda (a) tutmuyor (metinde
"hesaplar arası" yok) ve (b) de tutmuyor (`HesapSahibiElendi = false`) — katmanı **yalnız
(c)** açıyordu.

**Ayırt edici işaret gövdenin kendisinde:** `DBS` ve `NO.LU ABONE`. İkisinden biri geçen
satırda (c) devre dışı kalıyor; satır cari katmanlarına düşüyor. **Yalnız (c) kapanıyor** —
(a) ve (b)'ye dokunulmadı, "HESAPLAR ARASI E.F.T. VAKIFBANK/DENİZBANK" ve "İŞ BANKASI (…)"
satırları eskisi gibi kayıt defterine gidiyor.

Gerçek dosyada "ABONE" geçen üç satır daha var (Superonline ve Türk Telekom tahsilatları,
"Abone No:22912623 …"). Onlar zaten (c) kalıbında değil; kontrol onlarda hiçbir şey
değiştirmiyor, `329 T06` / `329 T01` eşleşmeleri duruyor.

### `102 1 5 06` hesabı DBS satırlarını adı yüzünden ÇEKMİYOR

Kontrol edildi: kayıt defteri eşleştirmesi **hesap adına bakmaz**. Metinde aranan iki alan
var — `BankaAdi` ve `EslestirmeAnahtarlari` (`MetinleAra`). Yani adı
"İş Bankası, Dbs Tl - 3430904, Borusan" olan hesap bu ad yüzünden hiçbir satırı çekmiyor;
yalnız `BankaAdi = "İş Bankası"` ile genel yarışa giriyor ve orada da `102 1 5 01`'in daha
uzun anahtarına ("Türkiye İş Bankası", 18 karakter > 10) yeniliyor — en uzun eşleşen
kazanıyor (§-arama sırası). Kullanıcı bu hesaba "Dbs" ya da "Borusan" anahtarı **tanımlasa
bile** DBS satırı kayıt defterine düşmüyor: (c) hiç açılmadığı için anahtar aramasına sıra
gelmiyor. Üçü de teste bağlandı.

### (c)'yi kapatmak tek başına yetmedi: abone adı kısaltılmış yazılıyor

Ölçüm: koşul kapatılınca satır `329 B15`'e gitmiyor, **çözülemedi** kalıyordu. Sebep,
bankanın abone adını bitiştirip kısaltması — `BORUSANPRE` = `BORUSAN` + `PRE`(mium).
Benzersiz önek katmanı hesap adının **metnin token'ıyla başlamasını** arıyor; burada ilişki
ters yönde: **token, hesap adının ilk kelimesiyle başlıyor**. Alt metin yedeği de tutmuyor
("BORUSANPRE" hesap adının içinde geçmiyor).

Bu yüzden ters yönde dar bir arama eklendi (`CariOnekIndeksi.KisaltmaOnekiyleEslesenler`):

- Yalnız **DBS satırlarında** çalışır — anahtar kelime `DBS`'yi izleyen, harflerden oluşan
  ve 6 karakterden uzun token (abone numarasına ya da "NO"/"LU"ya düşmesin).
- Yalnız hesap adının **ilk kelimesiyle** eşleşir, ortasıyla değil.
- İlk kelime en az `EnKisaCekirdek` (6) harf olmalı: "Aras Kargo"nun `ARAS`'ı (4),
  "Cms Jant"ın `CMS`'i (3) her şeye eşleşirdi.
- **En son** denenir: normal önek ya da alt metin araması bir şey bulduysa buraya hiç
  gelinmiyor.
- Sonuç önek katmanının kendi karar kurallarına tabi: tek aday otomatik, çok aday onaya
  düşer. Yani "kısaltma" gevşekliği tek başına yanlış kayıt üretemiyor.

Katman etiketi için yeni bir `KaynakKatman` değeri açılmadı; eşleşme yine bir önek
eşleşmesi olduğu için `BenzersizOnek` kullanılıyor (istemci DTO'sunun sayısal sözleşmesi
değişmedi).

**Alternatif neydi?** Satırı onaya düşürüp kullanıcının bir kez `329 B15` demesini beklemek
— öğrenme katmanı sonraki DBS satırlarını zaten çözerdi. Bu turda otomatik çözüm seçildi
çünkü kullanıcı beklenen sonucu (`329 B15`) açıkça verdi; gevşeklik yukarıdaki dört koşulla
sınırlandı. İstenirse `DbsAboneAramasi` çağrısı kaldırılınca davranış onay + öğrenmeye döner
ve §81'in geri kalanı (koşul c'nin kapanması) ayakta kalır.

**Anahtar kelimeler kodda, tabloda değil.** `DBS` / `ABONE`, `BankalarArasiIfadeleri` ile
aynı yerde ve aynı gerekçeyle sabit: bunlar kullanıcı yapılandırması değil, banka
gövdesinin dilbilgisi.

## 82. Düzeltilmiş ekstre orijinalin kopyası değil; sıfırdan yazılan dört kolonlu dosya

ORKA Veri Transferi ekranı `Tarih | Açıklama | Giren | Çıkan` bekliyor: başlık 1. satırda,
veri 2'den, künye bloğu yok. Eski sürüm bankanın **17 kolonlu** dosyasını açıp yalnız
açıklama kolonunu değiştiriyordu; çıktı 6 satırlık hesap künyesiyle ve bankanın tüm
kolonlarıyla geliyordu — ORKA bu yapıyı okumuyor.

### "Yalnız 17 satır" — nedeni kopyala-kaydet yöntemiydi

Ölçüm sırası:

1. **Satır döngüsü suçsuz.** Gerçek dosyada 287 satırın hepsi işleniyor,
   `KaynakSatirNo` 8–294 arası, hiçbiri sıfır değil, hepsi tekil; üretilen xlsx'in
   XML'inde de 297 `row` elemanı duruyordu. Yani satırlar dosyaya **yazılıyordu**.
2. **Ama üretilen dosya bozuktu.** Kaynak dosyayı ClosedXML ile açıp hiçbir şey
   değiştirmeden kaydetmek bile yeterli: üretilen dosya **ClosedXML'in kendisiyle bile
   yeniden açılamıyor** — `XLWorkbook.LoadStyle` içinde
   `ArgumentOutOfRangeException (index)`. Round-trip'te stil tablosu (`cellXfs` 6→7,
   `numFmts`) kaynakla tutarsızlaşıyor. Dosya boyu da 48 KB'den 42 KB'ye düşüyor.

Bozuk bir dosyayı okuyan taraf (Excel/ORKA) onarma moduna girip içeriğin bir kısmını
düşürüyor; kullanıcının gördüğü **17 veri satırı** buna karşılık geliyor. Yeni yöntemde
kaynak dosya hiç açılmıyor — sorun kökten kalkıyor. Testler üretilen dosyayı ClosedXML ile
**okuyor**: eski çıktıda bu mümkün değildi, artık regresyon otomatik yakalanır.

### Kaynak dosyaya ve `AciklamaKolonu`'na artık ihtiyaç yok

Eski sürüm iki ön koşul arıyordu: `DosyaIcerik` saklanmış olmalı ve `AciklamaKolonu`
belirlenmiş olmalı; ikisi de yoksa kural hatası veriyordu. Dosya artık satırlardan
üretildiği için ikisi de gereksiz; `DuzeltilmisEkstreHazir` sabit `true`.
(Yükleme listesindeki `KaynakDosyaVar` alanı bilgi amaçlı duruyor, dokunulmadı.)

### İki çıktı tek satır kümesinden üretiliyor

Robot kod listesini ORKA gridine **satır sırasına göre** yazıyor. Düzeltilmiş ekstre ile
kod listesi aynı satırları aynı sırada içermezse kodlar yanlış satırlara gider — sessiz ve
pahalı bir hata. Bu yüzden filtre iki yerde ayrı ayrı yazılmıyor; ikisi de
`OrkayaGidenSatirlar` üzerinden geçiyor: `SiraNo` sırası + "diğer bankada" işaretli
satırların düşürülmesi (§61). Hizalama teste bağlandı: 287 satırın her biri için tarih,
açıklama ve tutar iki çıktıda karşılaştırılıyor; ayrıca bir satır "diğer bankada"
işaretlenip **ikisinden birden** düştüğü doğrulanıyor.

### Kolon kararları

- **Tutar sayısal hücre**, metin değil: ORKA metin hücreyi yanlış ayrıştırabiliyor.
  Görünüm biçimi `#,##0.00`; hücrenin kendisi sayı.
- **Yönüne göre tek kolon dolar**, diğeri boş kalır. Tutar veritabanında her zaman
  pozitif, işaret `Yon` alanında (§-domain kuralı); dosyada da işaret değil kolon taşıyor.
- **Tarih gerçek tarih hücresi**, `dd.MM.yyyy` biçiminde. Metin yazılsaydı ORKA'nın kültür
  ayarına bağlı kalırdı.
- **Açıklama 50 karakterde kırpılıyor** (`AciklamaUretici.EnFazlaUzunluk`) — ORKA zaten
  kesiyor; kırpma üretimde değil çıktıda tekrar uygulanıyor ki kaynak ne olursa olsun sınır
  aşılmasın.

## 83. Muhasebe > Hesap Planı prod'da boş: teşhis

Sayfa prod'da "Hesap planı boş." diyor, local'de dolu, ekranda hata yok. Ölçülenler:

### 1. Veri kaynağı: `catalog.HesapPlani`, Banka Otomasyon'un tablosu değil

`HesapPlaniPage` → `GET /catalog/muhasebe/hesap-plani` → `HesapPlaniService.GetHepsiAsync`
→ **yalnız** `_db.HesapPlanlari` (tablo `catalog.HesapPlani`). Ağaç istemcide `UstHesapId`
ile kuruluyor.

Karıştırılan iki kaynak <b>değil</b>:
- `catalog.EkstreHesapPlani` — Banka Otomasyon'un kendi kopyası, **firma bazlı** (FirmaId),
  ekstre eşleştirmesi için (§70). Bu sayfa oraya hiç bakmıyor.
- `Fis`/`FisSatir` — hesap planı ağacı bunlardan **hesaplanmıyor**. Yalnız yan kolondaki
  **bakiye** mizan ucundan geliyor; bakiye gelmese bile ağaç dolu görünürdü.

### 2. Kapsam: `TenantNo` + JWT `tn` claim'i — ve claim header'ı EZİYOR

`HesapPlani : TenantEntity`; `CatalogContext`'te
`HasQueryFilter(x => x.TenantNo == _tenant.CurrentTenantNo)`. Değer `HttpCurrentTenant`'tan:
**önce JWT `tn` claim'i**, yoksa `x-tenant-no` header'ı.

İstemci `TenantHeaderHandler` ile session'daki seçili firmayı header'a koyuyor — ama
sunucu claim'i önceliyor. **Canlı ölçüldü** (yerel servis, gerçek veriyle):

| İstek | Sonuç |
|---|---|
| Token yok | **401** |
| `tn=201` (planı olan tenant) | **200**, 293 kayıt, 9 kök |
| `tn=999` (planı olmayan tenant) | **200, `[]`** |
| Token'da `tn` claim'i yok | **200, `[]`** |
| `tn=999` claim + `x-tenant-no: 201` header | **200, `[]`** → claim kazanıyor |
| claim yok + `x-tenant-no: 201` header | **200**, 293 kayıt |

Yani: kullanıcı ekrandan hangi firmayı seçerse seçsin, token'ında `tn` varsa Muhasebe
kapsamı **o**dur. Prod'da token'ın `tn`'i planı olmayan bir tenant ise sayfa sessizce boş
kalır — tam görülen tablo.

### 3. Hata neden görünmüyor: istemci her hatayı yutuyordu

`MuhasebeApi.GetHesapPlaniAsync` `catch (Exception) { return new(); }` yapıyordu. 401, 500,
zaman aşımı, gateway hatası — hepsi boş listeye dönüşüp ekranda "Hesap planı boş." diye
görünüyordu. **Boş ekran teşhis için kanıt değildi**: prod'da isteğin başarılı olup
olmadığı ekrandan anlaşılamıyordu.

Düzeltildi: `GetHesapPlaniSonucAsync` liste + `Basarili` döndürüyor, sayfa iki durumu ayrı
mesajla gösteriyor (aşağıda). Eski metot davranışını koruyor, diğer çağıranlar bozulmadı.

### 4. Prod verisi: buradan doğrulanamadı

Prod veritabanına bu makineden erişim yok (bağlantı dizeleri local ve docker içi:
`s_sqlserver`). Doğrulanabilen tek şey **rota**: `https://www.dijitalmasraf.com/catalog/muhasebe/hesap-plani`
token'sız **401** dönüyor — yani uç yayında ve gateway rotası çalışıyor, "404/rota yok"
senaryosu elendi.

Kalan iki soru prod erişimi istiyor:
1. `SELECT TenantNo, COUNT(*) FROM catalog.HesapPlani GROUP BY TenantNo` — prod'da satır var mı, hangi tenant'ta?
2. Prod token'ının `tn` claim'i ne? (Tarayıcı > Application > token'ı jwt.io'da açmak yeter.)

İkisi eşleşmiyorsa sebep budur ve **düzeltilecek bir kod hatası değildir**: o tenant için
plan yüklenmemiştir.

### 5. Plan nasıl yükleniyor — ve neden prod'da eksik olabilir

`MuhasebeSeed.SeedAsync` tenant başına MSUGT planını `thp-standart.json`'dan yüklüyor;
idempotent (plan doluysa dokunmuyor). İki dar yeri var:

- **Tenant listesi kodda sabit**: `Program.cs`'te `var tenants = new[] { "201","106","108","105","107","500" }`.
  Bu listede olmayan bir firma için seed **hiç çalışmaz**; yeni açılan firma planssız kalır.
- **Dosya yoksa sessizce çıkıyor**: `if (!File.Exists(path)) return;`. Dosya depoda var ve
  csproj `PreserveNewest` ile çıktıya kopyalıyor (kontrol edildi), ama yayında eksikse hata
  değil sessizlik üretir.

**Ekrandan yükleme yolu yok.** `HesapPlaniController`'da içe aktarma ucu yok; sayfadaki
ekleme yalnız **mevcut bir satırın altına** alt hesap açıyor (`AltHesapEkleAsync`). Plan
boşsa kullanıcı ilk kök hesabı ekranda oluşturamaz — boş plan çıkmaz sokak.

### 6. `Fis`/`FisSatir` query filter uyarısı bu sayfanın sebebi DEĞİL

Derleme uyarısı gerçek: `Fis`'te global query filter var, `FisSatir`'de yok ve ilişki
zorunlu. Ama:

- Hesap planı ağacı `Fis`/`FisSatir`'a **hiç dokunmuyor** (madde 1).
- `FisSatir` zaten tenant kolonu taşımıyor; izolasyon **tasarım gereği** `Fis` üzerinden.
  `RaporService`'teki üç sorgunun üçü de `FisSatirlar`'ı `Fisler` ile **join'leyerek**
  okuyor, filtre join'e uygulanıyor: ne satır kaybı ne de tenant sızıntısı var.
- `HesapPlaniService`'te iki yerde (`AnyAsync(s => s.HesapId == ...)`) join yok; orada
  kapsam `HesapId`'den geliyor (hesap zaten o tenant'ın), sonuç değişmiyor.

Yani filtreyi iki tarafa eşitlemek ya da ilişkiyi opsiyonel yapmak **bu boş sayfayı
düzeltmez**. Uyarıyı susturmak istenirse doğru yol `FisSatir`'a navigasyon üzerinden eşleşen
bir filtre (`HasQueryFilter(s => s.Fis.TenantNo == ...)`) eklemek; ilişkiyi opsiyonel yapmak
yanlış olur — satır fişsiz var olamaz. Bu tur kapsam dışı bırakıldı: davranış değişmiyor,
yalnız uyarı susuyor ve her sorguya ikinci bir filtre bindiriyor.

### 7. Yan bulgu: migration zinciri sıfırdan bir veritabanını kuramıyor

Teşhis için boş bir veritabanına (`HesapPlaniTani`) servis bağlandığında migration
`Error 4924` ile düştü; yalnız 16 tablo oluştu ve uygulama hatayı yutup açıldı. Bu, prod'da
**yeni bir veritabanı** kurulursa şemanın eksik kalacağı anlamına gelir. Mevcut prod
veritabanı zaten kurulu olduğu için bugünkü sorunla ilgisi yok, ama ayrı bir iş olarak
kayda geçiyor.

### Ekran mesajı: "boş" yerine ne yazıyor

Boş ekran artık iki ayrı mesaj:

- **İstek başarısızsa**: "Hesap planı alınamadı. Sunucuya ulaşılamadı ya da oturumunuz
  düşmüş olabilir…" — 401/500 bir daha "veri yok" gibi okunmaz.
- **İstek başarılı ama kayıt yoksa**: "Bu firma için hesap planı yüklenmemiş. Hesap planı
  firma (tenant) bazlıdır ve sunucu tarafında yüklenir; bu ekrandan boş bir plana ilk hesap
  eklenemez…"

Mesaj bilerek "Tanımlar'dan yükleyin" demiyor: bugün öyle bir ekran yok (madde 5). Olmayan
bir yola yönlendirmek kullanıcıyı boşuna dolaştırırdı.

## 84. Tekdüzen hesap planı ekrandan yükleniyor; açılış seed'i artık sessiz değil

§83'te ölçülen boşluk: plan yalnız açılışta ve yalnız `Program.cs`'teki **sabit tenant
listesi** (`201,106,108,105,107,500`) için yükleniyordu. Listede olmayan firma planssız
kalıyor, ekranda boş sayfa görünüyor ve **ekrandan yükleme yolu yok**; sayfadaki ekleme
yalnız mevcut bir satırın altına alt hesap açıyor.

### Seçilen yol: düğme, otomatik toplu seed değil

Ekrana **"Tek düzen hesap planını yükle"** düğmesi kondu (boş plan mesajının altında).
Yükleme, kullanıcının **o an bağlı olduğu** firmaya yazılır: `TenantNo`'yu
`CatalogContext.SaveChangesAsync` istekteki tenant'tan damgalar.

Değerlendirilen alternatif — "açılışta, kaydı olmayan **her** tenant'a otomatik yükle" —
seçilmedi:

- Açılışta hangi tenant'ların var olduğu tek bir kaynaktan gelmiyor; liste
  `Firmalar`/`Mukellefler`/kullanıcı kayıtlarından türetilseydi, sistemde adı geçen ama
  muhasebe kullanmayan her firmaya 293 satır yazılırdı — geri alması elle iş.
- Yükleme kararı muhasebeye ait: bir firma planını ORKA'dan farklı bir maskeyle kurmak
  isteyebilir. Kullanıcının bakarak bastığı düğme, açılışta sessizce yazılan 293 satırdan
  daha kontrollü.
- Boş sayfanın asıl teşhis maliyeti "veri yok" ile "hata var"ı ayırt edememekti (§83);
  o ayrım zaten düzeltildi.

**Açılıştaki sabit tenant listesine bu turda dokunulmadı.** Düğme boşluğu kapattığı için
listeyi türetilmiş hâle getirmek acil değil; istenirse ayrı bir iş olarak yapılır.

### Yükleyici tek kod, iki çağıran

`MuhasebeSeed.YukleAsync(db, kaynak, ct)` hem açılış seed'inin hem düğmenin arkasında.
İki ayrı yükleyici olsaydı biri güncellenip diğeri unutulurdu. Sonuç üç durumlu:
`Yuklendi` / `ZatenDolu` / `SablonYok`.

Şablon dosyası erişimi `ITekDuzenPlanKaynagi` arkasına alındı
(`DosyadanTekDuzenPlanKaynagi` → `Infrastructure/Setup/SeedFiles/thp-standart.json`).
Kazanç: testler dosyaya bağlı kalmadan sahte şablon verebiliyor ve "dosya yok" hâli
gerçekten sınanabiliyor.

### Sessiz çıkış kaldırıldı

Eskiden dosya yoksa `if (!File.Exists(path)) return;` — hiçbir iz bırakmadan. Artık:

- **Açılışta**: `SablonYok` sonucu `LogError` ile yazılıyor (tenant numarasıyla birlikte).
- **Düğmede**: uç 500 + açık mesaj dönüyor ("Tekdüzen hesap planı şablonu sunucuda
  bulunamadı (thp-standart.json)…") ve controller ayrıca sunucu loguna yazıyor.
- **Testte**: `Gercek_sablon_dosyasi_depoda_duruyor` dosyanın depodan düşmesini yakalar
  (yayına kopyalanması csproj'daki `PreserveNewest` kuralına bağlı).

### Uç: `POST /catalog/muhasebe/hesap-plani/tek-duzen-yukle`

| Durum | Yanıt |
|---|---|
| Yüklendi | `200 { adet, message }` |
| Plan zaten dolu | `409 { message }` — üzerine yazmaz, ikilemez |
| Şablon yok | `500 { message }` + sunucu logunda `LogError` |

**Canlı ölçüldü** (yerel servis, boş bir veritabanı, gerçek şablon dosyası):

```
tn=777, yüklemeden önce GET      → 200 []
POST .../tek-duzen-yukle         → 200 {"adet":293}
tekrar POST                      → 409 "zaten dolu"
tn=777 GET                       → 200, 293 kayıt, 9 kök
tn=888 GET                       → 200 []            (başka tenant etkilenmedi)
veritabanı                       → yalnız TenantNo=777 için 293 satır
```

### Bilinen sınır: kapsam hâlâ token'ın `tn` claim'i — firma seçimini eziyor

§83'te ölçüldü, burada kayda geçiyor: Muhasebe modülünün kapsamı JWT'deki **tek** `tn`
claim'inden geliyor ve istemcinin gönderdiği `x-tenant-no` header'ını (ekrandaki firma
seçimi) **eziyor**. Ölçüm: `tn=999` claim + `x-tenant-no: 201` header → boş liste.

Sonuç: **sekiz firmayı tek oturumla yöneten kullanıcı, ekranda hangi firmayı seçerse
seçsin bu sayfada aynı veriyi görüyor** — token'ının tenant'ınınkini. Düğme de o tenant'a
yazar. Banka Otomasyon modülü tam bu sebeple tenant kapsamından **`catalog.Firmalar.Id`**
kapsamına taşınmıştı (§68/§69): tek tenant claim'i çok firmalı çalışmayı taşımıyor.

Bu tur Muhasebe tarafı **bilerek değiştirilmedi** (kapsam değişikliği fiş, mizan, masraf
merkezi ve hesap planının tamamını ilgilendirir; ayrı bir iş). İleride Banka
Otomasyon'daki mekanizmaya taşınmalı: kapsam istekten (`?firmaId=`) gelir, sorgularda
görünür yazılır, `SaveChanges` boş kapsamı reddeder.

## 85. Üç yeni banka: tek kütüphane yetmedi, okuyucu zinciri kuruldu

**Karar:** Ekstre dosyası artık tek bir Excel kütüphanesiyle değil, imzaya bakan bir
**okuyucu zinciriyle** açılıyor (`EkstreTabloOkuyucu`). OLE2 imzalı dosya NPOI/HSSF ile,
zip imzalı dosya sırayla ClosedXML → NPOI/XSSF → ham XML (`HamXlsxOkuyucu`) ile denenir.
Tanınmayan imza anlaşılır hata verir.

**Neden:** Üç bankanın üç ayrı hastalığı var ve hiçbiri tek okuyucuyla geçilmiyor:

| Banka | Sorun | Çözen yol |
|---|---|---|
| İş Bankası | Dosya **eski `.xls`** (OLE2 kabı), xlsx değil | NPOI/HSSF |
| Akbank | Bazı okuyucular satırı **tek hücre** görüyor, hata da vermiyor | Kullanılabilirlik denetimi + sıradaki okuyucu |
| Ziraat | `styles.xml` **bozuk**; biçim tablosunu okuyan her kütüphane patlıyor | Zip içindeki XML'i doğrudan okuyan yedek yol |

Hata vermeyen ama işe yaramaz sonuç (Akbank) sessizce "başarılı" sayılmasın diye zincirde
bir de **kullanılabilirlik** denetimi var: hiçbir satırda iki dolu hücre yoksa sıradaki
okuyucuya geçilir. Yedek yola düşüldüğünde hangi okuyucunun neden başarısız olduğu
`Uyarilar`'a yazılır — sessizce düşülmez.

**Bedeli:** Ham XML yolunda hücrenin tarih biçimli olup olmadığı bilinemiyor (biçim tablosu
zaten bozuk). Tarih kolonundaki sayısal değerler Excel seri numarası aralığıyla
(1950–2079) ayrılıyor; bu yorum yalnız tarih kolonunda yapılıyor, tutarda değil.

**Vakıfbank ayrıştırıcısı bu iskelete taşınmadı.** Çalışıyor ve gerçek dosyayla
doğrulanmış durumda; taşımak kazanç değil risk olurdu.

## 86. İşlem tipi kolonu olmayan bankada uydurma işlem tipi türetilmedi

**Karar:** Akbank ve Ziraat ekstrelerinde işlem tipi kolonu yok; `IslemTipi` **boş**
bırakıldı. Bu iki bankanın şablonları `İçerir` eşleşmesiyle **ham açıklamadan**, sabit
kuralları da `KuralKapsami.Aciklama` ile tanımlandı.

**Neden:** Açıklamanın önekinden bir işlem tipi türetmek ("7777/MBL-" → `MBL`) kolaydı ama
tehlikeli: unvan çıkarılamayan satırların öğrenme anahtarı `ISLEM:<işlem tipi>` oluyor ve
uydurulmuş bir tip ilk onaydan sonra aynı kanaldan geçen **ilgisiz** satırları da aynı
hesaba çözerdi. `AciklamaUretici.SablonBul` zaten önce ham açıklamayı tarıyor (§ Vakıfbank
"Hesaplar Arası EFT" düzeltmesi), yani boş işlem tipi şablon eşleşmesini engellemiyor.

**Yan etki:** Şablonu tutmayan satırlarda açıklama üretimi eskiden yalnız işlem tipine
düşüyordu ve boş tiple metin `- Unvan` diye başlıyordu. Sıra artık işlem tipi → unvan →
bankanın kendi açıklaması (kırpılmış). Uydurma yok; en kötü ihtimalle bankanın metni.

## 87. Yön ve kredi numarası şablon tablosuna kolon eklenerek değil, yer tutucuyla çözüldü

**Karar:** `AciklamaUretici`'ye iki yer tutucu eklendi: `{YON}` (Gelen/Giden) ve `{KREDI}`
(açıklamadaki kredi hesap numarası). Şablon tablosuna yön kolonu **eklenmedi**.

**Neden:** İş Bankası aynı işlem tipini iki yönde de kullanıyor ("EFT" 242 satırın hem
tahsilat hem ödeme tarafı). Vakıfbank'ta bu sorun yoktu; orada yön zaten işlem tipinin
adında ("Gelen EFT Otomatik Yatan" / "Hesaba giden EFT"). Şablona `Yon` kolonu eklemek
tabloyu, DTO'yu, ekranı ve eşleşme sırasını birden değiştirirdi; yer tutucu aynı sonucu
veriden üretiyor ve şablon ekranında kendiliğinden görünüyor (liste tek yerde:
`AciklamaUretici.YerTutucular`).

`{KREDI}` ile öğrenme anahtarı **aynı kaynaktan** okunuyor (`Normalizasyon.KrediAnahtar`):
açıklamada yazan numara ile anahtardaki numara ayrışırsa kullanıcı iki farklı krediyi aynı
sanır. Aynı fonksiyon İş Bankası'nın yazımını da tanıyacak şekilde genişletildi
("KREDİ NO: 10080844268"; Vakıfbank'ta "… kredi hesap numaralı").

## 88. Banka referansı saklanıyor, otomatik mükerrer elemesi yazılmadı

**Karar:** `EkstreSatiri.Referans` alanı eklendi (İş Bankası "Referans", Akbank
"Fiş/Dekont No", Ziraat "Fiş No"); ayrıştırıcılar dolduruyor, hiçbir mantık **okumuyor**.

**Neden:** Referans bankanın kendi tekil anahtarı ve aynı dönemin ikinci kez yüklendiğini
görmenin en sağlam yolu. Ama satırı sessizce düşüren bir otomatik eleme, referansın tekil
olmadığı durumlarda (iptal/düzeltme kayıtları, bankanın numarayı tekrar kullanması) gerçek
satırları kaybettirirdi. Alan önce saklanıp gözlenecek; eleme kararı veriden sonra.

Migration: `20260828204703_BankaEkstreSatirReferansi` — tek kolon, `nvarchar(100)`, null
kabul eder.

## 89. İş Bankası'nda unvanın başta mı sonda mı olduğu işlem tipiyle değil veriyle ayrılıyor

**Karar:** İş Bankası açıklaması yıldızla ayrılmış alanlardan oluşuyor
(`UNVAN*0111*GÖVDE*REFERANS*KANAL`) ama "Havale" tipinde unvan **sonda**
(`2. FATURA BEDELİ ÖDEMESİ*OPAT OTOMOTİV …`). Baştaki unvanı yakalayan desen, ikinci alanın
**dört haneli banka kodu** olmasını şart koşuyor: `^([^*]{4,}?)\*\d{4}(?:\*|$)`.

**Neden:** Desen tablosunda deseni işlem tipine bağlayan bir alan yok; "Havale'de şu deseni
önce dene" demek için tabloya kolon, servise filtre ve ekrana alan eklemek gerekirdi. Banka
kodu çıpası aynı ayrımı **veriden** yapıyor: havale gövdesinde ikinci alan kod değil metin,
o yüzden baştaki desen hiç tutmuyor ve sıradaki (sondaki unvan) deseni kazanıyor.

Sondaki unvan deseni en az **iki kelime** arıyor: tek kelimelik kuyruklar ("FAST",
"8792586") unvan değil.

## 90. Ziraat'te IBAN'ın önündeki ad unvan sayılmadı

**Karar:** `Enpara Bank A.Ş./TR38…-BURAK GÜNEL/…` kalıbında yalnız IBAN'dan **sonraki** ad
için desen tanımlandı; IBAN'dan önceki ad için desen **yazılmadı**.

**Neden:** Oradaki ad karşı tarafın **bankası**, karşı tarafın kendisi değil. Desen
yazılsaydı yakalama sırayla ilk denenen olur ve her ödeme banka adına eşleşmeye çalışırdı;
banka isimli kayıtlar zaten benzersiz önek indeksine alınmıyor (`CariOnekIndeksi`), yani
satır hiç çözülmeden onaya düşerdi. IBAN'dan sonraki ad gerçek karşı tarafı veriyor.

## 91. Belge saklama: yeni mekanizma kurulmadı, repodaki kalıp izlendi

**Karar:** Beyanname belgeleri (tahakkuk/beyanname/dekont) ve firma belgeleri (imza
sirküleri, vergi levhası…) **FileApiService**'te saklanıyor; CatalogService'te yalnız
`FileId` + metadata (`FileName`, `ContentType`, `Length`, `CreatedAt`, yükleyen) duruyor.

**Neden:** Prompt "önce mevcut altyapıyı incele, yeni mekanizma icat etme" dedi; incelendi:

| Yer | Ne yapıyor |
|---|---|
| `TicaretSicilEk` | Kalıbın kaynağı: dosya FileApiService'te, kayıtta `FileId` |
| `JobAttachment` | Aynı kalıp, iş eklerinde |
| `IFileApiService.UploadGenericAsync(file, folder)` | `POST /uploads` → `{ Id, Key, FileName, ContentType, Length }` |
| `IFileApiService.GetDownloadAsync(id)` | `presignedUrl` → iframe ile tarayıcı içinde açılıyor |
| `IFileApiService.DeleteAsync(id)` | Yetim dosyayı silmek için |

Akış üç adım ve **telafi silmesi** de kalıbın parçası (`AddAppointmentPage`'teki gibi):
dosya yüklenir → metadata kaydedilir → kayıt başarısızsa yüklenen dosya silinir.
CatalogService'in FileApiService'e giden bir istemcisi yok; bu yüzden "artık sahipsiz
kalan dosya" bilgisi yanıtta `artikFileId` olarak istemciye dönüyor ve silmeyi o yapıyor.

Görüntüleme **indirme değil**: presigned URL bir `iframe`'e veriliyor
(`PdfGoruntuleyiciDialog`), indirme yalnız kullanıcı isterse. Aynı bileşen KDV
modülündeki `FaturaPdfDialog`'un genelleştirilmiş hâli; iki özellik de onu kullandığı
için `Pages` altında değil `Shared/Components` altında duruyor.

Doğrulama iki yerde: istemcide hızlı geri bildirim, sunucuda kesin karar (yalnız PDF,
0 < boyut ≤ 20 MB). İstemcideki kontrol atlanabilir olduğu için kayıt kuralı sunucuda.

## 92. Beyanname türleri sabit listeden tabloya taşındı; saklanan metin korundu

**Karar:** `catalog.BeyannameTurleri` (global tablo) eklendi. Özet matrisinin kolonları ve
Takip ekranının tür listesi buradan geliyor. Tablo üç alan taşıyor: **Deger** (kayıtlarda
saklanan metin), **Kod** (vergi kodu), **Ad** (okunur ad).

**Neden:** Liste `DeclarationFollow.razor` içinde `List<string>` olarak duruyordu
("0015 KDV-1", "SGK" …). Matris kolonlarını da oradan almak aynı listeyi iki ekranda ayrı
ayrı yaşatırdı; yeni bir tür eklemek de kod değişikliği demekti.

**Kritik ayrıntı — `Deger` aynen korundu.** Kurulu veritabanlarındaki kayıtlar
`Declaration.DeclarationType` alanına eski listedeki metni yazmış durumda; tanım tablosu o
metni taşımasaydı hiçbir mevcut kayıt matriste bir kolona düşmezdi.

Eşleştirme üç adımlı (`BeyannameTuruEsleyici`): tam değer → baştaki dört haneli vergi kodu
→ okunur ad. Hiçbiri tutmazsa **tahmin edilmez**: metin `EslesmeyenTurler` ile raporlanır
ve ekran "bu türler tanımlarda yok" uyarısı verir. Sessizce düşseydi kayıt matriste hiç
görünmez, kullanıcı da eksiği fark etmezdi.

Karşılaştırma Türkçe sadeleştirmeden geçiyor: invariant kültür 'ı' → 'I' ve 'i' → 'İ'
dönüşümünü yapmadığı için "GECİCİ" ile "gecici" `OrdinalIgnoreCase` altında bile
ayrışıyordu (aynı tuzak Banka Otomasyon'da başlık aramasını bozmuştu).

**Bilinen sınır:** `Declaration` tablosunda tenant query filter **yok** (modül baştan böyle
kurulmuş); `CustomerCompany`'de var. Özet matrisi satırlarını `CustomerCompany`'den
kurduğu ve yalnız görünen firmaların kayıtlarını topladığı için dışarıdan kayıt sızmıyor.
Beyanname tablosunun kendi kapsamı bu turda **bilerek değiştirilmedi** — Takip ekranı ve
yıllık özetler aynı tabloyu filtresiz okuyor, kapsam değişikliği ayrı bir iş.

## 93. Firma sicil alanları iki tabloya kopyalanmadı

**Karar:** Unvan, VKN, vergi dairesi, ticaret sicil no, e-posta ve telefon
`catalog.Firmalar`'da kalıyor; MERSİS, kuruluş tarihi, adres, NACE ve sermaye yeni
`FirmaSicilBilgileri` tablosunda. Ekran ikisini tek formda gösteriyor, **kaydetme ikisini
de yazıyor**.

**Neden:** Alanları yeni tabloya kopyalamak iki kaynaklı gerçek üretirdi: birini
güncelleyip diğerini unutmak an meselesi. Ayrı tablo tutmamak da olmazdı — `Firma`
kaydı kataloğun her yerinde kullanılıyor ve MERSİS/NACE gibi alanlar oraya ait değil.

Sicil kaydı firma başına tek (`FirmaId` benzersiz); ikinci kaydetme yeni satır açmıyor.

## 94. Firma Bilgileri kapsamı için ikinci bir mekanizma kurulmadı

**Karar:** Firma Bilgileri uç noktaları Banka Otomasyon'un `BankaFirmaFiltresi` +
`IBankaFirmaKapsami` ikilisini **doğrudan** kullanıyor: `?firmaId=` zorunlu, parametre
doğrulanıyor, kapsam her sorguda görünür yazılıyor, global query filter yok.

**Neden:** Prompt "kapsam Banka Otomasyon'daki mekanizmayla aynı olsun" dedi. Aynı işi
yapan ikinci bir filtre + arayüz yazmak, iki mekanizmanın zamanla ayrışması demekti
(§68–§72'deki kararların yalnız birinde uygulanması gibi). Arayüzün adındaki "Banka"
tarihsel; içinde bankaya özel hiçbir şey yok.

Kapsam ayarlanmadan yapılan okuma **hata veriyor**, boş liste dönmüyor: kapsamsız bir
istek "hiç kayıt yok" gibi görünüp kullanıcıyı yanıltırdı.

## 95. Anasayfa kendi hesabını yapmıyor

**Karar:** Anasayfa kartlarının sayıları, tıklanınca gidilecek sayfanın **kendi
servisinden** geliyor: banka satırları Banka Otomasyon'un firma seçim ekranını besleyen
`IFirmaOzetService`'ten, beyanname sayıları `catalog.Declarations`'tan. `AnasayfaService`
yalnız üç kaynağı tek çağrıda topluyor; kuralları saf bir fonksiyon
(`AnasayfaOzetKurucu`) uyguluyor.

**Neden:** Anasayfa kendi sorgusunu yazsaydı, aynı sayı iki ekranda farklı çıktığında
sebebin veri mi yoksa iki ayrı hesap mı olduğu anlaşılmazdı.

İki ayrıntı kayda değer:
- **Firma kapsamı yok.** Kullanıcı sekiz firmayı birlikte yönetiyor ve açılışta hepsinin
  durumunu görmek istiyor — Banka Otomasyon'un firma seçim ekranıyla aynı gerekçe (§69).
- **Yaklaşan ödemeler ay değil tarih aralığı sorguluyor.** Ağustos beyannamesinin vadesi
  eylülde; ay sorgusu o satırı hiç göstermezdi.
- "Bekleyen" = **ödemesi tamamlanmamış**. Beyanname hazırlanmış, hatta onaylanmış olabilir;
  kullanıcının anasayfada aradığı sayı paranın çıkıp çıkmadığı.

"Son kullanılan firmalar" **tarayıcıda** (localStorage) tutuluyor: kullanıcının kendi
gezinme geçmişi, sunucuya yazılacak bir veri değil. Liste gerçek kullanımdan besleniyor —
firma bilgileri ekranı açıldığında ve anasayfadan bir firmaya gidildiğinde yazılıyor.

## 96. Sol menü teması: renkler tek yerde, ayrım yalnız renkle değil

**Karar:** Sol menü koyu (`#1f2733` — saf siyah değil, yumuşak koyu lacivert-gri); başlık
ve içerik alanı açık kaldı. Bütün renk değerleri `app.css`'te tek bir `:root` bloğunda
CSS değişkeni olarak duruyor, kural blokları yalnız o değişkenleri kullanıyor.

**Neden:** Renk kodları kural bloklarına dağılsaydı tema değişikliği dosya taraması
gerektirirdi. Değişken bloğu aynı zamanda kontrast belgesi: seçilen değerlerin WCAG
oranları yorumda yazılı (metin 12:1, soluk metin 6.6:1, seçili satır 5.9:1).

Seçili satır **yalnız renkle** ayrılmıyor; sol kenarında bir çubuk var (`inset box-shadow`)
ve yazı kalınlaşıyor. Renk körü kullanıcıda seçim yine ayırt edilebilsin diye. Aynı
gerekçeyle klavye odağı da görünür (`:focus-visible` konturu).

`index.html`'deki `app.css?v=` sürümü artırıldı; aksi hâlde tarayıcı eski dosyayı
önbellekten verir ve menü beyaz kalırdı.

## 97. Alt menü: kutu değil girinti

**Karar:** Alt menü satırları da koyu (`#18202b` — üst seviyeden bir tık daha koyu).
Kart görünümü kaldırıldı: arka plan kutusu, köşe yuvarlaması, yan boşluk ve kenarlık yok;
yerine solda ince bir dikey çizgi (`inset box-shadow`) ve girinti var. Metin üst seviyeden
soluk (`#9aa7b8`), hover'da hafif açılıyor, seçili satırda metin beyaza dönüyor ve sol
çizgi vurgulu renge geçiyor.

**Neden kutu görünüyordu:** Radzen'in material teması ikinci seviye satırlara *açıkça*
beyaz zemin + 4px köşe + 0.5rem yan boşluk veriyor
(`.rz-panel-menu .rz-navigation-menu .rz-navigation-item-wrapper`). Birinci seviyeye böyle
bir kural yok — orada `.app-sidebar`'ın koyu zemini görünüyor. Bu yüzden §96'daki blok
üst seviyeyi koyultmuş, alt seviyeyi hiç etkilememişti.

İki ayrıntı:
- **Seçici seviyesi.** §96'daki "alt seviye soluk metin" kuralı
  `.rz-navigation-menu .rz-navigation-menu` yazıyordu; kök `<ul>` `rz-panel-menu` olduğu
  için bu üçüncü seviyeye denk geliyor ve hiç uygulanmıyordu. Doğrusu
  `.rz-panel-menu .rz-navigation-menu`.
- **Renkler yine tek `:root` bloğunda.** Radzen'in `--rz-panel-menu-item-2nd/3rd-level-*`
  token'ları `.app-sidebar` üzerinde bizim değişkenlerimize bağlandı: tek tek kural
  yazmadan Radzen'in kendi kuralları da koyu temayı izliyor, ham renk kodu hiçbir kurala
  dağılmıyor.

**Devre dışı görünen satır yok.** Menüde hiçbir `RadzenPanelMenuItem`'da `Disabled` yok;
yetki kontrolü satırı *çizmemekle* yapılıyor (`@if (canView...)` ve `AuthorizeView`).
Soluk görünen satırlar Radzen'in ikinci seviye "tertiary" gri metin rengiydi — gerçekten
devre dışı değillerdi.

`index.html`'de `app.css?v=` yine artırıldı. Ayrıca **sürümsüz ikinci `app.css` bağlantısı
kaldırıldı**: sürümlü bağlantıdan *sonra* geldiği için tarayıcı önbellekteki eski kopyayı
üstüne yükleyebiliyordu — sürüm artırmanın etkisini götüren asıl sebep buydu.

## 98. Beyanname türleri tek kaynak, seed adım adım yalıtık

**Karar:** Beyanname türlerinin tek kaynağı `catalog.BeyannameTurleri`. Takip sekmesindeki
sabit `List<string>` kaldırıldı; Takip filtresi, yeni/düzenle formu ve Özet matrisi aynı
tablodan okuyor (`GET api/catalog/beyanname/turler`). Tablo **Tanımlar** sekmesinden
(`/beyannameler/tanimlar`) yönetiliyor: vergi kodu + ad + saklanan değer + sıra + aktif.

**Silme yok, pasife alma var.** `Deger` alanı mevcut kayıtların `DeclarationType` metniyle
eşleşiyor; tanım silinseydi o türdeki eski kayıtlar Özet matrisinde kolonsuz kalırdı.
Pasif tanımın kolonu çizilmez, kayıtlar durur, tanım geri açılabilir.

**Asıl hata seed'in kendisinde değildi.** `BeyannameTuruSeed` ne sabit tenant listesine ne
de bir seed dosyasına bağlı — açılışta tenant'sız context ile bir kez çalışıyor. Sorun
şuydu: bütün global seed'ler **tek bir `try/catch`** içindeydi, sıradaki herhangi bir seed
patlayınca ondan sonrakilerin hiçbiri çalışmıyor ve geriye tek bir genel hata satırı
kalıyordu. Tablo bu yüzden yayında boş kaldı.

Kalıcı çözüm `Infrastructure/Seeding/SeedAdimi.cs`: her seed adımı kendi `try/catch`'inde,
adıyla loglanıyor — biri düşse sonrakiler çalışıyor ve hangisinin düştüğü logdan okunuyor.
Aynı yalıtım tenant döngüsüne de uygulandı (§83'teki hesap planı hatasının kardeşi).
Beyanname seed'i ayrıca sonucunu loglar; tablo seed sonrası hâlâ boşsa `LogError` düşer.

Kurulu veritabanları için kaçış yolu: **"Varsayılanları yükle"** düğmesi
(`POST .../turler/varsayilanlari-yukle`) — hesap planındaki "Tek düzen hesap planını yükle"
ucuyla aynı kalıp (§84). Satır bazında idempotent: eksikleri ekler, kullanıcının
düzenlediği adların üzerine yazmaz.

Özet sekmesi tablo boşken artık yalnız "tanım yok" demiyor; **Tanımlar ekranını açan bir
düğme** gösteriyor.

## 99. Firma bir oturum bağlamı değil, verinin bir boyutu — firma seçim ekranı geri alındı

**Bu karar bir önceki turu geri alıyor ve bilinçli bir yön değişikliğidir.** §69–§71 ile
Banka Otomasyon'a bir *firma seçim ekranı* (giriş kapısı) ve modül içi bir *firma bağlamı*
eklenmişti: kullanıcı önce bir firmaya "girer", sonra o firmanın ekranlarında çalışırdı.
Kapsamın tenant'tan alınması hatası (§68) böyle kapatılmıştı.

Kapsam düzeltmesi doğruydu ve **duruyor**. Yanlış olan, düzeltmenin arayüze taşınma
biçimiydi: pkfadmin tek oturumla sorumlu olduğu sekiz firmayı birlikte yönetiyor ve her
işlem için firma değiştirmek istemiyor. Giriş kapısı, bir veri sorununa oturum çözümü
getirmişti.

**Yeni kural:** Firma, verinin bir kolonu — ekranın bir kipi değil.

**Değişmeyen:** Veri modeli. `FirmaId` kapsamı bütün tablolarda aynen duruyor; hesap planı,
cariler, öğrenilen eşleşmeler, banka hesapları ve beyannameler firmadan firmaya farklı.
Migration yok. `IBankaFirmaKapsami` de duruyor — zaten oturumdan değil isteğin `?firmaId=`
parametresinden besleniyordu (§68); değişen, o parametreyi kimin doldurduğu.

**Kaldırılan:** Firma seçim ekranı (`FirmaSecimPage`), istemcideki firma oturumu
(`IBankaOtomasyonOturumu` ve oturum deposu) ve `FirmaBasligi`'ndaki "hangi firmadayız"
başlığı. `/banka-otomasyon` kökü artık Aktar'a yönleniyor. Üstteki genel FİRMA DEĞİŞTİR
(tenant seçimi) duruyor ama bu modüllerde hiçbir şeyi belirlemiyor.

**Okuma ile yazma ayrıştı** — kararın özü burada:

| | Kapsam kaynağı | `firmaId` yoksa |
|---|---|---|
| **Okuma (GET/HEAD)** | Kullanıcının seçtiği filtre | Tüm firmalar; her satır firma kolonu taşır |
| **Yazma** | Kaydın kendisi | **400** — ayrıca `SaveChangesAsync` ikinci kez reddeder |

Yazmada firma asla "aktif firma"dan türemiyor; ya **seçilen kayıttan** geliyor (ekstre bir
banka hesabına yüklenir, hesap zaten firmayı belirler) ya da **formda seçiliyor**. Filtre
ile form alanı bilerek ayrı iki kontrol: filtre neyin görüldüğünü, form alanı kaydın nereye
yazılacağını söyler. Tek kontrole bağlansalardı "listeyi daraltayım" derken kaydın firması
da değişirdi.

Kapsamsız yazmaya izin verilen tek yer `[FirmaKapsamiGerekmez]`: (1) "sahipsiz kayıtları
temizle" — kaydın kapsamsız oluşu işin kendisi (§71), (2) global yapılandırma tabloları
(açıklama şablonları, unvan desenleri, sabit kurallar, vergi kodları, işlem kategorileri) —
entity'leri `FirmaKapsamliEntity`'den türemiyor, bankanın yazım kalıbına ait. Nitelik dar
tutuldu: `FirmaKapsamliEntity` yazan hiçbir uca konmaz.

**Yıkıcı ve firma başına anlamlı işlemler "tüm firmalar" görünümünde kapalı:** veri
temizliği, hesap planı içe aktarımı/özeti, hesap sahibi unvanı, banka adı birleştirme,
öğrenilen eşleşme içe aktarımı. Hepsi kullanıcıya hangi firmayı seçmesi gerektiğini yazıyor.
Kapsamı örtük bir silme ya da "sekiz firmanın toplamı" gibi okunamayan bir özet olmaz.

**Firma adı kaybolmadı, yer değiştirdi.** Ekranın tepesindeki tek etiket yerine işin
yapıldığı yerde: listelerde satır başına firma kolonu, Aktar'daki hesap kartlarında firma
satırı, onay ekranının başlığında ekstrenin firması, yükleme/silme/kaydetme onaylarında
firma adı ("PKF Aday için 287 satır yüklenecek"). Yanlış firmaya veri girmeye karşı savunma
artık tek bir yerde değil, işlemin yanında.

Firma adları `catalog.Firmalar`'dan geliyor ve yanıtlara `BankaFirmaFiltresi` içinde tek
yerde yazılıyor. Beş servisin bir düzine dönüş noktasına dağıtılsaydı biri unutulur, o
listede firma kolonu sessizce boş çıkardı. Kapsamın kendisi (hangi kayıtların geldiği)
buraya taşınmadı — o hâlâ sorgularda görünür yazılı (§69 duruyor).

**Anasayfa** kırpmayı bıraktı: onay bekleyen satırı olan bütün firmalar listeleniyor.
Kırpma, listenin dışında kalan firmayı görünmez yapıyordu — oysa işi bekleyen firma tam da
gözden kaçandır.

**Kapsam dışı: Muhasebe.** Muhasebe'nin hesap planı `TenantNo` kapsamlı (`TenantEntity` +
global query filter, token'dan besleniyor), `FirmaId` değil. Oraya firma seçici koymak ya
veri modelini değiştirmeyi (bu turda hariç tutuldu) ya da firma diye tenant listelemeyi
gerektirirdi — pkfadmin tek tenant'ta olduğu için ikincisi §68'deki sorunun aynısını üretir.
Muhasebe bu turda hiç değiştirilmedi; kapsam farkı bilinçli bir açık uç.

**Testler:** İzolasyon testleri aynen duruyor ve geçiyor — tek firma kapsamında sorgu hâlâ
yalnız o firmayı görüyor. `TumFirmalarKapsamiTests` yeni hâli sınıyor: kapsamsız okuma iki
firmayı birlikte getiriyor, her satır kendi firmasını taşıyor, kapsamsız yazma reddediliyor,
silme komşu firmaya dokunmuyor. İstemci tarafında `BankaOtomasyonOturumuTests` (oturumun
kalıcılığını sınıyordu) yerini `FirmaKapsamiIstektenGelirTests`'e bıraktı: çağıranın verdiği
firma adrese aynen yansıyor mu, ardışık çağrılar birbirinin firmasını taşıyor mu.

## 100. Ajan hub'ı ayrı servis değil, CatalogService'in içinde

PkfRobot'un bağlanacağı SignalR hub'ı `CatalogService.Api` içinde
(`Features/Ajanlar/`) yaşıyor. Ayrı bir "RobotGateway" servisi açmak yeni bir
container, yeni bir compose girdisi, yeni bir Consul kaydı, yeni bir Dockerfile
ve deploy adımı demekti — karşılığında kazanılan hiçbir şey yok. Ajanın
işleyeceği banka aktarım paketini üreten uçlar (`api/catalog/banka-ekstre/*`)
zaten bu serviste; iş emri o veriye bakacaksa hub'ın da orada olması ağ üzerinden
bir tur eksiltiyor.

Dilim adı `Ajanlar`, `AgentHub` değil. C#'ta bir tipin adı içinde bulunduğu ad
alanının son parçasıyla aynı olduğunda (`Features.AgentHub.AgentHub`) her
`using` çözümlemesi belirsizleşiyor. Klasör adı Türkçe olunca repodaki diğer
dilimlerle de (`Banka`, `Firmalar`, `Mukellefler`) tutarlı. Hub sınıfının adı ve
`/agenthub` yolu değişmedi — ajanın gördüğü sözleşme aynı.

## 101. Hub Ocelot'tan geçmiyor, nginx doğrudan bağlıyor

`wss://dijitalmasraf.com/agenthub` isteği nginx'ten **doğrudan**
`catalogservice.api:5004`'e gidiyor; gateway'e uğramıyor.

SignalR bağlantısı gün boyu açık kalan bir WebSocket. Ocelot bunu sıradan bir
HTTP isteği gibi ele alıp kendi timeout ve buffering ayarlarını uyguluyor — bu
projede uzun süren batch rotalarında tam olarak bu yüzden zaman aşımı yaşandı.
Bir de her kopuş, ajanın yeniden bağlanıp yeniden kaydolması demek; dakikada bir
dönen bir kopuş/kayıt turu, hub'ın çözmesi gereken sorunun kendisi olurdu.

Baypas **yalnız hub'ın yolu için**. Durum ucu (`/api/catalog/agent/baglilar`)
sıradan bir HTTP isteği olduğu için Ocelot'tan geçmeye devam ediyor ve mevcut
`/catalog/{everything}` kuralına düşüyor: **`ocelot.json`'a hiç dokunulmadı.**
Gateway'i tamamen atlamak, ileride C adımında ekrandan yapılacak sıradan
çağrıları da kural dışı bırakırdı.

nginx bloğu artık **repodaki `Nginx/conf.d/dijitalmasraf.conf`'ın kendisinde**;
ara dosya (`deploy/nginx-agenthub.conf`) kaldırıldı. Önce ayrı tutulmuştu, çünkü
bloğu hem repoya işleyip hem sunucuda elle uygulamak aynı server bloğunda iki
`location /agenthub` demek olur; nginx bunu `duplicate location` ile reddedip
**hiç ayağa kalkmaz** — yanlış sırayla yapılan bir "iyileştirme" siteyi komple
düşürürdü. Tek kaynak kalınca o risk kapandı: uygulama yolu tek, imajı yeniden
derlemek (`docker compose build nginx.public`). Sunucuda elle yapıştırma yolu
artık **yok** — bir kez daha yapıştırılırsa duplicate hatası geri gelir.

`map $http_upgrade $connection_upgrade` da `nginx.conf`'a değil, aynı conf
dosyasının en başına yazıldı. `conf.d/*.conf` zaten `http` bağlamına dahil
ediliyor, yani `map` orada geçerli; iki dosyaya bölünmeyince "birini ekledim,
diğerini unuttum" hali de kalmıyor.

Hedef adres `catalogservice.api:5004` — `net_backendservices` ağındaki servis
adı. Container adı (`c_catalogservice`) de çözülür ama servis adı compose'un
sözleşmesi; replika sayısı değişse ya da container yeniden adlandırılsa ayakta
kalan bu.

## 102. Ajan listesi bellekte; kalıcı tablo yanlış bilgi üretirdi

Bağlı ajanlar `ConcurrentDictionary` içinde, veritabanında değil.

Kayıt bir **bağlantının** ömrü kadar anlamlı. Veritabanına yazılsaydı container
yeniden başladığında ya da ağ koptuğunda tabloda "bağlı" yazan ama gerçekte
kimsenin dinlemediği satırlar kalırdı — ve o satıra bakıp iş gönderen kod
sessizce boşluğa yazardı. Container yeniden başlarsa liste sıfırlanıyor, ajanlar
birkaç saniyede yeniden bağlanıyor; kaybedilen tek şey birkaç saniyelik görünürlük.

Anahtar `MakineId`, `ConnectionId` değil: "aynı makine listede iki kez
görünmesin" kuralını dictionary'nin kendisi garanti etsin diye. Aynı makine
ikinci kez bağlandığında eski kayıt çıkarılıyor ve **eski soket kapatılıyor**
(hub `Context.Abort`'u depoya delege olarak veriyor; depo SignalR tiplerine
bağlanmıyor). Buradaki asıl tuzak sıra: yeni bağlantı kaydolduktan sonra eski
soketin "koptum" bildirimi geliyor. `Cikar` bu yüzden `ConnectionId` eşleşmesine
bakıyor — bakmasaydı makine her yeniden bağlandığında listeden düşerdi. Testi
var (`Dusurulen_baglantinin_kopusu_yerine_gecen_kaydi_silmez`).

Zaman aşımı **okuma anında** süzülüyor, arka plan servisiyle değil. Ölü kaydı
yalnız listeyi okuyan görüyor; temizliği de o yapabilir. Ayrı bir
`BackgroundService` aynı işi bir zamanlayıcı, kendi hata yönetimi ve kendi
testleriyle yapardı.

Bunun bedeli açık: liste **tek container'a özgü**. CatalogService birden çok
kopya olarak çalıştırılırsa ajan yalnız bağlandığı kopyadan görünür. Tek makine
senaryosunda sorun değil; ölçeklenirse Redis backplane gerekir.

## 103. Sürüm kontrolü ve kimlik baştan konuldu

**Sürüm kontrolü ilk turda yazıldı**, "sonra ekleriz" denmedi. Ajan Google Drive
üzerinden elle dağıtılıyor: sunucu yeni bir sözleşmeye geçtiğinde eski kurulumlar
bir süre daha ayakta kalıyor ve uyumsuz ajan, hata vermek yerine yanlış iş yapar.
Eşik yapılandırmadan okunuyor (`AgentHub:AsgariAjanSurumu`), koda gömülmedi —
"şu sürümün altındakiler bağlanmasın" demek için sunucuyu yeniden derlemek
gerekmesin.

Karşılaştırma `Version.TryParse` ile, metinle değil: `"1.10.0" < "1.9.0"` metin
olarak doğrudur ve tam da sürüm ondanla geçtiğinde, yani en kritik anda yanlış
yanıt verirdi. Bozuk yazılmış bir **asgari sürüm ayarı** ise kontrolü atlatıyor,
kimseyi dışarıda bırakmıyor: tek bir yapılandırma yazım hatasının bütün ofisi
bağlantısız bırakması, korumanın kendisinden büyük bir risk.

**Kaydın sahibi token'dan alınıyor.** `MakineId` istekle geliyor ve ajanın kendi
beyanı — ona güvenilmiyor. Sahip alanı `sub` claim'inden okunup kayıtta
saklanıyor; "kim hangi makineye iş gönderebilir" kuralı buna dayanacak. Bu turda
kural yazılmadı (iş emri henüz yok), ama alan doldurulmadan bırakılsaydı
sonradan geriye dönük doldurulamazdı.

> Bu alan §104'te **`AjanId`** oldu: kayıt artık bir kullanıcıya değil ajanın
> kendi kimliğine bağlı. Ajanı hangi kullanıcının oluşturduğu IdentityService'teki
> `Ajanlar` tablosunda duruyor.

**Token WebSocket'te sorgu dizesinden okunuyor** — ama yalnız `/agenthub`
yolunda. WebSocket el sıkışmasında tarayıcı `Authorization` başlığı
gönderemediği için SignalR istemcileri token'ı `?access_token=` ile taşıyor.
Bunu bütün uçlarda kabul etmek, token'ın adres çubuğunda ve nginx erişim
kayıtlarında dolaşması demek olurdu; `OnMessageReceived` bu yüzden yol kontrolü
yapıyor. Yerelde doğrulandı: `/agenthub/negotiate?access_token=…` → 200,
`/api/catalog/agent/baglilar?access_token=…` → hâlâ 401.

## 104. Ajanın kendi kimliği var; ofis makinesinde kullanıcı sırrı durmuyor

Ajan (PkfRobot) ofisteki makinede **günlerce** bağlı kalacak. Kullanıcı token'ı
20 dakika yaşıyor (prod'da ölçüldü: `nbf`/`exp` farkı 1200 saniye), yani o
token'la bağlanan bir ajan yirmi dakikada bir düşerdi.

Akla gelen üç yoldan ikisi elendi:

- **Kullanıcı token'ının ömrünü uzatmak** — bütün kullanıcıları etkiler, üstelik
  ofis makinesinde uzun ömürlü bir *insan* yetkisi bırakır.
- **Refresh token'ı ajana vermek** — makinede duran şey yine kullanıcının
  kimliği olur; o makine fiziksel olarak erişilebilir bir yerde.

Kalan ve seçilen: **ajana özel, kullanıcıdan bağımsız bir kimlik.** Yönetim
ekranında anahtar üretiliyor, ajan onu saklıyor, `POST /auth/agent/token` ile 8
saatlik bir *ajan token'ına* çeviriyor. Anahtar iptal edilebilir; iptal edilince
o ajan bir daha token alamaz.

**Neden 8 saat, süresiz değil:** iptal edilen bir ajanın elinde duran token'ın
bir ömrü olmalı. 8 saat, "günde bir kez token al" ile "iptal en geç bir mesai
içinde etkisini gösterir" arasındaki denge. Ajan token'ı kullanıcı token'ıyla
**aynı imza / issuer / audience** taşıyor — onu doğrulayan servisler bunu da
doğrulayabilsin diye; ayrım imzada değil claim'lerde.

## 105. Anahtar bir paroladır: aynı hasher, önek yalnız aday daraltır

Anahtar 32 bayt kriptografik rastgele, `pkfr_` önekiyle. Önek bilerek var: bir
yapılandırma dosyasına ya da sohbete yapıştırıldığında ne olduğu okunsun,
sızdığında aranabilsin.

**Ham anahtar hiçbir yerde saklanmıyor.** Veritabanında yalnız hash'i var ve hash
ASP.NET Identity'nin `IPasswordHasher<T>`'ı (PBKDF2) ile üretiliyor — repoda
parolalar da (UserManager üzerinden) onunla tutuluyor, yani tuz/iterasyon kararı
tek yerde kalıyor. Düz SHA256 bilerek kullanılmadı: anahtar bir paroladır ve
hızlı hash tam da denenebilir olmasını sağlar. Repoda `BCrypt.Net-Next` paketi
duruyor ama hiçbir yerde kullanılmıyor; ikinci bir hash ailesi açmak yerine
kullanılan aileye bağlı kalındı.

Anahtar **bir kez** gösteriliyor. Kaybolursa geri getirilemez — yeni ajan
oluşturulur, eskisi iptal edilir. Bu bir eksiklik değil, tasarımın kendisi.

`AnahtarOnEki` (ilk 8 karakter) iki iş yapıyor: listede hangi satırın hangi
anahtar olduğunu göstermek, ve token isteğinde **hash doğrulamasına girecek
adayları daraltmak**. Kararı önek vermiyor — öneki tutan ama gövdesi tutmayan bir
anahtar reddediliyor (`Oneki_tutan_ama_govdesi_tutmayan_anahtar_reddediliyor`).
Önek üzerindeki indeks bu yüzden tekil değil.

Ham anahtarın loglanmadığı ayrıca sınanıyor: `AjanAnahtariSizmiyorTests`
kabul / ret / iptal yollarının hepsini dolaşıp yazılan bütün log satırlarında —
biçimlenmiş metinde **ve** yapılandırılmış alanlarda — anahtarın geçmediğini
doğruluyor. Başarısız denemede loglanan tek şey önek.

## 106. Ajan ve insan token'ları politikayla ayrıldı; ayrım `ajan_id`'ye dayanıyor

Hub `[Authorize]` iken kullanıcı token'ını da kabul ediyordu; artık
`[Authorize(Policy = AjanPolitikalari.YalnizAjan)]`. Tersi de kondu: durum ucu
(`/catalog/agent/baglilar`) yalnız **insan** token'ını kabul ediyor. İkisi
birbirinin tersi ve bilerek öyle — ajan olmayan bir istemci ajan gibi kaydolup iş
emri bekleyemesin, ajan da diğer ajanların listesini okumasın.

Token `typ: agent` claim'i taşıyor ama **karar `ajan_id`'ye bakıyor**. Sebebi:
JwtBearer gelen kısa claim adlarının bir kısmını uzun URI'lere çeviriyor
(`MapInboundClaims`), bu eşleme kütüphane sürümüyle değişebiliyor ve `typ` ayrıca
JWS başlığında da anlamı olan bir ad. `ajan_id` bize ait, eşleme tablosunda yeri
yok ve bir kullanıcı token'ında hiç bulunmuyor.

Bu varsayılmadı, sınandı: `AjanPolitikalariTests` token'ı IdentityService'teki
gibi basıp .NET 8 JwtBearer'ın kullandığı `JsonWebTokenHandler` ile doğruluyor ve
`ajan_id`'nin adı değişmeden çıktığını gösteriyor.

> **Çözümdeki mayın:** `System.IdentityModel.Tokens.Jwt` 7.0.3 ile
> `Microsoft.IdentityModel.*` 8.14 yan yana geliyor. Bu eşleşmede eski
> `JwtSecurityTokenHandler` **okuma** yaparken `iss` / `exp` / `nbf` alanlarını
> düşürüyor — onunla yapılan her doğrulama sessizce çöker. Token **basmak**
> etkilenmiyor (IdentityService bunu kullanıyor, ürettiği token doğru). Üretimde
> okuma yoluna hiç girilmiyor: .NET 8'de JwtBearer varsayılan olarak
> `JsonWebTokenHandler` kullanıyor. Bir yere `JwtSecurityTokenHandler.ValidateToken`
> yazacak olursanız önce paket sürümlerini hizalayın. Bu tuzağa bu turda bir kez
> düşüldü; test o yüzden `JsonWebTokenHandler` kullanıyor.

`AjanKaydi.KullaniciId` → `AjanId` oldu. Ajanı hangi kullanıcının oluşturduğu
hub'da kopyalanmıyor; o bilgi `Ajanlar` tablosunda duruyor ve yönetim ekranı iki
listeyi `AjanId` üzerinden eşleştiriyor. Aynı gerçeği iki yerde tutmamak için.

## 107. İptalde açık bağlantıyı düşüren şey yönetim ekranı, event bus değil

İptal IdentityService'te oluyor, açık soket CatalogService'te. İkisini bağlamanın
iki yolu vardı:

- **RabbitMQ integration event** — repoda kalıbı var (IdentityService yayınlıyor,
  NotificationService dinliyor). Ama CatalogService bugün hiçbir olayı
  dinlemiyor; ilk aboneliği eklemek, RabbitMQ erişilemediğinde CatalogService'in
  ayağa kalkışını riske atan yeni bir başlangıç bağımlılığı demekti.
- **Yönetim ekranının iptalden hemen sonra CatalogService'i çağırması** —
  `POST /catalog/agent/{ajanId}/dusur`. Seçilen bu.

Bedeli açık ve kabul edildi: iptal yönetim ekranı dışından yapılırsa bağlantı
kendiliğinden düşmez, en geç ajan token'ının ömrü (8 saat) dolunca düşer. Anahtar
o an zaten geçersiz olduğu için ajan yeniden token alamaz — yani kapı kapalı,
yalnız içeridekinin çıkması gecikiyor.

Düşürme ucu ajan bağlı değilken de başarılı dönüyor: "bu ajan bağlı değil"
istenen sonucun ta kendisi, hata değil.

## 108. Yeni uçlar mevcut gateway kurallarının altına yerleştirildi

Ocelot ve nginx yapılandırması **değişmedi**. Uç adresleri bunun için seçildi:

| Uç | Yol | Geçtiği kural |
|---|---|---|
| Ajan token'ı | `POST /auth/agent/token` | `/auth/{everything}` — kimlik istemiyor |
| Ajan yönetimi | `/auth/admin/agents` | `/auth/admin/{everything}` — `role: Admin` şart |
| Bağlantı düşürme | `POST /catalog/agent/{id}/dusur` | `/catalog/{everything}` |

Görev metnindeki `/api/identity/agent/token` yolu kullanılmadı: karşılığı olan
bir gateway kuralı yok, eklenseydi kabul kriterindeki "gateway değişmedi" şartı
düşerdi. Adresler testle sabitlendi (`AjanUclariTests`) — ön ek değişirse test
düşer.

Token ucu `[AllowAnonymous]` olmak zorunda (ajanın elinde token yok, anahtar
var), bu yüzden **IP başına dakikada 10 istek** sınırı kondu. Sınırın amacı
anahtarı tahmin etmeyi engellemek değil — 256 bitlik anahtar zaten tahmin
edilemez — servisin bir deneme selinde her istek için bir PBKDF2 hesabı yaparak
boğulmasını engellemek. Gerçek trafiğin sınıra yaklaşma ihtimali yok: ofisteki
ajan 8 saatte bir token alıyor.

## 109. Ajanın kimliği publish klasöründe değil, `%AppData%` altında ve şifreli

Ajan anahtarı `%AppData%\PkfRobot\agent.dat` içinde, **Windows DPAPI**
(`CurrentUser` kapsamı) ile şifreli duruyor. İki ayrı karar var, ikisinin de
gerekçesi ayrı:

**Neden publish klasörü değil.** Publish klasörü her güncellemede üzerine
yazılıyor. Anahtar orada dursaydı her yeni sürümde kaybolur, ofiste yeniden
girilmesi gerekirdi — `appsettings.json` disiplininin (bkz. OKUBENI) aynı
gerekçesi. `makine.dat` de aynı yerde: makine kimliği de güncellemede
kaybolmamalı.

**Neden DPAPI.** O makine fiziksel olarak erişilebilir bir yerde duruyor.
`CurrentUser` kapsamı, dosyayı başka bir makineye ya da başka bir Windows
kullanıcısına kopyalayanın okuyamaması demek — kopyalanan dosya işe yaramaz.
Çözülemeyen dosya hata vermiyor, `null` dönüyor: ajan anahtarı yeniden sorup
devam ediyor, bozuk bir dosya yüzünden takılıp kalmıyor.

**`MakineId` = makine adı + kalıcı GUID.** Her açılışta yeni bir kimlik
üretilseydi sunucudaki listede aynı makineden hayalet kayıtlar birikirdi: eski
kayıt ancak kalp atışı zaman aşımıyla (90 sn) düşüyor, yani her yeniden başlatma
bir buçuk dakikalık bir çift görüntü bırakırdı. Makine adı tek başına da
yetmiyor — iki ofiste aynı ada sahip iki PC olabilir.

## 110. `WithAutomaticReconnect` kullanılmadı; yeniden bağlanma elde yazıldı

SignalR'ın kendi yeniden bağlanması elindeki token'ı **aynen tekrar kullanıyor**.
Ajan token'ı 8 saat yaşıyor; gece kopan bir bağlantı, token bayatladıktan sonra
sabaha kadar susmadan başarısız denemeler yapardı ve kimse fark etmezdi.

Buradaki döngü sırayı tersine çeviriyor: önce token tazeliği (gerekiyorsa
yenileme), sonra bağlantı. Aralıklar 5s → 10s → 30s → 60s, sonra 60s sabit ve
sonsuz deneme. Tavan var çünkü gece ağ koparsa sabah bağlı olması gerekiyor;
ilk adımlar kısa çünkü kopuşların çoğu saniyelik. Başarılı bağlantıdan sonra
aralık sıfırlanıyor — bir gün önceki kopuş, bugünkü kopuşta bir dakika
beklemeyi gerektirmez.

**Kopuş kalp atışında fark ediliyor**, ayrı bir dinleyiciyle değil: kapalı bir
bağlantıda `KalpAtisi` çağrısı zaten patlıyor ve döngü yeniden bağlanmaya
düşüyor. Bedeli, kopuşun en geç bir kalp atışı aralığı (30 sn) kadar geç fark
edilmesi. Sunucu tarafı kopuşu anında görüyor (soket kapanınca ajan listeden
düşüyor), yani bu gecikme yalnız yeniden bağlanmayı geciktiriyor.

**401 kalıcı, 429 geçici.** Anahtar iptal edilmişse yeniden denemenin anlamı
yok: döngü duruyor ve "Yönetim > Ajanlar ekranından yeni anahtar üretin"
diyor. Hız sınırında ise sunucunun söylediği `Retry-After` kadar beklenip bir
kez daha deneniyor; başlık yoksa 60 saniyeye düşülüyor — sınıra takılmışken
hemen tekrar denemek aynı duvara çarpmak olurdu.

## 111. ORKA durumu ayrı bir çağrıyla değil, `Kaydol`'un tekrarıyla bildiriliyor

Sunucuda "durum bildir" diye bir hub metodu yok; ORKA alanı kayıt paketinin
kendisinde. Yeni bir metot eklemek yerine, durum değiştiğinde aynı bağlantıdan
ikinci kez `Kaydol` çağrılıyor. Sunucu tarafı bunu zaten **bilgi tazeleme**
sayıyor, düşürme değil (`AjanDeposu.Kaydet`, aynı `ConnectionId` için eski
kaydı düşürülmüş saymıyor) — yani sözleşmeye dokunmadan çalışıyor.

Bildirim **yalnız değişimde**: her kalp atışında kayıt göndermek, 30 saniyede bir
gereksiz bir yazma demek olurdu. Değişimin kendisi süreç listesinden okunuyor
(`OrkaWinIceberg.64`), pencere başlığından değil — başlık ORKA'nın sürümüne göre
değişiyor (bkz. OKUBENI), süreç adı değişmiyor.

ORKA'nın kapalı olması bağlantı için engel değil: ajan ORKA kapalıyken de bağlı
kalıyor, yalnızca durumu bildiriyor. Bu turda ajanın yaptığı iş zaten ORKA'ya
dokunmuyor.

## 112. Ajan log'u `AdimLogger` değil; maskeleme desenle yapılıyor

`AdimLogger` bir görev çalıştırması için klasör açıp ekran görüntüsü
biriktiriyor — ömrü dakikalarla ölçülen bir iş için doğru. Ajan **günlerce**
ayakta duruyor: her açılışta yeni klasör açmak ve tek dosyayı sınırsız
büyütmek aynı şey değil. Bu yüzden `%AppData%\PkfRobot\logs\ajan-<tarih>.log`,
günlük dosya, 14 günden eskiler siliniyor. Yeni kütüphane eklenmedi; biçim
(`saat [seviye] mesaj`) `AdimLogger` ile aynı.

**Maskeleme iki katmanlı.** Görev adımlarında alan **adına** bakılıyor:
`Hassas.Sozcukler` listesine `sifre` yanına `anahtar`, `token`, `agent`
eklendi — yeni bir sır alanı eklerken yapılacak tek şey ona doğru adı vermek.
Ajan log'unda ise **değerin kendisine** bakılıyor: `pkfr_…` ve üç parçalı JWT
desenleri yazılmadan önce eleniyor. İkincisi bir ağ, birincinin unutulduğu
durumu yakalamak için: anahtar zaten hiçbir yere yazılmıyor, ama ileride biri
hata mesajına koyarsa diske düz metin düşmesin.

Bu iddia denendi, varsayılmadı: yerel çalıştırmadan sonra hem
`%AppData%\PkfRobot\logs\ajan-2026-08-30.log` hem konsol çıktısı gerçek anahtara
karşı tarandı — `pkfr_` hiç geçmiyor. `agent.dat` de düz metin değil.

## 113. Test projesi Robot.Agent klasörünün *içinde* değil

`PkfRobot.UnitTests` önce `src/Robot.Agent/PkfRobot.UnitTests/` altına kondu ve
derleme kırıldı: `PkfRobot.csproj` varsayılan `**/*.cs` taramasıyla test
dosyalarını da kendi derlemesine alıyor, xunit referansı olmadığı için de
patlıyor. Proje `src/Robot.Agent.UnitTests/` olarak dışarı taşındı —
`Compile Remove` ile istisna yazmaktansa iç içe geçmeyi kaldırmak daha az
sürprizli.

Test projesi **çözüme eklendi**, `PkfRobot`'un kendisi hâlâ eklenmedi;
referans üzerinden derleniyor. Bunun bir sonucu var: `dotnet build`/`dotnet test`
artık `SmartExpenseSystem.sln` üzerinden `PkfRobot`'u da derliyor ve o proje
`net8.0-windows`/`win-x64` — **çözüm Linux'ta derlenmez oldu**. Kabul edildi:
CI zaten çözümü değil docker-compose'u derliyor, geliştirme de Windows'ta
yapılıyor.

`SelfContained` exe'ye proje referansı vermenin sorun çıkarabileceği
düşünülmüştü; denendi, çıkarmadı — ayrı bir kütüphane projesine bölmek
gerekmedi.

**Neyin test edilebildiği:** bağlantı mantığı `IHubBaglantisi` arkasında, token
alma gerçek `HttpMessageHandler` sahtesiyle (401/429/`Retry-After` gerçek HTTP
anlamlarıyla), beklemeler `Func<TimeSpan, CancellationToken, Task>` ile — testler
gerçekten beklemiyor, ne kadar bekleneceğini doğruluyor. UI otomasyonu ve
gerçek soket test edilmiyor; onların yeri ofisteki çalıştırma.

## 114. İşler veritabanında, ajan listesi bellekte

Bağlı ajanlar listesi bir bağlantının ömrü kadar yaşıyor ve kaybolması zararsız
(§102). **İşler öyle değil:** "bu ekstre ORKA'ya aktarıldı mı" sorusunun yanıtı
sunucu yeniden başlayınca da durmalı, geçmiş görülebilmeli. `catalog.AjanIsleri`
bu yüzden kalıcı ve kapsam kolonu `FirmaId` — banka otomasyonundaki diğer
tablolarla aynı, `SaveChangesAsync` kapsamsız kaydı ayrıca reddediyor (§68).

**Kimlik `Guid`, artan sayı değil.** Kimliği sunucu üretiyor ve ajan geri
bildiriyor; artan bir sayı olsaydı ajan komşu işlerin kimliğini tahmin
edebilirdi. Sahiplik kontrolü zaten var, ama tahmin edilemez kimlik bedava bir
katman.

**Zaman aşımı okuma anında işaretleniyor**, `BackgroundService` ile değil —
§102'deki yaklaşımın aynısı. Takılmış bir işin varlığı ancak birinin ona
bakmasıyla ya da aynı ajana yeni iş açılmasıyla önem kazanıyor; ikisi de aynı
süzgeçten geçiyor. Eşik `BaslamaZamani`'na değil `SonIlerlemeZamani`'na bakıyor:
uzun ama düzenli ilerleyen bir işi zaman aşımına uğratmak yanlış olurdu.

## 115. Aynı ajana tek iş — kural iki tarafta da var

Robot tek ORKA penceresiyle çalışıyor; paralel iş anlamsız. Sunucu ikinci isteği
**409** ile reddediyor ve çalışan işi yanıtta bildiriyor, böylece ekran "hangi
iş" diye ikinci bir sorgu atmıyor.

Ajan tarafında aynı kural **bir kez daha** var. İkisi de olmasaydı sunucunun
yanıldığı (ya da iki sunucu kopyasının aynı anda gönderdiği) durumda robot iki
işi birden ORKA'ya yazmaya kalkardı. Ajandaki kontrol sunucununkini yedekliyor,
onun yerine geçmiyor.

**Bekleyen iş bağlanınca gönderiliyor.** Ajan yokken açılan iş `Bekliyor`
kalıyor; `Kaydol` kabul edildikten **sonra** sıradan alınıyor — sürümü tutmayan
ajana iş vermenin anlamı yok.

Kuyruk üç noktada ilerliyor: bağlanmada, iş bitince ve iptalde. İlk yazımda
yalnız bağlanmada ilerliyordu ve sırada bekleyen ikinci iş, ajan bağlı ve boşta
dururken bir sonraki bağlanmaya kadar öylece kalıyordu. Her seferinde yalnız en
eski bekleyen gönderiliyor — ajan zaten tek iş yürütüyor.

## 116. Hedef ajan sorulmuyor, sunucu buluyor

Aktar ekranı "hangi ajana göndereyim" diye sormuyor: ofiste tek ayrılmış banka
bilgisayarı var ve bu soru kullanıcı için gürültü olurdu. `AjanId` boş
gelirse sunucu tek adayı kendisi buluyor — önce bağlı ajanlar, yoksa geçmiş
işlerdeki ajanlar.

Birden çok aday varsa istek **reddediliyor** ve alan zorunlu hâle geliyor:
yanlış makineye iş göndermek sessiz bir hata olurdu. Hiç aday yoksa mesaj ne
yapılacağını söylüyor ("ofisteki makinede PkfRobot'u --ajan ile başlatın").

## 117. Tarayıcı işi yoklayarak izliyor, SignalR ile değil

Durum kartı 2 saniyede bir `GET /catalog/agent/is/{id}` çağırıyor. Tarayıcıya
SignalR eklemek ikinci bir hub, Blazor WASM istemcisi ve ek nginx yapılandırması
demek; izlenen şey tek bir işin ilerlemesi ve yoklama bitince duruyor —
tamamlanmış bir işi saniyede bir sormanın karşılığı yok. Kazanç bu aşamada
maliyeti karşılamıyor, sonra eklenebilir.

## 118. CatalogService'in varsayılan politikası artık "insan"

A adımında yalnız hub ve durum ucu işaretlenmişti; geri kalan her `[Authorize]`
"kimliği doğrulanmış olsun" diyordu ve **ajan token'ı hepsinden geçiyordu**.
Ofisteki makinede duran bir anahtar, servisin bütün uçlarını açık tutuyordu.

`AuthorizationOptions.DefaultPolicy` artık "kimliği doğrulanmış **ve** ajan
değil". Ajanın girebileceği yerler tek tek `YalnizAjan` ile işaretleniyor: hub,
ve işin iki dosya ucu. Kapı varsayılan olarak kapalı, açılan yerler görünür.

Yerelde doğrulandı: ajan token'ıyla `/api/catalog/banka-ekstre/ekstre` → **403**,
`/api/catalog/agent/isler` → **403**; aynı uçlar kullanıcı token'ıyla 200.

## 119. Ajan dosyaları işine bağlı uçlardan indiriyor

D adımının görev metni "şu iki Banka Otomasyon ucunu ajana da aç" diyordu. Öyle
yapılmadı: o uçlarda dosya `?firmaId=` ile isteniyor ve ajan **her firmanın her
ekstresini** alabilirdi.

Yerine iki yeni uç: `GET /catalog/agent/is/{isId}/ekstre` ve `/kod-listesi`.
İkisi de ajanın **o an atanmış** işine bağlı — iş kimliği token'daki ajana ait
değilse 404, iş bitmişse 409. Firma kapsamı isteğin parametresinden değil işin
kendisinden kuruluyor. Ofisteki makinede duran anahtarın erişebildiği alan, o
anda yapmakta olduğu işten ibaret.

Yerelde doğrulandı: başka bir işin kimliğiyle 404, kullanıcı token'ıyla 403.

## 120. `OrkayaAktar` yükünü sunucu kuruyor

Firma kodu, banka hesabının ORKA kodu ve satır sayısı tarayıcıdan gelseydi robot,
doğruluğunu kimsenin denetlemediği değerlerle ORKA'ya yazardı. İstemci yalnız
`EkstreYuklemeId` gönderiyor; gerisini `OrkaAktarimYuku` veritabanından
dolduruyor.

Eksik bir şey varsa iş **hiç oluşmuyor** ve mesaj ne yapılacağını söylüyor
(firmanın ORKA kodu yok → "Yönetim > Firmalarım"). Ajanı yola çıkarıp orada
durdurmaktansa burada durmak.

Satır sayısı `EkstreYukleme.SatirSayisi`'ndan değil **dışa aktarımın kendi
sonucundan** geliyor: "diğer bankada" işaretli satırlar ORKA'ya gitmiyor ve iki
dosyada da yoklar. Yanlış sayıyı ajana vermek, ajanın doğrulamasını da yanlış
yapardı.

**`Firma.OrkaFirmaKodu` eklendi.** ORKA giriş zincirinde firmanın açıldığı kod
(ör. "0001") hiçbir yerde tutulmuyordu. Alan nullable: ORKA'ya aktarım
yapılmayan firmalarda gerekmiyor, gerektiğinde iş anlaşılır bir mesajla
reddediliyor.

## 121. Grid körlemesine dolduruluyor; güvence yazmadan önce alınıyor

ORKA'nın gridi (`TcxGridSite`) UI Automation'a kapalı tek bir blok — satır/hücre
okunamıyor (bkz. OKUBENI). Robot yazdığı değerin doğru satıra gittiğini
**ekrandan göremiyor**. Bu yüzden bütün güvence yazmadan önce alınıyor ve
doğrulamalardan biri tutmazsa iş **hiç başlamıyor**:

1. İki dosya da indi mi, ekstre boyutu makul mü
2. Kod listesi satır sayısı = iş paketindeki satır sayısı
3. Düzeltilmiş ekstrenin **veri satırı** sayısı (başlık hariç) = aynı sayı
4. Her satırda açıklama ve karşı hesap kodu dolu mu

Üçüncüsü en tehlikeli durumu yakalıyor: kodlar bir satır kayarsa her kayıt yanlış
hesaba gider ve bunu kimse fark etmez.

Satır sayısını okumak için **ClosedXML** eklendi. Dosyayı sunucu da ClosedXML ile
yazıyor; aynı kütüphaneyle okumak "kaç satır var" sorusuna iki tarafta aynı
yanıtı veriyor. Zip'i elle açıp `<row>` saymak bağımlılık eklemezdi ama paylaşılan
metin tablosu, birden çok sayfa ve başlık satırı derdi getirirdi.

**Kaydet'e basılmıyor.** `GridDoldur` yalnızca hücrelere yazıyor; kaydetme
kullanıcının işi. Kural bir testle sabitlendi: `gorevler/orkaya-aktar.json`
içinde `OnayGerekir` adımı yok ve tuşa basan hiçbir adımda "Kaydet"/"ALT+K"
geçmiyor.

## 122. Tek yeni adım tipi: `GridDoldur`

Görev metni "yeni adım tipi yazma" diyordu ama adı geçen `GridDoldur` adımı
motorda yoktu. İkisi birden mümkün olmadığı için ortası seçildi: akışın tamamı
mevcut adım tipleriyle JSON'da (`gorevler/orkaya-aktar.json`), yalnız görev
metninin kendi adlandırdığı `GridDoldur` eklendi.

Adım **veriyi JSON'dan almıyor**: satırlar sunucudan indirilen kod listesinden
geliyor ve motora `GridDoldurVerisi` olarak veriliyor. Böylece iş akışı JSON'da
kalırken veri koda da JSON'a da gömülmüyor.

**İlerleme kilometre taşları da JSON'da:** adımlara `Yuzde` alanı eklendi. Akışın
hangi noktasının "%25" olduğu akışın kendi bilgisi; akış değişince yüzdeler aynı
dosyada değişiyor. Kod yalnız "adım `Yuzde` taşıyorsa bildir" diyor.

## 123. Hata ekranı FileApiService'e, mevcut kalıpla

Hata anındaki ekran görüntüsü ajandan **doğrudan FileApiService'e**
(`POST /file/v1/uploads`) yükleniyor, dönen kimlik `IsBitti` ile sunucuya
geçiyor ve `AjanIsleri.HataEkraniDosyaId`'ye yazılıyor. Repodaki kalıp bu:
CatalogService dosyaları tutmuyor, istemci yükleyip kimliği veriyor (bkz.
Beyanname ekleri).

Not: FileApiService'in ajan/insan ayrımı yok, ajan token'ını kabul ediyor.
CatalogService'teki varsayılan politika (§118) oraya uygulanmadı — ayrı servis
ve bu turda kapsam dışı. Yüklenen tek şey bir hata ekranı görüntüsü.

Yükleme başarısız olursa iş sonucu **yine de** bildiriliyor: ekran görüntüsü bir
yardımcı, işin sonucunu bildirmeyi engellememeli.

## 124. Ajanın iş dosyaları `%AppData%` altında, başarısızlar 7 gün duruyor

İndirilen iki dosya `%AppData%\PkfRobot\isler\{isId}\` altına yazılıyor. Başarılı
işte klasör siliniyor; **başarısız işte duruyor** — ofiste "ne indirildi, ne
yazıldı" sorusunun yanıtı orada. 7 günden eskiler bir sonraki iş başlarken
temizleniyor: inceleme penceresi var ama disk sonsuza kadar dolmuyor.

## 125. Ajan kapanırken işi bir kez bildiriyor

Ctrl+C ile kapanan ajan, çalışan işi için `IsBitti(basarili: false)` gönderiyor —
yoksa iş, sunucunun zaman aşımına uğratmasına kadar (15 dk) "çalışıyor"
görünürdü.

İlk sürümde bu bildirim **iki kez** gidiyordu: çalıştırıcı kendi iptal
mesajını, kapanış yordamı da kendi mesajını gönderiyordu. Sunucu tarafında
zararsızdı (ilk biten hâl kalıyor) ama log ve geçmiş bulanıyordu, üstelik
kullanıcı "iptal edildi" yazısını görüyordu — oysa iptal eden yoktu. Şimdi
kapanış yordamı önce işin kendi bildirimini bekliyor (3 sn) ve yalnız gelmezse
kendi sözünü söylüyor. Durdurma sebebi de ayrı tutuluyor: "iptal edildi" ile
"ajan kapatıldı" farklı şeyler.
