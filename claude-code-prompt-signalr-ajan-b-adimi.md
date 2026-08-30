# Görev: PkfRobot'a SignalR İstemcisi (B Adımı)

Bu görevi baştan sona, soru sormadan tamamla. Önce mevcut `src/Robot.Agent/`
kodunu oku, aynı kalıbı izle. Belirsizlik çıkarsa kararını `KARARLAR.md`'ye yaz
ve devam et.

Mevcut testlerin tamamı aynen geçmeye devam etmeli.

---

## Kapsam

Bu tur **yalnız bağlantı**. Ajan hub'a bağlanacak, kendini tanıtacak, kalp atışı
gönderecek, ORKA durumunu bildirecek.

**İş alma ve çalıştırma bu turda YOK.** `GridDoldur` çağrısı, iş kuyruğu, ilerleme
bildirimi C adımında. Mevcut JSON adım motoruna dokunma.

Bu adımın doğrulaması: ajan çalışırken sunucudaki `baglilar` ucunda görünmesi,
kapatılınca listeden düşmesi, ağ koptuğunda kendiliğinden yeniden bağlanması.

## Mevcut yapı

- `src/Robot.Agent/`, `net8.0-windows`, çözüm dosyasına dahil **değil**
- Self-contained single-file publish, Google Drive üzerinden dağıtılıyor
- JSON tabanlı adım motoru — yeni iş akışı = yeni JSON, kod değişmez
- `appsettings.json` kaynak dosyası disiplini: yayınlamadan önce daima kaynak
  güncellenir, yoksa ofiste test edilmiş düzeltmeler eziliyor
- Tek bir ayrılmış banka bilgisayarında çalışacak

Önce bu projenin nasıl başlatıldığını incele — konsol mu, servis mi, arayüzü var
mı. Bağlantı yönetimini mevcut yapıya uygun yerleştir.

---

## 1. Ajan anahtarının saklanması

Sunucu tarafında ajan anahtarı üretildi (`pkfr_` önekli, A adımında). Ajan bunu
saklayacak.

- **Yeri:** `%AppData%\PkfRobot\agent.dat` (publish klasörü değil — publish
  üzerine yazıldığında kaybolmasın; mevcut `appsettings.json` disiplininin
  sebebiyle aynı)
- **Şifreleme:** Windows DPAPI (`ProtectedData.Protect`, `CurrentUser` kapsamı).
  Anahtar düz metin olarak diskte durmasın.
- **İlk kurulum:** dosya yoksa ajan anahtarı sorsun (konsol girdisi veya basit
  bir pencere), şifreleyip kaydetsin, bir daha sormasın
- **Sıfırlama:** `--anahtari-sifirla` argümanı dosyayı silsin, yeniden sorsun
- Anahtar **hiçbir yerde loglanmasın** — maskeleme kuralına dahil et
  (mevcut `Maskele` yöntemi `sifre` içeren alanları maskeliyor; `anahtar`,
  `token`, `agent` da eklensin)

## 2. Token alma

```
POST https://www.dijitalmasraf.com/auth/agent/token
Body: { AjanAnahtari }
→ ajan JWT'si, 8 saat ömürlü
```

- Ajan token'ı **bellekte** tut, diske yazma
- Süresi dolmadan **önce** yenile (örn. kalan süre 30 dakikanın altına düşünce)
- Anahtar geçersiz/iptal edilmişse (401) yeniden denemeyi bırak, anlaşılır bir
  mesaj göster: "Ajan anahtarı geçersiz veya iptal edilmiş. Yönetim > Ajanlar
  ekranından yeni anahtar üretin." Sonsuz döngüye girme.
- Hız sınırına takılırsa (429) `Retry-After` başlığına uy

## 3. Hub bağlantısı

```
wss://www.dijitalmasraf.com/agenthub
```

- Token `?access_token=` ile taşınsın (WebSocket başlık gönderemiyor; sunucu
  tarafı buna göre yapılandırıldı)
