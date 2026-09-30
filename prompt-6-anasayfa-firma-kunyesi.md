# PROMPT 6 — ANASAYFA FIRMA KUNYESI: SINIFLANDIRMA, TAKIP VE EKSIK BILGI

## AMAC
Var olan Anasayfa ekranini GELISTIRMEK. Sifirdan tasarim YOK. Mukellefiyet,
Sicil, Ortaklik ve Imza yetkilileri kartlari oldugu gibi kalacak.

Uc ekleme yapilacak:
1. Yeni kart: SINIFLANDIRMA VE MIZAN (Mukellefiyet ile Sicil arasina)
2. Sol firma rayi: uyari ucgeni yerine EKSIK ALAN SAYISI + filtre
3. Yeni kart: TAKIP (sorumlu, donemler, not, son islemler) — en altta

## KURAL
- Mevcut kartlarin icerigi, sirasi ve gorunumu DEGISMEYECEK. Tek istisna madde
  2.3: ORKA firma kodu Sicil kartindan yeni karta tasiniyor.
- Hicbir mali hesaplama degismeyecek. MizanHesaplayici, GelirTablosuCalculator,
  BilancoPanel, FinansalOranlarGrid, EtiketMotoru, GrupGorunumu DEGISMEYECEK.
- Eski dogru calisan hicbir sey bozulmayacak; bu is UZERINE EKLEME.
- Uc adim SIRAYLA yapilacak. Her adim sonunda DUR, derle, ne yaptigini bildir,
  sonraki adima gec.
- Commit atma.

## ADIM 0 — ONCE INCELE, kod yazmadan bildir
- Anasayfa ekranini olusturan bilesen(ler)in tam yolu
- Sol firma rayini ve uyari ucgenini ciziyor kod nerede; ucgen hangi kosulla
  cikiyor
- Firma entity'sinin tam alan listesi (Domain/Models altinda). Su alanlar VAR MI:
  defter usulu, vergi turu, mizan formati, hesap donemi, sorumlu kullanici,
  ORKA firma kodu
- Mukellefiyet turleri nerede tutuluyor: metin mi, ayri tablo mi, kod ve ad
  ayri mi?
- FirmaKontrolMizanAgaclari tablosunda YUKLENME ZAMANI, satir sayisi, borc/alacak
  toplami gibi ozet alanlar var mi? Yoksa mizanin en son ne zaman yuklendigi
  nereden bilinebilir?
- Kullanici tablosu ve mevcut kimlik dogrulama: sorumlu atamak icin baglanacak
  tablo hangisi?
- Uygulamada hali hazirda bir olay/audit kaydi var mi?
Bulduklarini yaz, sonra ADIM 1'e gec.

## ADIM 1 — SINIFLANDIRMA VE MIZAN KARTI

### 1.1 Yeni alanlar (Firma)
    DefterUsulu     enum: Belirsiz | BilancoEsasi | IsletmeHesabi
    VergiTuru       enum: Belirsiz | KurumlarVergisi | GelirVergisi
    MizanFormati    string (serbest metin degil, sabit listeden)
    HesapDonemi     enum: TakvimYili | OzelHesapDonemi
    OzelDonemBas    date?  (yalnizca OzelHesapDonemi ise)
    OzelDonemBit    date?

MizanFormati SIMDILIK sabit bir listeden secilen metin olacak:
"ORKA — döviz kolonlu", "ORKA — döviz kolonsuz", "Luca", "Mikro", "Logo",
"Belirsiz". Ileride mizan format profili tablosuna baglanacak; SIMDI O TABLOYU
KURMA, sadece bu alani ekle.

KOD AYRACI alani D tasariminda gorunuyor ama BU PROMPTTA YAPILMAYACAK. Profil
tablosu gelmeden dolduramayiz; karta koyma.

### 1.2 Mukellefiyetten otomatik doldurma — KOD ile, AD ile DEGIL
Firma kaydedilirken ya da mukellefiyet okundugunda:
- Mukellefiyet kodlari arasinda **0010** varsa: VergiTuru = KurumlarVergisi,
  DefterUsulu = BilancoEsasi
- 0010 yok, **0001** varsa: VergiTuru = GelirVergisi, DefterUsulu = Belirsiz
  (sahis mukellefi hem bilanco hem isletme hesabi olabilir, kullanici secer)
- Ikisi de yoksa: ikisi de Belirsiz, eksik sayilir

**0003 ASLA SINIFLANDIRMADA KULLANILMAYACAK.** Adi "GELİR VERGİSİ S.
(MUHTASAR)" oldugu icin ad uzerinden eslesen kod DGR'yi gelir vergisi mukellefi
sanar — DGR'de hem 0003 hem 0010 var. Eslestirme MUKELLEFIYET KODUNDAN yapilacak,
hesap adindan DEGIL. Bu testi yaz.

