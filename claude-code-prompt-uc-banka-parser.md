# Görev: Üç Banka Parser'ı — İş Bankası, Akbank, Ziraat

Bu görevi baştan sona, soru sormadan tamamla. Önce mevcut `VakifbankVadesizParser`
ve `IEkstreParser` arayüzünü oku, aynı kalıbı izle.

**Eşleştirme mantığına dokunma** — katman sırası, eşikler, benzersiz önek
algoritması aynen kalsın. Bu görev üç yeni parser ve onların desen/şablon/kural
satırları. Mevcut testlerin tamamı aynen geçmeye devam etmeli.

Aşağıdaki tüm yapı bilgisi ve sayılar **gerçek 7 aylık ekstrelerden** (01.01–
28.08.2026, PKF Aday) ölçüldü. Tahmin yok.

---

## Ortak notlar

- Üç parser da `IEkstreParser` uygular, `ParserTipi` sabitleri:
  `ISBANKASI_VADESIZ`, `AKBANK_VADESIZ`, `ZIRAAT_VADESIZ`
- Başlık satırı **isimle** bulunsun, bulunamazsa aşağıdaki sabit indekslere
  düşülsün ve `Uyarilar`'a yazılsın (Vakıfbank parser'ındaki kalıp)
- Türkçe `İ`/`ı` karşılaştırma tuzağına dikkat — Vakıfbank'ta başlık bu yüzden
  bulunamamıştı. `Normalizasyon.MetinNormalize`'dan geçir.
- `AyrilanSatir.KaynakSatirNo` ve `EkstreParseSonuc.AciklamaKolonu` doldurulmalı
- Her parser için DI kaydı: `builder.Services.AddSingleton<IEkstreParser, XParser>()`

---

## 1. İŞ BANKASI — `.xls` (eski OLE formatı)

**Önemli:** dosya `.xlsx` değil, eski `.xls`. ClosedXML bu formatı okuyamaz.
NPOI (`HSSFWorkbook`) veya eşdeğer bir kütüphane gerekiyor. Parser dosya
uzantısına/imzasına bakıp doğru okuyucuyu seçsin; yanlış formatta anlaşılır hata
versin.

**Yapı:** başlık **16. satırda**, veri 17'den. 418 veri satırı.

| # | Başlık | İçerik |
|---|---|---|
| 0 | `Tarih/Saat` | `26/08/2026-14:58:47` — tarih kısmını al |
| 1 | `Valör` | `26/08/2026` |
| 2 | `Kanal/Şube` | Sistem / İşCep / Şube |
| 3 | `İşlem Tutarı*` | **işaretli** (eksi = çıkan) |
| 4 | `Bakiye` | |
| 6 | `İşlem` | kısa kod: `E9`, `EF`, `CL`, `FA`… |
| 7 | `İşlem Tipi` | **şablon eşleşmesi bu kolondan** |
| 8 | `Açıklama` | |
| 14 | `Referans` | benzersiz — mükerrer kontrolü için sakla |

