# Görev: Banka Hesabı Eşleştirme Anahtarları + Ayrıştırıcı İsteğe Bağlı

Bu görevi baştan sona, soru sormadan tamamla. Önce ilgili kodu oku, sonra
değiştir. Belirsizlik çıkarsa mevcut kalıbı izle ve kararını `KARARLAR.md`'ye yaz.

---

## Sorun

Bankalar arası eşleştirme katmanı, ekstre açıklamasında `BankaHesabi.BankaAdi`
metnini arıyor. Bu tek başına yetmiyor, çünkü **aynı bankada birden fazla hesap
var**:

```
102 1 1 01    Vakıfbank, Vadesiz TL
102 1 1 04    Vakıfbank, Vadeli TL - Otomatik Süpürme Hesabı
102 1 32 87   TEB, Vadesiz TL
102 1 32 90   TEB, Marifetli TL
102 1 4 01    Ziraat, Vadesiz TL
102 1 4 04    Ziraat, Günlük Kazanan Hesap
102 1 4 05    Ziraat, Vadeli TL Kasa
102 1 5 01    İş Bankası, Vadesiz TL
102 1 5 07    İş Bankası, Vadeli TL
102 1 7 14    Akbank, Vadesiz TL
102 1 7 06    Akbank, Vadeli TL Serbest Plus
102 1 7 07    Akbank, Vadeli TL Blokaj
```

İkisinin de `BankaAdi` "Vakıfbank" olduğu için açıklamada "Vakıfbank" geçtiğinde
hangi hesap olduğu ayırt edilemiyor.

Gerçek ekstre açıklamaları şöyle:

```
"Otomatik Süpürme Pkf Aday"              → 102 1 1 04
"Hesaplararası Virman - Ziraat/Teb"      → TEB veya Ziraat hesabı
"Hesaplar Arası Eft - Vakıfbank"         → Vakıfbank hesabı
"Hesaplararası Virman - Vadeli"          → ilgili vadeli hesap
```

Bu, 102 grubu satırların tamamını etkiliyor — cevap anahtarındaki en büyük kalem
bu grup (Vakıfbank 48, İş Bankası 30, Ziraat 19, Akbank 10 satır).

Ayrıca kullanıcı `BankaAdi` alanına tam hesap adını yazma eğiliminde
(`"Vakıfbank, Vadeli Tl - Otomatik Süpürme Hesabı"`). Bu metin hiçbir açıklamada
geçmediği için eşleşme hiç olmuyor.

---

## 1. `EslestirmeAnahtarlari` alanı

`BankaHesabi`'na yeni alan: `EslestirmeAnahtarlari` (nullable, virgülle ayrılmış
metin listesi).

Bankalar arası eşleştirme katmanı şu sırayla arasın:

1. Önce `EslestirmeAnahtarlari` içindeki her anahtarı açıklamada ara
2. Hiçbiri tutmazsa `BankaAdi`'na düş

Karşılaştırma normalize edilmiş metin üzerinden olsun (Türkçe karakter
sadeleştirme, büyük-küçük harf duyarsız, fazla boşluk temizliği) — mevcut
`Normalizasyon` yardımcısını kullan.

**Çakışma kuralı:** Bir açıklamada birden fazla hesabın anahtarı eşleşiyorsa
**en uzun eşleşen anahtar** kazansın (`"Otomatik Süpürme"` > `"Vakıfbank"`).

**Belirsizlik kuralı:** Hiçbir anahtar eşleşmiyorsa ve aynı `BankaAdi`'na sahip
birden fazla aktif hesap varsa satır **onaya düşsün**, aday hesaplar seçenek
olarak listelensin. Rastgele veya "ilk bulunan" seçme.

Tek hesap varsa eskisi gibi doğrudan çözülsün.

## 2. `Ayristirici` zorunlu olmasın

