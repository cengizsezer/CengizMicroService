# Görev: Kişi Eşleştirme Düzeltmesi + Kişi Yönlendirme Tablosu + Analiz Dışa Aktarımı

Bu görevi baştan sona, soru sormadan tamamla. Önce ilgili kodu oku, sonra değiştir.
Belirsizlik çıkarsa mevcut kalıbı izle, kararını `KARARLAR.md`'ye yaz ve devam et.

## Bağlam

Gerçek Vakıfbank ekstresi (287 satır) gerçek hesap planıyla çalıştırıldı:
139 otomatik / 136 onay bekliyor / 12 çözülemedi. Çözülmüş satırların gözle
kontrol edilen 48 tanesinin **48'i de doğru**. Aşağıdaki maddeler bu
çalıştırmadan çıkan gerçek sorunlar.

---

## 1. Sabit kural grubu içinde yanlış kişi seçiliyor

Sabit kural (`İş Avansı`, `Masraf Ödemesi`, `Maaş Avansı` → `195`/`196`) ana grubu
doğru belirliyor, ama grup içindeki alt hesap araması difflib benzerliğiyle
yapılıyor ve **yanlış kişiyi** seçiyor. Gerçek örnekler:

```
"ABDULKADIR SAYICI Masraf Ödemesi Arta Tekmer"
   sistem → 195 01 A20  Abdülkadir Yılmaz   (0.65)
   doğrusu → 331 02      Abdulkadir Sayıcı    (planda var, ama 331 grubunda)

"dilara sager masraf ödemesi"
   sistem → 195 01 D06  Dilara Kaya          (0.67)
   doğrusu → planda yok, çözülemedi olmalı

"... İlyas hesabına giden FAST ödemesi"     (soyad yok)
   sistem → 195 01 I02  İlyas Yücel          (0.45)
   planda ayrıca 195 01 H13 İlyas Ömeroğlu var — belirsiz
```

Bu satırlar onay kuyruğunda olduğu için kayıt bozulmuyor, ama kullanıcı
`ONAYLA`'ya basarken yanlış kişi kolayca gözden kaçıyor.

### Düzeltme

Kural grubu içindeki alt hesap aramasında da **benzersiz önek yöntemi**
kullanılsın, difflib benzerliği değil:

- Çıkarılan isim **ad + soyad** olarak bir hesap adının öneki olmalı.
  Tek başına `ABDULKADIR` yetmez.
- **Birden fazla eşleşme** varsa (`İlyas` → Ömeroğlu ve Yücel) satır onaya
  düşsün, hepsi aday listelensin.
- **Hiç eşleşme yoksa** alt hesap boş bırakılsın, sadece ana grup (`195`)
  önerilsin. **Yakın isimli başka kişiyi asla önerme** — `dilara sager` için
  `Dilara Kaya` gösterilmesin.

### Kural ana grubu tek başına kilitlemesin

`ABDULKADIR SAYICI` hesap planında gerçekten var ama `331 02` (ortaklar)
altında; kural `195`'e kilitlediği için bulunamıyor.

Kural bir ana grup önerdiğinde, aynı ismin **başka gruplarda tam eşleşmesi**
varsa onlar da aday olarak gösterilsin ve satır onaya düşsün. Kural grubundaki
aday ilk sırada dursun, diğerleri alternatif olarak.

---

## 2. Kişi yönlendirme tablosu (yeni)

Bazı kişilerin ödemeleri kural grubuna değil, kendilerine özel bir hesaba
gitmeli. Örnek: ortaklar ve yöneticiler — `Abdulkadir Sayıcı`, `Abdülkadir
Şahin` gibi isimler görüldüğünde ve para **çıkıyorsa** `331` (ortaklara borçlar)
grubuna gitmeli, `195` personel avansına değil.

Bu bilgi koda gömülmemeli; kullanıcı kendi tanımlayabilmeli.

### Tablo

```
KisiYonlendirme
  Id, FirmaId
  IsimCekirdegi        (normalize, örn. "ABDULKADIR SAYICI")
  Yon                  (Giren | Cikan | Farketmez)
  HesapKodu            (örn. "331 02")
  Aciklama             (opsiyonel not)
  Aktif
```

