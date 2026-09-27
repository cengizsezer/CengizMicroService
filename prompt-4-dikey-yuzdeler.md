# PROMPT 4 — DİKEY YÜZDELER EKRANI YENİDEN TASARIM

## AMAC
Dikey Yuzdeler sekmesini yeniden duzenlemek. Bugunku dort alt sekme kalkiyor,
yerine solda UC GORUNUM geliyor: Bilanco · Gelir Tablosu · Ozet Grafik.

## KURAL
- Hicbir hesaplama, formul ya da oran degismeyecek. MizanHesaplayici, MaliTabloIsareti,
  GelirTablosuCalculator, hesap_plani.json, BilancoPanel, FinansalOranlarGrid,
  EtiketMotoru, GrupGorunumu DEGISMEYECEK.
- Bu gorev sunum katmanidir: ayni rakamlar, yeni yerlesim.
- Yeni kalem ekleme, mevcut kalem cikarma yok.
- Alt kirilimlar yine hicbir toplama girmiyor; yalnizca Ozet Grafik'te BILGI olarak gosterilir.
- Commit atma.

## 1. ONCE INCELE
Kod yazmadan once bildir:
- Dikey Yuzdeler sekmesini ve dort alt sekmesini olusturan bilesen(ler)in tam yolu
- Her alt sekmenin hangi bilesenle ciziliyor oldugu (GelirTablosuGrid, BilancoOzetGrid, digerleri)
- Bilanco tarafinda dikey yuzdenin PAYDASI ne: aktif kalemleri neye, pasif kalemleri neye bolunuyor
- Ozkaynak ve pasif toplami Detay.razor'daki donem kari duzeltmesini iceriyor mu
- Alt hesap agacina erisim: GetHesapAgaciAsync bu ekrandan cagrilabilir mi
Bulduklarini yaz, sonra uygula.

## 2. YERLESIM (uc gorunumde ortak kabuk)
Sol sutun 268px, zemin #F8FAFC, sag kenarlik 1px #E1E0D9.
- "GORUNUM" basligi, altinda uc buton:
  **Bilanço** ("Aktif ve pasif, kendi toplamlarına")
  **Gelir Tablosu** ("Net satışlara oranla")
  **Özet Grafik** ("Gelirler, giderler, varlıklar, kaynaklar")
  Secili olan: beyaz zemin, 1px #2C5282 kenarlik, `box-shadow: inset 3px 0 0 #2C5282`
- Ayirici cizginin altinda onay kutulari (gorunume gore degisir, asagida)
- En altta o gorunume ait ozet rakamlar

Sag sutun: yapiskan arac cubugu ve kolon basligi, altinda KAYAN govde.
Kaydirma yalnizca govdede olacak; sayfanin tamami kaymayacak.

## 3. GORUNUM 1 — BILANCO
Onay kutulari: "Sadece hareket görenler" (varsayilan acik) · "Sadece ana kalemler" ·
"Çubukları göster" (varsayilan acik)
Sol altta: AKTIF TOPLAM, PASIF TOPLAM ve fark varsa amber kutu.

Tablo kolonlari: KOD (58px) | HESAP ADI (esner) | ÖNCEKİ (116px) | CARİ DÖNEM (140px) |
DİKEY % (62px) | ÇUBUK (156px)

Satir tipleri:
- Bolum ayraci (AKTIF / PASIF): zemin #16182A, beyaz, 10px, letter-spacing .14em
- Roma grubu: zemin #2C5282, beyaz, 34px
- Alt grup: zemin #F8FAFC, ust ve alt kenarlik 1px #E1E0D9
- Hesap: 30px, alt kenarlik 1px #F8FAFC
- Toplam: zemin #EDF2F7, ust kenarlik 2px #2C5282, metin #1F3D61

AKTIF ve PASIF AYNI KAYDIRMADA alt alta. Ayri sekme yok.

"Sadece ana kalemler" isaretlenince hesap satirlari gizlenir, grup ve alt grup kalir —
bugunku "Bilanço Özet" alt sekmesinin yerini bu tutuyor.

## 4. GORUNUM 2 — GELIR TABLOSU
Ayni kabuk. Kolon basligi DİKEY % yerine "NET SATIŞ %".
Ara toplam satirlari (BRUT SATIS KARI, FAALIYET KARI, OLAGAN KAR) zemin #EDF2F7,
metin #1F3D61, kalin. 690 satiri zemin #2C5282 beyaz; 692 satiri zemin #16182A beyaz.
Bugunku "Gelir Tablosu Özet" alt sekmesi de "Sadece ana kalemler" ile karsilaniyor.

