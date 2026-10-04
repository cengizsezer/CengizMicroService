# PROMPT 17 — ISIN BASLANGIC DONEMI

## SORUN
Kullanici Is Takip Panosu'nda **Eylül 2026** sekmesinde duruyor, "+ İş ekle"
ile is giriyor. Is **Ekim**'e yaziliyor, Eylül sekmesi bos kaliyor.

Sebep: `FirmaIsi`'nin donemi yok. Tekrarlayan bir tanim ve ilk donemi
`YapilacaklarKurucu.OzelIlkDonem` tarafindan OLUSTURMA TARIHINDEN turetiliyor.
Bugun 3 Ekim oldugu icin hangi sekmede durulursa durulsun is Ekim'den basliyor.

Oysa dis muhasebede normal olan bu: **Ekim'de oturup Eylül'un isini girmek.**
Isin ilk donemi, kullanicinin durdugu sekme olmali.

## KURAL
- Prompt 6–16'da calisan hicbir sey bozulmayacak.
- Yasal islerin donem mantigi DEGISMEYECEK; onlar mukellefiyetten turuyor ve
  kendi takvimleri var. Bu prompt yalnizca FIRMAYA OZEL isler icin.
- Hicbir mali hesaplama degismeyecek.
- Adimlar SIRAYLA. Her adim sonunda DUR, derle, bildir.
- Commit atma.

## ADIM 0 — ONCE INCELE, kod yazmadan bildir
- `YapilacaklarKurucu.OzelIlkDonem` tam olarak ne yapiyor, neyi okuyor?
- `FirmaIsi`'de baslangici belirleyen HERHANGI bir alan var mi
  (`OlusturmaZamani` disinda)?
- Ilk donem hesabi kac yerde kullaniliyor: Yapılacaklar kuyrugu, Dönem panosu
  matrisi, satir listesi, Özet sheet'i — hepsi AYNI metodu mu cagiriyor?
- `TekSefer` tekrarli iste donem nasil belirleniyor (`TekSeferTarih`)?
- Is ekleme dialogu (`FirmaIsiDialog`) hangi parametreleri aliyor; panodan
  acilirken acik sekmenin donemi ona gecirilebilir mi?
- Toplu ekleme (`TopluIsDialog`) ve kopyalama (`IsKopyalaDialog`) ilk donemi
  nasil belirliyor?
Bulduklarini yaz, sonra ADIM 1'e gec.

## ADIM 1 — VERI MODELI

`FirmaIsi`'ye tek alan:

    IlkDonem  string?   — "2026-09" bicimi, DonemAnahtari ile AYNI format

- **Bos ise bugunku davranis aynen gecerli**: ilk donem `OlusturmaZamani`'ndan
  turetilir. Mevcut kayitlarin hicbiri bozulmaz, migration veri donusturmez.
- Dolu ise ilk donem O DONEMDIR; is o donemden itibaren gecerlidir, oncesinde
  hucre uretmez.
- Format dogrulamasi sunucuda; gecersiz deger reddedilir.
- Ucaylik ve yillik islerde de ayni alan kullanilir; `IlkDonem` ay olarak
  yazilir, donem gecerliligi MEVCUT `IsDonemi` mantigiyla hesaplanir.
  **Ikinci bir donem hesabi yazilmayacak.**
- `TekSefer` isinde `TekSeferTarih` ne yapiyorsa onu yapmaya devam eder;
  `IlkDonem` o tipte YOK SAYILIR.

## ADIM 2 — EKLERKEN DONEMI SECMEK

### 2.1 Dialog alani
`FirmaIsiDialog`'a gorunur bir alan: **"Başlangıç dönemi"** — ay secici.

- **Is Takip Panosu'ndan acilirsa**: acik sekmenin donemiyle ON DOLU gelir.
  Eylül sekmesindeysen "Eylül 2026" yazar.
- **Anasayfa'daki firma isleri kartindan acilirsa**: icinde bulunulan ay ile
  on dolu gelir (bugunku davranisin aynisi).
