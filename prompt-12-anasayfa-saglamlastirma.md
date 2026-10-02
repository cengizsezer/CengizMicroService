# PROMPT 12 — ANASAYFA SAGLAMLASTIRMA: IZOLASYON VE EKSIK BILGI ROZETI

## AMAC
Iki ayri sorun, ikisi de Anasayfa'da:

1. Bir ucun hatasi BUTUN sayfayi cokertiyor. `/isler` cagrisi hata verdiginde
   `Anasayfa.razor` NullReferenceException atiyor ve firma kunyesinin tamami
   kayboluyor. Bir kart calismiyorsa yalniz o kart bos gorunmeli.
2. Eksik bilgi rozeti eyleme donusmuyor. "13 eksik" diyor ama hangisinin
   gercekten gerekli oldugu belli degil; yeni acilmis firma ilk gunden kirmizi
   gorunuyor.

## KURAL
- Prompt 6–11B'de calisan hicbir sey bozulmayacak.
- Hicbir mali hesaplama degismeyecek.
- Yeni cekirdek tablo GEREKMIYOR. Rozet mevcut alanlardan turetilir.
- Adimlar SIRAYLA. Her adim sonunda DUR, derle, bildir.
- Commit atma.

## ADIM 0 — ONCE INCELE, kod yazmadan bildir
- `Anasayfa.razor` hangi uclari cagiriyor, her biri hangi karti besliyor?
  Liste cikar: uc → kart.
- NullReferenceException'in gercek yeri: 283 ve 126 numarali satirlarda hangi
  nesne null kaliyor, cagri basarisiz oldugunda koleksiyon null mi kaliyor yoksa
  bos liste mi atanıyor?
- Prompt 6'da etiket cagrilari icin kosulan izolasyon UYGULANDI MI? Uygulanmissa
  DESENI CIKAR; ikinci bir izolasyon deseni icat edilmeyecek, ona uyulacak.
- "Eksik bilgi" sayisi ve ray rozeti HANGI serviste uretiliyor, hangi alanlari
  sayiyor? Tam alan listesi.
- Firmanin olusturma zamani tutuluyor mu (`OlusturmaZamani` ya da benzeri)?
  Yoksa bildir — ADIM 2 ona bagli.
Bulduklarini yaz, sonra ADIM 1'e gec.

## ADIM 1 — KART BAZINDA IZOLASYON

### 1.1 Kural
Her kart kendi verisini KENDI basina yukler. Bir ucun hatasi yalnizca o karti
etkiler. Sayfa iskeleti, firma secici ve diger kartlar AYAKTA KALIR.

### 1.2 Yapilacak
- Her uc cagrisi kendi `try/catch` icinde. Hata yakalanınca:
  - ilgili koleksiyon **bos listeye** atanir, asla null birakilmaz
  - o kartin kendi hata durumu isaretlenir
- Kart govdesinde tek satir: **"Bu bölüm yüklenemedi · Yeniden dene"**.
  "Yeniden dene" YALNIZ o kartin cagrisini tekrarlar, sayfayi yenilemez.
- Hata metni kullaniciya teknik ayrinti gostermez; ayrinti log'a yazilir.
- Prompt 6'da etiket cagrilari icin kosulan izolasyon zaten varsa AYNI deseni
  kullan, yenisini yazma.

### 1.3 Dikkat
Bos liste ile hata AYNI SEY DEGIL. "Bu firmada hic is yok" ile "is listesi
yuklenemedi" farkli gorunecek. Ikisini ayni gostermek, veri kaybini sessizce
gizler.

## ADIM 2 — EKSIK BILGI ROZETI

### 2.1 Gerekli / ikincil ayrimi
Eksik alanlar IKI gruba ayrilir:

**Gerekli** — bu olmadan is yapilamaz:
vergi numarasi · vergi dairesi · defter usulu · vergi turu · hesap donemi ·
muhasebe programi · gecerli imza yetkilisi

