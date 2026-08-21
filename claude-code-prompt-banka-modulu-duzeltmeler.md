# Görev: Banka Ekstre Modülü — Düzeltmeler ve Eksikler

Bu görevi **baştan sona, bana soru sormadan** tamamla. Belirsizlik çıkarsa
"Belirsizlik kuralları" bölümüne bak, orada da yoksa en muhafazakâr seçeneği al,
kararını `KARARLAR.md` dosyasına yaz ve devam et.

Modül daha önce yazıldı. Bu prompt onu düzeltiyor ve eksikleri tamamlıyor.
**Önce ilgili kodu oku, sonra değiştir.** Sıfırdan yazma.

---

## Bağlam

İki firma var: **PKF Aday Bağımsız Denetim A.Ş.** ve **PKF İstanbul SMMM A.Ş.**
Her birinin ayrı hesap planı var. Dört banka: Vakıfbank, Akbank, İş Bankası,
Ziraat. Her bankada vadesiz/vadeli, bazılarında USD hesabı olabilir.

Banka ekstreleri her iki firmada da aynı formatta geliyor ve kullanıcı ikisini de
aynı yazım kalıbıyla işliyor. Bu yüzden parser'lar, açıklama şablonları ve unvan
çıkarma desenleri **firmadan bağımsız**; hesap planı ve unvan→kod eşlemesi
**firma bazlı**.

---

## 1. VKN katmanını kapat

Vakıfbank ekstresindeki VKN kolonu karşı tarafın değil **hesap sahibinin**
VKN'si. Ölçüm: 286 satırın hepsinde aynı değer (`0070511435`).

Açık bırakılırsa ilk onaydan sonra `VKN 0070511435 → 120 D22` kaydı düşer ve
sonraki tüm satırlar güven 1.0 ile aynı hesaba eşleşir. Güven 1.0 olduğu için
onaya da düşmez — sessiz ve yaygın bir hata olur.

