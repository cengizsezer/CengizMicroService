# PROMPT 8 — IS PROSEDURU: PROGRAM, ALICI, NASIL YAPILIR, EKLER

## AMAC
Firma isi su an bir satir. Bir PROSEDUR olacak: nerede yapiliyor, kime gidiyor,
nasil yapiliyor, kaniti ne. Sebep: ekip yeni, uc muhasebe programi karisik
kullaniliyor (Luca, ORKA, Logo) ve "bu is neredeydi, kime mail atiyordum"
sorusunun cevabi kimsenin kafasinda kalmamali.

Gercek ornek: CARRIERE firmasinda bordro Luca'da hazirlaniyor, ayin 25'inde
Hollanda'daki bir kisiye e-posta ile gonderiliyor. KDV hesabi ayin 15'inde
cikariliyor ve onaya gonderiliyor; ayin 28'indeki yasal KDV beyannamesinin on
adimi oluyor.

## KURAL
- Prompt 7'de calisan hicbir sey bozulmayacak. Bu is UZERINE EKLEME.
- Hicbir mali hesaplama degismeyecek.
- Adimlar SIRAYLA. Her adim sonunda DUR, derle, bildir.
- Commit atma.

## ADIM 0 — ONCE INCELE, kod yazmadan bildir
- Anasayfadaki BELGELER karti dosyalari NEREDE sakliyor? Tablo adi, dosya
  deposu (veritabani mi, disk mi, blob mu), yukleme ve indirme uclari.
  **Ikinci bir dosya deposu KURULMAYACAK**; ekler ayni mekanizmayi kullanacak.