- Firma bazlı.
- `Yon` alanı önemli: aynı kişi için giden ödeme `331`, gelen tahsilat başka bir
  hesap olabilir. `Farketmez` seçilirse iki yönde de aynı hesap.
- Eşleşme normalize isim çekirdeği üzerinden, tam eşleşme.

### Katman sırası

Kişi yönlendirme, **sabit kural katmanından önce** çalışsın. Yani `masraf
ödemesi` ifadesi geçse bile, kişi tabloda tanımlıysa oraya gitsin.

Eşleşme bulunursa güven yüksek (kullanıcı elle tanımlamış), otomatik çözülsün.

### Arayüz

Tanımlar altında yeni bölüm: **Kişi Yönlendirmeleri**. Vergi kodları bölümüyle
aynı kalıpta — liste, ekle, düzenle, sil. Kolonlar: isim, yön, hesap kodu, hesap
adı, açıklama.

Hesap kodu girilirken hesap planından öneri gösterilsin, geçersiz kod
kaydedilmesin.

**Onay ekranından kısayol:** kullanıcı bir satırı onaylarken "bu kişiyi hep bu
hesaba yönlendir" seçeneği olsun. Seçilirse `KisiYonlendirme` kaydı otomatik
oluşturulsun (yön, o satırın yönünden gelsin). Böylece kullanıcı Tanımlar'a gidip
elle girmek zorunda kalmaz.

---

## 3. Analiz için dışa aktarım (yeni)

Şu an dışa aktarım `OnayBekliyor` veya `Cozulemedi` satır varken 400 dönüyor —
doğru kural, **korunsun**. Ama sistemin ne önerdiğini incelemek için onaydan
önce bir çıktı gerekiyor.

Yeni uç nokta ve düğme: **"Analiz için dışa aktar"**. Durum ne olursa olsun tüm
satırları xlsx olarak versin. Kolonlar:

```
SiraNo | Tarih | Yon | Tutar | HamAciklama | UretilenAciklama
OnerilenHesapKodu | OnerilenHesapAdi | GuvenSkoru | KaynakKatman
Durum | AdaySayisi
```

Bu dosya ORKA'ya yüklenmez, yalnız inceleme içindir — düğme adı ve yanındaki
açıklama bunu net söylesin. Mevcut "Kod Listesi" ve "Düzeltilmiş Ekstre"
düğmeleri aynı kısıtla kalsın.

---

## Testler

Gerçek dosyanın kendi açıklama metinleriyle:

1. `ABDULKADIR SAYICI Masraf Ödemesi` → `195 01 A20 Abdülkadir Yılmaz`
   **önerilmiyor**; adaylar arasında `331 02 Abdulkadir Sayıcı` var; satır onaya
   düşüyor
2. `dilara sager masraf ödemesi` → `Dilara Kaya` önerilmiyor; alt hesap boş,
   ana grup `195` öneriliyor
3. `İlyas hesabına giden FAST` → iki adayla (`195 01 H13`, `195 01 I02`) onaya
   düşüyor
4. `Mesut Aktaş`, `Eda Budak`, `İlyas Ömeroğlu` gibi **ad + soyad tam geçen**
   satırlar eskisi gibi otomatik çözülmeye devam ediyor (regresyon)
5. `KisiYonlendirme`'ye `ABDULKADIR SAYICI / Çıkan / 331 02` eklenince aynı satır
   otomatik `331 02`'ye gidiyor
6. Aynı kişi için `Giren` yönlü ayrı kayıt tanımlanabiliyor ve doğru seçiliyor
7. Onay ekranındaki "bu kişiyi hep bu hesaba yönlendir" seçeneği kayıt
   oluşturuyor
8. Analiz dışa aktarımı çözülmemiş satır varken de çalışıyor; "Kod Listesi" ve
   "Düzeltilmiş Ekstre" hâlâ 400 dönüyor

## Kabul kriterleri

Derleme temiz, migration üretildi ve uygulandı, `has-pending-model-changes`
temiz, tüm testler geçiyor.

Sonunda `OZET.md` ve `KARARLAR.md` güncelle. Gerçek dosyayla çalıştırıp
otomatik / onay bekleyen / çözülemeyen sayılarının önceki tura (139 / 136 / 12)
göre nasıl değiştiğini yaz.