KISIT: VergiTuru = KurumlarVergisi ise DefterUsulu = IsletmeHesabi SECILEMEZ.
Arayuzde o secenek pasif, sebebi yaninda yazili.

Otomatik doldurma ONERIDIR: kullanici her zaman degistirebilir. Kullanici elle
degistirdiyse sonraki mukellefiyet okumasi UZERINE YAZMAZ.

### 1.3 Kart icerigi
Mukellefiyet kartinin HEMEN ALTINA, Sicil kartinin USTUNE.
Baslik "Sınıflandırma ve mizan", sag ustte "Düzenle".
Birinci satir: Defter usulü · Vergi türü · Mizan formatı · ORKA firma kodu ·
Hesap dönemi.
- Defter usulu ve vergi turu otomatik dolduysa altinda kucuk gri "0010'dan
  otomatik" notu.
- Mizan formatinin altinda "Boş şablon indir" baglantisi. Format Belirsiz ise
  baglanti PASIF.
Ikinci satir (ust kenarlikla ayrilmis): Son yüklenen mizan · Yükleme zamanı ·
Denge · Alt kırılım · sagda "Firma kontrolünde aç" baglantisi.

**MIZAN YUKLEME BUTONU KOYMA.** Yukleme Firma Kontrol ekranindadir ve orada
kalacak. Anasayfa mizanin DURUMUNU gosterir, yuklemez. Iki yukleme kapisi iki
farkli davranis demektir.

### 1.4 ORKA firma kodu tasiniyor
Sicil kartindan CIKARILIP bu karta alinacak. Sicil kartinda ticaret sicilinden
gelen bilgiler kalir; ORKA firma kodu bir program ayaridir. Alan ayni alan,
veri tasinmiyor, sadece gosterildigi yer degisiyor.

### 1.5 Mizan durumu nereden gelecek
ADIM 0'daki cevaba gore:
- Ozet alanlar zaten varsa onlari kullan.
- Yoksa FirmaKontrolMizanAgaclari'na su alanlar eklenecek:
  YuklenmeZamani, SatirSayisi, SeviyeSayisi, BorcToplam, AlacakToplam.
  Bunlar mizan kaydedilirken doldurulur. MEVCUT KAYITLAR SILINMEYECEK; eski
  kayitlarda bu alanlar bos kalir ve ekranda "—" gorunur, hata verilmez.
- Denge: BorcToplam ile AlacakToplam farki 0,01'den kucukse yesil
  "Borç = Alacak", degilse amber "Fark <tutar>". Hesaplama YAPMA, kayitli
  ozetten oku. Ozet yoksa "—".

## ADIM 2 — SOL RAY VE EKSIK BILGI SERIDI

### 2.1 Eksik alan sayisi
Her firma icin su alanlar kontrol edilir; bos olan her biri 1 eksik sayilir:

    Mukellefiyet : vergi dairesi, VKN, NACE kodu, ise baslama
    Sicil        : ticaret sicil no, MERSIS no, sermaye, kurulus tarihi, adres
    Siniflandirma: defter usulu, vergi turu, mizan formati, hesap donemi
    Takip        : sorumlu
    Ilgili kayit : en az bir ortak, en az bir imza yetkilisi

Toplam 16 kontrol. Liste KOD ICINDE TEK YERDE tanimlanacak, hem rayda hem uyari
seridinde ayni liste kullanilacak.

### 2.2 Ray gorunumu
- Ucgen ikon kalkiyor. Yerine SAYI rozeti: sari zemin, koyu kahve metin, eksik
  alan sayisi. Eksik yoksa rozet HIC CIKMAZ.
- Cari yilin mizani hic yuklenmemisse sari rozet yerine KIRMIZI unlem rozeti.
  Ikisi birden varsa kirmizi kazanir (daha agir).
- Renk tek basina anlam tasimasin: rozetin `title` degeri "3 eksik alan" /
  "mizan yuklenmemis" yazsin ve rayin altinda tek satirlik aciklama dursun.
- Firma satirinin ikinci satiri: VKN'in yanina " · " ile mizan formati eklenir
  (Belirsiz ise "—").
- Ust tarafa uc filtre cipi: "Tümü N" · "Eksik N" · "Mizan yok N". Sayilar
  gercek. Secili cip mavi.
- Grup sirketleri ile yeni musteriler ayrilacaksa bu PROMPTTA YAPILMAYACAK;
  duz liste kalsin.

