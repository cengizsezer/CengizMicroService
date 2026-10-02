# PROMPT 15 — IS TAKIP PANOSU: ADI, YERI VE ICINDEN EKLEME

## AMAC
Pano calisiyor ama is EKLEME hala yalniz Anasayfa'daki kartta. Kullanici
panoda duruyor ve "bu isi nereden ekleyecegim" diye soruyor. Ayrica
"Nasıl yapılır" sekmesinde tarif yazma ve alici ekleme yolu gorunmuyor.

Uc sey:
1. Sayfanin adi **İş Takip Panosu** olacak, sol menude **Anasayfa'nin hemen
   altina** tasinacak.
2. Is ekleme, Ctrl+V toplu ekleme ve baska firmadan kopyalama **bu sayfanin
   icinden** yapilabilecek.
3. "Nasıl yapılır" sekmesinde tarif yazma, duzenleme ve alici ekleme
   GORUNUR olacak.

## KURAL
- Prompt 6–14B'de calisan hicbir sey bozulmayacak.
- **Anasayfa'daki firma isleri karti KALACAK.** Kaldirilmayacak, degismeyecek.
  Oradan da eklenebilmeye devam edecek.
- **IKINCI BIR EKLEME SERVISI YAZILMAYACAK.** Panodaki dugmeler MEVCUT
  dialoglari ve MEVCUT uclari cagiracak. Yeni uc, yeni servis, yeni ayristirici
  YOK.
- Yeni tablo, yeni alan, migration GEREKMIYOR.
- Hicbir mali hesaplama degismeyecek.
- Adimlar SIRAYLA. Her adim sonunda DUR, derle, bildir.
- Commit atma.

## ADIM 0 — ONCE INCELE, kod yazmadan bildir
- Sol menu hangi dosyada tanimli, siralama nereden geliyor?
- Sayfanin route'u ne (`/donem-panosu`)? Degistirirsek eski adrese gelen
  baglanti ne olur? Prompt 13'te Yapılacaklar'dan verdigimiz baglanti ve
  prompt 14'teki `?sekme=nasil&is=...` baglantilari nereye bakiyor?
- **Prompt 14 ADIM 3'te "Tarif oluştur" dugmesi eklendigi raporlanmisti.
  Ekranda GORUNMUYOR.** Dugme kodda var mi? Varsa hangi kosulda ciziliyor,
  neden cikmiyor? Ekran goruntusunde "Hiçbir firmada tarif yazılmamış." ve
  "Tarif olmadan ek eklenemez." yaziyor ama hicbir dugme yok.
- "Kime gider" bolumunde "alıcı girilmemiş" yaziyor; alici eklemenin yolu
  var mi?
- Firma isleri kartindaki uc eylem (+ İş ekle, Yapıştır, Boş şablon indir,
  Başka firmadan kopyala) hangi bilesen ve hangi parametreleri istiyor?
  Panodan cagrilabilir mi, yoksa karta mi bagli?
Bulduklarini yaz, sonra ADIM 1'e gec.

## ADIM 1 — AD VE MENU YERI

- Sol menude madde adi: **İş Takip Panosu**, **Anasayfa'nin hemen altinda**.
  Yeni sira: Anasayfa · **İş Takip Panosu** · Yapılacaklar · Şirket · ...
- Sayfa basligi da **İş Takip Panosu** olacak. Alt baslik kalabilir.
- Route **degistirilebilir** (`/is-takip-panosu`), ama eski `/donem-panosu`
  adresi YENI ADRESE YONLENDIRECEK. Sorgu parametreleri (`?donem=`, `?firma=`,
  `?sekme=`, `?is=`, `?kisi=`) KORUNACAK.
- Uygulamadaki butun ic baglantilar yeni adrese cevrilecek; kirik baglanti
  kalmayacak. Prompt 13'teki Yapılacaklar baglantisi ve prompt 14'teki
  "Nasıl yapılır" baglantisi dahil.

