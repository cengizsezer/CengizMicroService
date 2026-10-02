# PROMPT 10B — GECMIS DONEMLER VE EKSIK DONEME NOT

Iki kucuk duzeltme, Dönem panosu uzerine.

## KURAL
- Prompt 10'da calisan hicbir sey bozulmayacak.
- Yapilacaklar sayfasinin davranisi DEGISMEYECEK.
- Hicbir mali hesaplama degismeyecek.
- Commit atma.

## 1. PASIF TAKVIM SATIRLARI DONEM PANOSUNDA DA HUCRE URETECEK

### Sorun
Prompt 7'de seed, son gunu gecmis takvim satirlarini pasif yaziyor. O bayragin
amaci Yapilacaklar kuyrugunda ocaktan beri her isin "gecikti" gorunmesini
onlemekti. Ama Dönem panosu ayni bayragi dinledigi icin Temmuz 2026 ve
oncesinde yasal kolonlar "—" gorunuyor — sanki o aylarda KDV, muhtasar ve damga
yukumlulugu YOKMUS gibi.

Donem panosunun isi tam olarak gecmise bakmak. Pasif bayragi gecmisi silmek
icin degil, kuyrugu temiz tutmak icin kondu.

### Yapilacak
- Dönem panosu matrisi uretilirken takvim satirinin **Aktif bayragi
  DIKKATE ALINMAYACAK**. Firmanin mukellefiyet kodu o donemde gecerliyse hucre
  cizilir; isaretli degilse "eksik" gorunur.
- **Yapilacaklar sayfasi AYNEN KALIR**: orada pasif satir hala is uretmez.
  Iki ekran ayni veriyi farkli amaclarla okuyor; kurallari ayri olabilir ve
  olmali.
- Bu ayrim koda yorum olarak yazilsin: "Pasif bayragi kuyrugu temiz tutmak
  icindir; gecmis donem gorunumunu kisitlamaz."

### Dikkat
Bu degisiklik gecmis aylarda cok sayida "eksik" hucre ortaya cikaracak — bu
DOGRU davranis, o isler gercekten isaretlenmemis. Ust seritteki sayilar da buna
gore artacak.

## 2. EKSIK DONEME NOT YAZILABILECEK

### Sorun
Not ve kanit yalnizca "Yapildi" isaretli doneme eklenebiliyor. Oysa panoya
bakarken en cok merak edilen sey bos hucrenin SEBEBI:
"Agustos'ta rapor gitmedi, musteri veriyi gec verdi."
Simdiki halde bu not yazilamiyor; bos hucre sessiz kaliyor.

### Yapilacak
- `IsTamamlama.TamamlanmaZamani` **nullable** olacak.
- "Yapildi" tanimi: `TamamlanmaZamani != null`.
- Kayit var ama zaman yoksa: is yapilmamis, ama notu ve/veya kaniti var.
- Hucre paneli:
  - "Yapıldı işaretle" kutusu isaretlenmeden de **not ve kanit alanlari ACIK**.
  - Kaydedilince tamamlama kaydi olusur, `TamamlanmaZamani` bos kalir.
  - Sonra isaretlenirse ayni kayda zaman yazilir; not ve kanit KORUNUR.
  - Isaret kaldirilinca zaman bosalir ama **not ve kanit SILINMEZ** —
    prompt 10'daki silme davranisi yalnizca kullanici kaydin tamamini silmek
    istediginde calisir. Bunun icin panele ayri bir "Bu dönem kaydını sil"
    secenegi gerekir; silmeden once sorar.
- Matris hucresi: notu ya da kaniti olan ama yapilmamis donem, duz bos hucreden
  AYIRT EDILECEK. Kesikli cerceve ayni kalir, icine kucuk bir not isareti
  konur. Renk tek basina anlam tasimasin; `title` metni "yapılmadı · notu var"
  desin.
- Ust serit sayilari: notu olan ama yapilmamis donem **EKSIK sayilir**.
  Not yazmak isi yapmis saymaz.

### Geriye uyum
Mevcut tamamlama kayitlarinin hepsinde `TamamlanmaZamani` dolu; davranislari
degismez. Migration yalnizca kolonu nullable yapar, veri donusturmez.

## DOGRULAMA
- Haziran 2026 doneminde yasal kolonlar hucre gosteriyor, "—" degil
- Yapilacaklar sayfasinda gecmis pasif donemler HALA is uretmiyor
- Isaretsiz bir hucreye not yazilip kaydedilebiliyor
- Isaretsiz hucreye kanit yapistirilabiliyor
- Sonradan isaretlenince not ve kanit yerinde duruyor
- Isaret kaldirilinca not ve kanit SILINMIYOR, yalnizca zaman bosaliyor
- "Bu dönem kaydını sil" ile silinince ekler FileApi'den de gidiyor
- Notu olan yapilmamis donem matriste duz bos hucreden ayirt ediliyor
- Ust seritte notu olan yapilmamis donem EKSIK sayiliyor
- Prompt 10'un 14 testi ve toplam 977 test geciyor, sayi azalmiyor

## TESLIM
- Degisen dosyalar ve migration adi
- Gecmis donemlerde ortaya cikan eksik sayisinda belirgin bir artis oldu mu
- Dogrulama maddelerinin sonucu
- Derleme ve test sonucu
- Commit atma.
