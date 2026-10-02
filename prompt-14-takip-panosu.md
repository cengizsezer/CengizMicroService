# PROMPT 14 — TAKIP PANOSU: SEKMELI TEK SAYFA

## AMAC
Mevcut **Dönem panosu** sayfasi, Excel calisma kitabi gibi SEKMELI tek bir
takip panosuna donusecek. Yeni sayfa ACILMAYACAK, sol menude tek madde kalacak,
adi **Dönem panosu** olarak KALACAK.

Sekmeler:

    Özet · Haziran 2026 · Temmuz 2026 · Ağustos 2026 · Eylül 2026 · Ekim 2026 │ Nasıl yapılır · Kişiler ve mailler

- **Özet**: satir firma, kolon DONEM. Hucre "5/6" orani.
- **Donem sekmeleri**: satir firma × is. Tiklanir, isaretlenir, not ve kanit eklenir.
- **Nasıl yapılır**: isin tarifi, FIRMA BAZINDA DEGIL IS BAZINDA.
- **Kişiler ve mailler**: kim, hangi e-posta, hangi is, hangi gun.

Tasarim panosundaki M, N, O, P tablolari bu sayfanin gorunumudur.

## KURAL
- Prompt 6–13'te calisan hicbir sey bozulmayacak.
- **ANASAYFAYA DOKUNULMAYACAK.** Firma isleri karti, Ctrl+V yapistirma, bos
  sablon ve kopyalama oldugu gibi kalir.
- **Yapılacaklar sayfasi KALIR.** O VADE merkezli ("son gun ne zaman"), bu sayfa
  DONEM merkezli ("Agustos kapandi mi"). Ikisi ayri amac, birlestirilmeyecek.
- Hicbir mali hesaplama degismeyecek.
- Donem gecerlilik mantigi prompt 10'da bulunan MEVCUT kodla ayni olacak;
  ikinci bir donem hesabi yazilmayacak.
- Beyanname tarihleri koda GOMULMEYECEK; vergi takviminden okunacak.
- Sifre, kullanici adi, erisim bilgisi TUTULMAYACAK. Bu sayfa kim/nereye/ne
  zaman tutar.
- Adimlar SIRAYLA. Her adim sonunda DUR, derle, bildir.
- Commit atma.

## ADIM 0 — ONCE INCELE, kod yazmadan bildir
- Mevcut Dönem panosu sayfasinin dosyalari ve servisi; matris hangi metottan
  uretiliyor.
- `IsTamamlama` tam alan listesi (prompt 10 ve 10B sonrasi), `DonemAnahtari`
  formatlari ve bir isin bir donemde gecerli olup olmadigini soyleyen metot.
- `FirmaIsi` tam alan listesi: `NasilYapilir`, `MenuYolu`, `SistemId`, `OnAdimi`
  ve `FirmaIsiAlicisi` yapisi (prompt 8 ve 9 sonrasi).
- `FirmaIsiEki` ve `IsTamamlamaEki` yukleme/silme uclari.
- Is ekleme dialogu hangi bilesen ve hangi uc.
- Projede sekmeli (tab) bir ekran deseni VAR MI? Varsa DESENINI CIKAR, ikinci
  bir sekme deseni icat edilmeyecek.
Bulduklarini yaz, sonra ADIM 1'e gec.

## ADIM 1 — SEKME SERIDI VE OZET SHEET

### 1.1 Sekme seridi
- Sayfanin ALTINDA, Excel gibi. Aktif sekme beyaz, ustunde ince renk serit.
- **Donem sekmeleri TURETILIR, koda yazilmaz**: icinde bulunulan donem ve
  ONCEKI BES donem. Kac donem gosterildigi tek bir sabitte dursun.
- Donem sekmesinde EKSIK varsa sekmenin uzerinde kucuk sayi rozeti: amber
  (1–3 eksik) ya da kirmizi (4+). Sinir degerleri tek yerde.
- Varsayilan acilan sekme: **bir onceki donem**. Icinde bulunulan ayin isi
  genelde bitmemistir.
- Secilen sekme URL'de tasinsin (`?donem=2026-09`), geri tusu calissin.

### 1.2 Özet sheet
- Satir firma, kolon donem (sekmelerdeki ayni donemler), hucre "5/6" cipi.
- Cip rengi: tam yesil · eksik az amber · eksik cok kirmizi. Esikler ADIM 1.1
  ile AYNI sabitten gelsin, ikinci bir esik tanimlama.
- Hucreye tiklayinca o donemin sekmesi O FIRMAYA FILTRELI acilir.
- Sagda **Sorumlu** kolonu.
- Ust serit uc kutu:
  1. secili donemin tamamlanma orani ve cubugu
  2. eksikler hangi iste yogunlasiyor ("Fatura girişi 3 firmada · ...")
  3. **KAPANMAMIS ONCEKI DONEM** — "Ağustos hâlâ açık · 3 firmada eksik".
     Bu kutu ZORUNLU: Eylul'e bakarken Agustos'un acik oldugu gorunmezse
     Agustos sessizce kapanir.
