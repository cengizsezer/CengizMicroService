# PROMPT 9 — SINIFLANDIRMA KARTININ BOLUNMESI VE KULLANILAN SISTEMLER

## AMAC
Anasayfadaki "Sınıflandırma ve mizan" karti iki ayri konuyu tasiyor ve sismek
uzere. Ikiye bolunecek:

- **Sınıflandırma** — vergi ve mevzuat: firma tipi, defter usulu, vergi turu,
  hesap donemi, SGK tesvik kademesi, muhtasar donemi
- **Kullanılan sistemler** — hangi is nerede yapiliyor: muhasebe programi,
  e-Fatura entegratoru, bordro, beyanname, banka portallari, firma kodlari

Sebep: uc muhasebe programi (Luca, ORKA, Logo) ve farkli e-Fatura
entegratorleri (DijitalPlanet, TURMOB, ...) karisik kullaniliyor. "Faturayi
nereden giriyorum" sorusunun cevabi bir yerde yazili olmali.

## KURAL
- Prompt 6, 7 ve 8'de calisan hicbir sey bozulmayacak.
- Hicbir mali hesaplama degismeyecek.
- Adimlar SIRAYLA. Her adim sonunda DUR, derle, bildir.
- Commit atma.

## ADIM 0 — ONCE INCELE, kod yazmadan bildir
- "Sınıflandırma ve mizan" kartini cizen dosyalar ve sunucu tarafi.
- `Firma.MizanFormati` alani ve `MizanFormatlari.Liste` nasil tutuluyor.
- **`Firma.OrkaFirmaKodu` alanini KIM OKUYOR?** Robot (PkfRobot), bir entegrasyon
  ya da baska bir servis okuyorsa LISTELE. Bu alan tasinmayacak, sadece
  gosterildigi yer degisecek — kiminin okudugunu bilmeden karar veremeyiz.
- Yonetim sayfasinda liste/parametre yoneten bir ekran var mi, varsa deseni ne?
- Projede birden cok deger secilen (multi-select) bir bilesen kullanildi mi?
Bulduklarini yaz, sonra ADIM 1'e gec.

## ADIM 1 — VERI MODELI

### 1.1 Yeni tablo: Sistem  (ortak liste)
    Id, Ad, Tur, Aktif, OlusturmaZamani, OlusturanKullaniciId

    Tur enum: Muhasebe | EFatura | Bordro | Banka | Beyanname | Diger

Liste BUTUN FIRMALAR ICIN ORTAK. DijitalPlanet bir kez tanimlanir, her firmada
ayni kaydi gosterir; boylece "DijitalPlanet kullanan firmalarim" filtrelenebilir.
Her firmaya serbest metin yazilirsa bu imkansiz olur.

Seed: Luca, ORKA, Logo, Mikro (Muhasebe) · DijitalPlanet, TURMOB (EFatura) ·
e-Beyanname GIB (Beyanname). Baskasini UYDURMA; kullanici ekleyecek.

### 1.2 Yeni tablo: FirmaSistemi
    Id, FirmaId, SistemId, FirmaKodu (string?), Sira