## ADIM 2 — FIRMA SECIMI VE EKLEME PANONUN ICINDEN

### 2.0 FIRMA SECICI — sayfanin kalici parcasi
Bugun firma yalnizca Ozet'ten hucreye tiklayinca gelen bir FILTRE. Kullanici
"firma seceyim, o firmanin aylik islerini takip edeyim" diyor. Secim sayfanin
UST SERIDINDE kalici olacak:

    [Firma: ▼ Bütün firmalar / AITEX TURKEY / CARRIERE / ...]

- Varsayilan **Bütün firmalar**.
- Secim BUTUN SEKMELERDE gecerli: Ozet, donem sekmeleri, Nasıl yapılır,
  Kişiler. Sekme degistirince secim KAYBOLMAZ.
- Adreste tasinir (`?firma=12`), geri tusu calisir, baglanti paylasilabilir.
- Secim varken:
  - Donem sekmesi yalniz o firmanin satirlarini gosterir
  - Ust seridin sayilari yalniz o firmayi sayar (prompt 14 ADIM 1 duzeltmesi)
  - Ozet sheet'i o firmanin satirini one alir, digerleri soluk kalir
  - Nasıl yapılır o firmada gecerli isleri listeler, "Kime gider"de o firma
    one alinir
  - Kişiler o firmanin kisilerini gosterir
- Yanindaki × ile "Bütün firmalar"a doner.
- **Anasayfa'daki firma secimiyle KARISTIRILMAYACAK**; bu sayfanin kendi
  secimidir, Anasayfa'nin secili firmasini DEGISTIRMEZ.

### 2.1 Ust seritte eylemler — HER SEKMEDE
Ozet, donem sekmeleri ve diger sekmelerin ustunde ayni eylem grubu olacak:

    [+ İş ekle]  [Yapıştır]  [Boş şablon indir]  [Başka firmadan kopyala]

- Hepsi MEVCUT bilesenleri cagiracak: `FirmaIsiDialog`, `TopluIsDialog`,
  `SablonIndirBaglantisi`, `IsKopyalaDialog`.
- **Firma secimi**: 2.0'daki secici doluysa eylem firmayi SORMAZ, o firmaya
  ekler. "Bütün firmalar" modundaysa once sorar; prompt 13'te kurulan
  `DonemFirmaSecDialog` kullanilacak, yenisi yazilmayacak.
  - Vazgecilirse hicbir sey olmaz.
- Ekleme bitince acik olan sekme **kendini tazeler**; kullanici sayfayi
  yenilemek zorunda kalmaz.
- Eklenen is o donemde gecerliyse hemen listede gorunur.