- FirmaIsi entity'sinin su anki tam alan listesi.
- Firma kaydinda mizan formati hangi alanda tutuluyor (prompt 6'da eklendi).
- Zengin metin / cok satirli metin icin projede kullanilan bir bilesen var mi?
- Dosya boyutu ve tur kisiti var mi, nerede tanimli?
Bulduklarini yaz, sonra ADIM 1'e gec.

## ADIM 1 — VERI MODELI

### 1.1 FirmaIsi'na yeni alanlar
    Program        enum: Belirtilmemis | Luca | Orka | Logo | Mikro |
                         EBeyanname | SGK | BankaPortali | Diger
    ProgramDiger   string?   (Program = Diger ise serbest metin)
    MenuYolu       string?   ornek: "Bordro > Raporlar > Ucret Bordrosu"
    NasilYapilir   string?   cok satirli serbest metin, adimlar
    OnAdimiOlduguIsId int?   (bu is baska bir isin hazirligiysa)

Program alani Firma'nin mizan formatindan BAGIMSIZ. Firmanin mizani Luca'dan
gelebilir ama bordro baska programda tutulabilir. Varsayilan deger onermek
icin firmanin mizan formati kullanilabilir ama KILITLENMEYECEK.

### 1.2 Yeni tablo: FirmaIsiAlicisi
    Id, FirmaIsiId, AdSoyad, Eposta, Rol (string: "Finans, Hollanda" gibi),
    AliciTipi enum: Kime | Bilgi,
    Sira int

Bir isin 0..n alicisi olur. E-posta zorunlu degil (bazi islerde kisi var ama
mail gitmiyor olabilir).

### 1.3 Yeni tablo: FirmaIsiEki
    Id, FirmaIsiId, DosyaAdi, ContentType, Boyut,
    YuklemeZamani, YukleyenKullaniciId, YukleyenKullaniciAdi,
    + Belgeler kartinin kullandigi depolama referansi (ADIM 0'daki cevaba gore)

Izin verilen turler: png, jpg, pdf, xlsx, xls, docx, csv, txt.
Boyut siniri Belgeler kartiyla AYNI olacak; yeni bir sinir uydurma.

### 1.4 EK, ISE AITTIR
Ekler firma notuna ya da Belgeler kartina DEGIL, ISE baglanir. Sebep: alti ay
sonra "hangi belge hangi ise aitti" aramasi baslar. Belgeler karti firma
belgeleri (imza sirkuleri, vergi levhasi) icin kalir, degismez.

## ADIM 2 — LISTE (tasarim H)
Satir yapisi korunacak, uc isaret eklenecek:
- **Program cipi** (mor tonlu): Program alanindan. Belirtilmemisse cip CIKMAZ.
- **Alici cipi** (mavi): "N alici" ya da tek aliciysa "1 alici · <ad>".
  Alici yoksa kesikli cerceveli soluk "alici yok" cipi — bu da bilgidir.
- **Ek cipi** (yesil): "N ek". Ek yoksa cip cikmaz.
Satirin sagina **"Nasıl yapılır"** baglantisi; prosedur panelini acar.

On adim bagi: `OnAdimiOlduguIsId` doluysa satirin altinda tek satir
"→ <hedef isin adi> işinin hazırlığı".

Yasal islerde de program ve alici gosterilecek (e-Beyanname, SGK gibi). Yasal
isin adi ve tarihi kilitli kalir ama PROGRAM, ALICI, MENU YOLU, NASIL YAPILIR
ve EKLER kullanici tarafindan DOLDURULABILIR. Bunlar takvimden gelmiyor,
firmanin kendi calisma seklidir.

## ADIM 3 — PROSEDUR PANELI (tasarim I)
Sagdan acilan panel, 760px. Bes bolum, bu sirayla:

1. **Nerede yapılır** — program cipi + menu yolu tek satirda. Firmanin mizan
   formatindan farkliysa altinda kucuk gri not: "Firmanın mizan formatı <X>".
2. **Kime gider** — alici listesi: ad, rol, e-posta, Kime/Bilgi rozeti.
   "+ Alıcı ekle".
3. **Nasıl yapılır** — numarali adimlar. Metin satir satir girilir, her satir
   bir adim olur. Bos satirlar atlanir.
4. **Ekler** — dosya kartlari (ad, tur ikonu, boyut), indirme, silme.
   "+ Dosya ekle". Gorsel eklerde kucuk onizleme.
5. **Geçmiş** — son 6 donem: donem, tarih, kim, durum. Yapilmamis donem
   kirmizi. "Tüm geçmiş".

Panelin altinda sabit serit: o donemin son gunu + "Yapıldı işaretle".
Ust kisimda "Düzenle" butonu.

## ADIM 4 — IS EKLE / DUZENLE DIALOGU
Mevcut dialog genisleyecek. Alanlar sirasiyla:
Baslik · Aciklama · Tekrar · Son gun kurali · Sorumlu ·
**Program · Menü yolu · Nasıl yapılır · Alıcılar · Ön adımı olduğu iş**
Ekler dialogda DEGIL, prosedur panelinde yonetilir (dosya yukleme kaydedilmemis
bir ise baglanamaz).

Uzun dialog olacagi icin iki sekme: **Zamanlama** ve **Prosedür**.

## CSS
Yeni siniflar `ys-` onekiyle devam. Mevcut `ys-`, `an-`, `bl-`, `or-`, `hs-`,
`et-`, `gg-`, `dy-`, `fk-mizan-*` siniflarina dokunma.

## DOGRULAMA
- Bir firmaya su is eklenebiliyor: baslik "Bordroyu hazırla ve Hollanda'ya
  gönder", her ay, ayin 25'i, Program Luca, menu yolu
  "Bordro > Raporlar > Ücret Bordrosu", bir alici (Kime) ve bir alici (Bilgi),
  nasil yapilir 5 adim
- Listede mor "Luca", mavi "2 alıcı" cipleri gorunuyor
- Prosedur paneli acilinca bes bolum de dolu geliyor
- Panele png ve xlsx ekleniyor, yesil "2 ek" cipi cikiyor, dosyalar
  indirilebiliyor
- Ek silinince cip "1 ek" oluyor
- Ikinci bir is "KDV hesabını çıkar ve onaya gönder" olusturulup
  OnAdimiOlduguIs olarak yasal KDV beyannamesi secilince listede ok satiri
  cikiyor
- Yasal bir ise program ve alici eklenebiliyor; ADI ve TARIHI hala
  degistirilemiyor
- Belgeler kartindaki dosyalar ETKILENMIYOR, ayri duruyor
- Prompt 7'nin 27 testi ve toplam test sayisi azalmiyor

## TESLIM
- ADIM 0'in cevaplari, ozellikle dosya deposunun ne oldugu
- Her adim icin degisen dosyalar ve migration adi
- Dogrulama maddelerinin sonucu
- Derleme ve test sonucu
- Commit atma.
