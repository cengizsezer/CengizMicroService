# Görev: DijitalMasraf — Banka Ekstresi İşleme Modülü

Bu görevi **baştan sona, bana soru sormadan** tamamla. Belirsizlik çıkarsa aşağıdaki
"Belirsizlik kuralları" bölümüne bak; orada da yoksa en muhafazakâr seçeneği al,
kararını `KARARLAR.md` dosyasına yaz ve devam et. Durma.

---

## 1. Önce incele, sonra yaz

Kod yazmadan önce repoyu oku ve mevcut kalıpları öğren. En az şunlara bak:

- Çözüm dosyası ve servis klasörleri — hangi mikroservisler var, isimlendirme nasıl
- Mevcut bir servisin tam dikey kesiti (Controller → Service → Repository → EF
  DbContext → Migration) — yeni modül birebir aynı kalıbı izleyecek
- `Ocelot` yapılandırması — yeni controller'ın rota gereksinimi var mı, yoksa
  otomatik mi geçiyor
- Blazor WASM tarafında bir mevcut sayfa: HttpClient kullanımı, DI kaydı,
  `MainLayout.razor` menü yapısı, ortak component'ler
- Mevcut Excel okuma kodu varsa hangi kütüphane kullanılmış (ClosedXML / EPPlus /
  NPOI). Varsa aynısını kullan, yoksa ClosedXML seç.
- Mevcut migration adlandırma ve `dotnet ef` kullanım şekli

İnceleme bitince `PLAN.md` yaz: hangi projeye ne ekleyeceğin, dosya listesi.
Sonra kodu yaz.

---

## 2. Ne yapıyoruz

Banka ekstresi Excel'i yükleniyor. Sistem her satır için iki şey üretiyor:

1. **Muhasebe açıklaması** — ham banka metninden şablonlu, temiz, ≤50 karakter
2. **Karşı hesap kodu** — hesap planından, güven skoruyla

Belirsiz satırlar klavye odaklı bir onay ekranına düşüyor. Kullanıcı onayladıkça
sistem öğreniyor. Çıktı: ORKA'ya aktarılacak satır listesi.

Bu aşamada **sadece Vakıfbank vadesiz TL** parser'ı yazılacak. Diğer bankalar
(Akbank, İş Bankası, TEB, Ziraat) sonra eklenecek — mimari buna hazır olmalı ama
şimdi yazma.

---

## 3. Domain modeli

```
BankaHesabi
  Id, BankaAdi, HesapTipi (Vadesiz|Vadeli), ParaBirimi,
  Iban, OrkaHesapKodu, ParserTipi, Aktif

EkstreYukleme
  Id, BankaHesabiId, DosyaAdi, YuklemeTarihi,
  DonemBaslangic, DonemBitis, SatirSayisi, Durum

EkstreSatiri
  Id, EkstreYuklemeId, SiraNo,
  Tarih, Yon (Giren|Cikan), Tutar,
  IslemTipi, HamAciklama, KarsiIban, KarsiVkn,
  UretilenAciklama, CikarilanUnvan,
  OnerilenHesapKodu, OnerilenHesapAdi, GuvenSkoru, KaynakKatman,
  OnaylananHesapKodu, Durum (Otomatik|OnayBekliyor|Onaylandi|Cozulemedi|DigerBankada)

OgrenmeKaydi
  Id, Anahtar, AnahtarTipi (AciklamaHash|Iban|Vkn),
  HesapKodu, Yon, KullanimSayisi, SonKullanim

HesapPlaniKaydi
  Id, Kod, Ad, NormalizeAd, AnaGrup, Aktif
```

`Durum = DigerBankada`: bankalar arası transferin karşı tarafı başka bankanın
ekstresinde işlendiyse bu satır dışa aktarımdan düşer. Kullanıcı elle işaretler.

Hesap kodları **boşluklu** saklanır ve boşluklu yazılır: `120 D22`, `102 1 1 01`,
`329 P27`. Formatı değiştirme, ORKA tanımaz.

---

## 4. Vakıfbank parser

