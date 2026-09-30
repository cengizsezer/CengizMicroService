# PROMPT 5 — MIZAN FORMAT PROFILLERI (ORKA / LUCA)

## AMAC
Mizan parserini SABIT KOLON HARFINDEN kurtarmak. Bugun parser kolonlari harfle
okuyor ve dosya yapisi farkli oldugunda SESSIZCE yanlis okuyor. Bunun yerine
PROFIL tabanli, BASLIK SATIRINDAN eslesen ve yuklemeden ONCE dogrulayan bir
yapiya gecilecek.

Iki profil gomulu gelecek: ORKA ve Luca. Ikisi de gercek dosyayla dogrulanacak.

## KURAL
- Hicbir mali hesaplama, formul ya da oran degismeyecek. MizanHesaplayici,
  MaliTabloIsareti, GelirTablosuCalculator, hesap_plani.json, BilancoPanel,
  FinansalOranlarGrid, EtiketMotoru, GrupGorunumu DEGISMEYECEK.
- Bu gorev VERI OKUMA katmanidir.
- Mevcut ORKA dosyalari birebir ayni sonucu vermeye devam edecek (madde 10).
- Kayitli mizan agaclari (FirmaKontrolMizanAgaclari) bozulmayacak, migration ile
  silinmeyecek.
- DONEM KARI / 690 sorunu BU PROMPTUN KAPSAMINDA DEGIL. Sadece madde 1'de
  bildirilecek, dokunulmayacak. Ayri prompt gelecek.
- Commit atma.

## 1. ONCE INCELE — kod yazmadan bildir
- ExcelMizanParser.cs tam yolu. Kolonlari nasil okuyor: sabit harf, sabit index,
  yoksa baslik arama? Ilgili satirlari yaz.
- Veri hangi satirdan baslamis kabul ediliyor. Baslik satiri atlaniyor mu?
- Tutar parse'i: hangi metod, hangi CultureInfo, hata halinde ne oluyor
  (atliyor mu, sifir mi yaziyor mu, exception mi)?
- Alt kirilim agacini kuran kod hangi dosyada, kod ayracini NEREDEN aliyor
  (sabit bosluk mu)?
- GelirTablosuCalculator.GetDonemKari: 690 hesabinin BAKIYESINI mi okuyor, yoksa
  6xx/7xx hesaplarindan mi HESAPLIYOR? Tam satiri yaz. (Sadece rapor, degistirme.)
- Nazim hesaplar (800) nerede disarida birakiliyor? Kok kodu sabit yazili mi?
- Mizan yukleme UI'i hangi dosyada, hangi endpoint'e gidiyor?
Bulduklarini yaz, sonra uygula.

## 2. MIZAN FORMAT PROFILI MODELI
Yeni model `Domain/Models/FirmaKontrol/MizanFormatProfili.cs`:

    Id, Ad, Aciklama
    SheetDeseni        : regex, null ise ilk sheet
    BaslikAnahtari     : baslik satirini bulmak icin aranan metin
    Kolonlar           : Dictionary<MizanAlani, string[]>  (alan -> kabul edilen baslik metinleri)
    KodAyraci          : char?   null ise alt kirilim HIC kurulmaz
    NazimHesapKokleri  : string[]

`MizanAlani` enum: HesapKodu, HesapAdi, BorcHareket, AlacakHareket, BorcBakiye,
AlacakBakiye, DovizBorc, DovizAlacak.
HesapKodu, HesapAdi, BorcBakiye, AlacakBakiye ZORUNLU; digerleri opsiyonel.

Profil bir VERI kaydidir. Yeni program eklemek yeni profil tanimlamaktir, kod
degisikligi degil. Program adina gore `if (orka)` / `if (luca)` dallanmasi
HICBIR YERDE OLMAYACAK.

### Gomulu profil 1 — "ORKA — doviz kolonlu"
    SheetDeseni       : "^Tfrm_bilancogenelmizan"
    BaslikAnahtari    : "Hesap Kodu"
    HesapKodu         : "Hesap Kodu"
    HesapAdi          : "Hesap Adi"
    DovizBorc         : "Doviz Borc"
    DovizAlacak       : "Doviz Alacak"
    BorcBakiye        : "TL Borc Bakiye", "Borc Bakiye"
    AlacakBakiye      : "TL Alacak Bakiye", "Alacak Bakiye"
    KodAyraci         : ' '
    NazimHesapKokleri : ["8"]