Tarih formatı `gg/aa/yyyy`, ayraç **eğik çizgi** (Vakıfbank'ta noktaydı).

**İşlem tipleri** (7 ayda): `EFT` 242, `Kredi` 37, `Para Transferi` 27, `Ücret` 26,
`Yatırım` 24, `FAST` 19, `Havale` 17, `Vadeli - Faiz Verme` 13, `Para Çekme` 9,
`Kredi Kartı` 3, `Çek` 1.

### Unvan çıkarma — `*` ayraçlı

Karşı tarafın adı `*` ile ayrılmış alanlardan **ilkinde**:

```
RENKLER MAKİNA VE YEDEK PARÇA SANAYİ VE TİCARET A.*0111**8792586
GY VARLIK KİRALAMA ANONİM ŞİRKETİ*0153*FATURA BEDELİ GÖNDEREN: ...
MUHAMMED MUHSİN*0111*Muhsin Group carisine istinaden*1629974954*FAST
SAURER TEKSTİL A.Ş*0062*SAURER DOMİCİLATION SERVICE 052026*3667000239*FAST
```

**İstisna — `Havale` tipinde unvan SONDA**, son `*`'dan sonra:

```
2. FATURA BEDELİ ÖDEMESİ*OPAT OTOMOTİV İNŞAAT ELEKTRONİK TURİZM GIDA PAZARLAMA...
6481373184-OPH Fon Bağımsız Denetim Hizmeti ... *OSMANLI PORTFÖY...
```

İki desen tanımla: `^([^*]{4,})\*` (baş) ve `\*([^*]{4,})$` (son). Sıra: baş
deseni önce, `Havale` tipinde son deseni önce.

### Şablon ve kural eşleşmeleri (ölçülen dağılım)

| İşlem Tipi | Satır | Ana gruplar | Şablon / kural |
|---|---|---|---|
| EFT | 148 | 120:69, 102:51, 770:27 | `Gelen Eft - {UNVAN}` / `Giden Eft - {UNVAN}` |
| Kredi | 33 | 300:13, 780:13, 770:7 | `Kredi No: {NO}` |
| Yatırım | 22 | 102:12, 118:7, 770:3 | `Yatırım Hesabına/Hesabından Aktarma` |
| Para Transferi | 18 | 102:16, 309:2 | `Hesaplararası Virman` |
| FAST | 17 | 120:7, 770:4, 102:4, 136:2 | `Gelen Eft - {UNVAN}` |
| Ücret | 16 | 770:11, 780:3, 102:2 | banka gideri |
| Para Çekme | 9 | 309:9 | `Kredi Kartı Ödemesi` |
| Vadeli - Faiz Verme | 8 | 102:8 | `Hesaplararası Virman` |
| Havale | 7 | 120:6, 329:1 | `Gelen Eft - {UNVAN}` |

### Sabit kurallar (yeni)

```
"EFT Ücret"                        → 770 03 005  (Banka Komisyonu)
"KREDİ NO:" + "ANAPARA TAHSİLAT"   → 300         (kredi anapara)
"/ERKN.ODEM" veya "BSMV"           → 780         (finansman gideri)
"MKK SAKLAMA KOMİSYONU"            → 770 03 005
"YATIRIM HESABI SAKLAMA ÜCR."      → 770 03 005
"YATIRIM HESABINA/HESABINDAN AKTARMA" → 102 1 5 04
"KMH" + "KULLANDIRIM ÜCRETİ"       → 770 03 005
"KMH BORCUNA KARŞILIK"             → 102
"CH/KKH VİRMAN"                    → 309
"KRE.KART BORÇ ÖDEME"              → 309
"NET FAİZ BED.OTO.VİRMAN"          → 102 1 5 07  (vadeli hesap)
```

Kredi satırlarında öğrenme anahtarı **kredi numarasını** içersin
(`KREDİ NO: 10080844268` → `KREDI:10080844268`) — mevcut `Normalizasyon.KrediAnahtar`
kalıbını genişlet.

**Bankalar arası tespiti:** açıklama hesap sahibinin adıyla başlıyor ve içinde
IBAN + banka adı var →
`PKF ADAY BAĞIMSIZ DENETİM A.Ş.*TR400001500158007298490100*VAKIFBANK*0082558`.
IBAN bitişik yazılmış; banka kayıt defteri IBAN katmanı bunu çözmeli.

---

## 2. AKBANK — `.xlsx`

**Önemli:** openpyxl bu dosyayı okuyamıyor (tek hücre görüyor). ClosedXML da
patlayabilir. Parser gerçek dosyayla test edilmeli; ClosedXML başarısız olursa
ham XML okuma (SharedStrings + sheet XML) yedeği gerekir.

**Yapı:** başlık **10. satırda**, veri 11'den. 186 veri satırı.

| # | Başlık | İçerik |
|---|---|---|
| 0 | `Tarih` | `27.08.2026` |
| 1 | `Saat` | `10:21` |
| 2 | `Tutar` | **işaretli** |
| 3 | `Bakiye` | |
| 4 | `Borç/Alacak` | `B` / `A` — yön bu kolondan, tutarın işaretiyle çapraz doğrula |
| 5 | `Açıklama` | |
| 6 | `Fiş/Dekont No` | |

**İşlem tipi kolonu YOK** — şablon eşleşmesi açıklamadan yapılacak.

### Açıklama kalıpları

Akbank açıklamaları kısa ve kodlu. Ölçülen dağılım: **82/167 satır `102` grubu**
(bankalar arası) — en büyük kalem.

```
7777/MBL-6973644-Pkf Aday Bağımsız Denetim Anonim Şirketi-HESAPLAR ARASI EFT - TEB
7777/MBL-KISMİ ÖDEME                                    → 102
7777/MBL-VİRMAN-VADELİ HESABA TRANSFER 0698-0268799     → 102 1 7 06
7777/MBL-HESAP AÇILIŞI                                  → 102
7777/MBL-Kredi Kartı Ödemesi                            → 309
EFT: PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ HESAPLAR ARASI EFT - Akbank  → 102
8425183-DISC Akademi Eğitim Ve Yazılım A.Ş.-100 kredi + ...  → 329 (giden)
DBS ODM/5089567/0801422766                              → 329 (tedarikçi, DBS)
FATURA ÖDEME/VF000011536945                             → 329
Artı Para Faizi                                         → 770 / 780
```

**Unvan çıkarma:** `-` ile ayrılmış alanlarda, genelde **ikinci** alan
(`{no}-{UNVAN}-{açıklama}`). Desen: `^\d+-([^-]{4,})-`. Ayrıca
`EFT: {UNVAN} ...` kalıbı.

`7777/MBL-` ve `DBS ODM/` önekleri gürültü, unvan çıkarmadan önce atılsın.

**Dikkat:** `DBS ODM` satırları bankalar arası DEĞİL, tedarikçi ödemesi. Vakıfbank
tarafında da aynı hata çıkmıştı (Borusan DBS). `DBS` geçen satırlarda banka kayıt
defteri katmanı tetiklenmesin.

### Ana grup dağılımı

`102`:82, `329`:43, `780`:10, `770`:9, `101`:5, `361`:4, `309`:4, `331`:4,
`136`:3, `120`:2

---

## 3. ZİRAAT — `.xlsx` (bozuk stil tablosu)

**Kritik:** dosyanın `styles.xml`'i bozuk. openpyxl `expected <class
'openpyxl.styles.fills.Fill'>` hatasıyla açamıyor. **ClosedXML'in de patlaması
çok muhtemel.**

Parser bunu öngörsün: normal okuma başarısız olursa **ham XML okumaya** düşsün —
zip'ten `xl/sharedStrings.xml` ve `xl/worksheets/sheet*.xml` okunup hücreler
elle ayrıştırılsın. Bu yedek yol gerçek dosyayla test edilmeli; ham XML ile
356 veri satırı okunabiliyor.

Hücre referansı (`r` niteliği) bazı hücrelerde eksik olabilir — o durumda sıradaki
kolon indeksi kullanılsın.

**Yapı:** başlık **12. satırda**, veri 13'ten. 356 veri satırı.

| # | Başlık | İçerik |
|---|---|---|
| 0 | `Tarih` | `26.08.2026` |
| 1 | `Fiş No` | `F08179` |
| 2 | `Açıklama` | |
| 3 | `İşlem Tutarı` | **işaretli** |
| 4 | `Bakiye` | |

**İşlem tipi kolonu YOK** — üç bankadan en az bilgi veren format.

### Açıklama kalıpları

**173/300 satır `102` grubu** — çoğunluk bankalar arası.

```
62286065-5010                                        → 102 1 4 05 (vadeli kasa)
62286065-5022                                        → 102 1 4 04 (günlük kazanan)
707-62286065-5003 No.lu Vds. Hes.tan 62286065-5022 -Hes.Açılış...   → 102
707-62286065-5003 No.lu Vds. Hes. Aktararak 62286065-5020 No.lu Vadeli Hes.tan Para Çekme → 102
Gönd: PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ HESAPLAR ARASI E.F.T. Ziraat Bankası TL   → 102
Gönd: PKF ADAY BAĞIMSIZ DENETİM ZİRAAT BANKASI 0064-T.İŞ BANKASI A.S.  → 102 1 5 01
Enpara Bank A.Ş./TR380015700000000105549208-BURAK GÜNEL/2026000000008 NL. FT. ÖDEMESİ  → 329
31.12.2026 Sorumlu Tedarik Zinciri Güvence Denetimi 1. Taksit HANEDAN DÖVİZ VE ALTIN TİC → 120
0707GKDS26000982 Ref,USD 2000TL.92919,40 KMV Matrahı İnternet Döviz Satış İşlemi → 770 03 005
Aday/Gentech - ch eft GE...                          → cari
```

**Unvan çıkarma desenleri:**

1. `^Gönd:\s*(.+?)\s+HESAPLAR ARASI` — bankalar arası, unvan hesap sahibi
2. `/TR\d{24}-([^/]{4,})/` — IBAN'dan sonra, eğik çizgiler arasında (`BURAK GÜNEL`)
3. `^([A-ZÇĞİÖŞÜ][^/]{4,}?)\s*/\s*TR\d` — IBAN öncesi banka adı (bu **banka**,
   unvan değil — elenmeli)
4. Sondaki büyük harfli unvan: `([A-ZÇĞİÖŞÜ][A-ZÇĞİÖŞÜ\s\.]{8,})$`

**Hesap numarası kalıbı önemli:** `62286065-5010`, `62286065-5022`,
`62286065-5020` — bunlar Ziraat'in kendi alt hesapları. Bu numaraları banka
kayıt defterindeki eşleştirme anahtarlarına ekle:
`102 1 4 05` → `5010`, `102 1 4 04` → `5022`.

### Ana grup dağılımı

`102`:173, `329`:29, `780`:19, `770`:18, `120`:14, `136`:13, `361`:8, `300`:6,
`309`:5, `118`:5

---

## Yeni kategoriler ve kurallar

Vakıfbank'ta olmayan, bu üç bankada geçen gruplar:

- **`780` Finansman gideri** (74 satır toplam) — kredi faizi, BSMV, KMH faizi
- **`361` SGK** (12 satır) — SGK ödemeleri
- **`309` Kredi kartı** (30 satır)
- **`300` Kredi** (26 satır)
- **`118` Menkul kıymet** (12 satır)
- **`101` Alınan çekler** (5 satır)

Bunlar için sabit kural ve kategori kaydı oluştur. Kategori kapsama listesinde
her banka için doğru görünsünler.

---

## Testler

Her parser için, **gerçek dosyanın kendi metinleriyle**:

1. Başlık satırı isimle bulunuyor, uyarı çıkmıyor
2. Doğru satır sayısı ayrışıyor (İş Bankası 418, Akbank 186, Ziraat 356)
3. Tarih ve yön doğru okunuyor (Akbank'ta `B/A` kolonu ile tutarın işareti
   çapraz doğrulanıyor)
4. Unvan çıkarma: her bankadan en az üç gerçek satır doğru unvanı veriyor
5. İş Bankası `Havale` tipinde unvan sondan alınıyor
6. Akbank `DBS ODM` satırı banka kayıt defterine düşmüyor, cari katmanına gidiyor
7. Ziraat bozuk `styles.xml` yedek yoluyla okunuyor
8. İş Bankası `.xls` formatı okunuyor

## Kabul kriterleri

1. Derleme temiz, tüm mevcut testler aynen geçiyor
2. Migration gerekiyorsa üretildi ve uygulandı
3. Üç parser DI'a kayıtlı, `EkstreParserSecici` otomatik topluyor
4. Seed'e üç bankanın desen / şablon / sabit kural satırları eklendi
   (`Ayristirici` alanı ilgili banka)
5. Yeni kategoriler (`780`, `361`, `309`, `300`, `118`, `101`) tanımlı
6. Yukarıdaki sekiz test yazıldı ve geçiyor

Sonunda `OZET.md` ve `KARARLAR.md` güncelle. Her banka için gerçek dosyayla
çalıştırıp kaç satırın ayrıştığını ve kaçının şablon/kural eşleşmesi bulduğunu
yaz.