`IEkstreParser` arayüzü tanımla, `VakifbankVadesizParser` uygula. Arayüz ham
dosyayı alıp `EkstreSatiri` listesi döndürür. Sonraki bankalar aynı arayüzü
uygulayacak.

Ölçülmüş dosya yapısı (xlsx):
- Başlık blokları var, **veri 8. satırdan başlıyor**
- 0-tabanlı kolon indeksleri: `2` = tarih, `5` = işlem tipi, `6` = tutar
  (işaretli: negatif = çıkan), `8` = kanal, `14` = karşı VKN, `15` = B/A,
  `16` = açıklama (354 karaktere kadar uzayabiliyor)
- Kolon indekslerini sabit yazma — başlık satırından isimle bul, bulamazsan
  yukarıdaki indekslere düş ve logla

Karşı IBAN'ı açıklamadan `TR\d{2}[\s\d\*]{16,30}` ile çıkar. Ölçümde 286 satırın
97'sinde IBAN vardı; bu en değerli anahtar, kaçırma.

---

## 5. Açıklama üretimi

İşlem tipinden şablon seç, unvanı yerleştir, **50 karakteri aşma** (ORKA kesiyor).
Her kelimenin ilk harfi büyük (Title Case) — mevcut muavin böyle.

| İşlem tipi (Vakıfbank) | Şablon |
|---|---|
| Gelen EFT Otomatik Yatan, Tös Hesaba Havale, Alınan havale, Gelen FAST Anlık Ödeme, Gelen EFT Ödeme | `Gelen Eft - {UNVAN}` |
| FAST Anlık Ödeme, Hesaba giden EFT, Gönderilen havale | `Giden Eft - {UNVAN}` |
| Otomatik Süpürme İşlemleri Virman | `Otomatik Süpürme Pkf Aday` |
| Virman | `Hesaplararası Virman - {HESAP}` |
| HGS Bakiye Yükle, *Otoyolu Bakiye Yükle | `Hgs Bakiye Yüklemesi - {PLAKA}` |
| MKK Masrafı, DIT Yp transfer | `Banka Gideri` |
| Vergi Tahsilatı | `Vergi Ödemesi - {VERGI}` |
| Kredi Kartı Borç Öde | `Kredi Kartı Borç Ödemesi` |

Bankalar arası hareketlerde unvan yerine banka adı kullanılır:
`Hesaplar Arası Eft - {BANKA}`.

Şablon tablosu **koda gömülmez** — veritabanında veya JSON'da dursun, yeni banka
eklerken kod değişmesin.

---

## 6. Unvan çıkarma

Ham açıklamadan karşı tarafın unvanını çıkaran desen listesi. Ölçülmüş kapsama
(286 satır üzerinde, ilk yakalayan desen):

| Desen | Satır |
|---|---|
| `sorgu numaralı (.+?) tarafından` | 120 |
| `nolu ([A-ZÇĞİÖŞÜ0-9][^/]{4,70}?) hesab` | 72 |
| `sorgu no'lu \S+ (.+)$` | 32 |
| `nolu ([A-ZÇĞİÖŞÜ][A-ZÇĞİÖŞÜ0-9.\s&]{4,60})` | 12 |
| `^([A-ZÇĞİÖŞÜ0-9][^/]{4,60}?)\s*/\s*[A-ZÇĞİÖŞÜ]` | 6 |
| `^(.+?)\s*\(` (parantez öncesi) | ~30 |

Desenler sırayla denenir, ilk yakalayan kazanır. Desen listesi de veritabanında,
banka bazlı. Hiçbiri tutmazsa `CikarilanUnvan = null` ve satır onaya düşer.

---

## 7. Karşı hesap eşleştirme — katmanlı

Sırayla dene, ilk çözen kazanır. `KaynakKatman` alanına hangisinin çözdüğünü yaz
(hata ayıklama için kritik).

**Katman 1 — IBAN.** `OgrenmeKaydi` içinde `AnahtarTipi=Iban` eşleşmesi.
Güven = 1.0.