- "Sadece eksikler" filtresi.

### 1.3 Mevcut matris
Prompt 10'un firma × is matrisi KAYBOLMUYOR; donem sekmesinin ICINDE
yasamaya devam ediyor (ADIM 2). Özet sheet onun yerine gecmez, yanina gelir.

## ADIM 2 — DONEM SHEET'I

### 2.1 Satir listesi
Satir = firma × is. Kolonlar:

    [tik] · Firma · İş · Program · Kime gider · Son gün · Not/ek

- Isaretli satir: uzeri cizili, soluk, sayfanin altina DUSMEZ — yeri degismez.
  Siralama her tikte zipladigi icin yer degistirme yanlis olur.
- Program mor cip (prompt 9 `SistemId`). Sistem yoksa kesikli cerceveli
  "program yok" cipi.
- Yasal is mavi **"yasal"** cipi tasir; adi ve tarihi KILITLI, yalniz isaretlenir.
- "Not/ek" kolonu: o donemde not ya da ek varsa rozet. Prompt 10B'deki
  "yapilmadi · notu var" ayrimi KORUNUR.
- Filtreler: Sadece eksikler · Firmaya gore grupla.
- **"+ İş ekle"**: MEVCUT is ekleme dialogunu acar, once firma sorar. Yeni bir
  ekleme yolu DEGILDIR, ayni uca ikinci bir giristir. Ikinci bir ekleme servisi
  yazilmayacak.

### 2.2 Sag panel
Satira tiklayinca sagda panel acilir. Prompt 10'daki hucre panelinin AYNI
mantigi; yeniden yazilmayacak, tasinacak.

Icerik sirasiyla:
1. Baslik: firma · donem · is adi + program, alici, son gun cipleri
2. **Bu dönem yapıldı olarak işaretle**
3. **Bu döneme not** — isaret olmadan da yazilabilir (prompt 10B)
4. **Ctrl+V ile kanıt yapıştır** — gorsel yapistirma + dosya secme
   (xlsx, pdf, png, jpg, docx, csv). Mevcut sinirlar AYNEN.
5. **Geçen dönem** blogu — ZORUNLU. Onceki donemin notu ve ekleri.
   Kayit yoksa blok CIKMAZ.
