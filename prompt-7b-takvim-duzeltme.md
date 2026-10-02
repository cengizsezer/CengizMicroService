# PROMPT 7B — VERGI TAKVIMI KAYDIRMA VE TEKRARLAYAN NOT

Uc kucuk duzeltme. Prompt 7'nin uzerine.

## 1. HAFTA SONU KAYDIRMASINI KALDIR  — oncelikli
Seed, son gunu hafta sonuna dusen beyannameleri pazartesiye kaydiriyor. Bu
YANLIS. Gercek tahakkuk fisleri nominal tarihi yaziyor:

| Beyanname            | Seed'deki | Gercek tahakkuk fisi |
|----------------------|-----------|----------------------|
| Muhtasar 08/2026     | 28.09     | 26.09 (Cumartesi)    |
| Gecici vergi 2026/1  | 18.05     | 17.05 (Pazar)        |
| KDV 08/2026          | 28.09     | 28.09 (dogru)        |

Kaydirma yuzunden uygulama GERCEKTEN GEC bir tarih gosteriyor. Kullanici ona
guvenirse beyannameyi gec verir.

Yapilacak:
- Kaydirma mantigi tamamen KALDIRILACAK. Son gun nominal kuralla hesaplanacak:
  KDV izleyen ayin 28'i, muhtasar ve damga izleyen ayin 26'si, gecici vergi
  17.05 / 17.08 / 17.11, kurumlar izleyen yilin 30 Nisan'i.
- Mevcut seed satirlari YENIDEN URETILECEK (idempotent seed; kullanicinin elle
  degistirdigi satirlar KORUNACAK — elle degistirilmis satiri ayirt edebilmek
  icin gerekiyorsa VergiTakvimi'ne `ElleDuzenlendi bool` alani ekle).
- Vergi Takvimi ekraninda satir basina elle duzenleme zaten var, kalsin.
- Ekranin basina tek satirlik not: "Tarihler nominal kuraldan uretilir.
  Hafta sonu, resmî tatil ve bayram kaydirmalari OTOMATIK YAPILMAZ; gerekiyorsa
  satiri elle duzeltin."

Sebep kayda gecsin: yarim calisan bir kaydirma kurali, hic olmamasindan kotudur.
Bayram kaydirmasi zaten yapilmiyordu; hafta sonu kaydirmasi da yanlis yonde
calisiyor. Nominal tarih guvenli taraftir.

## 2. TEKRARLAYAN NOTU KALDIR
`FirmaNotu` tablosunu hem Takip kartindaki "Not" bolumu hem "Kalici talimatlar"
karti okuyor; ayni not iki yerde gorunuyor. **ONCE BAK**: prompt 8–11 arasinda
bu zaten duzeltilmis olabilir. Duzeltilmisse bu maddeyi ATLA ve bildir.
- Takip kartindaki NOT bolumu KALDIRILACAK.
- "Kalici talimatlar" karti tek yer olarak kalacak.
- Takip kartinda sorumlu, donemler ve son islemler kalacak.
- Veri silinmeyecek, yalnizca gosterildigi yer tekillesecek.

## 3. KALAN DateTime.Today
Prompt 6'daki panel servisi hala `DateTime.Today` cagiriyor. Enjekte edilen
`TimeProvider`'a cevrilecek. Davranis degismeyecek; amac testlerin sabit tarihle
calismasi.

## KURAL
- Prompt 7'de yapilan hicbir sey geri alinmayacak.
- Prompt 8, 9, 10, 10B, 11 ve 11B'de calisan hicbir sey bozulmayacak. Takvim
  satirlari artik Donem panosunu da besliyor; son gun degisince o ekranin
  hucreleri de dogru kaymali, kirilmamali.
- Hicbir mali hesaplama degismeyecek.
- Commit atma.

## DOGRULAMA
- Muhtasar 08/2026 son gunu 26.09.2026, gecici vergi 2026/1 son gunu 17.05.2026,
  KDV 08/2026 son gunu 28.09.2026
- Elle duzenlenmis bir takvim satiri seed tekrar calistiginda DEGISMIYOR
- Takip kartinda Not bolumu yok; Kalici talimatlar kartinda notlar duruyor
- Donem panosu ve Yapilacaklar yeni tarihlerle dogru calisiyor
- Mevcut testlerin hepsi geciyor, sayi azalmiyor (11B sonrasi sayi neyse o)

## TESLIM
- Degisen dosyalar ve varsa migration
- Yeniden uretilen takvim satir sayisi
- Dogrulama maddelerinin sonucu
- Derleme ve test sonucu
- Commit atma.
