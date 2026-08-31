# Görev: PkfRobot — Windows Forms Arayüzü ve Kalibrasyon

Bu görevi baştan sona, soru sormadan tamamla. Önce `src/Robot.Agent/` içindeki
adım motorunu, `Tikla` adımını, `--kalibre` modunu ve ajan bağlantı katmanını
oku. Belirsizlik çıkarsa kararını `KARARLAR.md`'ye yaz ve devam et.

Mevcut testlerin tamamı aynen geçmeye devam etmeli.

---

## Amaç

PkfRobot şu an konsol uygulaması; koordinatlar ve yollar JSON/config dosyalarına
elle yazılıyor. Makine değiştiğinde veya ORKA'nın ekran düzeni bozulduğunda
dosya düzenlemek gerekiyor.

Hedef: küçük bir masaüstü uygulaması. Kullanıcı koordinatları **ekrana
tıklayarak** seçsin, dosya yollarını gezginle bulsun, ayarlar o makinede kalsın
ve bozulduğunda yeniden kalibre edilebilsin.

**Adım motoruna, JSON görev tanımlarına ve ajan bağlantı mantığına dokunma.**
Bu tur arayüz ve ayar yönetimi.

---

## 1. Uygulama biçimi

- WinForms (proje zaten `net8.0-windows`)
- **Argümansız çalıştırılırsa arayüz açılsın.** `--ajan`, `--kalibre`, `--probe`
  gibi mevcut konsol modları aynen kalsın — hata ayıklarken gerekiyor.
- Pencere küçük olsun (hesap makinesi boyutu), her zaman üstte seçeneği bulunsun
- **Sistem tepsisine küçülsün.** Kapatma düğmesi uygulamayı kapatmasın, tepsiye
  indirsin; çıkış tepsi menüsünden olsun. Ajan arka planda bağlı kalmalı.
- Tepsi simgesi bağlantı durumunu göstersin (bağlı / bağlanıyor / kopuk)

## 2. Ana ekran — durum

- Hub bağlantı durumu, son kalp atışı
- ORKA çalışıyor mu
- Çalışan iş varsa: iş tipi, ilerleme, mesaj
- Son beş işin özeti (tarih, sonuç, süre)
- Log penceresi (son satırlar) ve "log klasörünü aç" düğmesi

Ajan bağlantısı arayüzden başlatılıp durdurulabilsin.

## 3. Ayarlar — yollar

Gezgin düğmesiyle seçilecek:

- **ORKA exe yolu** (varsayılan `C:\WinIceberg\OrkaWinIceberg.64.exe`)
- İndirilen iş dosyalarının klasörü
- Log klasörü

Seçilen yolun gerçekten var olduğu kontrol edilsin, yoksa kırmızı uyarı.

## 4. Ayarlar — ORKA giriş bilgileri

- Firma kodu (ORKA'da F7 sonrası girilen)
- Kullanıcı kodu (`pkf03` gibi)
- Şifreler **DPAPI ile şifreli** saklansın, ajan anahtarındaki kalıbın aynısı.
  Ekranda yıldızlı gösterilsin, loglanmasın.

## 5. Koordinat kalibrasyonu — işin özü

Her koordinat kaydı için bir satır: **ad**, **mevcut değer**, **Seç** düğmesi,
**Dene** düğmesi.

### Seçme akışı

1. Kullanıcı "Seç"e basar
2. Uygulama küçülür, **ORKA penceresi öne getirilir**
3. Ekranın üstünde ince bir bilgi şeridi: "Hedefe tıklayın · İptal için Esc"
4. Kullanıcı ORKA'da hedefe tıklar
5. Tıklanan nokta yakalanır, **ORKA penceresine göre orana çevrilir**
6. Uygulama geri gelir, değer forma yazılır

### Oran dönüşümü — kritik

Mevcut `Tikla` adımı **pencereye göre oranlı** koordinat kullanıyor
(`X:0.081 Y:0.320` gibi), mutlak ekran koordinatı değil. Sebebi: pencere
maximize olduğu için oran sabit kalıyor.

Seçici de aynısını yapmalı:

```
oranX = (tiklananX - pencereSol) / pencereGenislik
oranY = (tiklananY - pencereUst) / pencereYukseklik
```

- Hedef pencere **ORKA olmalı** — başka bir pencereye tıklanırsa uyarı ver ve
  kaydetme
- ORKA maximize değilse uyar: "ORKA'yı tam ekran yapın, koordinatlar buna göre
  hesaplanıyor"
- Mutlak koordinat **hiçbir yerde saklanmasın**

### Dene düğmesi

Kaydedilen koordinatı doğrulamanın tek yolu denemek:

1. ORKA öne getirilir
2. O koordinata tıklanır
3. **Ekran görüntüsü alınır** ve uygulamada gösterilir
4. Kullanıcı doğru yere tıkladığını gözüyle görür

Bu olmadan kalibrasyon kör bir iş olur.

### Kayıtlı koordinatlar

Mevcut JSON görevlerindeki `Tikla` adımlarından türetilsin — hangi koordinatlar
kullanılıyorsa arayüzde o satırlar çıksın. Elle liste yazma; görev dosyaları
değişince arayüz de değişsin.

Her koordinatın **açıklaması** olsun: "Veri Transferi > Banka Ekstreleri
düğmesi" gibi. Kullanıcı altı ay sonra hangi noktanın ne olduğunu bilmeli.

## 6. Ayarların saklanması

- `%AppData%\PkfRobot\ayarlar.json` — publish klasörü **değil**. Mevcut
  `appsettings.json` disiplininin sebebi bu: publish üzerine yazınca ofiste test
  edilmiş düzeltmeler siliniyordu.
- Şifreler ayrı, DPAPI ile şifreli
- **Yedekle / geri yükle** düğmesi: ayarları tek dosyaya aktarıp geri alma.
  Makine değiştiğinde kalibrasyonu baştan yapmamak için.

## 7. Modülerlik

Yeni bir ayar veya koordinat eklemek kod değişikliği gerektirmesin:

- Ayar tanımları bir listede (ad, tip, açıklama, varsayılan) dursun
- Arayüz o listeden üretilsin
- Yeni JSON görev dosyası eklenince yeni koordinatlar arayüzde kendiliğinden
  görünsün

---

## Testler

Arayüz test edilemez ama mantık edilebilir. Ayır ve sına:

- Mutlak koordinat → oran dönüşümü doğru (farklı pencere boyutlarıyla)
- Oran → mutlak dönüşümü tersini veriyor
- ORKA dışı pencereye tıklama reddediliyor
- Ayarlar kaydedilip okunuyor, şifreler düz metin değil
- Yedek alınıp geri yüklenince ayarlar aynı
- Koordinat listesi JSON görev dosyalarından doğru türetiliyor

## Kabul kriterleri

1. Derleme temiz, publish alınabiliyor, mevcut testler aynen geçiyor
2. Argümansız çalıştırınca arayüz açılıyor; `--ajan` konsol modu bozulmadı
3. Yukarıdaki testler yazıldı ve geçiyor
4. Ayarlar `%AppData%` altında, şifreler şifreli, loglarda geçmiyor
5. Adım motoruna ve JSON görev tanımlarına dokunulmadı

Sonunda `OZET.md` ve `KARARLAR.md` güncelle. Kalibrasyonun nasıl yapılacağını
adım adım yaz — ofiste bu belgeye bakarak çalışacaksın.