- Parser `KarsiVkn` alanını doldurmasın
- VKN öğrenme katmanı okunmasın, yazılmasın
- Katmanı **silme**, banka bazlı bir bayrakla kapalı tut (başka bankada karşı
  tarafın VKN'si gerçekten gelebilir)

## 2. IBAN katmanını çıkar

Kullanıcı IBAN verisini düzenli tutmuyor ve güvenilir bulmuyor. IBAN'a dayalı
eşleştirme katmanını devre dışı bırak. Parser IBAN'ı ayrıştırmaya devam edebilir
(bilgi olarak saklanır) ama eşleştirmede kullanılmasın.

**Not:** Ölçülen %88 otomatik / %97.4 isabet oranları zaten IBAN kullanılmadan
elde edildi. Bu değişiklik başarımı düşürmez.

Kalan katman sırası:
1. Geçmiş onay (öğrenme tablosu)
2. Banka kayıt defteri (bankalar arası hareketler)
3. Sabit kural tablosu (işlem tipi → hesap)
4. Unvan benzerliği

---

## 3. Öğrenme anahtarı: ham hash değil, unvan çekirdeği

Şu anki tasarımda öğrenme anahtarı ham açıklamanın hash'i. **Bu asla ikinci kez
eşleşmez**, çünkü banka her satıra farklı sorgu numarası, tarih ve tutar yazıyor.

Anahtar, **normalize edilmiş unvan çekirdeği** olmalı:

```
ham    : "...sorgu numaralı DAGİ GİYİM SANAYİ VE TİCARET ANONİM ŞİRKETİ tarafından..."
çıkar  : "DAGİ GİYİM SANAYİ VE TİCARET ANONİM ŞİRKETİ"
normalize: "DAGI GIYIM"          ← anahtar bu
```

Normalizasyon: büyük harf → Türkçe karakter sadeleştirme (İ→I, Ş→S, Ğ→G, Ü→U,
Ö→O, Ç→C) → alfanümerik dışını boşluk → gürültü kelimeleri at
(`ANONIM SIRKETI SIRKET STI AS LIMITED LTD SANAYI TICARET TIC SAN VE ITH IHR
HIZMETLERI`) → tek harfli token'ları at.

Unvan çıkarılamayan satırlarda (banka masrafı, HGS, vergi vb.) anahtar olarak
işlem tipi + sabit metin kullanılsın.

## 4. Çoklu token çıpası

Aday daraltmada **sadece ilk kelimeyi** çıpa olarak kullanma. Banka, unvanın
önüne kendi iç kodunu ekleyebiliyor:

```
"NAOSKZ NAOS İSTANBUL KOZMETİK SANAYİ VE TİCARET A.Ş."
  çıpa "NAOSKZ"   →   0 aday
  çıpa "NAOS"     →   2 aday, skor 1.00 → 120 N15 Naos İstanbul Kozmetik  ✓
  çıpa "ISTANBUL" → 126 aday, skor 0.73 → alakasız hesap                  ✗
```

Algoritma: normalize unvanın **her token'ını sırayla çıpa olarak dene**. O
token'la başlayan hesapları getir, kalan metinle (token dahil, sonrası) skorla.

**Aday sayısı patlayan çıpalar güvenilmez.** `ISTANBUL` gibi genel bir token
126 aday getirip alakasız bir hesabı öneriyor. Bir çıpa yapılandırılabilir bir
eşiği (varsayılan 25) aşan sayıda aday getiriyorsa o çıpanın sonucunu dikkate
alma. Hiçbir çıpa sonuç vermezse satır onaya düşsün.

## 5. Uyarlanabilir anahtar: aynı unvan ailesinden çoklu cari

Bazı cariler aynı çekirdeği paylaşıyor ama ayrı hesaplar:

```
329 P04   Park Plaza Yönetimi, Aidat
329 P05   Park Plaza Yönetimi, Elektrik
329 P27   Park Plaza 19. Kat
```

Anahtar **her zaman** çok parçalı olmasın — çoğu satırda çekirdek tek başına
doğru (`NAOS ISTANBUL KOZMETIK` → tek aday). Gereksiz kelime eklemek anahtarın
ikinci ay tutmamasına yol açar.

Kural:

- Çekirdek **tek aday** getiriyorsa → anahtar sadece çekirdek
- Çekirdek **birden fazla aday** getiriyorsa → aile tespit edildi:
  - Aday hesap adlarının **ortak kısmı** çekirdek, **farklı kısımları** ayırt
    edici kelimeler (Aidat, Elektrik, 19. Kat)
  - Bu kelimeler ham banka açıklamasında aranır; bulunursa ilgili alt hesap
    seçilir, anahtar `çekirdek + ayırt edici kelime` olarak kaydedilir
  - Bulunamazsa veya iki aday 0.05 içindeyse → **her zaman onaya düşsün**, tüm
    aile üyeleri seçenek olarak listelensin

Aramada sıra: önce genişletilmiş anahtar, tutmazsa sade çekirdek.

## 6. Öğrenilen eşleşmeler yönetilebilir olsun

Yanlış onaylanan bir eşleşme bir daha sorulmadan tekrarlanır. Bu yüzden:

- Tanımlar altında **"Öğrenilen Eşleşmeler"** ekranı: anahtar, hesap kodu, hesap
  adı, yön, kullanım sayısı, son kullanım. Arama, düzenleme, silme.
- Onay ekranında bir satır geçmiş onaydan çözülmüşse (`KaynakKatman = geçmiş`)
  ve kullanıcı kodu değiştirirse, **öğrenme kaydı da güncellensin** — sadece o
  satır değil. Aksi halde hata gelecek ay geri gelir.

---

## 7. Firma (tenant) ayrımı

Önce repodaki mevcut firma kapsamı kalıbını incele (FirmaId alanı mı, EF global
query filter mı, header/claim'den mi geliyor). **Kendi mekanizmanı icat etme**,
mevcut kalıbı uygula.

**Firma bazlı:** `HesapPlaniKaydi`, `BankaHesabi`, `EkstreYukleme`,
`EkstreSatiri`, ve unvan→kod eşleşmeleri.

**Global (firma bağımsız):** parser'lar, açıklama şablonları, unvan çıkarma
desenleri, normalizasyon kuralları.

Öğrenme tablosunu ikiye böl:

```
KimlikKaydi     GLOBAL      Anahtar, AnahtarTipi, NormalizeUnvan, KullanimSayisi
HesapEslesmesi  FİRMA BAZLI FirmaId, AnahtarCekirdek, AyirtEdiciEk (nullable),
                            HesapKodu, Yon, KullanimSayisi, SonKullanim
```

Bir unvanın kim olduğu her firmada aynıdır; hangi koda gittiği firmaya özeldir.
Aday'da öğrenilen bir unvan, SMMM'de karşına çıktığında kimlik hazır olur,
sadece yerel kod eşlenir.

Firma bazlı tablolarda hiçbir sorgu firma filtresi olmadan çalışmasın.

Migration yaz, mevcut migration'ı silme. Test: iki farklı firma aynı unvanı
farklı koda eşlesin ve birbirinin verisini görmesin.

---

## 8. Menü ve sayfa yapısı

Şu an hesap planı yükleme günlük kullanılan ekranda duruyor. Hesap planı yılda
birkaç kez değişir; günlük ekranda olmamalı.

```
Banka İşleme
  ├ İşleme      (günlük ana ekran, varsayılan)
  └ Tanımlar    (kurulum, nadiren açılır)
```

**Firma bağlamı:** Sayfa içine firma seçici koyma. Uygulamanın üstündeki mevcut
FİRMA DEĞİŞTİR bağlamını kullan; repoda bu nasıl okunuyorsa aynı kalıbı izle.
Firma değiştiğinde sayfa yenilensin.

**Tanımlar** — üç bölüm:
- Hesap planı: son içe aktarım tarihi, kayıt sayısı, "Güncelle" ile xlsx
  yükleme. İçe aktarım kayıtları **silmesin**: yeni kodları ekle, değişen adları
  güncelle, ORKA'da olmayanları pasife çek.
- Banka hesapları: mevcut CRUD listesi buraya taşınsın.
- Öğrenilen eşleşmeler (madde 6).

**İşleme** — günlük ekran:
- Üstte dönem seçici.
- Banka sekmeleri, tanımlı banka hesaplarından türetilsin. Sekme başlığında o
  bankanın onay bekleyen satır sayısı rozet olarak görünsün.
- Aktif sekmenin altında o bankanın her hesabı için bir kart: hesap adı
  (Vadesiz TL), ORKA kodu, durum rozeti.
  - Ekstre yüklüyse: dosya adı, satır sayısı, otomatik / onay bekleyen /
    çözülemeyen sayıları, "Onay ekranı" düğmesi. Hepsi çözülmüşse "Dışa aktar".
  - Yüklü değilse: kesikli çerçeveli boş kart, sürükle-bırak veya dosya seç.
- Hesap planı hiç yüklenmemişse burada sadece kısa uyarı ve Tanımlar'a bağlantı
  olsun; yükleme formu bu sayfada olmasın.
- Son hesap planı içe aktarımı 30 günden eskiyse yumuşak bir hatırlatma göster.

---

## 9. Bilinmeyen hesap kodu

Kullanıcı onay ekranında hesap planında bulunmayan bir kod yazarsa:
- "Bu kod hesap planında yok — ORKA'da yeni açıldıysa hesap planını güncelleyin"
  uyarısı çıksın
- Kodun kaydedilmesine izin verilsin
- Ama **öğrenme kaydı yazılmasın** — doğrulanmamış kod kalıcılaşmasın

---

## 10. Dışa aktarım: iki parça

Şu an sadece karşı hesap kodu listesi üretiliyor. ORKA'ya yüklenen ekstre
dosyasındaki açıklama da bizim ürettiğimizle değiştirilmeli, yoksa grid'de ham
banka metni görünür.

Dışa aktarım şunları üretsin:
1. **Düzeltilmiş ekstre dosyası** — orijinal ekstre yapısında, açıklama kolonu
   `UretilenAciklama` ile değiştirilmiş (≤50 karakter, ORKA kesiyor)
2. **Karşı hesap kodu listesi** — satır sırasına göre, ORKA gridine yazılacak
   kodlar. PkfRobot'un `GridDoldur` adımı bu listeyi tüketecek: her satır için
   `{ SiraNo, Aciklama, KarsiHesapKodu }`. Açıklama alanı robotun satır
   doğrulaması için gerekli, çıkarma.

`OnayBekliyor` veya `Cozulemedi` satır varsa dışa aktarım 400 dönmeye devam
etsin.

---

## 11. İleri dönük alan

`EkstreSatiri`'ne `EslesenKarsiSatirId` (nullable) alanı ekle. İki firma da
sistemde olduğu için, Aday'dan SMMM'ye giden bir transferin karşı tarafı diğer
firmanın ekstresinde bulunabilir; ileride grup içi çapraz doğrulama yapılacak.
Şimdi mantık yazma, sadece alanı ve migration'ı ekle.

---

## Belirsizlik kuralları

- Eşleştirme belirsizse → **onaya düşür**, tahmin etme
- Mimari karar belirsizse → repodaki benzer koda bak, aynısını yap
- Kütüphane belirsizse → repoda zaten kullanılanı seç
- Alan gerekli mi belirsizse → nullable yap
- Migration çakışırsa → yeni migration ekle, mevcut migration'ı silme

Asla: hesap kodu uydurma, eşikleri gevşetme (`OtomatikEsik` 0.85,
`AdayFarki` 0.05), hesap kodu formatını değiştirme (boşluklu: `120 D22`,
`102 1 1 01`).

---

## Kabul kriterleri

Bitirmeden hepsini kendin doğrula:

1. Çözüm derleniyor, yeni uyarı yok; testler geçiyor
2. Migration'lar oluşturuldu ve uygulanıyor; `has-pending-model-changes` temiz
3. VKN katmanı kapalı; parser `KarsiVkn` doldurmuyor
4. IBAN katmanı eşleştirmede kullanılmıyor
5. Öğrenme anahtarı unvan çekirdeği — aynı cari farklı sorgu numarasıyla ikinci
   kez geldiğinde geçmiş onaydan çözülüyor (test yaz)
6. Çoklu token çıpası çalışıyor — `NAOSKZ NAOS İSTANBUL KOZMETİK` → `120 N15`
   (test yaz)
7. Aday patlaması olan çıpa (`ISTANBUL`, 126 aday) dikkate alınmıyor (test yaz)
8. Park Plaza ailesi onaya düşüyor ve üç aday da listeleniyor (test yaz)
9. İki firma birbirinin hesap planını ve eşleşmelerini görmüyor (test yaz)
10. Menü İşleme / Tanımlar olarak ayrıldı; hesap planı yükleme günlük ekranda
    değil
11. Öğrenilen eşleşmeler ekranından kayıt düzenlenebiliyor ve siliniyor
12. Dışa aktarım iki parça üretiyor; eksik satır varken 400 dönüyor

## Sonunda

`OZET.md` ve `KARARLAR.md` güncelle: ne değişti, hangi kararlar neden alındı,
ne eksik kaldı, yeni banka parser'ı eklerken nereye dokunulacak.
