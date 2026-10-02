# PROMPT 11B — YAZILMAMIS TEKRAR/GUN DE AMBER

Prompt 11'de sordugun karara cevap. Kucuk bir duzeltme.

## KURAL
- Prompt 11'de calisan hicbir sey bozulmayacak.
- Yeni alan, yeni tablo, yeni migration YOK.
- Commit atma.

## KARAR
Hic yazilmamis `Tekrar` ya da `Gun` satiri **AMBER** olacak. Yesil kalmayacak.

Sebep: tek kolonlu yapistirmada butun satirlar yesil goruntuyle "her ay ·
donem sonu" aliyor. 17 firmaya aylik tekrarli is girilmis olur ve satir yesil
oldugu icin kullaniciya bakmasi gerektigini soyleyen hicbir isaret kalmaz.
Amber satiri ENGELLEMEZ — zaten ekler — yalnizca isaretler. Listede olmayan
programa uygulanan kuralin aynisi.

## YAPILACAK

### 1. Durum kurali
- `Tekrar` hucresi bos ya da hic kolon eslesmemis  -> satir AMBER
- `Gun` hucresi bos ya da hic kolon eslesmemis     -> satir AMBER
- Varsayilan deger YINE uygulanir (aylik · donem sonu). Degeri atmak degil,
  isaretlemek istiyoruz.
- Tabloda etiket **"(varsayılan — kontrol et)"** olsun, yalniz "(varsayılan)"
  degil. Kullanici tablodaki hucreden degistirince satir YESILE doner.

### 2. Dogrulama metnindeki 6. satir
Prompt 11'de "Her yl" satirinin duzeltilince yesile donmesini yazmistim. O
satirda gun yok; artik duzeltilince **AMBER** olacak, kirmizi degil. Bu dogru
davranis, prompt 11'deki beklenti bu maddede gecersiz.

### 3. Uc sayi ve tek kolon
- Tek kolonlu yapistirma (yalniz basliklar): butun satirlar AMBER, hepsi
  eklenir. "N eksik bilgiyle eklenecek" sayisi satir sayisina esit olur.
- Ust seritteki uc sayi bu kurala gore guncellenir.

### 4. Baska firmadan kopyalama
Kopyalanan isler zaten tekrar ve gun tasiyor; onlar ETKILENMEZ. Bu kural
yalniz yapistirma onizlemesi icin.

## DOGRULAMA
- Yalniz baslik iceren tek kolonlu yapistirmada butun satirlar amber, hepsi
  ekleniyor
- "Sözleşme yenileme" satirinin tekrar hucresi "Her yıl" yapilinca satir
  AMBER oluyor (gun bos), kirmizi degil
- Gun hucresi de doldurulunca satir YESILE donuyor
- Tekrar ve gunu dolu olan satirlar hala YESIL — prompt 11'in 1, 2, 5. satirlari
  degismedi
- Program bulunamayan satirlar hala amber (3 ve 4. satir)
- Tabloda bos alanin yerinde "(varsayılan — kontrol et)" goruluyor
- Mevcut testlerin hepsi geciyor, sayi azalmiyor; etkilenen TopluIsTests
  beklentileri bu karara gore guncellenir

## TESLIM
- Degisen dosyalar
- Dogrulama metnindeki 7 satirin yeni durumlari (kac yesil / amber / kirmizi)
- Derleme ve test sonucu
- Commit atma.