### Gomulu profil 2 — "ORKA — doviz kolonsuz"
Ayni, DovizBorc ve DovizAlacak YOK. (Yasadigimiz kayma hatasinin dogru profili.)

### Gomulu profil 3 — "Luca"
    SheetDeseni       : "^mizan$"
    BaslikAnahtari    : "HESAP KODU"
    HesapKodu         : "HESAP KODU"
    HesapAdi          : "HESAP ADI"
    BorcHareket       : "BORC"
    AlacakHareket     : "ALACAK"
    BorcBakiye        : "BORC BAKIYESI"
    AlacakBakiye      : "ALACAK BAKIYESI"
    KodAyraci         : '.'
    NazimHesapKokleri : ["9"]

Luca dosyasinda para birimi (TL/USD/EUR) tasiyan bir kolon var ama BASLIGI YOK.
Bilerek eslenmiyor. Yaprak hesap tespiti bu kolondan DEGIL, "alt dugumu olmayan
dugum" kuralindan yapilacak. Kimse bu kolonu kazara eslemesin.

## 3. BASLIK EŞLEŞTIRME — TAM ESLESME, CONTAINS DEGIL
Baslik metinleri karsilastirilmadan once normalize edilir: Trim, NBSP temizligi,
ardarda bosluklar tek boslugua indirgenir, buyuk harfe cevrilir, Turkce karakterler
ASCII karsiligina cevrilir (C->C, G->G, I->I, O->O, S->S, U->U; hem i hem I ayni
harfe dussun).

ESLESME TAM OLACAK. `Contains` KULLANILMAYACAK. Sebep: Luca dosyasinda hem "BORC"
hem "BORC BAKIYESI" basligi var. Contains ile "BORC" arayan kod hareket kolonunu
bakiye kolonu sanar ve dosya sessizce yanlis okunur. Bu tam olarak kacindigimiz
hata sinifi.

## 4. BASLIK SATIRI TESPITI
Sheet'in ilk 30 satirinda BaslikAnahtari'ni normalize tam eslesmeyle tasiyan ilk
hucre aranir.
- Bulunan hucrenin SATIRI baslik satiridir.
- Bulunan hucrenin KOLONU HesapKodu kolonudur.
- Diger alanlar ayni satirda kendi baslik metinleriyle aranir.
- Veri satirlari baslik satirinin BIR ALTINDAN baslar.
Bulunamazsa: profil reddedilir. Hata mesaji "Baslik satiri bulunamadi" + ilk 10
satirin ozeti (satir no, ilk 6 hucrenin degeri). Sessiz devam YOK.

ORKA dosyasinda baslik 1. satirda, Luca dosyasinda 7. satirda. Ikisi de ayni kodla
bulunacak, satir numarasi hicbir yerde sabit yazilmayacak.

## 5. OTOMATIK PROFIL TESPITI
Dosya secilince butun profiller sirayla denenir. Bir profil "uyuyor" sayilir:
sheet deseni tutuyorsa VE baslik satiri bulunduysa VE zorunlu alanlarin hepsi
eslestiyse.
- Tam bir profil uyuyorsa o profil secili gelir, yesil cip: "<profil adi> —
  baslik satirindan taninkdi".
- Hicbiri uymuyorsa amber cip "Format taninamadi", firmanin kayitli profili
  varsa o kullanilir, yoksa kullanici secer.
- Birden fazlasi uyuyorsa en fazla alan eslesen kazanir, esitlikte listede once
  gelen.
Kullanici dropdown'dan her zaman elle gecersiz kilabilir. Tespit ONERIDIR,
karar kullanicinindir.

## 6. SAYI OKUMA — AYNI DOSYADA IKI FORMAT OLABILIR
- Hucre numeric ise (double/int/decimal) dogrudan alinir.
- Hucre metin ise: Trim, NBSP ve normal bosluklar temizlenir, binlik ayraci
  kaldirilir, ondalik virgul noktaya cevrilir, InvariantCulture ile parse edilir.
