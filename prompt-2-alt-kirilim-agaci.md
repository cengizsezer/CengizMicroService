# PROMPT 2 — ALT HESAP KIRILIM AĞACI

## AMAC
Mizandaki alt hesap kirilimlarini (600 1, 600 1 21 gibi) okumak, agac olarak tutmak ve
bir hesap secici bilesende gostermek. Bu, sonraki adimda kurulacak etiket sisteminin temeli.

## KURAL — EN ONEMLI MADDE
**Alt kirilimlar HICBIR toplama girmez.** Bilanco, Gelir Tablosu, Vergi Hesaplamasi,
Dikey Yuzdeler ve Finansal Oranlar eskisi gibi YALNIZCA uc haneli ana hesap kodlarindan
hesaplanmaya devam eder. Alt kirilimlar ayri bir yapida durur ve sadece bu gorevde
eklenen secici bilesende gorunur.
Bunun sebebi: 600 = 21.458.746,99, 600 1 = 21.458.746,99, 600 1 21 = 9.264.692,00 —
ayni para. Toplama karistirilirsa ciro uce katlanir.

Ayrica:
- MizanHesaplayici, MaliTabloIsareti, hesap_plani.json, BilancoPanel, MizanGrid,
  BilancoOzetGrid degismeyecek.
- Mevcut `GetRawMizanValuesAsync` ciktisi DEGISMEYECEK: iceriginde yine sadece uc haneli
  kodlar olacak. Yeni veri yeni bir kanaldan gelecek.
- Commit atma.

## 1. ONCE INCELE
- `ExcelMizanParser` alt kirilim satirlarini nerede eliyor (AnaHesapKoduPattern /
  AtlamaSebebi.HiyerarsikAltKod), atlanan satirlar hangi listeye yaziliyor
- Parse sonucu hangi tipte donuyor, `_rawCariByFirm` nasil dolduruluyor,
  `GetRawMizanValuesAsync` bu veriyi nasil servis ediyor
- Mizan yukleme akisi: MizanUploadDialog -> IExcelMizanParser -> UpdateMizanFromExcelAsync
Bulduklarini yaz, sonra uygula.

## 2. VERI MODELI
Yeni kayit tipi, ornek ad `HesapDugumu`:
- `Kod` (string, mizandaki hali: "600 1 21")
- `Ad` (string)
- `Seviye` (int, kodun bosluga gore parca sayisi: "600" = 1, "600 1" = 2, "600 1 21" = 3)
- `UstKod` (string?, son parca atilmis hali: "600 1 21" -> "600 1"; seviye 1 ise null)
- `AnaHesapKodu` (string, ilk parca: "600")
- `BorcBakiye`, `AlacakBakiye`, `Bakiye` (decimal)

Seviye ve ust kod KODDAN turetilir, sabit karakter genisligi VARSAYMA.
Kod parcalari harf de icerebilir: "120 1 A08", "770 6 255" gercek orneklerdir.
Ayirici bosluktur; birden fazla ardisik bosluk tek ayirici sayilir.

## 3. PARSER
`ExcelMizanParser` alt kirilim satirlarini artik ATMAYACAK, ayri bir listeye yazacak.
- Uc haneli satirlar: bugunku davranis aynen devam (ana hesap sozlugune gider)
- Alt kirilim satirlari: `HesapDugumu` olarak yeni listeye eklenir
- Ilk parcasi uc haneli sayi OLMAYAN satirlar (baslik, toplam, bos) eskisi gibi atlanir
  ve `AtlananSatirlar` teshis listesinde kalir
- Alt kirilimlar ana hesap sozlugune KESINLIKLE yazilmaz

Siniri: 10.000 dugumden fazla ise fazlasini alma, teshis listesine "kirilim siniri asildi"
yaz ve yuklemeyi bozma.

## 4. SAKLAMA
Agac, ham mizan degerleriyle AYNI yerde tutulur: bellekte, firma + donem bazinda,
her mizan yuklemesinde yeniden kurulur. Yeni bir kalicilik katmani, yeni tablo,
yeni veritabani yazma YOK. Erisim icin mevcut ham deger servisinin yanina
`GetHesapAgaciAsync(firmaId, donem)` benzeri tek bir metot eklenir,
`IReadOnlyList<HesapDugumu>` doner. Agac yapisi listeden `UstKod` ile turetilir.

