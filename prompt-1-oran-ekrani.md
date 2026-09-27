# PROMPT 1 — FİNANSAL ORANLAR EKRANI YENİDEN TASARIM

## KURAL
- Calisan hicbir hesaplama degistirilmeyecek. Oran formulleri, referans degerleri, durum
  kararlari, MizanHesaplayici, MaliTabloIsareti, hesap_plani.json AYNEN KALACAK.
- Bu gorev sadece SUNUM katmanini degistirir: ayni oranlar, yeni yerlesim.
- Yeni oran EKLEME, mevcut oran CIKARMA yok.
- Commit atma.

## 1. ONCE INCELE
Kod yazmadan once bildir:
- Finansal Oranlar sekmesini olusturan bilesen(ler)in tam yolu
- Bes alanin (Likidite, Faaliyet, Finansal Yapi, Karlilik-Satis, Karlilik-Sermaye/Varlik)
  nasil olusturuldugu: tek bilesen mi, alan basina bir bilesen mi
- Her oranin nasil tanimlandigi: ad, formul metni, deger, referans, durum hangi tipte tutuluyor
- Ozkaynaklar ve Pasif Toplam degerlerini NEREDEN aliyor. BilancoPanel ile AYNI kaynak mi?
  Farkli kaynaktan aliyorsa DUR ve bildir, kendin birlestirme.

## 2. YENI YERLESIM
Sayfa ikiye bolunur: solda sade rakam listesi, sagda secili alanin grafigi.
Mevcut "her alan icin ayri kart + yan yana iki tablo" duzeni kalkar.

### 2.1 Ust serit (tam genislik, zemin #F8FAFC, alt kenarlik 1px #E1E0D9)
- Toplam oran sayisi ve alan sayisi: "35 oran · 5 alan"
- Durum sayaclari, her biri renkli nokta + metin:
  OLUMLU #0ca30c / metin #046a04 · SINIRDA #fab219 / metin #8a5a00 ·
  OLUMSUZ #d03b3b / metin #b02a2a · REFERANSSIZ #CBD5E1 / metin #52514E
- Sagda "Referanslari duzenle" butonu (islevsiz olabilir, yerini tutsun)

### 2.2 Sol sutun (genislik 600px, sag kenarlik 1px #E1E0D9, kendi icinde kayar)
Kolon basligi (yapiskan): Oran | Deger (96px) | Referans (68px) | Durum (76px)

Alan basliklari: zemin #F8FAFC, ust ve alt kenarlik 1px #E1E0D9, 10px, 700,
letter-spacing .1em, #52514E, solda "1 · LİKİDİTE" sagda oran sayisi.
Alan basliklari kaydirirken yapiskan olsun.

Oran satiri: min 28px, ad 12px, deger 12px 600 monospace saga dayali,
referans 10px #898781, durum 10px 700 nokta + metin.
Hesaplanamayan oran: ad ve deger soluk (#898781 / #CBD5E1), durum yerine sebep
("STOK YOK", "GİDER YOK", "UVYK YOK").
Secili alanin satirlari: zemin #EDF2F7, sol ic golge 3px #2C5282.

Faaliyet alani iki donem mizani olmadan hesaplanamiyorsa alan basligi altinda
tek satir bilgi ve "Onceki donemi yukle" baglantisi; 10 orani tek tek listeleme.

### 2.3 Sag sutun (kalan genislik, zemin #F8FAFC, padding 14px 24px)
Ustte alan sekmeleri (her alan icin bir buton). Secili: #2C5282 zemin, beyaz yazi.
Hesaplanamayan alanin sekmesi soluk.

Altinda secili alanin grafik kartlari (beyaz zemin, 1px #E1E0D9, radius 10px, padding 16px 18px):

**Referansli oranlar icin BANT GRAFIK.** Oranlarin birimleri farkli oldugu icin ortak
eksen olarak "referansin kati" kullanilir: cizilen deger = oran / referans degeri.
1,00x tam ortada, siyah 2px dikey cizgi ile isaretli; eksen 0 ile 2,0x arasi.
2,0x ustunu kirp, cubugun sag ucuna ">>" isareti koy ve gercek degeri satir sonunda yaz.
Cubuk rengi durumun rengi. Satir sonunda deger "1,32x" bicinde.
Kart basliginin altinda: "1,00x = referans degeri".

**Referanssiz oranlar icin DUZ YUZDE CUBUGU.** Eksen 0-100, referans cizgisi yok,
cubuk rengi #2a78d6, satir sonunda "%76,81".
Yuzde 100'u asan oran varsa (ornegin Donem Net Kari / Duran Varliklar %1.721,61)
o satiri cubuksuz, yalniz rakam olarak goster; ekseni bozmasin.

**Finansal Yapi alaninda ayrica KAYNAK YAPISI cubugu.** Tek satir, %100 genislikte,
uc parca: KVYK #eb6834, UVYK #1baf7a, Ozkaynaklar #2a78d6, aralarinda 2px bosluk.
Altinda her parcanin adi, renk karesi ve yuzdesi yazili (renk tek basina anlam tasimasin).

**Likidite alaninda ayrica DONEN VARLIK / KVYK cubugu.** Iki yatay cubuk ayni olcekte,
altinda net isletme sermayesi farki.

### 2.4 Bilanco denklik uyarisi
Aktif Toplam ile Pasif Toplam arasindaki fark 0,01'den buyukse ust seritin altinda
amber serit: zemin #FFFBEB, kenarlik 1px #F59E0B, ucgen uyari ikonu, metin #7C4A0A:
"Bilanco denk degil (fark X) — mali yapi oranlari guvenilir degil." ve "Bilancoya git" baglantisi.
Fark yoksa serit hic cikmaz.

## 3. DAVRANIS
- Sag sekmeden alan secilince sol listede o alanin satirlari isaretlenir ve o alana kaydirilir.
- Sol listede bir alan basligina tiklanmasi da ayni secimi yapar.
- Grafikler mevcut oran degerlerinden turetilir, yeniden hesaplama YOK.

## 4. CSS
Yeni siniflar `or-` onekiyle. `bl-` (Bilanco) ve `fk-mizan-*` siniflarina dokunma.
Renk ve olcu degerleri dosyanin basinda CSS degiskeni olsun.
Durum renkleri: nokta/cubuk dolgu renkleri yukaridaki degerler, METIN renkleri ise
#046a04 / #8a5a00 / #b02a2a (acik zeminde okunabilirlik icin).

## 5. DOGRULAMA (firmaId 3, 2026)
Yeni ekranda su degerler eskisiyle BIREBIR AYNI cikmali:
Cari 1,32 · Asit-Test 1,32 · Nakit 0,00 · Cabuk 11,48 ·
Brut Satis Karliligi %76,81 · Faaliyet Karliligi %53,86 · Net Kar Marji %51,67 ·
ROA %68,74 · DNK/KVYK %94,74 · DNK/Donen %71,60
Bir tanesi bile degisirse DUR ve bildir.

## 6. TESLIM
- Degistirilen ve eklenen dosyalar
- Ozkaynak/Pasif kaynagi BilancoPanel ile ayni mi
- Dogrulama listesindeki on degerin tuttugu mu
- Derleme ve test sonucu
- Commit atma.
