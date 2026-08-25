# Görev: Öğrenilen Eşleşmeler — Toplu İçe Aktarma

Bu görevi baştan sona, soru sormadan tamamla. Önce ilgili kodu oku, sonra değiştir.
Belirsizlik çıkarsa mevcut kalıbı izle, kararını `KARARLAR.md`'ye yaz ve devam et.

**Eşleştirme mantığına dokunma** — katman sırası, eşikler, algoritma aynen kalsın.
Bu görev yalnız yeni bir içe aktarma yolu eklemek. Mevcut testlerin tamamı aynen
geçmeye devam etmeli.

## Neden

Öğrenme tablosu şu an yalnız onay ekranından tek tek doluyor. Elimizde ORKA
yevmiyesinden çıkarılmış **402 doğrulanmış eşleşme** var (PKF Aday, 7 aylık
geçmiş). Bunlar kullanıcının geçmişte kendi verdiği kararlar — onay ekranından
tek tek geçmekle aynı şey, sadece toplu hali. Elle girilmesi mümkün değil.

## Dosya formatı

Tek sayfa, 1. satır başlık, veri 2. satırdan. Kolonlar başlık **adıyla** bulunur,
sıraya güvenilmez (mevcut banka hesabı içe aktarımıyla aynı kalıp):

| Kolon | Zorunlu | Not |
|---|---|---|
| `Anahtar Cekirdek` | evet | Normalize edilmiş unvan çekirdeği, örn. `NAOS ISTANBUL KOZMETIK` |
| `Hesap Kodu` | evet | Boşluklu format, aynen saklanır (`120 N15`) |
| `Hesap Adi` | hayır | Sadece bilgi; doğrulama hesap planından yapılır |
| `Yon` | hayır | `Giren` \| `Cikan` \| `Farketmez`, boşsa `Farketmez` |
| `Kullanim Sayisi` | hayır | Boşsa 1 |
| `Son Kullanim` | hayır | `gg.aa.yyyy` |

Türkçe karakter toleransı olsun (`Anahtar Çekirdek` / `Anahtar Cekirdek` ikisi de
kabul), büyük-küçük harf ve baş/son boşluk önemsenmesin.

## Davranış

- Anahtar tipi **unvan çekirdeği** olarak yazılsın (`Belirsizlik` değil).
- **Kullanıcının kendi kararı korunsun:** aynı anahtar için zaten bir kayıt varsa
  **üzerine yazma**, o satırı "atlandı" olarak raporla. Kullanıcı onay ekranından
  verdiği karar, geçmişten türetilen kayda göre önceliklidir.
- Anahtar içe aktarımdan önce **aynı normalizasyondan geçirilsin** — dosyadaki
  değer zaten normalize gelse bile, sistemin kendi normalizasyonuyla yeniden
  üretilsin ki eşleşme kesin olsun.
- **Doğrulama, satır bazlı.** Geçersiz satır tüm içe aktarımı düşürmesin:
  - Hesap kodu firmanın hesap planında yok → hata, satır atlanır
  - Anahtar 8 karakterden kısa → hata
  - Anahtar hesap sahibi çekirdeklerinden birini kapsıyor → hata (hesap sahibinin
    kendi adı asla öğrenilmemeli)
  - `Yon` tanınmıyor → hata
  - Aynı anahtar dosyada iki kez → hata
- **Sonuç raporu:** eklenen / atlanan (mevcut) / hatalı sayıları ve her hatalı
  satır için satır numarası + sebep. Mevcut `{ SatirNo, Field, Message }`
  sözleşmesine uy.
- İçe aktarım **firma bazlı** — seçili firmanın kapsamına yazılsın.

## Uç nokta ve arayüz

```
POST /api/catalog/banka-otomasyon/ogrenilen-eslesmeler/ice-aktar   (multipart)
GET  /api/catalog/banka-otomasyon/ogrenilen-eslesmeler/sablon
```

Tanımlar > Öğrenilen eşleşmeler bölümüne **"Toplu İçe Aktar"** düğmesi, dosya
seçici ve **"Örnek şablon indir"** bağlantısı. İçe aktarım sonrası liste
yenilensin, sonuç raporu ekranda görünsün.

Onay kutusunda firma adı yazsın ("PKF Aday için 402 eşleşme içe aktarılacak").

## Testler

- Geçerli dosya: 3 satır → 3 eklendi
- İkinci kez aynı dosya → 0 eklendi, 3 atlandı (mevcut korundu)
- Hesap planında olmayan kod atlanır ve raporlanır
- Hesap sahibi çekirdeğini kapsayan anahtar reddedilir
- Kolon sırası değiştirilmiş dosya yine okunur
- Farklı firmada aynı anahtar ayrı kayıt olur (kapsam izolasyonu)
- İçe aktarılan eşleşme, sonraki ekstre yüklemesinde geçmiş onay katmanından
  çözülüyor

## Kabul kriterleri

1. Derleme temiz, tüm mevcut testler aynen geçiyor
2. Migration gerekiyorsa üretildi ve uygulandı
3. Şablon indirme çalışıyor
4. Hatalı satır tüm içe aktarımı düşürmüyor
5. Mevcut kayıtlar korunuyor

Sonunda `OZET.md` ve `KARARLAR.md` güncelle.