**Katman 2 — Geçmiş onay.** Normalize edilmiş açıklamanın hash'i.
Güven = 1.0.

**Katman 3 — Banka kayıt defteri.** İşlem tipi bankalar arası ise (virman,
süpürme, hesaplar arası EFT), metinde geçen banka adını `BankaHesabi`
tablosundan bul, `OrkaHesapKodu` yaz. Güven = 0.95.
**Bu katman en yüksek getirili olan** — ölçümde 174 satırın 54'ü buradan
çözülüyordu, hiç cari eşleştirmesi gerektirmeden.

**Katman 4 — Sabit kural tablosu.** İşlem tipi → hesap kodu doğrudan eşlemesi
(banka masrafı → 770, HGS → 740, kambiyo vergisi → 770 vb.). Yapılandırılabilir
tablo.

**Katman 5 — Unvan benzerliği.** Aşağıdaki bölüm.

**Çözülemezse** `Durum = Cozulemedi`, onaya düşer.

### Yön → ana grup

Ölçülmüş dağılım (174 satır):

- Giren → `120` (141 satır giren, 1 çıkan)
- Çıkan → `329` (33 satır çıkan, 2 giren)

Yani yön ana grubu belirler. **Ama istisna var** — kural olarak uygula, istisnayı
onaya düşür, asla sessizce ters yöne yazma.

### Benzerlik algoritması

1. Unvanı normalize et: büyük harf, Türkçe karakter sadeleştirme
   (İ→I, Ş→S, Ğ→G, Ü→U, Ö→O, Ç→C), alfanümerik dışını boşluğa çevir
2. Gürültü kelimeleri at: `ANONIM SIRKETI SIRKET STI AS LIMITED LTD SANAYI
   TICARET TIC SAN VE ITH IHR HIZMETLERI`
3. **Arama uzayını daralt:** cari kodları unvanın ilk harfiyle başlıyor
   (`120 D22` = Dagi, `329 K08` = Kemal). Normalize unvanın ilk harfini alıp
   sadece o harfle başlayan kodlarda ara. Bulamazsan tüm gruba genişlet.
4. Levenshtein tabanlı oran hesapla. Biri diğerinin ilk 14 karakteriyle
   başlıyorsa skoru 0.95'e yükselt.

### Karar eşikleri

- Skor **≥ 0.85** ve en yakın ikinci adayla fark **≥ 0.05** → `Otomatik`
- Skor ≥ 0.85 ama ikinci aday 0.05 içinde → `OnayBekliyor`, **iki adayı da göster**
- Skor < 0.85 → `OnayBekliyor`

İkinci kural şart. Ölçümde iki yüksek güvenli hata çıktı ve ikisi de "aynı unvan
ailesinden birden fazla cari" tipindeydi:
- `Pkf İstanbul Yeminli Mali Müşavirlik` → doğru `136 17`, yanlış `136 16`
- `Park Plaza Yönetimi` → doğru `329 P27`, yanlış `329 P04`

Bu eşiklerle ölçülen sonuç: 86 cari satırının %88'i otomatik, isabet %97.4.

---

## 8. Öğrenme

Kullanıcı bir satırı onayladığında:
- Normalize açıklama hash'i → hesap kodu kaydet
- Satırda IBAN varsa IBAN → hesap kodu kaydet
- Aynı anahtar zaten varsa `KullanimSayisi` artır, `SonKullanim` güncelle
- Kullanıcı önerilen koddan **farklı** bir kod seçtiyse eski kaydı ez

Bu tablo modülün asıl değeri. İlk günkü isabet oranı değil, üçüncü aydaki
onay kuyruğu uzunluğu başarı ölçüsü.

---

## 9. API

Mevcut controller kalıbını izle:

```
POST   /api/banka-hesaplari                 hesap ekle
GET    /api/banka-hesaplari
POST   /api/ekstre/yukle                    multipart, bankaHesabiId + dosya
GET    /api/ekstre/{id}/satirlar            filtre: durum
PUT    /api/ekstre/satir/{id}/onayla        { hesapKodu }
PUT    /api/ekstre/satir/{id}/diger-bankada
POST   /api/ekstre/{id}/disa-aktar          ORKA çıktısı
POST   /api/hesap-plani/ice-aktar           xlsx yükle
```

