# PROMPT 3A — ETİKET SİSTEMİ (TANIM VE ETİKETLEME)

## AMAC
Alt hesaplara etiket takmak. Ornek: 600 1 20 -> "Kurumsal Strateji", 600 1 21 -> "PKF Aday".
Bu adimda SADECE tanim, kural motoru ve etiketleme ekrani yapilir.
Raporlarda kullanim (yasal/yonetsel gorunum, grup matrisi) AYRI bir adimda, 3B'de.

## KURAL
- Bu adimda HICBIR mevcut rakam degismeyecek. Bilanco, Gelir Tablosu, Vergi Hesaplamasi,
  Dikey Yuzdeler ve Finansal Oranlar aynen kalacak. Etiketler henuz hicbir hesaplamaya girmiyor.
- Alt kirilimlar yine hicbir toplama girmiyor.
- MizanHesaplayici, MaliTabloIsareti, hesap_plani.json, BilancoPanel, MizanGrid,
  BilancoOzetGrid, FinansalOranlarGrid degismeyecek.
- Commit atma.

## 1. VERI MODELI

**EtiketBoyutu**: Id · Ad ("Grup Sirketi") · Kapsam (TumFirmalar | BuFirma) ·
FirmaId (Kapsam=BuFirma ise dolu, degilse null) · Sira

**EtiketDegeri**: Id · BoyutId · Ad ("PKF Aday") · Renk (hex) · Sira

**EtiketKurali**: Id · BoyutId · Sira · EslesmeTipi (KodDeseni | AdIcerir | TekSecim) ·
Desen (string) · DegerId · FirmaId (null = tum firmalar)

Etiketler DONEMDEN BAGIMSIZDIR: kural hesap koduna baglanir, her donemde ayni kurallar calisir.
Yeni donem mizani yuklendiginde etiketler kendiliginden uygulanir, yeniden tanimlanmaz.

Saklama: agacin saklandigi servisle ayni (CatalogService). Ancak kurallar TANIMDIR,
turetilmis veri degildir — agac gibi JSON blob'a yazilmaz, normal tablolarda dursun.

## 2. KURAL MOTORU
Bir dugume etiket atanirken kurallar Sira'ya gore gezilir, ILK ESLESEN kazanir, devam edilmez.

- **KodDeseni**: sondaki `*` joker. `600 1 21*` hem "600 1 21" hem de onun altindaki
  butun kodlari eslestirir. Joker yoksa tam eslesme.
- **AdIcerir**: hesap adinda gecen metin. Buyuk/kucuk harf ve Turkce karakter duyarsiz
  (i/I/ı/İ, ş/s, ğ/g, ü/u, ö/o, ç/c).
- **TekSecim**: tam kod eslesmesi, tek bir dugum.

Hicbir kurala uymayan dugum ETIKETSIZ kalir.

## 3. CIFT SAYIM KORUMASI — EN KRITIK MADDE
600, 600 1 ve 600 1 21 ayni paradir. Etiket toplamlari hesaplanirken her ana hesap icin
agacta yukaridan asagiya inilir:

- Dugum etiketliyse: TUM bakiyesi o etikete yazilir ve **altina INILMEZ**.
- Dugum etiketsiz ve cocugu varsa: cocuklarina inilir. Ayrica
  `(dugumun bakiyesi - cocuklarin bakiye toplami)` farki varsa o fark "Atanmamis"a yazilir
  (dogrudan ust hesaba yazilmis tutar).
- Dugum etiketsiz ve cocugu yoksa: bakiyesi "Atanmamis"a yazilir.

Bu kuralin sonucu: **her etiketin toplami + Atanmamis = ana hesabin bakiyesi, HER ZAMAN.**
Bu esitligi birim testiyle dogrula; tutmuyorsa algoritma yanlistir.

## 4. EKRAN
`Detay.razor`'a EN SONA yeni sekme: "ETİKETLER".
Sona ekle ki mevcut sekme indeksleri kaymasin; `VergiSekmesiIndeksi` ve
`BilancoSekmesiIndeksi` degismemeli.

### 4.1 Sol sutun (genislik 340px, zemin #F8FAFC, sag kenarlik 1px #E1E0D9)
- "ETIKET BOYUTLARI" basligi ve boyut listesi. Secili boyutun sol kenarinda
  `box-shadow: inset 3px 0 0 #2C5282`, beyaz zemin, 1px #2C5282 kenarlik.
  Her boyutun altinda "N deger · tum firmalarda" / "sadece bu firma" bilgisi.
