# PROMPT 6C — SEED KORUMASI VE TIRE SARTININ KALDIRILMASI

Iki duzeltme. Kucuk ama ikisi de veri kaybi riski tasiyor.

## KURAL
- Prompt 6 ve 6B'de yapilan hicbir sey geri alinmayacak.
- Hicbir mali hesaplama degismeyecek.
- Commit atma.

## 1. SEED ASLA DOLU ALANI GERI ALMAYACAK  — oncelikli
Bugun acilis seed'i kod ayiklanamayinca VergiTuru ve DefterUsulu alanlarini
Belirsiz'e ceviriyor. Bu bir veri kaybi yolu: bugun tire yuzunden olur, yarin
baska bir parser degisikligi yuzunden olur, ve her seferinde butun firmalarin
siniflandirmasi SESSIZCE silinir.

Kural:
- Seed ve mukellefiyet okumasi YALNIZCA `Belirsiz` olan alani doldurur.
- Dolu bir alan (otomatik dolmus olsa bile) HICBIR KOSULDA Belirsiz'e
  cevrilmez, degistirilmez.
- Ayiklama basarisiz olursa: alan oldugu gibi kalir, log'a uyari dusulur,
  firmanin mukellefiyet kartinda ayristirilamayan metin zaten gorunuyor.
- Kullanicinin elle degistirdigi deger de aynen korunur (prompt 6'daki kural).

Tek istisna: kullanici arayuzden kendisi Belirsiz secerse. O bir kullanici
karari, seed degil.

Test: siniflandirmasi dolu bir firmanin mukellefiyet metni ayristirilamaz hale
getirilip seed calistirildiginda VergiTuru ve DefterUsulu AYNEN kalmali.

## 2. TIRE SARTI KALKIYOR
Prompt 6B'de "kodun ardindan tire gelmesi beklenecek" dedim. Yanlisti; o sart
hicbir sey kazandirmiyor ama tiresiz yazilmis her firmayi siniflandirma disinda
birakiyor.

Sebep: metin zaten virgul / noktali virgul / satir sonundan parcalara boluniyor
ve kod PARCANIN BASINDA araniyor. Bu ikisi bir arada "2021 yılından itibaren"
gibi metinleri zaten eliyor — 2021 ne `0\d{3}` desenine uyuyor ne de bir parcanin
basinda duruyor. Tire fazladan bir sart.

Yeni kural: bir parca `0\d{3}` ile BASLIYORSA kod sayilir. Ardindan tire,
bosluk, nokta gelebilir ya da hic bir sey gelmeyebilir.

Kabul edilmesi gereken bicimler:
    "0010 - KURUMLAR VERGİSİ"
    "0010- KURUMLAR VERGİSİ"
    "0010-KURUMLAR VERGİSİ"
    "0010 KURUMLAR VERGİSİ"      <- BUGUN CALISMIYOR, calisacak
    "0010"
    " 0010 · Kurumlar"

Kabul edilmemesi gereken:
    "2021 yılından itibaren mükellef"
    "Vergi dairesi 0010 numaralı yazı ile bildirmiştir"   <- kod parca basinda degil

Prompt 6B'nin ayiklama testleri aynen gecmeye devam etmeli; yukaridaki tiresiz
bicim icin yeni test eklenecek.

## 3. OZEL HESAP DONEMI YILI — simdilik degistirme
Kapanis yilina gore adlandirma (01.07.2025–30.06.2026 → 2026) SIMDILIK KALSIN.
Dogru adlandirma muhasebe programinin kendi adlandirmasina bagli; mizan
yuklenirken yil eslesmezse dosya yanlis doneme baglanir. Gercek bir ozel hesap
donemli mizan gorulunce karara baglanacak.
Kod icinde bu karari tek bir yerde tut ve basina su yorumu yaz:
"Ozel hesap donemi yili kapanis yilina gore. Mizan dosyasinin kendi
adlandirmasiyla dogrulanmadi."

## DOGRULAMA
- Madde 1'deki test geciyor: dolu siniflandirma seed'den sonra AYNEN duruyor
- "0010 KURUMLAR VERGİSİ" (tiresiz) metninden 0010 cikiyor ve firma kurumlar
  vergisi + bilanco esasi siniflaniyor
- "2021 yılından itibaren" metninden kod CIKMIYOR
- Prompt 6B'nin butun testleri hala geciyor
- 865 test hala geciyor, azalmamali

## TESLIM
- Degisen dosyalar
- Dogrulama maddelerinin sonucu
- Derleme ve test sonucu
- Commit atma.
