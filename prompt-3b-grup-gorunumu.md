# PROMPT 3B — YASAL / YÖNETSEL GÖRÜNÜM VE GRUP MATRİSİ

## AMAC
3A'da tanimlanan etiketleri kullanmak. Iki cikti:
1. Gelir Tablosu'nda etikete gore dagitim ve yasal / yonetsel gorunum
2. "Kim ne getirdi" grup matrisi

## KURAL — EN ONEMLI MADDE
**Yonetsel gorunum yalnizca EKRANDIR.** Beyanname, vergi hesaplamasi, bilanco, mizan,
dikey yuzdeler ve disa aktarim ciktilarinin hicbiri yonetsel gorunumden ETKILENMEZ.
Yasal gorunum varsayilandir ve sayfa her acildiginda yasal ile baslar.
Yonetsel gorunum secili oldugu her yerde ekranda acikca belirtilir.

Ayrica:
- Alt kirilimlar yine hicbir standart toplama girmiyor.
- MizanHesaplayici, MaliTabloIsareti, hesap_plani.json degismeyecek.
- 3A'daki cift sayim korumasi ve kural motoru degismeyecek, aynen kullanilacak.
- Commit atma.

## 1. ONCE INCELE
- Gelir Tablosu sekmesini olusturan bilesen ve satirlarini nasil urettigi
- 3A'da yazilan kural motoru ve etiket toplama fonksiyonunun imzasi ve nerede oldugu
- Bir firmanin baska bir firmanin mizanina/agacina erisimi icin mevcut yol var mi
  (GetHesapAgaciAsync firmaId aliyor mu, yoksa oturumdaki firmaya mi bagli)
- Firma listesine nasil erisiliyor (firma secim ekranini besleyen servis)
Bulduklarini yaz, sonra uygula.

## 2. ETIKET DEGERI ILE FIRMA ESLESMESI — YENI ALAN
`EtiketDegeri`'ne `FirmaId` (nullable) alani ekle: "bu etiket hangi firmaya karsilik geliyor".
Deger ekleme/duzenleme formunda firma secimi olsun, bos birakilabilsin
(bos = sistemde firmasi olmayan bir etiket, orn. "Grup disi").

Bu alan olmadan yonetsel gorunum kurulamaz: bir defterde "PKF Aday" etiketli tutarin,
PKF Aday firmasinin yonetsel gelirine eklenecegini baska turlu bilemeyiz.

## 3. GELIR TABLOSU — ETIKETE GORE DAGITIM
Gelir Tablosu sekmesinin ustune bir secim serisi:
`[ Yasal görünüm ]  [ Yönetsel görünüm ]` ve yaninda boyut secici (varsayilan: ilk boyut).
Varsayilan YASAL.

### Yasal gorunum
Bugunku tablo, hicbir degisiklik yok. Tek fark: kapsamdaki ana hesaplarin
(orn. 600) altinda etiket kirilimi acilip kapanabilsin:
```
600  YURTİÇİ SATIŞLAR              21.458.746,99
  ● Kurumsal Strateji              12.194.054,99
  ● PKF Aday                        9.264.692,00
```
Bu kirilim bilgi amaclidir, hicbir toplami degistirmez.

### Yonetsel gorunum
Su hesap gosterilir, adim adim, ekranda acikca:
```
Net satışlar (yasal)                                    22.020.812,69
− bu defterde başka şirkete etiketli tutar              (9.264.692,00)
+ diğer defterlerde bu şirkete etiketli tutar            [hesaplanır]
Net satışlar (yönetsel)                                 12.756.120,69
```
- "Baska sirkete etiketli": etiket degerinin FirmaId'si, bakilan firmadan FARKLI olanlar.
- "Diger defterlerde bu sirkete etiketli": diger firmalarin mizanlarinda,
  FirmaId'si bakilan firma olan etiketli tutarlar.
- Mizani yuklenmemis firma varsa o satir "[firma adi] mizanı yüklenmedi" diye
  ayrica gosterilsin ve toplamin eksik oldugu belirtilsin. Sifir yazip gecme.
- Etiketsiz (Atanmamis) tutarlar defterin sahibinde kalir.

