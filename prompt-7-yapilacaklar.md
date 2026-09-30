# PROMPT 7 — FIRMA ISLERI VE YAPILACAKLAR EKRANI

## AMAC
Firmaya ait tekrarlayan isleri tutmak ve butun firmalardaki isleri tek ekranda
toplamak. Iki yeni yer:
1. Firma kartinda "Firma işleri" ve "Kalıcı talimatlar" (anasayfa, secili firma)
2. Sol menude yeni sayfa: "Yapılacaklar" (butun firmalar, firma ustu)

## ONKOSUL
PROMPT 6 BITMIS OLMALI (FirmaNotu, FirmaOlayKaydi, SorumluKullaniciId).
Bitmediyse: "Bende" filtresini ve sorumlu alanini atla, gerisini yap, eksigi bildir.

## KURAL
- Hicbir mali hesaplama degismeyecek.
- Beyannameler ekrani BOZULMAYACAK. Bu is onun yerine gecmez, ustune okur.
- E-POSTA GONDERME BU PROMPTUN KAPSAMINDA DEGIL. Sistem hatirlatir, gondermez.
  "Yönetim raporu e-postası" bir IS baslidir; uygulama mail atmaz.
- Adimlar SIRAYLA. Her adim sonunda DUR, derle, bildir.
- Commit atma.

## ADIM 0 — ONCE INCELE, kod yazmadan bildir

### 0.1 Beyannameler ekrani — en kritik soru
- Ekranin ve bilesenlerinin tam yolu
- Hangi tabloyu okuyor? Beyanname kaydi tutuluyor mu, yoksa sadece liste mi?
- Firma + donem bazinda "verildi / verilmedi" durumu tutuluyor mu?
- Son gun / vade tarihi tutuluyor mu, tutuluyorsa NEREDEN geliyor: veritabani mi,
  kod icinde sabit mi, hesaplaniyor mu?
- Mukellefiyet turlerine gore hangi beyannamenin gerektigini belirleyen bir
  mantik var mi?

### 0.2 Digerleri
- Sol menunun tanimlandigi dosya ve menu maddesi ekleme sekli
- Anasayfa bileseninin tam yolu (prompt 6 ile degismis olabilir)
- Mukellefiyet turleri: kod ve ad ayri mi tutuluyor, yoksa tek metin mi?
- Kullanici tablosu ve oturumdaki kullaniciya nereden erisiliyor?
- Uygulamada bugunun tarihi nereden okunuyor: dogrudan DateTime.Now cagrilari mi,
  yoksa enjekte edilen bir saat mi?
Bulduklarini yaz, sonra ADIM 1'e gec.

### 0.3 Iki yol
- **Beyannameler zaten firma + donem + durum tutuyorsa**: yasal isler ORADAN
  okunacak. Yeni yasal is kaydi URETME, yeni vade tarihi URETME. Yapilacaklar
  ekrani o veriyi gosterir ve isaretleme ayni tabloya yazar.
- **Tutmuyorsa**: madde 1.2'deki VergiTakvimi tablosu kurulacak.
Hangi yolda oldugunu ADIM 0 raporunda net yaz. Ikisini birden yapma.

## ADIM 1 — VERI MODELI

### 1.1 FirmaIsi — firmaya ozel is tanimi
    Id, FirmaId, Baslik, Aciklama
    Tekrar        enum: TekSefer | Aylik | UcAylik | Yillik
    GunKurali     enum: AyinGunu | AySonu | DonemSonu
    AyinGunu      int?     (GunKurali = AyinGunu ise 1-31)
    TekSeferTarih date?    (Tekrar = TekSefer ise)
    SorumluKullaniciId int?   (bos ise firmanin sorumlusu)
    Aktif         bool
    OlusturanKullaniciId, OlusturmaZamani

AyinGunu 31 iken 30 ceken ayda son gune duser, subatta 28/29'a duser.

### 1.2 VergiTakvimi — yalnizca ADIM 0.3 ikinci yolda
    Id, MukellefiyetKodu (0015, 0003, 0033, 0010, 0040)
    Ad, Tekrar, Yil, DonemNo, DonemBas, DonemBit, SonGun, Aktif

