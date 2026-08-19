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