### ZORUNLU UYARI
Yonetsel gorunum secildiginde tablonun ustunde amber serit:
"Gelir dagitildi, gider dagitilmadi. Bu gorunumdeki kar oranlari yaniltir;
yalnizca ciro analizi icin kullanin."
Gider hesaplari da ayni boyutla etiketlenmis olsa bile bu uyari kalir
(gider dagitimi bu adimin kapsaminda degil).

## 4. GRUP MATRISI
Detay.razor'a EN SONA yeni sekme: "GRUP GÖRÜNÜMÜ". Sona ekle, mevcut indeksler kaymasin.

Tablo: **satir = kimin defteri · sutun = gelir gercekte kime ait**
- Satirlar: secili boyutun degerlerinde FirmaId'si dolu olan firmalar + bakilan firma
  (tekrarsiz). Baska firma cekme, liste bu kadarla sinirli kalsin.
- Sutunlar: boyutun degerleri + "Atanmamis" + satir toplami
- Hucre: o defterde o etikete dusen tutar
- Mizani yuklenmemis firma satiri: hucreler yerine "Bu defterdeki dagilim bilinmiyor"
  ve "Mizan yükle" baglantisi
- En altta sutun toplamlari

### CIFT SAYIM KONTROLU
Satir toplamlarinin toplami ile sutun toplamlarinin toplami esit olmak ZORUNDA.
Esit degilse tablonun ustunde kirmizi uyari ciksin: bir hesap iki kurala birden uymus demektir.
Bunu bir birim testiyle de dogrula.

### Altinda
Etikete gore ciro payi: tek satirlik yatay yigin cubuk, her parcanin yaninda
renk karesi + ad + yuzde (renk tek basina anlam tasimasin).

## 5. PERFORMANS
Grup matrisi diger firmalarin agacini okuyacagi icin SADECE sekmeye girildiginde yuklensin,
sayfa acilisinda degil. Yukleme sirasinda iskelet/bekleme durumu gosterilsin.
Bir firmanin verisi alinamazsa o satir "ulasilamadi" desin, tablo bozulmasin.

## 6. DAYANIKLILIK
3A'daki koruma burada da gecerli: etiket ya da matris cagrilarindan biri patlarsa
yalnizca o bolum etkilensin, sayfanin geri kalani calismaya devam etsin.
Global hata toast'i cikmasin.

## 7. CSS
Yeni siniflar `gg-` onekiyle. `et-`, `bl-`, `or-`, `hs-`, `fk-mizan-*` siniflarina dokunma.

## 8. DOGRULAMA (firmaId 3, 2026)
Hazirlik: "Grup Şirketi" boyutu, kapsam `600,601,610`, degerler
Kurumsal Strateji (FirmaId = bu firma) ve PKF Aday (FirmaId = PKF Aday firmasi),
kurallar `600 1 20*` -> Kurumsal, `600 1 21*` -> Aday.

Beklenen:
- Yasal net satislar **22.020.812,69** (bugunku degerle ayni, degismemeli)
- Yonetsel net satislar **12.756.120,69**
  (22.020.812,69 - 9.264.692,00; Aday mizani yuklu degilse eklenecek tutar yok
   ve bu ekranda "PKF Aday mizanı yüklenmedi" olarak belirtilmeli)
- Matris, Kurumsal Strateji satiri:
  Kurumsal **12.194.054,99** · Aday **9.264.692,00** · Atanmamis **562.065,70** ·
  satir toplami **22.020.812,69**
- Satir toplamlari = sutun toplamlari
- Yonetsel gorunumde amber uyari serti gorunuyor
- Yasal gorunum varsayilan; sayfa yenilenince yasal geliyor
- Bilanco farki hala **-7.123.455,07**, Finansal Yapi **%50,73 / %49,27 / %49,27 / 0,97**
- Vergi Hesaplamasi ve Beyanname ciktilarinda hicbir degisiklik yok

## 9. TESLIM
- Eklenen ve degisen dosyalar
- FirmaId alaninin nasil eklendigi ve migration durumu
- Yonetsel hesabin hangi dosyada oldugu
- Madde 8'deki rakamlarin tuttugu mu
- Matris cift sayim kontrolunun nerede ve testinin gectigi
- Yonetsel gorunumun hicbir resmi ciktiya sizmadiginin nasil garanti edildigi
- Derleme ve test sonucu
- Commit atma.