- Her iki durumda da kullanici DEGISTIREBILIR.
- Alanin altinda tek satir aciklama: "Bu iş hangi dönemden itibaren takip
  edilsin."

### 2.2 Toplu ekleme (Ctrl+V) ve kopyalama
- `TopluIsDialog`: yapistirilan butun satirlar ACIK SEKMENIN donemini alir.
  Onizleme dialogunun ust kisminda yazar: "Başlangıç dönemi: Eylül 2026" ve
  oradan degistirilebilir — satir satir degil, HEPSI icin tek deger.
- `IsKopyalaDialog`: kopyalanan isler ACIK SEKMENIN donemini alir. Kaynak
  firmadaki `IlkDonem` TASINMAZ; kopyalanan is hedef firmada secilen donemden
  baslar.
- Panodan degil Anasayfa'dan acilirsa ikisi de icinde bulunulan ayi kullanir.

### 2.3 Dogrulama
- Gecmise donuk donem SERBESTTIR; kullanici Ekim'de Eylül'un, hatta Haziran'in
  isini girebilir. Engelleme YOK.
- Gelecege donuk donem de serbesttir.
- Donem sekmelerinde gosterilen aralik disina cikan bir donem secilirse
  kaydedilir, yalnizca o sekme serit disinda kalir; ◀ ▶ ile ulasilir.

## ADIM 3 — ETKILER

### 3.1 Dönem panosu
- Is, `IlkDonem`'den itibaren her gecerli donemde hucre uretir.
- Oncesindeki donemlerde "—" (bu firmada yok) gorunur. BOS HUCRE DEGIL;
  o donemde o is gercekten yoktu.
- Özet sheet'indeki oranlar buna gore guncellenir.

### 3.2 Yapılacaklar kuyrugu
- Gecmis bir doneme yazilan isin son gunu gecmisse kuyrukta **gecikmis**
  gorunur. Bu DOGRU davranis — is gercekten gecikmis.
- Ama kullanici kapanmis bir donemi geriye donuk kaydediyor olabilir. Bu
  yuzden Dönem panosunda o donemi **"Yapıldı" isaretlemesi** kuyruktan
  dusurur; mevcut davranis budur, degistirilmeyecek.

### 3.3 Is listesi
- Firma isleri kartinda ve pano satirinda isin yaninda, `IlkDonem` dolu ve
  gecmisteyse kucuk bir not: "Eylül 2026'dan beri". Bugunku davranistaki
  isler icin bu not CIKMAZ.

## DOGRULAMA
- Eylül sekmesinde "+ İş ekle" ile eklenen is **Eylül**'de goruluyor,
  Ekim'de degil
- Dialogdaki "Başlangıç dönemi" alani acik sekmeyle on dolu geliyor ve
  degistirilebiliyor
- Anasayfa kartindan eklenen is icinde bulunulan aya yaziliyor (eski davranis)
- Eylül'den once ki donemlerde o is "—" gosteriyor, bos hucre degil
- Ctrl+V ile yapistirilan butun satirlar acik sekmenin donemini aliyor
- Onizlemede tek yerden donem degistirilebiliyor
- Kopyalanan isler hedef firmada acik sekmenin doneminden basliyor
- Uc ayda bir olan is `IlkDonem` sonrasinda yalniz ilgili aylarda hucre
  gosteriyor; donem gecerlilik mantigi degismedi
- `TekSefer` isinde davranis degismedi
- `IlkDonem` bos olan MEVCUT isler eskisi gibi calisiyor
- Yasal islerin donemleri DEGISMEDI
- Mevcut testlerin hepsi geciyor, sayi azalmiyor

## TESLIM
- ADIM 0'in cevaplari, ozellikle ilk donem hesabinin kac yerde kullanildigi
- Migration adi
- Degisen dosyalar
- Dogrulama maddelerinin sonucu
- Derleme ve test sonucu
- Commit atma.