- "+ Yeni boyut" butonu
- Ayirici cizginin altinda secili boyutun DEGERLERI: renk karesi (10x10, radius 2px),
  ad, sagda o etikete dusen toplam tutar (monospace, tabular-nums)
- "Atanmamis" satiri amber zeminde (#FFFBEB), metin #7C4A0A, tutariyla
- "+ Deger ekle" butonu
- En altta KAPSAM karti: parcali yatay cubuk (her degerin payi + atanmamis, aralarinda 2px
  bosluk) ve altinda "Net satislarin %X'i etiketli. Y atanmamis." cumlesi

### 4.2 Sag sutun — KURALLAR tablosu
Kolonlar: Sira | Eslesme tipi | Desen | Etiket | Kac hesap | Tutar
- Satirlar yukari/asagi butonuyla siralanabilsin (sira onemli, ilk eslesen kazaniyor)
- Tablo basliginin yaninda "Yukaridan asagiya uygulanir, ilk eslesen kazanir" notu
- "+ Kural ekle" butonu. TekSecim tipinde kural eklerken mevcut `HesapSeciciDialog` acilsin.
- Etiket hucresinde cip: renk karesi + deger adi, ince kenarlikli
- Tablonun altinda, etiketsiz kalan bakiyeli hesap varsa amber serit:
  ucgen uyari ikonu + "Hicbir kurala dusmeyen N hesap var" + "Goster" baglantisi

### 4.3 Sag sutun — HESAPLAR VE ETIKETLERI tablosu
Kolonlar: Kod | Hesap adi | Bakiye | Etiket
- Etiket hucresi: kuraldan gelen etiket cip olarak gosterilir
- Etiketsiz satirlar amber zeminde; etiket hucresinde bir acilir liste ile tek tiklik
  atama yapilabilsin. Boyle bir atama, o koda ait bir **TekSecim kurali** olarak kaydedilsin.
- Ust tarafta arama kutusu (kod ve ad) ve "Sadece gelir hesaplari" onay kutusu
- Ana hesap satirinda etiket hucresinde "alt kirilimdan" yazsin (kendisi etiketli degilse)

## 5. TEMIZLIK
PROMPT 2'de Mizan sekmesine konan gecici **"Hesap seç (deneme)"** butonu ve ona bagli
`secilenHesapOzeti` / `HesapSeciciAcAsync` uyeleri KALDIRILSIN.
`HesapSeciciDialog` kalir; artik kural ekleme akisindan cagrilir.

## 6. CSS
Yeni siniflar `et-` onekiyle. `bl-`, `or-`, `hs-`, `fk-mizan-*` siniflarina dokunma.
Renk ve olcu degerleri dosyanin basinda CSS degiskeni olsun.

## 7. DOGRULAMA (firmaId 3, 2026 — strateji mizani)
Su tanimlar yapilarak test edilecek. Boyut: "Grup Sirketi", Kapsam: TumFirmalar.
Degerler: "Kurumsal Strateji" (#2a78d6) · "PKF Aday" (#eb6834) · "PKF Ekip" (#1baf7a)
Kurallar:
1. KodDeseni `600 1 20*` -> Kurumsal Strateji
2. KodDeseni `600 1 21*` -> PKF Aday

Beklenen sonuc:
- Kurumsal Strateji **12.194.054,99**
- PKF Aday **9.264.692,00**
- Ikisinin toplami = 600'un bakiyesi **21.458.746,99** (cift sayim yok)
- 601 (1.228.856,20) ve 610 (-666.790,50) etiketsiz, amber zeminde listeleniyor
- Diger sekmelerdeki hicbir rakam degismedi

Birim testler:
- Madde 3'teki "etiketler toplami + Atanmamis = ana hesap bakiyesi" esitligi, 600 icin
- Ic ice etiket senaryosu: `600 1` etiketliyken `600 1 21` de etiketliyse UST dugum kazanir,
  alt dugum ayrica SAYILMAZ; toplam yine 21.458.746,99 cikar
- Dogrudan ust hesaba yazilmis tutar senaryosu: ana hesap 1.000, tek cocugu 600 ise
  etiketlenen 600, Atanmamis 400 olmali
- AdIcerir kuralinda Turkce karakter duyarsizligi ("aday" ile "ADAY" ayni eslesir)

## 8. TESLIM
- Eklenen ve degisen dosyalar
- Kural motorunun ve cift sayim korumasinin hangi dosyada oldugu
- Madde 7'deki dort rakamin tuttugu mu
- Gecici "Hesap seç (deneme)" butonunun kaldirildigi
- Derleme ve test sonucu
- Commit atma.