## 5. CUBUK KURALI (gorunum 1 ve 2)
Cubuk genisligi = dikey yuzdenin MUTLAK degeri. Tam genislik = %100. Baska olcek yok.
Pozitif #2a78d6, negatif #eb6834. Negatif tutarlar parantezli ve metin rengi #8a3a12.
Arac cubugunda su not: "Çubuklar dikey yüzdeyi gösterir · tam genişlik = %100".
"Çubukları göster" kapatilinca kolon tamamen kalkar (duz tablo, Excel'e yapistirmak icin).

## 6. AKTIF/PASIF TABAN UYARISI — ZORUNLU
Bilanco gorunumunde aktif kalemleri aktif toplamina, pasif kalemleri pasif toplamina
oranlanir. Aktif ile Pasif arasindaki fark 0,01'den buyukse sol sutunda amber kutu ciksin:
"FARK <tutar> · Aktif ve pasif ayrı tabanlara oranlandı".
Fark yoksa kutu hic cikmaz. Bu uyari olmadan iki sutunun yuzdeleri yanlis karsilastirilir.

## 7. GORUNUM 3 — OZET GRAFIK
Onay kutulari: "Alt kırılımları göster" (varsayilan acik) · "Sıfır bakiyeleri de göster"
Sol altta: TICARI KAR ve altinda "Gelirler − satış iadeleri − giderler".

Ust serit: dort kutu yan yana — GELIRLER · GIDERLER · VARLIKLAR · KAYNAKLAR toplamlari.

Altinda dort bolum, her biri kendi kartinda, hepsi ayni kaydirmada:

**GELIRLER** = 600 + 601 + 642 + 645 + 646 + 679
600'un altinda hesap agacindan gelen alt kirilim, girintili ve soluk renkte.
En altta ayri bir eksi satir: 610 Satistan Iadeler.

**GIDERLER** = 740 + 760 + 770 + 654 + 656 + 689
770'in altinda hesap agacindan gelen alt kirilim, iki seviye girintili.

**VARLIKLAR** = aktif kalemleri, buyukten kucuge sirali.
Kontra hesaplar netlenerek gosterilir (255 ile 257 tek satir "Demirbaşlar (net)",
264 ile 268 tek satir "Özel Maliyetler (net)"). Netleme yalnizca bu gorunumde,
bilanco toplamlarina dokunmaz.

**KAYNAKLAR** = pasif kalemleri, buyukten kucuge sirali.
Ozkaynak kalemleri #1baf7a, borc kalemleri #eb6834. Renk tek basina anlam tasimasin:
kart basliginda "yeşil özkaynak, turuncu borç" yazsin.

Her bolumun altinda tek cumlelik bir not satiri (zemin #F8FAFC).

### Ozet Grafik cubuk kurali — GORUNUM 1-2'DEN FARKLI
Burada cubuk o BOLUMUN EN BUYUK KALEMINE gore olceklenir (siralama okunsun diye),
yaninda her zaman gercek yuzde yazili durur. Kart basliginda hangi kurala gore
cizildigi belirtilsin. Iki farkli kural bilerek var; karistirma.

### Alt kirilim yoksa
Hesap agaci yuklu degilse (mizan yeniden yuklenmemisse) alt kirilim satirlari
hic cizilmez, ana hesaplar normal gorunur, HATA VERILMEZ.
Bolum basliginda tek satir: "Alt kırılım için bu dönemin mizanını yeniden yükleyin".

## 8. CSS
Yeni siniflar `dy-` onekiyle. `bl-`, `or-`, `hs-`, `et-`, `gg-`, `fk-mizan-*`
siniflarina dokunma. Renk ve olcu degerleri dosyanin basinda CSS degiskeni olsun.
Tutarlar monospace ve `font-variant-numeric: tabular-nums`.

## 9. DOGRULAMA (firmaId 3, 2026)
Bilanco gorunumu:
- I Dönen Varlıklar 15.889.452,31 %96,01 · II Duran Varlıklar 660.856,62 %3,99
- 120 Alıcılar 10.120.346,89 %61,15 · 136 Diğer Çeşitli Alacaklar 3.846.996,20 %23,24
- 193 Peşin Ödenen Vergiler 1.165.312,89 %7,04 · 102 Bankalar 57.558,81 %0,35
- AKTİF TOPLAM 16.550.308,93 %100,00
- III KVYK 12.008.789,15 %50,73 · V Özkaynaklar 11.664.974,85 %49,27
- Fark 7.123.455,07 oldugu icin amber kutu GORUNMELI

Gelir tablosu gorunumu:
- Brüt satışlar 22.687.603,19 %103,03 · Net satışlar 22.020.812,69 %100,00
- Brüt satış kârı 16.914.349,78 %76,81 · Faaliyet kârı 11.860.605,69 %53,86
- Olağan kâr 11.427.177,93 %51,89 · Ticari kâr 11.377.357,68 %51,67
- Dönem net kârı 8.533.018,26 %38,75

Ozet Grafik:
- GELİRLER 22.968.518,68 · GİDERLER 10.924.370,50 · VARLIKLAR 16.550.308,93 ·
  KAYNAKLAR 23.673.764,00
- 600 altinda Kurumsal 12.194.054,99 ve Aday 9.264.692,00
- 770 altinda Çeşitli Giderler 1.457.943,41, onun altinda Ortak Alan Giderleri 1.416.054,75;
  Dışarıdan Sağlanan Fayda ve Hizmet 739.128,04, onun altinda SMMM Ücretleri 739.128,04

### Birim test — dort bolum birbirini kapatmali
`Gelirler 22.968.518,68 − satış iadeleri 666.790,50 − Giderler 10.924.370,50 = 11.377.357,68`
Bu esitlik ticari kara (690) esit olmak ZORUNDA. Tutmuyorsa bir hesap iki bolumde
sayilmis ya da atlanmis demektir; testi yaz ve gectigini raporla.

Diger sekmelerdeki (Bilanço, Gelir Tablosu, Vergi, Finansal Oranlar, Etiketler,
Grup Görünümü) HICBIR rakam degismemeli.

## 10. TESLIM
- Eklenen ve degisen dosyalar
- Dort alt sekmenin nasil uc gorunume donustugu
- Iki farkli cubuk kuralinin nerede uygulandigi
- Madde 9'daki rakamlarin tuttugu mu
- Dort bolum kapama testinin gectigi
- Derleme ve test sonucu
- Commit atma.
