# Görev: Banka Ekstre Modülü — Gerçek Veri Düzeltmeleri (Tur 2, birleşik)

Bu görevi baştan sona, soru sormadan tamamla. Önce ilgili kodu oku, sonra değiştir.
Belirsizlik çıkarsa mevcut kalıbı izle, kararını `KARARLAR.md`'ye yaz ve devam et.

## Bağlam

Vakıfbank ekstresi (287 satır) gerçek hesap planıyla (6.128 hesap) çalıştırıldı:
128 otomatik, 123 onay bekliyor, 36 çözülemedi. Aşağıdaki maddelerin hepsi bu
çalıştırmadan ve gerçek veri üzerinde yapılan ölçümlerden çıktı. Verilen sayılar
tahmin değil, gerçek dosyadan sayıldı — eşikleri ve kuralları bu ölçümlere
dayanarak koy, kendi tahminini üretme.

---

## 1. Cari eşleştirme: "benzersiz önek" yöntemi

Mevcut yaklaşım tek yönlü: açıklamadan desenle unvan çıkar, sonra hesap planında
benzerlik ara. Buna **ters yönde** çalışan yeni bir katman ekle.

Yöntem, kullanıcının ORKA'da elle yaptığı şeyi taklit ediyor: arama kutusuna kısa
bir önek yazar (`baycan elek`), **tek sonuç** çıkarsa onu seçer, birden fazla
çıkarsa bakıp karar verir.

### Algoritma

1. Açıklamayı normalize et ve token'lara ayır:
   - Türkçe sadeleştirme: `İ→I`, `Ş→S`, `Ğ→G`, `Ü→U`, `Ö→O`, `Ç→C`
   - Alfanümerik dışını boşluğa çevir, fazla boşlukları teke indir
   - **Unvan eki gürültüsü:** `ANONİM`, `ŞİRKETİ`, `ŞİRKET`, `ŞTİ`, `A.Ş.`,
     `LİMİTED`, `LTD`, `SANAYİ`, `SAN`, `TİCARET`, `TİC`, `VE`, `İTH`, `İHR`,
     `HİZMETLERİ`
   - **Bankacılık dolgu gürültüsü:** `NOLU`, `SORGU`, `NUMARALI`, `TARAFINDAN`,
     `TARAFINA`, `HESABINDAN`, `HESABINA`, `TARİHLİ`, `GELEN`, `GİDEN`, `EFT`,
     `FAST`, `HAVALE`, `TRANSFER`, `CARİ`, `HESAP`, `ÖDEME`, `ÖDEMESİ`,
     `FATURA`, `ŞUBESİ`, `MERKEZ`, `IBAN`, `NEZDİNDEKİ`, `VADESİZ`, `MAHSUBEN`
   - Sayıdan ibaret token'ları at, tek harfli token'ları at

2. n=4'ten n=2'ye inerek açıklamanın **ardışık token dizilerini** (n-gram) dolaş.

3. Her n-gram için, hesap adı çekirdeği **o n-gram ile BAŞLAYAN** cari hesapları
   bul.

4. **Tam bir tane** bulunuyorsa aday. **Birden fazla** bulunuyorsa satır onaya
   düşsün ve hepsi aday olarak listelensin — rastgele veya "ilki" seçme.

5. Uzun n-gram'dan gelen eşleşme önce gelsin.

6. Hiçbir n-gram sonuç vermezse yedek olarak bitişik alt metin araması yapılsın;
   orada da tek sonuç varsa kabul, çoklu ise onaya düşsün.

### "Başlıyor" kontrolü kritik, "içeriyor" değil

ORKA hesap adlarını **50 karakterde kesiyor**: 6.128 kaydın **914'ü** 48-50
karakter ve son kelime ortasından kopmuş. Örnek:

```
120 B62 | "Baycan Elektrik Müteahhitlik Sanayi Ve Ticaret Ano"
```

Açıklamada ise `...MÜTEAHHİTLİK SANAYİ VE TİCARET ANONİM` yazıyor. Bitişik alt
metin eşleşmesi bu yüzden tutmuyor. Önek eşleşmesi kesilmeden etkilenmiyor —
`BAYCAN ELEKTRIK` her iki tarafta da baştan başlıyor.

### İndeks kuralları