**TARIHLER KODA GOMULMEYECEK.** Yasal sureler yil icinde teblig ile degisiyor ve
uzatiliyor; gomulu tarih bir yil sonra sessizce yanlis olur. Tarihler bu tabloda
durur, Yonetim ekranindan duzenlenir.

Seed verisi 2026 ve 2027 icin girilecek AMA: seed'i girdikten sonra kullaniciya
"su tarihleri koydum, yururlukteki takvimle dogrula" diye LISTELE. Onaylanmadan
dogru sayma.

### 1.3 IsTamamlama — tek tablo, hem yasal hem ozel
    Id
    KaynakTip      enum: Yasal | Ozel
    KaynakId       int      (VergiTakvimi.Id veya FirmaIsi.Id)
    FirmaId
    DonemAnahtari  string   ("2026-09", "2026-Q3", "2026")
    TamamlanmaZamani, KullaniciId

Benzersiz indeks: (KaynakTip, KaynakId, FirmaId, DonemAnahtari).
Isaret kaldirilinca satir SILINIR. Boylece hem "bu donem yapildi mi" hem
"gecen yil hangi ay yapmisim" ayni tablodan cikar.

### 1.4 Kalici talimat — YENI TABLO ACMA
Prompt 6'daki `FirmaNotu` tablosu bu isi goruyor. Yeni tablo kurma; kartin
basligi "Kalıcı talimatlar" olacak, veri FirmaNotu'ndan gelecek.

