# PROMPT 11 — CTRL+V ILE TOPLU IS EKLEME

## AMAC
Firma isleri kartina Ctrl+V ile satir yapistirilabilecek. 17 firmaya tek tek
dialog acarak is girmek yapilacak is degil.

Felsefe mizan yuklemenin aynisi: **YAPISTIR → NE ANLADIGINI GOSTER → ONAYLAT →
SONRA EKLE.** Sessiz ekleme yok.

Ayrica: isler cogu firmada ayni (bordro, KDV hazirlik, banka ekstresi). Ayni
listeyi 17 kez yapistirmak yerine **baska firmadan kopyalama** da olacak.

## KURAL
- Prompt 6–10B'de calisan hicbir sey bozulmayacak.
- Hicbir mali hesaplama degismeyecek.
- Adimlar SIRAYLA. Her adim sonunda DUR, derle, bildir.
- Commit atma.

## ADIM 0 — ONCE INCELE, kod yazmadan bildir
- **Projede HALIHAZIRDA yapistirma ile veri alan bir ekran var mi?** Kullanici
  "takvim bolumundeki gibi" dedi. Vergi Takvimi ekraninda ya da baska bir yerde
  pano yapistirma varsa DESENINI CIKAR; ikinci bir yapistirma deseni icat
  edilmeyecek, ona uyulacak.
- `FirmaIsi` ve `FirmaIsiAlicisi` kaydetme ucu (prompt 8) ve `SistemId` baglantisi
  (prompt 9).
- Excel'den yapistirmada pano icerigi: `text/plain`, satirlar `\r\n`, hucreler
  `\t`. Projede bunu ayristiran bir yardimci var mi?
- xlsx uretimi icin projede kullanilan kutuphane (bos sablon indirmek icin).
Bulduklarini yaz, sonra ADIM 1'e gec.

## ADIM 1 — YAPISTIRMAYI YAKALA VE AYRISTIR

### 1.1 Yakalama
Firma isleri karti odaktayken Ctrl+V calisacak. Ayrica kart basliginda
**"Yapıştır"** butonu olacak (pano izni olmayan tarayici ya da ortam icin).

### 1.2 Ayristirma
- Satirlar `\r\n` / `\n`, hucreler `\t` ile ayrilir. Tek kolonlu yapistirma da
  gecerlidir: her satir bir is BASLIGI sayilir, gerisi bos kalir.
- **Ilk satir baslik mi?** Hucre degerleri bilinen alan adlariyla eslesiyorsa
  (Baslik, Tekrar, Gun, Program, Alici, Aciklama, Menu yolu) baslik sayilir.
  Kullanici bu karari degistirebilir.
- Kolon eslesmesi otomatik yapilir, kullanici DEGISTIREBILIR.

### 1.3 Deger cozumleme — TURKCE, ESNEK
`Tekrar` icin kabul edilecekler (buyuk/kucuk ve Turkce karakter farki onemsiz):
    her ay · aylik · ayda bir            -> Aylik
    uc ayda bir · 3 ayda bir · 3 aylik   -> UcAylik
    yilda bir · yillik · senede bir      -> Yillik
    tek sefer · bir kez · tek            -> TekSefer

`Gun` icin:
    1-31 arasi sayi    -> AyinGunu
    ay sonu · ayin sonu -> AySonu
    donem sonu          -> DonemSonu
    tarih (gg.aa.yyyy)  -> TekSefer isinde TekSeferTarih

`Program` icin: Sistem listesinde (prompt 9) ADA GORE aranir, normalize
karsilastirma ile. Bulunamazsa **bos birakilir** ve satir AMBER isaretlenir —
satir yine de eklenir. Yeni Sistem kaydi OTOMATIK ACILMAZ.

`Alici` icin: "Ad Soyad <eposta@firma.com>" ya da ayri iki kolon. Yalniz ad
varsa e-posta bos kalir, bu gecerlidir.

## ADIM 2 — ONIZLEME DIALOGU
Yapistirma sonrasi dialog acilir. Dort bolum:

### 2.1 Kolon eslesmesi
Mizan yukleme diyalogundaki ile ayni bicim: Alan | Kolon | Dosyadaki baslik.
Eslesmeyen alan "— eşleşmedi, boş kalacak" der. Kullanici eslesmeyi degistirebilir.

### 2.2 Uc sayi
    N is eklenecek            (yesil)
    N eksik bilgiyle eklenecek (amber)
    N okunamadi, atlanacak     (kirmizi)

