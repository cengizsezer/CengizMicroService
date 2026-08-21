# Görev: Banka Hesapları Toplu İçe Aktarma

Bu görevi baştan sona, soru sormadan tamamla. Belirsizlik çıkarsa mevcut hesap
planı içe aktarımının kalıbını izle.

## Neden

Banka hesapları şu an tek tek elle giriliyor. PKF Aday'da 19 hesap var; iki firma
ve zamanla eklenecek hesaplarla bu iş tekrarlanacak. Hesap planı içe aktarımı
zaten var — aynı kalıpla banka hesabı içe aktarımı ekle.

## Önce incele

`EkstreHesapPlaniService` ve ona bağlı controller / DTO / Blazor bölümünü oku.
Yeni özellik birebir aynı kalıbı izleyecek: aynı hata sözleşmesi, aynı upsert
mantığı, aynı kolon-başlıktan-bul yaklaşımı.

## Dosya formatı

Tek sayfa, 1. satır başlık, veri 2. satırdan. Kolonlar başlık **adıyla** bulunur,
sıraya güvenilmez:

| Kolon | Zorunlu | Not |
|---|---|---|
| `Orka Hesap Kodu` | evet | Boşluklu format, aynen saklanır (`102 1 32 87`) |
| `Hesap Adi` | evet | |
| `Banka Adi` | evet | Vakıfbank, Ziraat, Akbank, İş Bankası, TEB… |
| `Hesap Tipi` | evet | `Vadesiz` \| `Vadeli` |
| `Para Birimi` | evet | TL / USD / EUR |
| `Parser Tipi` | hayır | Boşsa hesap tanımlanır ama ekstre yüklenemez |
| `IBAN` | hayır | |

Başlıklarda Türkçe karakter toleransı olsun (`Hesap Adi` / `Hesap Adı` ikisi de
kabul edilsin), büyük-küçük harf ve baş/son boşluk önemsenmesin.

## Davranış

- **Upsert:** `Orka Hesap Kodu` + `FirmaId` anahtar. Varsa güncelle, yoksa ekle.
  Dosyada olmayan mevcut hesaplara **dokunma** (hesap planındaki gibi pasife
  çekme — kullanıcı bir bankayı bilerek dışarıda bırakmış olabilir).
- **Doğrulama, satır bazlı.** Geçersiz satır tüm içe aktarımı düşürmesin;
  geçerliler işlensin, geçersizler rapor edilsin:
  - Kod hesap planında yok → hata, o satır atlanır
  - Kod `102` ile başlamıyor → uyarı, yine de eklenir
  - `Hesap Tipi` tanınmıyor → hata
  - `Parser Tipi` kayıtlı ayrıştırıcılardan biri değil → hata (geçerli değerleri
    mesajda listele)
  - Aynı kod dosyada iki kez → hata
- **Sonuç raporu:** eklenen / güncellenen / atlanan sayıları ve her hatalı satır
  için satır numarası + sebep. Mevcut `{ field, message }` sözleşmesine uy.

## Uç nokta ve arayüz

```
POST /api/catalog/banka-ekstre/banka-hesaplari/ice-aktar   (multipart)
```

Tanımlar > Banka hesapları bölümüne "Toplu İçe Aktar" düğmesi ve dosya seçici.
İçe aktarım sonrası liste yenilensin, sonuç raporu ekranda görünsün. Mevcut tekli
CRUD kalsın.

Ayrıca **örnek şablon indir** bağlantısı olsun — doğru başlıklara sahip boş bir
xlsx üretsin, kullanıcı formatı tahmin etmesin.

## Testler

- Geçerli dosya: 3 satır → 3 eklendi
- İkinci kez aynı dosya → 3 güncellendi, 0 eklendi
- Geçersiz `Hesap Tipi` olan satır atlanır, diğerleri işlenir
- Hesap planında olmayan kod atlanır ve raporlanır
- Kolon sırası değiştirilmiş dosya yine okunur
- Farklı firmada aynı kod ayrı kayıt olur (tenant izolasyonu)

## Kabul kriterleri

1. Derleme temiz, testler geçiyor
2. Migration gerekiyorsa üretildi ve uygulandı
3. Şablon indirme çalışıyor
4. Hatalı satır tüm içe aktarımı düşürmüyor
5. Tekli CRUD bozulmadı

Sonunda `OZET.md` ve `KARARLAR.md` güncelle.