6. Altta "Nasıl yapılır" baglantisi (ADIM 3'teki sekmeye gider) ve Kaydet.

### 2.3 Degismeyecekler
- Isaret kaldirilinca not ve kanit SILINMEZ (prompt 10B).
- Pasif takvim satiri gecmis donemde hucre uretmeye devam eder (prompt 10B).
- Ust serit sayilari matristeki durumlarla tutar.

## ADIM 3 — NASIL YAPILIR SHEET'I

### 3.1 Sorun
Bugun `NasilYapilir`, `MenuYolu` ve program `FirmaIsi` uzerinde, yani FIRMA
BAZINDA. "Bordro" tarifi dort firmada dort kez yaziliyor; biri guncellenince
digerleri eskide kaliyor.

### 3.2 Yeni tablo: IsTarifi
    Id, Ad, SistemId (nullable), MenuYolu, NasilYapilir, OlusturmaZamani

`FirmaIsi`'ye **`IsTarifiId` (nullable FK)** eklenir.

- `FirmaIsi.NasilYapilir` ve `MenuYolu` alanlari **SILINMEZ**. Firmaya ozel
  istisna olarak kalirlar. Panelde once tarif gosterilir, firmaya ozel metin
  varsa "Bu firmada farklı" basligiyla ALTINDA gosterilir.
- `FirmaIsiEki` (prosedur eki) tarife TASINABILIR; tasima bu promptta
  YAPILMAYACAK, yalnizca tarif ekleri icin ayni dosya deposu kullanilacak.

### 3.3 Mevcut veriden tarif uretimi
- Isler basliga gore gruplanir. Normalize karsilastirma: Turkce kucultme
  (I/İ tuzagina dikkat), bosluk ve noktalama kaldirilir.
- Bir grupta en az bir `NasilYapilir` ya da `MenuYolu` dolu kayit varsa, EN DOLU
  olanindan bir `IsTarifi` uretilir ve gruptaki butun `FirmaIsi` kayitlari ona
  baglanir.
- Hicbiri dolu degilse tarif uretilmez; is "tarif yok" olarak gorunur.
- **Catisma raporlanir**: ayni baslikta FARKLI `NasilYapilir` metni olan gruplar
  listelenir. Otomatik birlestirme YAPILMAZ, kullanici karar verir.

### 3.4 Sheet gorunumu
- Sol liste: is adi · ritmi · kac firmada gecerli · kac eki var.
  Tarifi olmayan is kesikli **"tarif yok"** cipi tasir.
- Sagda dort bolum: **Nerede yapılır** (program + menu yolu, tek satirlik kod
  bicimi) · **Kime gider** (Kime/Bilgi ayrimiyla) · **Nasıl yapılır** (numarali
  adimlar) · **Ekler**.
- Ekler bolumunun altinda ZORUNLU uyari serid: *"Bu bölüm döneme ait değil.
  Buradaki ekler her ay aynıdır. O ayın çıktısı dönem sekmesine eklenir."*
- Arama kutusu.

### 3.5 Iki ek turu
`FirmaIsiEki` = PROSEDUR eki, kalicidir, her donem ayni.
`IsTamamlamaEki` = DONEM eki, o aya aittir, kanittir.
AYNI dosya deposunu kullanirlar, AYRI kayitlardir, AYRI ekranlarda dururlar.
Birlestirilmeyecek.

## ADIM 4 — KISILER VE MAILLER SHEET'I

### 4.1 Yeni tablo: Kisi
    Id, FirmaId, Ad, Eposta, Rol, Notu, Aktif

`FirmaIsiAlicisi` serbest ad/e-posta tutmak yerine **`KisiId` (FK)** tutacak.

- Mevcut alici kayitlarindan Kisi uretilir: ayni firmada ayni e-posta TEK kisi
  olur. E-postasi bos olanlar ada gore eslesir.
- Eslesmeyen ya da supheli kayitlar RAPORLANIR, otomatik birlestirilmez.
- `FirmaIsiAlicisi` uzerindeki Kime/Bilgi ayrimi KALIR.

Sebep: ayni kisi birden cok iste alicidir ama e-postasi tek yerde durmali.
Adresi degisince bagli isler birden duzelmeli. Her ise serbest metin yazilirsa
uc ay sonra ayni kisinin uc yazimi olur ve aranamaz.

### 4.2 Sheet gorunumu
- Tablo: Firma · Kişi · E-posta · Hangi iş · Ne zaman · Rol (Kime/Bilgi).
- **Alicisi girilmemis is ACIKCA gorunur** — amber "eksik" cipi. Gizlenmez,
  bos birakmak da bilgidir.
- Arama: firma, kisi ve e-posta uzerinde.
- "+ Kişi ekle".

### 4.3 Ayin mail takvimi — sag kolon
- Gun gun: hangi gun hangi is, kime. "25 · Bordro · J. van Dijk, R. de Vries ·
  4 firma".
- **TURETILIR**, elle doldurulmaz. Kaynagi `FirmaIsi` gun kurali + alicilar.
  Bir isin gunu degisirse takvim kendiliginden kayar.
- Altinda "Alıcısı olmayan işler" kutusu.

## CSS
Yeni siniflar `dp-` onekiyle devam. `ys-`, `an-`, `bl-`, `or-`, `hs-`, `et-`,
`gg-`, `dy-`, `fk-mizan-*` siniflarina DOKUNMA.

## DOGRULAMA
- Sol menude tek madde var, adi Dönem panosu; ikinci sayfa acilmadi
- Alt sekme seridi goruluyor; donem sekmeleri turetilmis, koda yazilmamis
- Varsayilan sekme bir onceki donem; `?donem=2026-09` ile acilis calisiyor
- Eksigi olan donem sekmesinde sayi rozeti var
- Özet sheet'te hucreye tiklayinca o donem o firmaya filtreli aciliyor
- "Kapanmamis onceki donem" kutusu goruluyor
- Donem sekmesinde satir tiklenip isaretleniyor, satirin YERI DEGISMIYOR
- Isaretsiz satira not yaziliyor ve kaniti yapistiriliyor
- "Geçen dönem" blogu onceki donemde kayit varsa doluyor, yoksa cikmiyor
- "+ İş ekle" mevcut dialogu aciyor, ikinci bir ekleme servisi yok
- Nasıl yapılır sheet'inde tarif IS BAZINDA listeleniyor
- Ayni baslikli isler tek tarife baglandi; catisanlar raporlandi
- Firmaya ozel `NasilYapilir` metni kaybolmadi, "Bu firmada farklı" olarak duruyor
- Kişiler sheet'inde ayni e-posta tek kisi olarak goruluyor
- Alicisi olmayan is amber "eksik" ile goruluyor
- Mail takvimi is kayitlarindan turetiliyor; bir isin gunu degisince kayiyor
- Anasayfa, firma isleri karti, Ctrl+V yapistirma ve Yapılacaklar BOZULMADI
- Mevcut testlerin hepsi geciyor, sayi azalmiyor

## TESLIM
- ADIM 0'in cevaplari, ozellikle mevcut sekme deseni var miydi
- Her adim icin degisen dosyalar ve migration adi
- Tarif uretiminde kac grup olustu, kac catisma raporlandi
- Kisi uretiminde kac alici kaydi kac kisiye indi, kac supheli eslesme cikti
- Dogrulama maddelerinin sonucu
- Derleme ve test sonucu
- Commit atma.