Bir firmanin ayni turden birden fazla sistemi olabilir (iki banka portali).
`FirmaKodu` o sistemdeki firma kodudur (Luca'da 0417 gibi).

**ORKA firma kodu TASINMAYACAK.** `Firma.OrkaFirmaKodu` oldugu yerde kalir, tek
kaynak odur. Kartta gosterilir ve oradan duzenlenir ama veri ayni alana yazilir.
ADIM 0'da kim okuyorsa kirilmasin.

### 1.3 MIZAN FORMATI ILE PROGRAM AYNI SEY DEGIL
`Firma.MizanFormati` = "ORKA — doviz kolonlu" gibi bir MIZAN PROFILI.
Muhasebe programi = "ORKA".
Ikisi birlestirilmeyecek. MizanFormati alani AYNEN KALIR (mizan parseri okuyor).
Muhasebe sistemi secilince mizan formati icin oneri yapilabilir, zorlanmaz.

### 1.4 Firma'ya yeni alanlar
    FirmaTipi          enum: Belirsiz | SermayeSirketi | Sahis | AdiOrtaklik | IsOrtakligi
    SgkTesvikKademesi  enum: Belirsiz | Tesviksiz | ImalatDisi | Imalat
    MuhtasarDonemi     enum: Belirsiz | Aylik | UcAylik
    SistemNotu         string?   ("DijitalPlanet şifresi firma yetkilisinde" gibi)

SgkTesvikKademesi degerleri icin isveren SGK oraninin ne oldugu arayuzde
gorunsun: Tesviksiz %21,75 · Imalat disi %19,75 · Imalat %16,75. Bu oranlar
DEGISEBILIR; koda gomme, tek bir yerde sabit tut ve yaninda "oranlar degisirse
burayi guncelle" yorumu birak.

## ADIM 2 — KARTLARIN BOLUNMESI
Mevcut kart ikiye ayrilir, sirasi: Mukellefiyet → **Sınıflandırma** →
**Kullanılan sistemler** → Sicil → Ortaklik → Imza yetkilileri → ...

**Sınıflandırma karti**: firma tipi · defter usulu · vergi turu · hesap donemi ·
SGK tesvik kademesi · muhtasar donemi. Otomatik dolan alanlarin altinda
"0010'dan otomatik" notu KALIR.

**Kullanılan sistemler karti**:
- Birinci satir: muhasebe programi · e-Fatura entegratoru · bordro · beyanname ·
  banka portallari (coklu)
- Ikinci satir: ORKA firma kodu · diger sistemlerin firma kodlari · sistem notu
- Ucuncu satir: son yuklenen mizan · yukleme zamani · denge · alt kirilim ·
  "Firma kontrolünde aç" (mevcut davranis, AYNEN tasinir)

Bos olan alan "—" gosterir, kart gizlenmez.

## ADIM 3 — SISTEM EKLEME VE LISTE BAKIMI

### 3.1 Acilir listeden ekleme
Her sistem alani kendi turunun kayitlarini listeler (e-Fatura listesinde banka
cikmaz). Listenin altinda **"+ Yeni ekle"**. Yazilir, aninda listeye girer ve o
firmaya atanir. Kullanici Yonetim ekranina GITMEK ZORUNDA KALMAZ — 17 firmayi
girerken o kesinti isi birakmaya yol acar.

### 3.2 Benzer isim uyarisi — ZORUNLU
Yeni kayit eklenirken mevcut kayitlarla karsilastirilir. Karsilastirma
normalize edilmis metin uzerinden: Turkce kucultme (I/İ tuzagina dikkat),
bosluk, nokta, tire ve alt cizgi kaldirilir, buyuk/kucuk fark etmez.
Benzer bulunursa sorulur: *"DijitalPlanet zaten listede, onu mu demek
istediniz?"* — Kullanici israr ederse eklenir, ama SORMADAN GECILMEZ.

Sebep: uc hafta sonra listede "DijitalPlanet", "Dijital Planet" ve
"dijitalplanet" olursa filtreleme ve arama coker.

### 3.3 Yonetim → Sistemler ekrani
Tur bazinda liste. Yeniden adlandirma · **birlestirme** (iki kayit yanlislikla
acilmissa, birini digerine tasir ve FirmaSistemi kayitlarini gunceller) ·
pasife alma. Pasif sistem yeni secimlerde cikmaz ama mevcut atamalar bozulmaz.

Birlestirme bugun gerekmez, alti ay sonra gerekir; o zaman 17 firmayi elle
duzeltmek istemezsin.

## ADIM 4 — FIRMA ISLERI ILE BAGLANTI
Prompt 8'de `FirmaIsi.Program` KAPALI BIR ENUM olarak yazilmisti. YANLISTI —
DijitalPlanet ve TURMOB listede yoktu, birinci haftada "Diger · serbest metin"e
duserdi.

`FirmaIsi.Program` enum'u KALDIRILACAK, yerine `SistemId` (nullable FK) gelecek.
- Mevcut Program degerleri ayni adli Sistem kayitlarina baglanir; eslesmeyen
  deger varsa yeni Sistem kaydi olusturulur ve RAPORLANIR.
- Is eklenirken program varsayilani firmanin sistemlerinden gelir; is baska
  yerde yapiliyorsa degistirilir.
- Listedeki mor program cipi ayni sekilde calisir.

## CSS
Yeni siniflar `an-` onekiyle devam. Mevcut `ys-`, `an-`, `bl-`, `or-`, `hs-`,
`et-`, `gg-`, `dy-`, `fk-mizan-*` siniflarina dokunma.

## DOGRULAMA
- Anasayfada iki ayri kart goruluyor, alan dagilimi yukaridaki gibi
- Bir firmaya e-Fatura entegratoru olarak DijitalPlanet atanabiliyor
- "+ Yeni ekle" ile "Uyumsoft" eklenip aninda ataniyor
- "Dijital Planet" eklenmeye calisilinca benzer isim uyarisi cikiyor
- Ayni firmaya iki banka portali atanabiliyor
- Yonetim → Sistemler'de iki kayit birlestirilince firma atamalari dogru kayiyor
- Pasife alinan sistem yeni secimde cikmiyor, mevcut atama bozulmuyor
- `Firma.OrkaFirmaKodu` degeri ve onu okuyan her yer CALISMAYA DEVAM EDIYOR
- `Firma.MizanFormati` degismedi, mizan yukleme eskisi gibi calisiyor
- Firma isleri listesindeki program cipi hala cikiyor, mevcut isler bozulmadi
- Mevcut testlerin hepsi geciyor, sayi azalmiyor

## TESLIM
- ADIM 0'in cevaplari, ozellikle OrkaFirmaKodu'nu kimin okudugu
- Her adim icin degisen dosyalar ve migration adi
- Program enum'undan SistemId'ye tasimada eslesmeyen deger cikti mi
- Dogrulama maddelerinin sonucu
- Derleme ve test sonucu
- Commit atma.