- `WithAutomaticReconnect` yeterli değil — token süresi dolduğunda yeniden
  bağlanma da başarısız olur. Kendi yeniden bağlanma döngünü yaz: kopunca token
  tazeliğini kontrol et, gerekirse yenile, sonra bağlan.
- **Üstel geri çekilme**: 5s, 10s, 30s, 60s, sonra 60s sabit. Sonsuz dene —
  gece ağ koparsa sabah bağlı olmalı.
- Bağlanınca `Kaydol` çağır: `MakineId`, `MakineAdi`, `AjanSurumu`,
  `IsletimSistemi`, `OrkaCalisiyorMu`
- `MakineId` **kararlı** olsun — makine adı + kalıcı bir GUID (ilk çalıştırmada
  üretilip `agent.dat` yanında saklanır). Her açılışta değişmemeli, yoksa
  sunucuda hayalet kayıtlar birikir.
- Sunucu kaydı reddederse (eski sürüm) mesajı göster ve **yeniden deneme** —
  güncelleme gerekiyor, döngüye girmenin anlamı yok

## 4. Kalp atışı ve ORKA durumu

- 30 saniyede bir `KalpAtisi()` çağır
- ORKA çalışıyor mu kontrolü: `OrkaWinIceberg.64` süreci ayakta mı
  (`Process.GetProcessesByName`). Durum değiştiğinde sunucuya bildir — her kalp
  atışında değil, yalnız değişimde.
- ORKA'nın çalışması bağlantı için şart değil; ajan ORKA kapalıyken de bağlı
  kalsın, yalnız durumu bildirsin

## 5. Görünürlük

Ajan tek bir makinede sessizce çalışacak. Durumunu görebilmek gerekiyor:

- Konsol çıktısı veya log dosyası: bağlantı durumu, son kalp atışı, token
  yenileme, hatalar
- Log dosyası `%AppData%\PkfRobot\logs\` altında, günlük dosya, eskiler
  temizlensin (örn. 14 gün)
- Mevcut loglama neyse onu kullan, yeni kütüphane ekleme

## 6. Sürüm

`AjanSurumu` derleme sürümünden okunsun, elle yazılmasın. Sunucu asgari sürüm
kontrolü yapıyor; sürümü artırmayı unutmak sessiz bir uyumsuzluk yaratır.

---

## Testler

Robot.Agent çözüme dahil değil ve UI otomasyonu test edilemiyor. Bağlantı
mantığını **test edilebilir bir sınıfa ayır** (hub bağlantısı bir arayüz
arkasında olsun) ve şunları sına:

- Token süresi dolmak üzereyken yenileniyor
- 401 alınca yeniden denemiyor, anlaşılır hata veriyor
- 429 alınca `Retry-After` kadar bekliyor
- Geri çekilme aralıkları doğru ilerliyor
- `MakineId` iki çalıştırmada aynı kalıyor
- ORKA durumu yalnız değişimde bildiriliyor

Test projesi çözüme eklenebilir (Robot.Agent'in kendisi hariç); mevcut kalıba
uygun bir yere koy.

## Kabul kriterleri

1. Robot.Agent derleniyor ve publish alınabiliyor
2. Çözümdeki mevcut testler aynen geçiyor, yeni testler yazıldı ve geçiyor
3. Ajan yerelde çalıştırılıp hub'a bağlanıyor, `baglilar` ucunda görünüyor,
   kapatılınca düşüyor — bunu **gerçekten çalıştırarak** doğrula
4. Ağ kesilip geri geldiğinde kendiliğinden bağlanıyor
5. Anahtar hiçbir log satırında geçmiyor
6. `appsettings.json` kaynak dosyası güncellendi (publish'te ezilmesin)

Sonunda `OZET.md` ve `KARARLAR.md` güncelle. Ajanın ilk kurulumunu adım adım
yaz — anahtarı nereden alıp nasıl gireceğini, publish'i nereye kopyalayacağını,
Windows'ta başlangıçta otomatik çalışması için ne yapılacağını.