## 5. DOGRULAMA KONTROLU
Her ana hesap icin: dogrudan altindaki (bir alt seviyedeki) dugumlerin bakiyeleri toplami
ana hesabin bakiyesine esit mi? 0,01 tolerans.
- Esitse: o dugume "tutuyor" isareti
- Esit degilse: "tutmuyor" isareti ve farkin tutari
Bu bir HATA DEGIL, bilgidir; yuklemeyi engellemez, sadece gosterilir.

## 6. HESAP SECICI BILESEN
Yeni bilesen, ornek ad `HesapSeciciDialog.razor`. Modal.
Girdi: firmaId, donem, istege bagli filtre (orn. sadece gelir hesaplari).
Cikti: secilen `HesapDugumu` ve kullanicinin sectigi isaret (+ / -).

Icerik:
- Ust: baslik, kapat butonu, arama kutusu (kod ve adda, buyuk/kucuk harf ve Turkce karakter
  duyarsiz; eslesen metin parcasi isaretlenir), "Sadece bakiyesi olanlar" onay kutusu,
  "Tumunu kapat" butonu
- Orta sol: agac. TDHP ana grubuna gore bolum basliklari ("6 · GELİR TABLOSU HESAPLARI").
  Her dugum satirinda: acilir ok (cocugu varsa), kod (monospace), ad, bakiye (saga dayali).
  Girinti seviyeye gore. Kapali dugumde "N alt kirilim" bilgisi.
  Ana hesapta dogrulama rozeti (madde 5).
- Orta sag (genislik ~308px): secilen hesabin karti (kod, ad, bakiye, borc/alacak yonu),
  ust hesap zinciri, ana hesap icindeki yuzde payi, isaret secimi (+ ekle / - cikar),
  "Ekle" ve "Vazgec" butonlari
- Alt serit: "N satir · N ana hesap · N alt kirilim · en fazla N seviye" ve
  "Alt kirilimlar toplamlara girmez" notu

Performans: agac kapali baslar, sadece acilan dallar render edilir.

## 7. ISARET YONU
Bir dugumun ekranda gosterilen tutari, hesabin TDHP grubuna gore sunum yonune cevrilmis
olmalidir: 600 alacak bakiyeli oldugu halde kullaniciya "21.458.746,99" gorunur,
"-21.458.746,99" degil. Bu cevrim icin mevcut `MaliTabloIsareti` mantigi kullanilir,
elle isaret cevirme yazilmaz. Ekranda gorunen sayi ile ileride hesaplamada kullanilacak
sayi AYNI olmali.

## 8. BU ADIMDA YAPILMAYACAKLAR
- Etiket sistemi yok (sonraki adim)
- Ozel oran formulune baglama yok (sonraki adim)
- Gelir tablosunda veya bilancoda alt kirilim gosterimi yok
- Secici su an yalnizca acilip secim dondurebilen bir bilesen olarak durur;
  nereden acilacagini sonraki adim belirler. Test icin Mizan sekmesine gecici bir
  "Hesap seç" butonu koyabilirsin, teslimde bunu belirt.

## 9. DOGRULAMA (firmaId 3, 2026 — strateji mizani)
- Toplam dugum: 234 satir, bunun 42'si ana hesap, 192'si alt kirilim
- En derin seviye: 4
- 600 agaci: 600 -> 600 1 -> (600 1 20 = 12.194.054,99 ; 600 1 21 = 9.264.692,00)
  ve 12.194.054,99 + 9.264.692,00 = 21.458.746,99 = 600 (dogrulama "tutuyor" demeli)
- 770 agaci: 5 alt grup, toplamlari 2.374.451,51 (dogrulama "tutuyor")
- 120 1 altinda 6 cari hesap, 120 3 altinda 6 cari hesap
- Bilanco, Gelir Tablosu, Vergi ve Finansal Oranlar sekmelerindeki HICBIR rakam degismemeli.
  Bir tanesi bile degisirse DUR ve bildir.

## 10. TESLIM
- Eklenen ve degistirilen dosyalar
- Parser'in alt kirilimi nereye yazdigi ve ana hesap sozlugune karismadiginin nasil garanti edildigi
- Dogrulama listesindeki sayilarin tuttugu mu
- Seciciyi test icin nereye bagladigin
- Derleme ve test sonucu
- Commit atma.