### 2.2 Ctrl+V dogrudan panoda
- Donem sekmesi odaktayken **Ctrl+V** ayni toplu ekleme onizlemesini acar
  (prompt 11'deki davranisin aynisi, ayni ayristirici).
- Firma secili degilse once firma sorar.
- Yapistirma panosu izni yoksa dialog elle yapistirma kutusuyla acilir.

### 2.3 Bos liste metni
Donem sekmesinde hic satir yoksa:
"Bu dönemde iş yok. **+ İş ekle** ile ekleyebilir ya da Excel'den **Ctrl+V**
ile yapıştırabilirsin."
Ozet sheet'i bos ise benzer bir cumle ve ayni dugmeler.

## ADIM 3 — NASIL YAPILIR SEKMESINDE YAZMA VE DUZENLEME

Ekran goruntusunde bu sekme TAMAMEN OKUNUR gorunuyor. Yazma yolu yok.

### 3.1 Tarif yazma
- Tarifi olmayan iste, "NASIL YAPILIR" bolumunun yerinde net bir dugme:
  **"+ Tarif yaz"**. Bugunku "Hiçbir firmada tarif yazılmamış." cumlesi
  aciklama olarak KALIR, ama yanina dugme gelir.
- Tarifi olan iste her bolumun basliginin yaninda **"Düzenle"**:
  - **Nerede yapılır**: program (Sistem listesinden) + menu yolu
  - **Nasıl yapılır**: numarali adimlar, serbest metin
- Kaydet / Vazgec. **KAYDET ADIMI KULLANICININDIR**, otomatik kaydetme YOK.

### 3.2 Ekler
- "Tarif olmadan ek eklenemez." cumlesi DOGRU, kalsin — ama tarif olusunca
  o bolumde **"+ Ek yükle"** ve **Ctrl+V ile yapıştır** alani cikacak.
- Prosedur eki / donem eki ayrimi ve uyari seridi AYNEN kalir.

### 3.3 Kime gider
- "alıcı girilmemiş" yazan satirin yaninda **"+ Alıcı ekle"** olacak.
- Alici ekleme MEVCUT kisi akisini kullanir (prompt 14 ADIM 4 ve 14B'deki
  onizleme kurali dahil). Yeni bir alici yazma yolu ACILMAYACAK.
- Alici eklenince satir hemen guncellenir.

### 3.4 Bos liste
Hic is yoksa sol listede: "Henüz iş yok. İş ekleyince tarifi buradan
yazabilirsin." + "+ İş ekle" dugmesi.

## CSS
Mevcut `dp-` onekine devam. `ys-`, `an-`, `bl-`, `or-`, `hs-`, `et-`, `gg-`,
`dy-`, `fk-mizan-*` siniflarina DOKUNMA.

## DOGRULAMA
- Sol menude madde adi "İş Takip Panosu" ve Anasayfa'nin hemen altinda
- Eski `/donem-panosu` adresi yeni adrese yonleniyor, sorgu parametreleri
  korunuyor
- Ust seritte firma secici var, varsayilan "Bütün firmalar"
- Firma secilince secim SEKME DEGISTIRINCE KAYBOLMUYOR; dort sekmede de gecerli
- Secim adreste tasiniyor, geri tusu calisiyor
- Firma secili iken ust seridin sayilari yalniz o firmayi sayiyor
- Bu sayfadaki firma secimi Anasayfa'nin secili firmasini DEGISTIRMIYOR
- Uygulamada kirik baglanti kalmadi
- Ozet sheet'inden "+ İş ekle" ile is eklenebiliyor; once firma soruyor
- Donem sekmesinde firma filtresi acikken firma SORULMUYOR, o firmaya ekliyor
- Donem sekmesinde Ctrl+V toplu ekleme onizlemesini aciyor
- "Boş şablon indir" ve "Başka firmadan kopyala" panodan calisiyor
- Ekleme bitince sekme kendini tazeliyor, yeni is listede goruluyor
- Bos donem sekmesinde ekleme metni ve dugmeler goruluyor
- "Nasıl yapılır" sekmesinde tarifi olmayan iste "+ Tarif yaz" GORUNUYOR
- Tarif yazilip kaydedilebiliyor; otomatik kaydetme yok
- Tarif olusunca ek yukleme ve Ctrl+V alani cikiyor
- "alıcı girilmemiş" yanindan alici eklenebiliyor, mevcut kisi akisi kullaniliyor
- Anasayfa'daki firma isleri karti BOZULMADI, oradan da eklenebiliyor
- Ikinci bir ekleme servisi ya da ucu ACILMADI
- Mevcut testlerin hepsi geciyor, sayi azalmiyor (1060 ya da uzeri)

## TESLIM
- ADIM 0'in cevaplari, ozellikle "Tarif oluştur" dugmesi kodda var miydi,
  yoksa neden gorunmuyordu
- Her adim icin degisen dosyalar
- Yeni ve eski route, yonlendirmenin nasil yapildigi
- Dogrulama maddelerinin sonucu
- Derleme ve test sonucu
- Commit atma.
