# PROMPT 6B — PROMPT 6 SONRASI DUZELTMELER

Prompt 6 tamam. Alti kucuk duzeltme. Ilk ucu PROMPT 7'nin ONKOSULU.

## KURAL
- Prompt 6'da yapilan hicbir sey geri alinmayacak; bu is uzerine ekleme.
- Hicbir mali hesaplama degismeyecek.
- Commit atma.

## 1. MUKELLEFIYET KODU AYIKLAMA — SERTLESTIR  [prompt 7 onkosulu]
Bugun serbest metinden 4 haneli sayilar ayikliyor. Prompt 7 yasal isleri tam bu
listeden turetecek, yani bu ayiklama artik kritik yol ustunde.

- Desen `\b0\d{3}\b` olacak: butun mukellefiyet kodlari sifirla basliyor, boylece
  metindeki yil (2021, 2026) ve parantez ici numaralar elenir.
- Kodun ardindan bosluk ve tire gelmesi beklenecek ("0010 - ", "0010-").
- Ayiklanan kod listesi ve ayiklanamayan kalan metin LOGLANACAK.
- Bilinmeyen kod REDDEDILMEYECEK, sadece siniflandirmada kullanilmayacak.
  (Tam mukellefiyet kodu listesini kimse garanti edemez; bilmediklerimizi atmak
  yerine gecirip gostermek dogru.)
- Mevcut 0003/0010 kurallari aynen kalacak.

Test: "0003 - GELİR VERGİSİ S. (MUHTASAR) , 0010 - KURUMLAR VERGİSİ , 0015 - ...,
0033 - ..., 0040 - DAMGA VERGİSİ (BEYANNAMELİ DAMGA VERGİSİ MÜKELLEFİ)"
metninden tam olarak [0003, 0010, 0015, 0033, 0040] cikmali.
Icinde "2021 yılından itibaren" gecen bir metinden 2021 CIKMAMALI.

## 2. KIRMIZI "!" KURALI — OCAK-NISAN SORUNU  [prompt 7 onkosulu]
Bugunku kural "cari takvim yilinin mizani yok" demek. Ocak-Nisan arasi onceki
yil kapatilirken neredeyse butun firmalar kirmizi olur; rozetin en cok gerektigi
ayda hicbir sey ifade etmez.

Yeni kural:
- **Kirmizi "!"**: firmanin HIC mizani yok. Sadece bu.
- **Cari donemin mizani yok ama gecmis donem var**: kirmizi degil, 16'lik eksik
  listesine bir madde olarak eklenir ve sari sayiya dahil olur.

Ayrica: "cari donem" takvim yili varsayilmayacak. Firmanin HesapDonemi alani
OzelHesapDonemi ise donem OzelDonemBas / OzelDonemBit'ten hesaplanacak.

## 3. MUKELLEFIYET KARTINDA KOD CIPLERI  [prompt 7 onkosulu]
Prompt 6'da yapilmadi, hakli olarak: prompt "kartlar degismeyecek" derken
dogrulama listesi cipleri istiyordu, celiskiliydi. Karar: CIPLER YAPILACAK.

Sebep: kodlari artik ayikliyoruz ve prompt 7 onlara guvenecek. Cipler ayiklamanin
dogru calistigini gozle gormenin en kolay yolu.

- Mukellefiyet turleri alani ham metin yerine ayiklanan kodlardan cip olarak
  cizilecek: "0003 · Muhtasar" gibi, kod + adin kisa hali.
- 0010 (ya da 0001) cipi vurgulu, siniflandirmanin kaynagi oldugu belli olsun.
- Ayiklanamayan artik metin varsa ciplerin altinda kucuk gri satirda AYNEN
  gosterilecek. Sessizce yutulmayacak.
- Kartin diger alanlari (vergi dairesi, VKN, ise baslama, NACE, e-Fatura,
  e-Defter) DEGISMEYECEK.

## 4. RAYA GERI GELECEK IKI UYARI
Eski ucgen imza yetkisi (60 gunden az) ve ortaklik payi (%100 tutmuyor)
durumlarini da kapsiyordu. 16'lik listede yoktular, ucgen kalkinca raydan
dustuler ve yalniz detaydaki kutuda kaldilar. Bu bir gerileme.

- Ikisi de eksik/uyari sayisina DAHIL EDILECEK, rozette gorunecek.
- Ikisi "eksik alan" degil "uyari" oldugu icin seritte ayri cumlede yazilsin:
  "Eksik bilgi: ... · Uyarı: imza yetkisi 45 gün sonra doluyor"
- Rozet `title` metni ikisini de sayacak.

## 5. SURESI DOLMUS TEK YETKILI
Bugun suresi dolmus yetkililer rozete hic sayilmiyor. Gerekcesi dogru: silinmeyen
eski sirkuler kalici kirmizi yapar. Ama tek yetkilisi olan ve o yetkisi dolmus
firma HICBIR SEY gostermiyor — asil tehlikeli durum o.

Kural: suresi dolmus yetkililer sayilmaz, ANCAK firmanin gecerli (suresi
dolmamis) HICBIR imza yetkilisi yoksa uyari cikar:
"Geçerli imza yetkilisi yok". Bu uyari 4. maddedeki uyari grubuna girer.

## 6. BOS SABLON — UYDURMA FORMATLARI KAPAT
Mikro ve Logo basliklari gercek dosya gorulmeden yazildi. Bunlar musteriye
gonderilecek dosyalar; uydurma sablon indirtmek musteriye yanlis sablon gondermek
demek.

- "Boş şablon indir" YALNIZCA ORKA (iki varyant) ve Luca icin aktif.
- Mikro, Logo ve digerleri icin buton PASIF, yaninda: "bu programın gerçek
  mizan çıktısı henüz görülmedi".
- Prompt 5 (mizan format profilleri) gelince sablon profilden uretilecek ve bu
  kisit kalkacak.

## DOGRULAMA
- Madde 1'deki iki metin testi geciyor
- Hic mizani olmayan firma kirmizi; cari yili eksik ama gecmis yili olan firma
  kirmizi DEGIL, sari sayisi bir artmis
- Ozel hesap donemli bir firmada cari donem takvim yilindan farkli hesaplaniyor
  (birim test)
- Mukellefiyet kartinda DGR icin bes cip goruluyor, 0010 vurgulu
- Imza yetkisi 45 gun kalan test firmasinda rayda uyari gorunuyor
- Gecerli yetkilisi olmayan test firmasinda "Geçerli imza yetkilisi yok" cikiyor
- Mikro secili iken sablon butonu pasif, ORKA secili iken aktif
- 848 test hala geciyor, sayi artmis olabilir ama azalmamali

## TESLIM
- Degisen dosyalar ve varsa migration
- Dogrulama maddelerinin sonucu
- Derleme ve test sonucu
- Commit atma.
