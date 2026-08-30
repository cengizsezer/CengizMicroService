# Görev: Gerçek ORKA Aktarımı (D Adımı)

Bu görevi baştan sona, soru sormadan tamamla. Önce `src/Robot.Agent/` içindeki
JSON adım motorunu, `GridDoldur` adımını ve C adımında eklenen `IIsCalistirici`
arayüzünü oku.

Mevcut testlerin tamamı aynen geçmeye devam etmeli.

---

## Kapsam

C adımındaki sahte iş yerine **gerçek ORKA aktarımı**. Ajan iş paketini indirip
ORKA'yı sürecek.

Bağlantı, iş kuyruğu ve ilerleme altyapısı değişmeyecek — yalnız
`IIsCalistirici`'nin gerçek uygulaması ekleniyor.

---

## 1. İş paketi

Yeni iş tipi: `OrkayaAktar`

Yük (`Yuk`) içeriği:
```
EkstreYuklemeId
FirmaId
BankaHesabiOrkaKodu      (ör. "102 1 1 01" — hangi hesaba aktarılacak)
FirmaKodu                (ORKA giriş zincirinde kullanılan)
SatirSayisi              (doğrulama için)
```

**Dosyalar iş yüküne gömülmesin.** Ajan iki dosyayı ayrı indirsin:

```
GET /api/catalog/banka-ekstre/{yuklemeId}/duzeltilmis-ekstre   → xlsx
GET /api/catalog/banka-ekstre/{yuklemeId}/kod-listesi          → JSON
```

Ajan token'ıyla erişilebilsin (şu an bu uçlar kullanıcı token'ı bekliyorsa
ajan token'ını da kabul edecek şekilde genişlet — yalnız bu iki uç).

İndirilen dosyalar `%AppData%\PkfRobot\isler\{isId}\` altına yazılsın, iş
bitince temizlensin (başarısızsa incelemek için 7 gün kalsın).

## 2. Ön doğrulamalar — çalıştırmadan önce

Sırayla, biri tutmazsa **hiç başlama** ve sebebini bildir:

- İki dosya da indi mi, boyutları makul mü
- Düzeltilmiş ekstre satır sayısı = kod listesi satır sayısı = `SatirSayisi`
- Kod listesindeki her satırda `Aciklama` ve `KarsiHesapKodu` dolu mu
- ORKA süreci çalışıyor mu (çalışmıyorsa başlat, giriş zincirini yürüt)

Sayı uyuşmazlığı en tehlikeli durum — kodlar yanlış satırlara gider. Burada
durmak, yanlış kayıttan kat kat iyi.

## 3. ORKA akışı

Mevcut JSON adım motorunu kullan, yeni adım tipi yazma. Akış:

1. ORKA açık değilse başlat, giriş zincirini yürüt (şifre+Enter → F7 → firma
   kodu → Enter → Enter → `pkf03` → ALT+T → Enter)
2. Modül gezinme: RIGHT×3 + DOWN×1 → TRANSFERLER
3. Veri Transferi ekranı (fare tıklamaları — pencere maximize, oranlı koordinat)
4. Düzeltilmiş ekstre dosyasını yükle
5. Grid'e konumlan, **`GridDoldur`** çalıştır — kod listesi girdi olarak
6. **Kaydet'e ASLA basma.** Bu kural değişmedi ve değişmeyecek.

İş akışının kendisi JSON'da tanımlı olsun (`gorevler/orkaya-aktar.json` gibi),
kod içine gömülmesin. Firma kodu ve dosya yolu JSON'a parametre olarak geçsin.

## 4. İlerleme bildirimi

Anlamlı adımlarda bildir, her satırda değil:

```
%5   ORKA başlatılıyor
%15  Giriş yapılıyor
%25  Veri Transferi ekranı açılıyor
%35  Ekstre dosyası yükleniyor
%45  Grid doğrulanıyor (satır sayısı)
%50-95  Karşı hesap kodları yazılıyor (her 10 satırda bir bildir)
%100 Tamamlandı — Kaydet'e basılmadı, kontrol edip kaydedin
```

`GridDoldur` satır bazında ilerleme dönebiliyorsa onu kullan; dönmüyorsa
ilerleme geri çağrısı ekle.

## 5. Sonuç ve hata

**Başarılı sonuç özeti:**
```
{ YazilanSatir, ToplamSatir, SureSaniye, KaydetBasilmadi: true }
```

Kullanıcıya ekranda net bir mesaj: *"175 satır yazıldı. ORKA'da kontrol edip
Kaydet'e basın."*

**Hata durumunda:**
- `GridDoldur`'un durma sebebi olduğu gibi iletilsin (satır no + sebep)
- **Ekran görüntüsü alınsın** ve sunucuya yüklensin — hata ekranında
  görüntülenebilsin. Mevcut dosya saklama altyapısını kullan.
- ORKA'da kaydedilmemiş değişiklik kaldıysa kullanıcıya söylensin: *"ORKA'da
  yarım kalmış giriş var, kaydetmeden ekranı kapatın."*

Ekran görüntüsünde hassas veri olabilir — sunucuda firma kapsamıyla saklansın,
belirli süre sonra temizlensin.

## 6. Güvenlik kuralları

Bunlar pazarlık konusu değil:

- **Kaydet'e basılmaz.** Kullanıcı gözle kontrol edip kendisi basar.
- **Herhangi bir doğrulama tutmazsa iş durur.** Tahmin edip devam etme.
- **ORKA'nın veritabanına yazma yok.** Yalnız arayüz üzerinden.
- Ajan iş sırasında ORKA penceresini öne getirir; kullanıcı o sırada makineyi
  kullanıyorsa uyarılsın (iş başlarken ekranda bir bildirim)

---

## Testler

ORKA'ya bağlı davranışlar ev makinesinde test edilemez. Test edilebilir kısmı
ayır ve şunları sına:

- İş paketi doğrulaması: satır sayısı uyuşmazlığında iş başlamıyor
- Kod listesinde eksik alan varsa iş başlamıyor
- Dosya indirilemezse anlaşılır hata
- İlerleme yüzdeleri doğru sırada ve artan
- Sonuç özeti doğru üretiliyor
- Hata durumunda ekran görüntüsü yükleme çağrılıyor (sahte ile)

ORKA akışının kendisi **ofiste elle** doğrulanacak — `OZET.md`'ye adım adım
kontrol listesi yaz.

## Kabul kriterleri

1. Derleme temiz, publish alınabiliyor, mevcut testler aynen geçiyor
2. Yukarıdaki testler yazıldı ve geçiyor
3. C adımının bağlantı ve iş altyapısı değişmedi — yalnız `IIsCalistirici`
   uygulaması eklendi
4. `GridDoldur`'un Kaydet'e basmama kuralı korundu; bunu bir testle sabitle
5. İş akışı JSON'da, kodda gömülü değil
6. Ofis doğrulama kontrol listesi `OZET.md`'de

Sonunda `OZET.md` ve `KARARLAR.md` güncelle.