- "" ve "-" degeri 0 kabul edilir.
- Parse edilemezse satir ATLANMAZ: hata listesine (satir no, kolon, ham deger)
  yazilir ve DOGRULAMA BASARISIZ olur.

Luca dosyasinda veri satirlari metin (`'335.803.846,02 '` — sonunda bosluk var),
TOPLAM satiri gercek float (`846358121.84`). Ayni dosyada iki format. Ikisi de
calisacak.

## 7. VERI SATIRI / TOPLAM SATIRI AYRIMI
Hesap kodu bos olan ya da rakamla baslamayan satir VERI DEGILDIR.
TOPLAM satiri yakalanir, madde 8'de dogrulama icin kullanilir, veriye girmez.
Luca'da bu satirin hesap adi "TOPLAM :", hesap kodu bos.

## 8. ALT KIRILIM AGACI
Ayrac profilden gelir, kodun icinden TAHMIN EDILMEZ. KodAyraci null ise alt
kirilim hic kurulmaz, ana hesaplar normal calisir, hata verilmez.
Ana hesap = ilk segment. Derinlik degisken (Luca'da 3 seviye: `102` / `102.01` /
`102.01.004`; ORKA'da `600` / `600 1` / `600 1 21`). Segmentler alfanumerik olabilir.

### KUMULATIF PARENT
Luca dosyasinda ana hesap ve ara seviye satirlari alt hesaplarin TOPLAMINI tasir.
Butun satirlari toplarsan genel toplamin ~4 katini alirsin.
- Her dugumun bakiyesi KENDI SATIRINDAN okunur; cocuklarin toplami yeniden
  toplanmaz.
- Mevcut top-down yurume kurali korunur: etiketlenen dugum kendi bakiyesini alir
  ve altina inmez.
- Bir dugumun kendi satir bakiyesi ile cocuklarinin toplami ARASINDA FARK
  OLABILIR ve bu hata degildir: ana hesap alt hesaplarindaki ters bakiyeleri
  netler. Gunvor dosyasinda bu fark 45.456,59. Buna dayanan bir dogrulama YAZMA.

## 9. DOGRULAMA KAPISI — YUKLEMEDEN ONCE, ZORUNLU
Asagidakiler yukleme oncesi calisir ve sonuclari diyalogda listelenir.

ENGELLEYICI (Yukle butonu devre disi):
- Baslik satiri bulunamadi
- Zorunlu alanlardan biri eslesmedi
- Bir tutar parse edilemedi
- Borc bakiye toplami != Alacak bakiye toplami (ANA HESAP seviyesinde, tolerans
  0,01)

UYARI (engellemez, listelenir):
- TOPLAM satiri varsa: YAPRAK seviyesi toplamiyla karsilastirilir. ANA HESAP
  toplamiyla DEGIL — ikisi netleme yuzunden farkli olur ve her Luca dosyasinda
  yanlis alarm verir.
- 6xx gelir hesabi borc bakiyeli ya da 7xx gider hesabi alacak bakiyeli cikiyorsa
  (hesap adi ve tutarla birlikte). Bu bizim kayma hatasini yakalayan sinyal.
- Hesap planinda bulunamayan ana hesap kodlari
- Satir sayilari: toplam veri satiri / ana hesap / alt kirilim / seviye sayisi

## 10. UI — MIZAN YUKLEME DIYALOGU
Tasarimdaki iki ekrana gore:
- Dosya karti (ad, boyut, sheet adi) + "Degistir"
- Format cipi (yesil taninkdi / amber taninamadi) + format dropdown
- KOLON ESLESMESI tablosu: Alan | Kolon harfi | Dosyadaki baslik | Ilk veri satiri
  degeri. Eslesmeyen alan satiri kirmizi, yaninda "<- beklenen: <baslik>".
- DOGRULAMA listesi: gecen maddeler yesil tik, uyarilar amber, engelleyiciler
  kirmizi capraz.
- Engelleyici varsa altta kirmizi serit "Mizan denk olmadan yukleme yapilamaz" ve
  varsa mavi oneri kutusu "Basliklar <profil> profiline uyuyor" + "Bu profille
  yeniden oku" butonu.
- Yukle butonu dogrulama gecmeden PASIF.

## 11. BOS SABLON INDIRME
Secili profilin baslik satirini tasiyan bos bir .xlsx uretilir. Sheet adi profilin
SheetDeseni'ne uyan sabit bir ad. Sadece baslik satiri, veri yok, formul yok.
Amac: musteriden gelen dosya hicbir profile uymuyorsa icine yapistirilsin.
Diyalogda ve firma tanimi ekraninda ayni buton.

## 12. REGRESYON VE TESTLER
Iki gercek dosya fixture olarak repoya girecek:
`tests/fixtures/mizan_orka_dgr.xlsx` ve `tests/fixtures/mizan_luca_gunvor.xlsx`.

### ORKA — degismemesi gerekenler (firmaId 3, 2026)
- AKTIF TOPLAM 16.550.308,93
- I Donen Varliklar 15.889.452,31 · II Duran Varliklar 660.856,62
- 120 Alicilar 10.120.346,89 · 102 Bankalar 57.558,81
- III KVYK 12.008.789,15 · V Ozkaynaklar 11.664.974,85
- Net satislar 22.020.812,69 · Ticari kar 11.377.357,68
- 600 altinda Kurumsal 12.194.054,99 ve Aday 9.264.692,00
Bu rakamlarin BIRI DEGISIRSE dur ve bildir.

### Luca — yeni testler (Gunvor dosyasi)
- Baslik satiri: 7
- HesapKodu kolonu: B · HesapAdi: C · BorcHareket: J · AlacakHareket: K ·
  BorcBakiye: L · AlacakBakiye: M
- Veri satiri sayisi: 190
- Ana hesap (3 haneli, ayracsiz): 28
- Alt kirilim: 39 ara seviye (1 nokta) + 96 yaprak (2 nokta) = 135
- Ana hesap seviyesi borc bakiye toplami = alacak bakiye toplami =
  845.881.874,12
- Yaprak seviyesi borc bakiye toplami = alacak bakiye toplami = 845.927.330,71
- Ana ile yaprak arasindaki netleme farki 45.456,59 — UYARI DEGIL, beklenen
- TOPLAM satiri L/M degeri 845.927.330,71 ve yaprak toplamiyla ESITTIR
- Nazim hesaplar 900 ve 901, her biri 11.265.030,00, bilanco toplamlarina
  GIRMEZ
- 102 BANKALAR borc bakiye 335.803.846,02, altinda 102.01 Tl Bankalar
  3.430.937,06 ve onun altinda 102.01.013 Garanti Bankasi Kozyatagi 2.970.319,28
- 580 GECMIS YILLAR ZARARLARI 441.995.856,50 BORC bakiyeli (dogru, negatif
  ozkaynak kalemi) — isaret cevirme yapilmayacak
- 502 SERMAYE DUZELTMESI OLUMLU FARKLARI 199.063.181,45 hesap planinda VAR MI,
  kontrol et ve bildir

### Kayma hatasi testi — ZORUNLU
Luca dosyasi ORKA profiliyle okutuldugunda parser SESSIZ BASARILI DONMEYECEK.
Beklenen: baslik satiri bulunamadi ya da zorunlu alan eslesemedi hatasi.
Bu testi yaz.

### Bilinen ve BU PROMPTTA DOKUNULMAYACAK
Gunvor dosyasinda 690 hesabi 74.011.342,30 alacak bakiyeli ama 622
(15.945.662,25) ve 770 (325.971,57) hala acik. Bu yuzden bilancoda
16.271.627,36 fark cikacak. BU BEKLENEN DAVRANIS, madde 9'daki denge kapisi
mizan seviyesinde calisiyor ve mizan denk. Bilanco ekranindaki fark ayri bir
prompt ile duzeltilecek. BURADA DUZELTMEYE CALISMA, GelirTablosuCalculator'a
DOKUNMA.

## 13. TESLIM
- Madde 1'in cevaplari
- Eklenen ve degisen dosyalar
- Uc profilin tanimi ve nerede duruyor
- Madde 12'deki ORKA rakamlarinin aynen tuttugu
- Madde 12'deki Luca rakamlarinin tuttugu
- Kayma hatasi testinin gectigi
- 502 hesabinin hesap planinda olup olmadigi
- Derleme ve test sonucu
- Commit atma.
