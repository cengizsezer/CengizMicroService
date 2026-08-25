# Görev: Kural Kategorileri + Banka Adı Açılır Listesi

Bu görevi baştan sona, soru sormadan tamamla. Önce ilgili kodu oku, sonra değiştir.
Belirsizlik çıkarsa mevcut kalıbı izle, kararını `KARARLAR.md`'ye yaz ve devam et.

**Eşleştirme mantığına dokunma** — katman sırası, eşikler, algoritma, desenler
aynen kalsın. Kategori yalnız etiket ve görünüm; karar mekanizmasına girmiyor.
Mevcut testlerin tamamı aynen geçmeye devam etmeli.

---

## 1. İşlem kategorisi

Kurallar şu an mekanizmaya göre ayrılmış (sabit kural, vergi kodu, kişi
yönlendirme, açıklama şablonu). Kullanıcı ise muhasebe kategorisine göre
düşünüyor ve kontrol ediyor. Kategoriler bankadan bağımsız.

Dört bankanın gerçek verisinden ölçülen liste — seed'e bunlar konsun:

| Kategori | Ana grup |
|---|---|
| Hesaplar arası | 102 |
| Müşteri tahsilatı | 120 |
| Tedarikçi ödemesi | 329 |
| Grup içi cari | 136 |
| Diğer alacak | 159 |
| Personel iş avansı | 195 |
| Personel maaş avansı | 196 |
| Banka gideri | 770 |
| Araç/hizmet gideri | 740 |
| Finansman gideri | 780 |
| Kredi | 300 |
| Kredi kartı | 309 |
| Ortaklar | 331 |
| Diğer borç | 336 |
| Vergi borcu | 360 |
| SGK | 361 |
| KKEG | 689 |

### Uygulama

**Tablo yapılarını değiştirme.** Mevcut kayıtlara nullable bir `IslemKategorisi`
alanı eklensin: `SabitKural`, `VergiKoduEslemesi`, `KisiYonlendirme`,
`AciklamaSablonu`.

Kategori listesi yönetilebilir bir tablo olsun (ad + varsayılan ana hesap grubu +
sıra + aktif). Kullanıcı yeni kategori ekleyebilsin, mevcutları düzenleyebilsin.

Mevcut seed kayıtlarına uygun kategoriler atansın (`MKK Masrafı` → Banka gideri,
`İş Avansı` → Personel iş avansı, `HGS Bakiye Yükle` → Araç/hizmet gideri,
`Hesaplar Arası EFT` → Hesaplar arası, vb.).

### Görünüm — sade

"Bu bankanın kuralları" sekmesine **Kategoriler** görünümü ekle.

Tek liste, üç kolon:

```
kategori adı                    hesap kodu        kural sayısı
```

Hesap kodu birden fazlaysa `195 · 196` gibi yazılsın. Üstte tek satır özet:
`Vakıfbank · 13 / 17 kategori tanımlı`.

- Tanımlı kategoriler **tamamen sade** — renk yok, ikon yok, etiket yok.
- **Kuralı olmayan kategoriler hafif kırmızı zeminde ve kırmızı metinle**, sayı
  yerine `yok` yazsın.
- Ayrı kapsama özeti kutusu, renkli etiket bulutu, ikon kalabalığı olmasın.

Bir kategoriye tıklanınca o kategoriye ait kurallar **accordion** olarak
açılsın — mekanizması ne olursa olsun hepsi tek listede, mekanizma küçük bir
etiketle belirtilsin (`sabit kural`, `şablon`, `vergi kodu`, `kişi`). Kullanıcı
buradan da düzenleyebilsin. Kapalıyken liste sade kalsın.

Amaç: yeni banka (Ziraat, Akbank, İş Bankası) eklerken eksik kategorileri
kontrol listesi olarak görmek.

### Onay ekranı

Satırın hangi kategoriye düştüğü küçük bir etiket olarak görünsün. Kategoriye
göre filtreleme olsun — kullanıcı benzer satırları gruplayıp gözden geçirebilsin.

---

## 2. Banka adı açılır liste olsun

Banka adı alanı serbest metin olduğu için tutarsızlık tekrar tekrar oluşuyor.
Gerçek veride girilmiş yanlış değerler: `Vakıf Bank Eur`, `Vakıf Bank Usd`,
`Vakıfbank Vadeli`, `İŞ BANKASI`, `Ziraat Bankası`. Her biri ayrı sekme açtı;
11 sekme çıktı, oysa 8 banka var.

Bu sadece görüntü sorunu değil: "aynı banka önceliği" kuralı `BankaAdi` üzerinden
çalıştığı için bankalar arası eşleştirme bozuluyor.

Autocomplete + uyarı yeterli olmadı. Alanı **açılır liste + "yeni banka ekle"**
yapısına çevir:

- Kullanıcı mevcut banka adlarından seçsin (varsayılan davranış)
- Gerçekten yeni bir banka ekliyorsa ayrı bir adımdan geçsin ("Yeni banka ekle"
  düğmesi → ad girme → onay)
- Serbest yazım varsayılan olmasın

**Birleştirme işlemi ekle.** Tanımlar > Banka hesapları listesinde, aynı bankanın
farklı yazımlarını seçip tek ada indirebilsin. Kaç hesabın etkileneceğini
gösteren onay adımı olsun.

Toplu içe aktarımda da `Banka Adı` mevcut adlarla eşleşmiyorsa uyarı satırı
raporlansın (kayıt yine de eklensin, engelleme yok).

---

## Kabul kriterleri

1. Derleme temiz, tüm mevcut testler aynen geçiyor
2. Migration üretildi ve uygulandı, `has-pending-model-changes` temiz
3. 17 kategori seed'de; mevcut kurallara kategori atanmış
4. Kategoriler görünümü sade; tanımsız kategoriler kırmızı ve `yok` yazıyor
5. Accordion açılıyor, kurallar mekanizma etiketiyle listeleniyor
6. Onay ekranında kategori etiketi ve kategoriye göre filtre var
7. Banka adı açılır liste; yeni banka ayrı adımdan ekleniyor
8. Birleştirme işlemi çalışıyor ve etkilenecek hesap sayısını gösteriyor

Sonunda `OZET.md` ve `KARARLAR.md` güncelle.