Şu an ayrıştırıcı seçimi zorunlu. Ama hesapların çoğuna **ekstre
yüklenmeyecek** — sadece karşı hesap olarak bulunabilmek için tanımlılar
(vadeli, süpürme, blokaj, yatırım hesapları).

- `Ayristirici` nullable olsun, formda "Yok" / boş seçenek bulunsun
- Ayrıştırıcısı olmayan hesap İşleme ekranında **kart göstermesin** (ekstre
  yüklenemez), ama kayıt defterinde ve eşleştirmede kullanılsın
- Ayrıştırıcısı olmayan bir hesaba ekstre yüklenmeye çalışılırsa anlaşılır hata
  dönsün

## 3. `BankaAdi` için kullanıcı yönlendirmesi

Form alanının ipucu metnini netleştir: buraya **kısa banka adı** yazılmalı
(`Vakıfbank`, `Ziraat`, `TEB`, `Akbank`, `İş Bankası`), tam hesap adı değil.
Hesap adı zaten ayrı `HesapAdi` alanında.

Kaydetme sırasında `BankaAdi` 25 karakterden uzunsa veya virgül/tire içeriyorsa
yumuşak bir uyarı göster (engelleme, sadece uyar).

## 4. Toplu içe aktarıma kolon ekle

`Eslestirme Anahtarlari` kolonu ekle (isteğe bağlı, virgülle ayrılmış). Şablon
üretimine de ekle. Mevcut doğrulama ve upsert mantığı aynen kalsın.

---

## Seed / varsayılan öneri

Yeni hesap kaydedilirken `EslestirmeAnahtarlari` boşsa, `HesapAdi`'ndan
otomatik öneri üret ve forma doldur (kullanıcı düzenleyebilsin). Öneri:
hesap adından banka adını ve genel kelimeleri (`Vadesiz`, `TL`, hesap numarası)
çıkardıktan sonra kalan ayırt edici kelimeler.

Örnekler:
```
"Vakıfbank, Vadeli Tl - Otomatik Süpürme Hesabı"  → "Otomatik Süpürme, Süpürme"
"Teb, Marifetli Tl - Maslak, 129-154401190"       → "Marifetli"
"Ziraat Bankası, Günlük Kazanan Hesap - 5022"     → "Günlük Kazanan"
"Akbank, Vadeli Tl Serbets Plus, Blokaj"          → "Blokaj, Serbest Plus"
```

---

## Testler

- `"Otomatik Süpürme Pkf Aday"` → `102 1 1 04` (anahtar), `102 1 1 01` değil
- `"Hesaplar Arası Eft - Vakıfbank"` → anahtar tutmaz, iki Vakıfbank hesabı var
  → **onaya düşer**, iki aday listelenir
- Tek hesaplı banka (Fibabanka) → anahtar olmasa da `BankaAdi`'ndan çözülür
- En uzun anahtar kazanır: `"Vakıfbank Otomatik Süpürme"` içinde hem
  `"Vakıfbank"` hem `"Otomatik Süpürme"` varsa ikincisi seçilir
- Ayrıştırıcısız hesap kaydedilebilir; İşleme ekranında kart çıkmaz
- Ayrıştırıcısız hesaba ekstre yüklenmeye çalışılınca anlaşılır hata
- Toplu içe aktarım `Eslestirme Anahtarlari` kolonunu okur
- Türkçe karakter/büyük-küçük harf duyarsız eşleşme

## Kabul kriterleri

1. Derleme temiz, tüm testler geçiyor
2. Migration üretildi ve uygulandı, `has-pending-model-changes` temiz
3. Yukarıdaki testlerin hepsi yazıldı ve geçiyor
4. Mevcut tekli CRUD ve toplu içe aktarım bozulmadı
5. Ayrıştırıcısı olmayan hesap eşleştirmede kullanılıyor ama ekstre kabul etmiyor

Sonunda `OZET.md` ve `KARARLAR.md` güncelle.
