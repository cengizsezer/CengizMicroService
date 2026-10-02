# PROMPT 10 — DONEM PANOSU

## AMAC
Yeni sayfa: hangi ayin isi bitti, nerede eksik var. Bugune kadar yapilan her sey
VADE merkezliydi ("ne zaman son gun"). Bu ekran DONEM merkezli ("Agustos
kapandi mi"). Ikisi farkli: Agustos doneminin KDV'si 28 Eylul'de veriliyor.

Matris: satirlar firma, kolonlar is. Eksikler delik olarak gorunur.
Hucreye tiklayinca yapildi isaretlenir, kanit yapistirilir, o doneme not yazilir.

## KURAL
- Prompt 6–9'da calisan hicbir sey bozulmayacak.
- Hicbir mali hesaplama degismeyecek.
- YENI CEKIRDEK TABLO GEREKMIYOR: veri `FirmaIsi` + `IsTamamlama`'dan geliyor,
  bu ekran onlarin doneme gore cevrilmis halidir.
- Adimlar SIRAYLA. Her adim sonunda DUR, derle, bildir.
- Commit atma.

## ADIM 0 — ONCE INCELE, kod yazmadan bildir
- `IsTamamlama` tablosunun tam alan listesi ve `DonemAnahtari` formati
  ("2026-09", "2026-Q3", "2026" — hangi is tipinde hangisi).
- Bir isin belirli bir donemde GECERLI olup olmadigini soyleyen mantik nerede
  (uc ayda bir olan is her ayda cikmamali).
- Prompt 8'deki ek yukleme akisi: `FirmaIsiEki` ve FileApi uclari.
- Yapilacaklar sayfasinin satir uretimi hangi serviste.
Bulduklarini yaz, sonra ADIM 1'e gec.

## ADIM 1 — VERI MODELI (kucuk)

### 1.1 IsTamamlama'ya alan
    Not  string?   o doneme ozel serbest not

### 1.2 Yeni tablo: IsTamamlamaEki
    Id, IsTamamlamaId, DosyaAdi, ContentType, Boyut,
    YuklemeZamani, YukleyenKullaniciId, YukleyenKullaniciAdi,
    + FileApi dosya kimligi (prompt 8'deki `FirmaIsiEki` ile AYNI alanlar)

**IKI EK TURU AYRI KAYITTIR, BIRLESTIRILMEYECEK:**
- `FirmaIsiEki` (prompt 8) = PROSEDUR eki. Ise aittir, kalicidir, her donem ayni.
  "Bu is boyle yapilir": menu yolu ekran goruntusu, bos sablon, ornek mail.
- `IsTamamlamaEki` (bu prompt) = DONEM eki. O aya aittir. "Bu ay yaptim, cikti bu."

Ayni dosya deposunu (FileApi) kullanirlar. Boyut ve tur kisitlari prompt 8 ile
AYNI olacak; yeni sinir uydurma.

Tamamlama kaydi silinince (isaret geri alininca) ekleri de silinecek — dosyalar
FileApi'den de temizlenecek.

## ADIM 2 — MATRIS SAYFASI
Sol menuye yeni madde: **Dönem panosu**, Yapılacaklar'in hemen altina.

### 2.1 Ust serit
- Donem secici: ◀ Agustos 2026 ▶. Varsayilan: bir onceki ay (icinde bulundugun
  ayin isleri genelde bitmemistir, bakmak isteyecegin kapanan aydir).
- Ozet: "54 / 62 yapildi" + ilerleme cubugu + yuzde.
- Eksik kutusu: kac eksik ve HANGI ISTE yogunlastigi:
  "Fatura girisi 3 firmada · Banka isleme 2 firmada · Rapor 2 firmada".
  Bu cumle 17 satiri tek tek taramaktan hizlidir.
- "Sadece eksikler" filtresi.

### 2.2 Matris
Satir = firma, kolon = is. Kolonlar o donemde EN AZ BIR firmada gecerli olan
islerden uretilir; bos kolon cizilmez.

Hucre durumlari — **UCU DE AYRI GORUNECEK**:
- **Yapildi**: yesil zemin, tik. Kanit varsa kosede kucuk atac isareti.
- **Eksik**: kesikli cerceveli bos kutu.
- **Bu firmada yok**: soluk tire "—".

Son ikisini ayni gostermek panoyu yaniltici yapar: CUBIC'te rapor gonderimi
diye bir is YOK, eksik degil. Bunu karistirma.

Sagda satir sonu rozeti: "5/6". Firmanin o donemdeki tamamlanma orani.
Eksigi olan satir hafif kirmizi zeminli.

Satir sayisi cok olursa firma kolonu yapisik (sticky) kalacak.

### 2.3 Donem gecerliligi
Bir is o donemde gecerli degilse hucre "—" olur. Ornek: uc ayda bir olan gecici
vergi hazirligi yalnizca 3, 6, 9, 12. aylarda cikar. Bu mantik ADIM 0'da
bulunan mevcut kodla AYNI olmali; ikinci bir donem hesabi yazilmayacak.

## ADIM 3 — HUCRE PANELI
Hucreye tiklayinca kucuk panel acilir (sagda surgu degil, hucreye yakin kutu).

Icerik, bu sirayla:
1. Baslik: firma adi · donem · isin adi
2. **Yapıldı işaretle** onay kutusu. Isaretlenince tamamlama kaydi olusur.
3. **Ctrl+V ile kanit yapistir** alani: kesikli cerceveli bolge.
   - Panodan GORSEL yapistirma calisacak (clipboard paste event, image/png).
     Kullanici ekran goruntusu alip dogrudan yapistirabilmeli.
   - Dosya secerek de yuklenebilecek (xlsx, pdf, png, jpg, docx, csv).
   - Yuklenen dosyalar liste halinde, indirme ve silme ile.
4. **Bu döneme not** — serbest metin.
5. **Geçen dönem** blogu — ZORUNLU, bu ekranin asil degeri burada:
   bir onceki donemin eki ve notu gosterilir.
   "Luca'dan gelir tablosunu alip sablonun 2. sayfasina yapistirdim. Kur satirini
   elle guncellemek gerekiyor." gibi bir not, ertesi ay on dakika kazandirir.
   Onceki donemde kayit yoksa bolum CIKMAZ.
6. Altta: "Nasıl yapılır" baglantisi (prompt 8'in prosedur panelini acar) ve
   Kaydet.

Isaret geri alinabilecek. Geri alininca ek ve not da silinir, kullaniciya
once sorulur.

## ADIM 4 — ERISIM VE IZOLASYON
- Sayfa bir ucun hatasinda TAMAMEN COKMEYECEK. Matris yuklenemezse hata mesaji
  gosterilir; hucre paneli hata verirse yalniz panel etkilenir.
  (Prompt 6'da ayni izolasyon etiket cagrilari icin sart kosulmustu.)
- Panoya yapistirma izni yoksa ya da tarayici desteklemiyorsa dosya secme
  yolu calismaya devam edecek.

## CSS
Yeni siniflar `dp-` onekiyle. Mevcut `ys-`, `an-`, `bl-`, `or-`, `hs-`, `et-`,
`gg-`, `dy-`, `fk-mizan-*` siniflarina dokunma.

## DOGRULAMA
- Donem panosu sol menuden aciliyor, varsayilan donem onceki ay
- Matris firma x is olarak ciziliyor; yapildi / eksik / bu firmada yok UC AYRI
  gorunumde
- Uc ayda bir olan is yalnizca ilgili aylarda hucre gosteriyor, digerlerinde "—"
- Hucreye tiklayinca panel aciliyor, yapildi isaretlenebiliyor, isaret geri
  alinabiliyor
- Panodan ekran goruntusu Ctrl+V ile yapistirilabiliyor ve kaydediliyor
- Dosya secerek xlsx yuklenebiliyor; izin verilmeyen tur reddediliyor
- Bu doneme not kaydediliyor
- Onceki donemde ek ve not varsa "Gecen donem" blogu doluyor; yoksa cikmiyor
- Isaret geri alininca ekler ve not siliniyor, dosyalar FileApi'den de gidiyor
- Ust seritteki sayilar matristeki durumlarla tutuyor
- Prosedur ekleri (prompt 8) ETKILENMIYOR, ayri duruyor
- Yapilacaklar sayfasi ve firma isleri karti bozulmadi
- Mevcut testlerin hepsi geciyor, sayi azalmiyor

## TESLIM
- ADIM 0'in cevaplari
- Her adim icin degisen dosyalar ve migration adi
- Donem gecerlilik mantiginin mevcut kodla ayni oldugunun teyidi
- Dogrulama maddelerinin sonucu
- Derleme ve test sonucu
- Commit atma.