### 1.5 Yasal isler TURETILIR, SAKLANMAZ
Yasal is kaydi veritabanina yazilmaz. Her acilista hesaplanir:
firmanin mukellefiyet kodlari x ilgili takvim satirlari. Yalnizca TAMAMLAMA
kaydi saklanir.
Sebep: mukellefiyet degisince (firma KDV'den cikinca) liste kendiliginden
duzelir; yazilmis kayitlari temizlemek gerekmez.

### 1.6 Bugunun tarihi
Durum hesabi `DateTime.Now`'i DOGRUDAN CAGIRMAYACAK. Enjekte edilebilir bir saat
(`ISaatServisi` / `TimeProvider`) uzerinden okunacak ki testler sabit tarihle
calissin. Projede daha once gercek saate bagli testler kirilmisti; ayni hataya
dusme.

## ADIM 2 — FIRMA KARTI (tasarim F)
Anasayfada, prompt 6'daki Takip kartinin ALTINA iki kart.

### 2.1 Firma işleri
Iki gruplu tek liste:
- **Yasal — mükellefiyetten türetildi**: satirlar KILITLI. Ad ve tarih
  degistirilemez, silinemez. Yalnizca yapildi isaretlenir. Grup basliginda
  "tarihler Vergi takviminden gelir, elle değiştirilmez" notu.
- **Firmaya özel — senin eklediklerin**: tam duzenlenebilir, silinebilir.

Satir: onay kutusu | baslik + alt satirda (kaynak · tekrar · donem ya da son
yapilma) | son gun | durum rozeti.

Kart basliginda gecikmis ve bu ay sayilari rozet olarak.
"+ İş ekle" yalnizca ozel is ekler.

### 2.2 Isaretleme davranisi
Isaretlenen is LISTEDEN KAYBOLMAZ. Ustu cizilir, yesil "bu dönem yapıldı"
rozeti alir ve bir sonraki donemin satiri gorunur. Isaret kaldirilabilir.

### 2.3 Kalıcı talimatlar
Ayri kart. Madde isaretli duz liste. Tarih yok, onay kutusu yok, durum yok.
Kartin altinda tek satir: "Talimatın tarihi ve yapıldı hali yoktur — tarihi olan
bir şey yazacaksan yukarıya iş olarak ekle." Bu ayrim korunacak; talimata tarih
alani EKLEME.

## ADIM 3 — YAPILACAKLAR EKRANI (tasarim G)

### 3.1 Menu
Sol menude **Anasayfa'nin hemen altina** "Yapılacaklar". Beyannameler maddesi
oldugu yerde kalir, icerigi degismez.

### 3.2 Ekran
Firma ustu: secili firmadan BAGIMSIZ, butun firmalari gosterir.
Gruplama ACILIGA gore, firmaya gore DEGIL:
- **Gecikti** (son gun gecmis, tamamlanmamis) — kirmizi kart
- **Bu hafta** (bugun + 7 gun) — amber kart
- **Bu ay** — notr kart
- Gerisi "Sonra" basligi altinda katlanmis

Bos grup HIC CIZILMEZ.

Filtreler: Tümü · Bende · Yasal · Firmaya özel.

### 3.3 AYNI IS TEK SATIR — zorunlu
Ayni yasal is (ayni ad, ayni son gun) birden fazla firmada varsa TEK SATIR
olarak cizilir: firma kolonunda "9 firma", acilinca firma firma isaretlenir.
Tek satirdaki onay kutusu HEPSINI isaretler ve once onay sorar.

Firmaya ozel isler de ayni basligi ve ayni gunu tasiyorsa ayni sekilde gruplanir.

Bu olmadan liste ayni isin tekrarindan okunmaz hale gelir: 25 firma x 5 yasal is
= 125 satir.

### 3.4 Anasayfa seridi
Anasayfanin en ustune, firma kunyesinin USTUNE tek satir:
"2 gecikmiş iş · 5 bu hafta — bütün firmalar" + "Yapılacaklar →" baglantisi.
- Secili firmadan bagimsiz oldugu satirin ICINDE yazili olacak.
- GECIKMIS IS YOKSA SERIT HIC CIKMAZ. Her gun duran uyari uc gunde gorunmez olur.

### 3.5 Durum hesabi
    tamamlama kaydi var                       -> Tamam
    son gun < bugun                           -> Gecikti (kac gun)
    son gun <= bugun + 7                      -> Bu hafta (kac gun var)
    son gun ayni ay icinde                    -> Bu ay
    digerleri                                 -> Sonra
Renk tek basina anlam tasimasin: her rozette metin de yazsin.

## CSS
Yeni siniflar `ys-` onekiyle. `an-`, `bl-`, `or-`, `hs-`, `et-`, `gg-`, `dy-`,
`fk-mizan-*` siniflarina dokunma.

## DOGRULAMA (DGR, firmaId 3)
- DGR mukellefiyet kodlari: 0003, 0010, 0015, 0033, 0040
- Bu kodlardan turetilen yasal is sayisi ve adlari raporda LISTELENSIN
- Ayni firmaya "Yönetim raporu e-postası · her ay · ayın 1'i" isi eklenince:
  bugun 28.09.2026 oldugu icin 01.09.2026 tamamlanmamissa GECIKTI cikmali
- Isaretlenince ustu cizilmeli, 01.10.2026 satiri gorunmeli
- Isaret kaldirilinca tekrar GECIKTI'ye donmeli
- Ayin 31'i kurali ile eklenen is, 30 ceken ayda 30'a, subatta 28'e dusmeli
  (birim test yaz)
- Yapilacaklar ekraninda ayni yasal isin 9 firmasi TEK SATIR gorunmeli
- Gecikmis is yokken anasayfa seridi CIKMAMALI
- Sabit tarihli test: saat servisine 28.09.2026 verilerek yukaridakiler
  dogrulanmali, gercek saate bagli test YAZILMAYACAK

Beyannameler ekraninda HICBIR SEY degismemeli. Bilanço, Gelir Tablosu, Vergi,
Finansal Oranlar, Dikey Yüzdeler, Etiketler, Grup Görünümü HICBIR rakam
degismemeli.

## TESLIM
- ADIM 0'in cevaplari ve 0.3'te hangi yolun secildigi
- Her adim icin eklenen/degisen dosyalar ve migration adi
- Turetilen yasal islerin listesi
- Seed edilen vergi takvimi satirlari (kullanici dogrulayacak)
- Dogrulama maddelerinin sonucu
- Derleme ve test sonucu
- Commit atma.