### 2.3 Satir tablosu
Her satir: sira · baslik · tekrar · gun · program · alici · durum.
- **Kirmizi satir eklenmez** ama listeden silinmez; hucreye tiklayip
  duzeltilebilir. Duzeltilince durumu yesile doner.
- **Amber satir EKLENIR**, eksik alan bos kalir. Engellemek yanlis olur —
  kullanici on isi birden giriyor, eksigi sonra prosedur panelinden doldurur.
- Satir tek tek **atlanabilir** (sagda kucuk carpi).

### 2.4 Alt serit
- Onay kutusu: **"Aynı başlıktaki mevcut işin üzerine yazma"** — VARSAYILAN ACIK.
  Kapatilirsa ayni baslikli mevcut is guncellenir.
- "Vazgeç" ve "N işi ekle".

## ADIM 3 — BOS SABLON
Dialogda ve kart basliginda **"Boş şablon indir"**. Dogru baslik satirini tasiyan
bos bir .xlsx verir: Başlık · Tekrar · Gün · Program · Menü yolu · Alıcı ·
Açıklama. Ikinci satirda ORNEK bir satir olsun (bicimi gostersin), kullanici
silecek. Mizan sablonuyla ayni dongu: Excel'de doldur, geri yapistir.

## ADIM 4 — BASKA FIRMADAN KOPYALA
Kart basliginda ikinci bir secenek: **"Başka firmadan kopyala"**.
- Firma secilir, o firmanin OZEL isleri listelenir (yasal isler kopyalanmaz,
  onlar zaten mukellefiyetten turuyor).
- Hangi islerin kopyalanacagi tek tek secilebilir, varsayilan hepsi secili.
- Kopyalanan: baslik, aciklama, tekrar, gun kurali, program, menu yolu,
  nasil yapilir metni, alicilar.
- **Kopyalanmayan**: tamamlama kayitlari, donem ekleri, prosedur ekleri,
  on adim baglantisi, sorumlu. Bunlar firmaya ozgudur.
- Ayni baslikli is varsa atlanir ve raporlanir.

Ekler neden kopyalanmiyor: bir firmanin ekran goruntusu digerinde yaniltici
olur. Kullanici isterse elle ekler.

## CSS
Yeni siniflar `ys-` onekiyle devam. Mevcut `ys-`, `an-`, `dp-`, `bl-`, `or-`,
`hs-`, `et-`, `gg-`, `dy-`, `fk-mizan-*` siniflarina dokunma.

## DOGRULAMA
Asagidaki metin Excel'den yapistirilmis gibi verilerek test edilecek
(hucreler sekme ile ayrilmis):

    Başlık	Tekrar	Gün	Program	Alıcı
    Bordroyu hazırla ve Hollanda'ya gönder	Her ay	25	Luca	Ad Soyad <ad@firma.nl>
    KDV hesabını çıkar ve onaya gönder	her ay	15	Luca
    Banka ekstrelerini indir	Aylık	ay sonu	Garanti
    Mizanı müşteriden iste	her ay	20	lucca
    Geçici vergi hesabını hazırla	üç ayda bir	10	Luca
    Sözleşme yenileme	Her yl
    Defter tasdiki	yılda bir	ay sonu

- Ilk satir baslik olarak taniniyor
- 7 satirdan 6'si eklenecek, 1'i ("Her yl") okunamiyor ve kirmizi
- "lucca" satiri AMBER: eklenir, program bos kalir
- "Aylık", "her ay", "Her ay" ucu de Aylik olarak cozumleniyor
- "ay sonu" AySonu, "20" AyinGunu, "üç ayda bir" UcAylik
- Alici "Ad Soyad <ad@firma.nl>" ad ve e-posta olarak ayriliyor
- Kirmizi satirin tekrar hucresi duzeltilince yesile donuyor
- Tek kolonlu yapistirma (yalniz basliklar) calisiyor
- "Üzerine yazma" acikken ayni baslikli mevcut is atlaniyor ve raporlaniyor
- Bos sablon indiriliyor, basliklar dogru, ornek satir var
- Baska firmadan kopyalama yalniz ozel isleri aliyor; ekler, tamamlamalar ve
  sorumlu kopyalanmiyor
- Mevcut testlerin hepsi geciyor, sayi azalmiyor

## TESLIM
- ADIM 0'in cevaplari, ozellikle mevcut yapistirma deseni bulundu mu
- Her adim icin degisen dosyalar
- Dogrulama metnindeki 7 satirin her birinin nasil cozumlendigi
- Derleme ve test sonucu
- Commit atma.