**Ikincil** — eksik olmasi normaldir, zamanla dolar:
adres · telefon · e-posta · NACE kodu · ortaklik paylari · sistem notu ·
banka portallari

Rozet YALNIZ gerekli alanlari sayar. Ikincil eksikler karti acinca "—" olarak
gorunur, rozete girmez.

Sebep: "13 eksik" bir sey soylemiyor. "3 gerekli alan eksik" ne yapilacagini
soyluyor.

### 2.2 Rozet metni
- Gerekli eksik varsa: **"N gerekli alan eksik"**, kirmizi.
- Gerekli eksik yok, uyari varsa: **"N uyarı"**, amber.
- Ikisi de yoksa rozet CIKMAZ. Yesil tik da koymayalim; gurultu olur.
- `title` metni hangi alanlarin eksik oldugunu SAYAR:
  "Eksik: vergi dairesi, defter usulu, muhasebe programı".
- Mevcut "Eksik bilgi:" metnindeki iki nokta sonrasi BOSLUK HATASI duzeltilecek.

### 2.3 Yeni firma ilk gunden kirmizi olmayacak
Firma olusturuldugu gun kunyesi zaten bos. Ilk gunden kirmizi rozet, rozetin
anlamini yok ediyor.
- Firma olusturulali **7 gunden az** ise rozet kirmizi degil **notr/gri**
  gorunur, metin **"Künye tamamlanıyor · N gerekli alan"** olur.
- Sayim AYNEN devam eder, yalniz rengi ve metni degisir. Eksik saklanmiyor.
- 7 gun degeri koda GOMULMEYECEK; tek bir sabitte dursun, yaninda
  "yeni firma toleransi" yorumu olsun.
- `OlusturmaZamani` alani yoksa ADIM 0'da bildir, bu maddeyi ATLA ve soyle.

### 2.4 Banner karta gore gruplanacak
Ust bannerda eksikler tek uzun cumle halinde akiyor. Kart basligina gore
gruplanacak:
    Sınıflandırma: defter usulü, vergi türü
    Kullanılan sistemler: muhasebe programı
    İmza yetkilileri: geçerli imza yetkilisi yok
Her grup kendi kartina baglanti olacak; tiklayinca o karta kayar.

## CSS
Mevcut `an-` onekine devam. `ys-`, `dp-`, `bl-`, `or-`, `hs-`, `et-`, `gg-`,
`dy-`, `fk-mizan-*` siniflarina DOKUNMA.

## DOGRULAMA
- `/isler` ucu bilerek hata dondurulunce: sayfa cokmuyor, firma kunyesi
  goruluyor, yalniz firma isleri karti "Bu bölüm yüklenemedi · Yeniden dene"
  gosteriyor
- "Yeniden dene" yalniz o kartin cagrisini tekrarliyor
- Hic is olmayan firma ile is listesi yuklenemeyen firma AYRI gorunuyor
- Rozet yalniz gerekli alanlari sayiyor; adres eksik olan firmada rozet cikmiyor
- `title` metni eksik alanlarin adlarini sayiyor
- "Eksik bilgi:" sonrasi bosluk duzeldi
- Bugun acilan firma kirmizi degil, gri "Künye tamamlanıyor" gosteriyor
- 7 gunden eski ve gerekli alani eksik firma KIRMIZI gosteriyor
- Banner kart basligina gore gruplu, her grup ilgili karta goturuyor
- Anasayfa'nin diger bolumleri, Dönem panosu, Yapılacaklar ve Ctrl+V
  yapistirma BOZULMADI
- Mevcut testlerin hepsi geciyor, sayi azalmiyor (1011 ya da uzeri)

## TESLIM
- ADIM 0'in cevaplari: uc → kart listesi, null'in gercek yeri, prompt 6
  izolasyonu var miydi, `OlusturmaZamani` var mi
- Her adim icin degisen dosyalar
- Gerekli / ikincil ayrimindan sonra ornek bir firmada sayinin kactan kaca
  dustugu
- Dogrulama maddelerinin sonucu
- Derleme ve test sonucu
- Commit atma.