### 2.3 Uyari seridi
Bugun tek eksigi yaziyor. Artik HEPSINI yazacak, kart adiyla birlikte:
"Eksik bilgi: Sicil › MERSİS no · Takip › sorumlu atanmamış"
Ucten fazlaysa ilk uc tanesi ve "+N tane daha".
Sagda "Tamamla" butonu: ilk eksik alanin bulundugu karti acip o alana odaklanir.
Eksik yoksa serit HIC CIKMAZ.

## ADIM 3 — TAKIP KARTI

### 3.1 Sorumlu
Firma'ya `SorumluKullaniciId` (nullable FK). Kartta ad ve "Ata" / "Değiştir".
Atanmamissa kesikli cerceveli bos avatar ve "Atanmamış".

### 3.2 Donemler
Hangi yillarin mizani yuklu: FirmaKontrolMizanAgaclari'ndan okunan yil listesi.
Yuklu olan yesil cip, cari yil yuklu degilse gri cip. Salt okunur.

### 3.3 Not
Yeni tablo `FirmaNotu`: Id, FirmaId, Metin, OlusturanKullaniciId, OlusturmaZamani.
Firma basina birden fazla not olabilir, en son eklenen kartta gorunur, digerleri
"Tüm notlar" ile acilir. Silme yalnizca notu ekleyen kullaniciya acik.

### 3.4 Son islemler
Yeni tablo `FirmaOlayKaydi`: Id, FirmaId, OlayTipi, Aciklama, KullaniciId, Zaman.

OlayTipi sabit liste: FirmaOlusturuldu, MukellefiyetGuncellendi, SicilGuncellendi,
SiniflandirmaGuncellendi, SorumluAtandi, NotEklendi, MizanYuklendi,
EtiketKuraliEklendi, EtiketKuraliSilindi.

**YALNIZCA YAZMA ISLEMLERI KAYDEDILIR. GORUNTULEME KAYDI TUTULMAYACAK.**
(Tasarimda "Bilanço görüntülendi" satiri vardi, o YANLIS. Her ekran acilisini
kaydetmek listeyi kullanissiz hale getirir ve cok kullanicili ortamda takip
hissi yaratir. Sadece degisiklikler.)

Kartta son 5 kayit, altinda "Tüm hareketler". Kayit yoksa "Henüz işlem yok".
Olay kaydi yazma islemi ASLA asil islemi bozmayacak: ayri try/catch, hata
loglanir, kullaniciya yansimaz.

### 3.5 Imza yetkilileri karti — tek kucuk ekleme
Kart basligina rozet: yetki bitisine 365 gunden az kalan yetkili varsa amber
"N yetki 1 yıldan kısa", 90 gunden az varsa kirmizi. Yoksa rozet cikmaz.
Kartin icerigi degismiyor.

## CSS
Yeni siniflar `an-` onekiyle. `bl-`, `or-`, `hs-`, `et-`, `gg-`, `dy-`,
`fk-mizan-*` siniflarina dokunma. Mevcut anasayfa siniflarini yeniden
adlandirma.

## DOGRULAMA (DGR, firmaId 3)
Ekranda su degerler gorunmeli:
- Vergi dairesi SARIYER · VKN 2951070824 · NACE 661907 · e-Fatura var ·
  e-Defter var
- Mukellefiyet cipleri: 0003, 0010, 0015, 0033, 0040
- Defter usulu "Bilanço esası", vergi turu "Kurumlar vergisi", ikisi de
  "0010'dan otomatik" notlu
- Ticaret sicil no 305811-5 · sermaye 500.000,00 TRY · kurulus 14.04.2021
- MERSIS no bos → "+ ekle" baglantisi
- ORKA firma kodu 0521 YENI KARTTA, Sicil kartinda YOK
- Ortaklik toplami 500.000,00 ve %100
- Zeynep POTUR yetki bitisi 22.12.2028 → 365 gunden fazla, rozet CIKMAZ
- Eksik alan sayisi: MERSIS no + sorumlu + ise baslama = en az 3, rayda rozet
  bu sayiyi gostermeli

Diger sekmelerdeki (Bilanço, Gelir Tablosu, Vergi, Finansal Oranlar, Dikey
Yüzdeler, Etiketler, Grup Görünümü) HICBIR rakam degismemeli.

## TESLIM
- ADIM 0'in cevaplari
- Her adim icin: eklenen ve degisen dosyalar, migration adi
- 0003 testinin gectigi
- Dogrulama listesindeki degerlerin tuttugu
- Eski mizan agaci kayitlarinin bozulmadigi
- Derleme ve test sonucu
- Commit atma.
