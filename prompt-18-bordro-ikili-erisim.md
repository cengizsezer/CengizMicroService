# PROMPT 18 — BORDRO HESAPLAYICI: HEM UYGULAMA ICINDE HEM ACIK ADRESTE

## AMAC
Bordro hesaplayici bugun yalnizca uygulama icinde, **Hesaplamalar** sekmesinin
altinda. Eskiden `dijitalmasraf/payrollcalculator` adresinden SIFRESIZ
aciliyordu ve o kullanim geri isteniyor.

Iki yerden de calisacak:

1. **Uygulama ici** — `Hesaplamalar` altinda, bugunku gibi, oturum acmis
   kullanici icin, normal kabukla (menu, ust serit, firma secici).
2. **Acik adres** — `/payrollcalculator`, **oturum ACMADAN**, kabuksuz.

## TEK KURAL, HER SEYDEN ONEMLI
**HESAPLAYICI BIR KEZ YAZILACAK.** Iki ayri kopya OLMAYACAK. Hesaplama
mantigi tek bir bilesende durur; iki sayfa o bileseni gosterir. Kopyalanirsa
alti ay sonra biri guncellenir, digeri sessizce yanlis rakam uretir — bordro
hesabinda bu kabul edilemez.

## DIGER KURALLAR
- Prompt 6–17'de calisan hicbir sey bozulmayacak.
- **HICBIR MALI HESAPLAMA DEGISMEYECEK.** Bu prompt bir TASIMA ve ERISIM
  isidir; formul, oran, tavan, kesinti mantigina DOKUNULMAYACAK.
- Acik sayfa uygulamanin baska hicbir verisine erisemeyecek.
- Adimlar SIRAYLA. Her adim sonunda DUR, derle, bildir.
- Commit atma.

## ADIM 0 — ONCE INCELE, kod yazmadan bildir
Bu adim belirleyici. Kod yazmadan once sunlari cikar:

1. **Hesaplayici hangi dosyalarda?** Sayfa, bilesen ve varsa hesaplama sinifi.
   Hesaplama mantigi sayfanin icinde mi, ayri bir sinifta mi?
2. **SUNUCUYA SORUYOR MU?** En kritik soru. Hesaplayici calisirken hangi
   uclari cagiriyor:
   - Hic cagirmiyor, her sey istemcide mi?
   - Parametre cekiyor mu (asgari ucret, SGK oranlari, tavan katsayisi,
     vergi dilimleri)? Hangi uctan?
   - Sonucu kaydediyor mu? Nereye?
   - Firma ya da personel verisi okuyor mu?
   Her biri icin uc adi ve ne dondurdugu.
3. **Kimlik dogrulama nasil kurulu?** `App.razor`'da `AuthorizeRouteView` var
   mi, varsayilan politika ne, `[AllowAnonymous]` kullanilan baska bir sayfa
   var mi? Varsa DESENINI CIKAR — ikinci bir desen icat edilmeyecek.
4. **Kabuk (layout) nasil secliyor?** `MainLayout` disinda sade bir layout var
   mi? Yoksa yenisi gerekecek.
5. **Gateway/Ocelot tarafinda** anonim gecen bir rota var mi? Varsa deseni ne?
6. **Eski adres** `/payrollcalculator` bugun ne donduruyor — 404 mu, giris
   ekranina mi gidiyor?
7. Hesaplayici firma secimine ya da oturumdaki kullaniciya BAGLI MI calisiyor?
   (Orn. secili firmanin SGK tesvik kademesini okuyorsa, anonim kullanicida
   o veri yok.)

Bulduklarini yaz, sonra DUR. **ADIM 1'e gecmeden once cevabini bekle** —
2. ve 7. maddelerin cevabi tasarimi degistirir.

## ADIM 1 — HESAPLAYICIYI AYIR

- Hesaplama mantigi ve arayuzu tek bir bilesene toplanir:
  `BordroHesaplayici.razor` (ad degisebilir, mevcut adlandirmaya uy).