- **Sadece cari gruplarından** kurulsun: `120`, `329`, `136`, `159`, `195`,
  `196`, `320`, `331`, `336`. Gider hesapları girmesin — planda
  `622 0 03 00 PKF ADAY BAĞIMSIZ DENETİM`, `740 0 BAĞIMSIZ DENETİM` gibi
  firmanın kendi adını taşıyan kayıtlar var; indekse girerse her satır onlara
  eşleşir.
- **Adında `BANKASI` / `BANKA` / `BANK` / `FİNANS` / `KATILIM` geçen cariler
  çıkarılsın.** Açıklamalarda gönderen/alıcı banka adı geçiyor; `ZİRAAT BANKASI`
  metni `320 1 10011 ZİRAAT BANK` carisiyle eşleşip **16 satırı yanlış
  çözüyordu**. Bankalar zaten banka kayıt defteri katmanının işi.
- Hesap sahibinin tüm yazımları (madde 2) indekste olmasın.
- Çekirdeği 6 karakterden kısa hesaplar girmesin. (8 de olur, fark etmiyor;
  12 yapılırsa isabet çöküyor — kullanma.)
- İndeks firma bazlı ve **yükleme başına bir kez** kurulsun, satır başına değil.

### Ölçüm

Gerçek dosya, 87 cari satırı:

| Yöntem | Otomatik çözülen | Doğru | Yanlış | İsabet |
|---|---|---|---|---|
| Bitişik alt metin + en uzun | 69 | 60 | 9 | %87 |
| **Benzersiz önek (+ alt metin yedeği)** | **57** | **56** | **1** | **%98** |

Benzersiz önek daha az satır çözüyor ama neredeyse hiç yanlış yapmıyor. Kalan 22
satır çoklu aday olarak onaya düşüyor. **Bu değiş tokuş bilinçli** — muhasebede
yanlış kayıt, onaya düşen satırdan çok daha pahalıdır. Kapsamı artırmak için
bu yöntemi gevşetme.

### Katman sırası

geçmiş onay → banka kayıt defteri → sabit kural → **benzersiz önek** →
desen tabanlı unvan benzerliği

---

## 2. Yön kuralı sahte belirsizliği çözsün

Onaya düşen satırların büyük kısmı gerçek belirsizlik değil: **aynı carinin iki
grup altındaki kopyası.**

Gerçek dosyadan: `Zafer Genç`, `Burak Günel`, `Yurtiçi Kargo`, `Aras Kargo`,
`Ufuk Çolak` — hepsi `159` + `329` çifti, hesap adları **birebir aynı**. Şu an
hepsi gereksiz yere onaya düşüyor.

Kural: aday listesindeki hesapların **hesap adı çekirdeği aynı** ve sadece ana
grupları farklıysa, yön belirlesin:

- Para **çıkıyorsa** → `329` / `320`
- Para **giriyorsa** → `120` / `159`

Onaya düşürme.

**Gerçek belirsizlik, adların FARKLI olduğu durumdur** ve onaya düşmeye devam
etmeli:
- `Park Plaza Yönetimi, Aidat` / `Park Plaza Yönetimi, Elektrik` /
  `Park Plaza 19. Kat` (4 hesap)
- `Pardus Portföy` fonları (37 hesap)
- `Cms Jant` / `Cms Jant Makina`

---

## 3. Belirsizlik çözümü öğrenilsin

Kullanıcı çoklu adaydan birini seçtiğinde bu karar kaydedilsin ve **aynı
belirsizlik bir daha sorulmasın** — kullanıcı değiştirene kadar.

- **Anahtar:** belirsizliği üreten n-gram (`PARK PLAZA YONETIMI`,
  `PARDUS PORTFOY YONETIMI`) + firma