Dışa aktarım, `Cozulemedi` veya `OnayBekliyor` satır varsa **400 döner**. Eksik
listeyle ORKA'ya gitmenin anlamı yok.

---

## 10. Blazor arayüz

Mevcut sayfa kalıbını ve stilini izle. `MainLayout.razor` menüsüne "Banka
İşleme" ekle.

**Sayfa 1 — Banka hesapları.** Basit CRUD listesi.

**Sayfa 2 — Ekstre yükleme.** Hesap seç, dosya seç, yükle, işleme sonucu özet
sayaçları (toplam / otomatik / onay bekleyen / çözülemeyen).

**Sayfa 3 — Onay ekranı.** En önemli ekran. Kurallar:

- Varsayılan filtre: sadece onay bekleyenler. Çözülmüşler ayrı sekmede.
- **Fare gerektirmez.** Sayfa açılınca odak ilk belirsiz satırın kod
  kutusunda. `Enter` onayla + sonraki satıra atla, `↓`/`↑` satır değiştir,
  `Esc` alanı temizle.
- Kod kutusu yazdıkça hesap planından filtreli öneri gösterir; seçilince
  hesap adı yanında görünür.
- Her satırda `KaynakKatman` küçük bir etiket olarak görünür
  (IBAN / geçmiş / kural / benzerlik) — hangi katmanın yanıldığını görmek için.
- Yakın adaylı satırlarda iki aday da tıklanabilir/seçilebilir olarak listelenir.
- Onay anında satır listeden düşer, sayaç güncellenir.

---

## 11. Belirsizlik kuralları

- Bir eşleştirme belirsizse → **onaya düşür**, tahmin etme
- Bir mimari karar belirsizse → mevcut repodaki benzer koda bak, aynısını yap
- Kütüphane seçimi belirsizse → repoda zaten kullanılanı seç
- Bir alan gerekli mi belirsizse → nullable yap
- Migration çakışırsa → yeni migration adı ver, mevcut migration'ı **silme**

Asla: hesap kodunu uydurma, eşik değerlerini gevşetme, "en yakın kodu" düşük
skorda otomatik yazma, hesap kodu formatını değiştirme.

---

## 12. Kabul kriterleri

Bitirmeden önce hepsini kendin doğrula:

1. Çözüm derleniyor, uyarı üretmiyor
2. Migration oluşturuldu ve uygulanıyor
3. Hesap planı xlsx içe aktarımı çalışıyor (kolonlar: `Hesap Kodu`, `Hesap Adı`)
4. Vakıfbank ekstresi yüklenince satırlar ayrışıyor, tarih/tutar/yön doğru
5. Açıklama üretimi 50 karakteri aşmıyor
6. Katman sırası doğru çalışıyor — `KaynakKatman` alanı doluyor
7. Eşik altı ve yakın adaylı satırlar `OnayBekliyor` durumunda
8. Onay sonrası `OgrenmeKaydi` yazılıyor; aynı açıklama tekrar gelirse
   katman 2'den çözülüyor
9. Onay ekranı **tamamen klavyeyle** kullanılabiliyor
10. Eksik satır varken dışa aktarım engelleniyor

Ayrıca birim testi yaz:
- Parser: örnek satırlardan doğru tarih/tutar/yön çıkarma
- Unvan çıkarma: 6 desenin her biri için en az bir örnek
- Normalizasyon: Türkçe karakter ve gürültü kelime temizliği
- Eşik mantığı: 0.90 tek aday → otomatik; 0.90 + 0.88 iki aday → onay

---

## 13. Sonunda

`OZET.md` yaz: ne yaptın, hangi dosyalar, hangi kararları neden aldın, ne
eksik kaldı, sonraki banka parser'ı eklerken nereye dokunulacak.