- Bilesen **kendi basina ayakta durur**: oturum, firma secimi, menu ya da
  uygulama durumu BILMEZ.
- Oturuma bagli bir sey varsa (ADIM 0 madde 7) bu bilesene **parametre**
  olarak gecirilir, bilesen kendisi okumaz. Anonim kullanimda parametre
  bos gelir ve bilesen varsayilanla calisir.
- Hesaplama mantigi zaten ayri bir sinifsa OLDUGU YERDE KALIR, tasinmaz.
- Bu adimda **gorunur hicbir sey degismez**; uygulama ici sayfa ayni calisir.

## ADIM 2 — IKI SAYFA, TEK BILESEN

### 2.1 Uygulama ici
Mevcut sayfa `BordroHesaplayici`'yi gosterir. Adres, menu yeri ve gorunum
AYNEN KALIR. Oturuma bagli parametreler varsa buradan gecirilir.

### 2.2 Acik adres
Yeni sayfa: `/payrollcalculator`

- `@attribute [AllowAnonymous]` — ADIM 0'da bulunan desene uyarak.
- **Sade kabuk**: menu yok, firma secici yok, ust serit yok. Yalniz basit bir
  baslik seridi (uygulama adi) ve hesaplayici.
- Uygulamanin hicbir sayfasina baglanti VERMEZ. Girise yonlendirme yok.
- Oturum acmis biri bu adrese gelirse de ayni sade sayfa acilir; kabuga
  dondurulmez.

### 2.3 Sunucu tarafi
ADIM 0 madde 2'nin cevabina gore:

- **Hic uc cagirmiyorsa**: sunucuda hicbir sey yapilmaz. En iyi durum.
- **Parametre cekiyorsa**: o uc anonim erisime acilir, ama:
  - YALNIZ okuma, YALNIZ parametre (oranlar, tavanlar, dilimler)
  - Firma, personel, kullanici verisi DONDURMEZ
  - Mevcut ucu olduğu gibi acma; gerekiyorsa yalniz parametre donduren
    DAR bir uc ayrilir
  - Gateway tarafinda da anonim gecirilir
- **Sonuc kaydediyorsa**: anonim kullanimda KAYDETME OLMAZ. Kaydet dugmesi
  anonim sayfada cikmaz. Hesaplama ekranda kalir, istemci tarafindadir.

## ADIM 3 — GUVENLIK KONTROLU
Acik sayfa internete acik demektir. Su dort maddeyi TEK TEK dogrula ve
sonucunu yaz:

1. Sayfa kaynagi ve agdaki istekler uygulamanin baska hicbir ucunu CAGIRMIYOR.
2. Anonim acilan uc (varsa) firma, personel, kullanici ya da mali veri
   DONDURMUYOR — yalniz sabit parametreler.
3. Sayfada uygulamanin diger bolumlerine baglanti ya da yonlendirme YOK.
4. Anonim kullanicinin yazma yapabildigi hicbir uc acilmadi.

Bu dordunden biri saglanmiyorsa DUR ve bildir, devam etme.

## DOGRULAMA
- Uygulama icinde Hesaplamalar altindaki bordro hesaplayici ESKISI GIBI
  calisiyor; adres, menu yeri, gorunum ve SONUCLAR degismedi
- `/payrollcalculator` oturum acmadan aciliyor
- Acik sayfada menu, firma secici ve ust serit YOK
- Ayni girdiler iki sayfada AYNI sonucu veriyor (bir ornekle goster)
- Hesaplama mantigi tek yerde; ikinci bir kopya yok
- ADIM 3'un dort maddesi saglandi
- Mevcut testlerin hepsi geciyor, sayi azalmiyor (1080 ya da uzeri)

## TESLIM
- ADIM 0'in cevaplari — ozellikle 2. ve 7. madde
- Her adim icin degisen dosyalar
- Acilan anonim uc varsa adi ve TAM OLARAK ne donduruyor
- ADIM 3'un dort maddesinin sonucu
- Ayni girdiyle iki sayfadan alinan sonuc
- Derleme ve test sonucu
- Commit atma.