- **Değer:** seçilen hesap kodu
- **Güvenlik kaydı:** aday kümesinin özetini (kod listesinin hash'i) de sakla.
  Yeni bir cari açılıp aday kümesi değişirse eski karar sessizce uygulanmasın,
  satır tekrar onaya düşsün. Aksi halde yeni açılan bir Park Plaza hesabı hiç
  görünmez olur.
- Kayıtlar mevcut **Öğrenilen Eşleşmeler** ekranından görülebilsin, düzenlenebilsin,
  silinebilsin.

Not: bu ayki 21 belirsizliğin her biri bir kez geçiyor, yani ilk ay kazanç yok.
Kazanç ikinci aydan itibaren — kargo firmaları, personel, Park Plaza her ay
tekrar ediyor.

---

## 4. Hesap sahibi unvanı çoklu olmalı

Şu an tek metin alanı var. Bankalar aynı firmayı çok farklı yazıyor. Gerçek
dosyada sayılan yazımlar:

```
PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ   168
ADAY BAĞIMSIZ DENETİM                      88 + 36 + 11
PKF ADAY BAĞIMSIZ DENETİM A.Ş.             87 + 28 + 7 + 4 + 2
PKF ADAY                                    22
ADAY BAĞIMSIZ DENETİM VE SMMM A.Ş.
PKF ADAY BAĞIMSIZ DENETİM AŞ.
```

Kullanıcı tek yazım girdi; kalanlar elenmiyor ve karşı taraf sanılıyor.

- `HesapSahibiUnvani`'nı çoklu değer kabul edecek şekilde değiştir (satır satır
  veya ikinci bir `HesapSahibiTakmaAdlari` alanı). Eleme, bu değerlerin
  **herhangi birinin** çekirdeğiyle eşleşmede yapılsın.
- **Çekirdek eşitliği yerine kapsama kontrolü kullan.** `PKF ADAY BAGIMSIZ
  DENETIM` ile `ADAY BAGIMSIZ DENETIM` çekirdek olarak eşit değil ama aynı firma.
  Çıkarılan unvanın çekirdeği, hesap sahibi çekirdeklerinden birinin alt metniyse
  (veya tersi) elensin.
- **Öneri üret:** kullanıcı bir unvan girdiğinde, yüklenmiş ekstrelerin
  açıklamalarında geçen benzer yazımları tarayıp aday olarak göster, tek tıkla
  eklensin. `ADAY BAĞIMSIZ DENETİM VE SMMM A.Ş.` gibi yazımlar ancak böyle
  bulunur.
- Aynı koruma benzersiz önek indeksine de uygulansın.

---

## 5. Açıklamanın sonundaki satıcı adı

Bir grup satırda karşı tarafın adı açıklamanın **sonunda**, `Tahsilatı` /
`Tahsilat` ile biten bir ifade olarak geçiyor. Ortadaki `Ad Soyad/Unvan:` alanı
hesap sahibinin kendi unvanı — karşı taraf değil, bazen maskeli.

```
"Temsilci Numarası:81027,Bayi Ad Soyad/Unvan:PKF ADAY BAĞIMSIZ DENETİM A.Ş.,Belbim Temsilci Tahsilatı"
   → Belbim        → 329 B43 Belbim Elektronik Para Ve Ödeme
"Abone No:22912623, Ad Soyad/Unvan:PK* AD** BA****** DE*****, ... Superonline Tahsilatı"
   → Superonline   → 329 T06 Turkcell Superonlıne
"2124260093 Abone Nolu, Adı/Ünvanı:, Fatura No:..., Türk Telekom Ses/Data/ICT Tahsilatı"
   → Türk Telekom  → 329 T01 Türk Telekom A.Ş
"Müşteri No:4129479, Ad Soyad/Unvan:PKF ADAY ..., Turknet Tahsilatı"
   → Turknet       → 329 T61 Turknet İletişim Hizmetleri
```

- Yeni desen: açıklamanın sonundaki `Tahsilatı`/`Tahsilat` ile biten ifadeyi
  yakala. `Temsilci`, `Bayi`, `Ses/Data/ICT`, `Abone` gibi genel ekleri at.
- Bu desen diğerlerinden **önce** denensin — aynı metinde `Ad Soyad/Unvan:` alanı
  da var ve mevcut desenler oraya takılıyor.
- `Ad Soyad/Unvan:`, `Adı/Ünvanı:`, `Soyadi/Unvani:` alanlarını unvan kaynağı
  olarak **hiç kullanma**.

---

## 6. Düşük skorlu öneri gösterme

`Superonline Tahsilatı` satırında sistem **0.20 skorla** `329 A33 Adobe Systems
Ireland` önerdi. `Turknet Tahsilatı` → `329 N21 Novatek` (0.21). Alakasız.

Skor **0.40'ın altındaysa öneri hiç gösterilmesin**, satır `Çözülemedi` olsun ve
kod kutusu boş kalsın. Alakasız öneri boş kutudan daha kötü — kullanıcı
yanlışlıkla onaylayabilir ve sistem onu öğrenir.

---

## 7. Vergi Tahsilatı ve plaka anahtarı

İşlem tipi `Vergi Tahsilatı` olan satırlarda karşı hesap metnin içeriğine göre
değişiyor; tek kural yetmiyor. Gerçek dosyadaki 5 vergi satırı **dört farklı
hesaba** gitmiş:

```
"9085/TRAFİK CEZ. Tahsilatı ... Plaka:34MRP081"  → 689 9 1 (KKEG)
"0040/S.DAMGA V..."                              → 360 01 004
"0033/... beyanname"                             → 770 04 001
```

- Bu satırlarda unvan çıkarma yapılmasın.
- Metindeki **vergi kodu** (`9085`, `0040`, `0033`) ve anahtar kelimeler
  (`TRAFİK CEZ`, `DAMGA`, `BEYANNAME`) çıkarılıp yönetilebilir bir eşleme
  tablosundan aday hesap önerilsin. Tablo Tanımlar altından düzenlenebilsin.
- Tek aday varsa otomatik, birden fazla veya hiç yoksa onaya düşsün.

**Plaka anahtarı:** Metinde plaka geçiyorsa (`Plaka:34MRP081`, `34MRP471 Nolu
plakanın`) ve hesap planında o plakayı adında taşıyan hesap varsa aday olarak öne
çıkarılsın. Plakalar planda boşluklu (`740 99 01 01 09 — 34 Mrp 081 Araç Otopark
Yakıt Vb.`), metinde bitişik (`34MRP081`) — karşılaştırmada boşlukları temizle.

Aynı plakanın birden fazla hesabı var (`34 Mrp 081 Araç Kira Bedeli` /
`Araç Otopark Yakıt Vb.`), o yüzden plaka tek başına karar vermesin, adayları
daraltsın ve satır onaya düşsün. Aynı mantık HGS ve otoyol yükleme satırlarında
da geçerli.

---

## Kabul kriterleri

Derleme temiz, migration üretildi ve uygulandı, tüm testler geçiyor. Aşağıdaki
senaryolar **gerçek dosyadaki açıklama metinleriyle** test edilsin:

1. `BAYCAN ELEKTRİK` geçen satır → `120 B62` (hesap adı 50 karakterde kesik
   olmasına rağmen önek eşleşmesi çalışıyor)
2. `SOLVİA YAZILIM VE DANIŞMANLIK` geçen satır → `120 S97`
3. `ZİRAAT BANKASI` metni geçen satırlar banka isimli cariye eşleşmiyor
4. `İSGOLD ALTIN RAFİNERİSİ ANONİM ŞTİ` → `120 I55`, alıcı `ADAY BAĞIMSIZ
   DENETİM VE SMMM A.Ş.` yazımı eleniyor
5. `Enpara Bank A.Ş. BURAK GÜNEL hesabına giden FAST` → `329 B41` — yön kuralı
   `159 B41` / `329 B41` çiftini onaya düşürmeden çözüyor
6. `Yurtiçi Kargo`, `Aras Kargo`, `Zafer Genç`, `Ufuk Çolak` satırları da yön
   kuralıyla çözülüyor, onaya düşmüyor
7. `HESAPLAR ARASI E.F.T. VAKIFBANK/DENİZBANK` → `102 1 3 02`, açıklama
   `Hesaplar Arası E.F.T. - Denizbank`
8. `HESAPLAR ARASI EFT VAKIFBANK/TÜRKİYE İŞ BANKASI` → `102 1 5 01`
9. `KEMAL GÜLMAN VK POLAT GÜLMAN PARK PLAZA 19.KAT` satırı **onaya düşüyor**
   (hem Kemal Gülman hem Park Plaza eşleşiyor)
10. Park Plaza (4 hesap) ve Pardus Portföy (37 fon) satırları onaya düşüyor,
    tüm aile üyeleri listeleniyor
11. Park Plaza belirsizliği bir kez çözülünce ikinci yüklemede sorulmuyor;
    aday kümesi değişirse tekrar soruluyor
12. `Belbim Temsilci Tahsilatı` → `329 B43`; `Superonline Tahsilatı` → `329 T06`
13. 0.20 skorlu aday öneri olarak gösterilmiyor
14. `9085/TRAFİK CEZ` satırı onaya düşüyor, adaylar arasında `689 9 1` ve
    `740 99 01 01 09` var
15. Hesap sahibinin altı yazımının hepsi eleniyor

Sonunda `OZET.md` ve `KARARLAR.md` güncelle. Gerçek dosyayla çalıştırıp
otomatik / onay bekleyen / çözülemeyen sayılarının önceki tura (128 / 123 / 36)
göre nasıl değiştiğini yaz.
