# Görev: SignalR Hub + Agent Kaydı (A Adımı)

Bu görevi baştan sona, soru sormadan tamamla. Önce ilgili kodu oku, sonra değiştir.
Belirsizlik çıkarsa mevcut kalıbı izle, kararını `KARARLAR.md`'ye yaz ve devam et.

Mevcut testlerin tamamı aynen geçmeye devam etmeli.

---

## Amaç ve kapsam

Kullanıcı, dijitalmasraf.com'dan "ORKA'ya aktar" dediğinde ofisteki banka
bilgisayarında çalışan **PkfRobot** (Windows, `net8.0-windows`, FlaUI) uygulaması
işi yapacak. Sunucu o makineye doğrudan uzanamayacağı için ajan **dışarı doğru**
bağlanacak; SignalR ile ters yön.

**Bu tur yalnız sunucu tarafı.** Windows ajanı (B adımı) ve Blazor durum
göstergesi (C adımı) ayrı turlarda yapılacak. Bu turda ajan kodu yazma.

Bu adımın doğrulaması: tarayıcıdan "bağlı ajanlar" ucuna bakınca boş liste
görebilmek, ve bir test istemcisiyle bağlanınca ajanın listede görünmesi.

---

## Kararlar (önceden verildi, tartışma)

- **Hub `CatalogService.Api` içine girecek.** Ayrı servis açma — yeni container,
  compose girdisi, deploy adımı demek olur ve kazancı yok. Banka aktarım
  paketini üreten uçlar zaten bu serviste (`api/catalog/banka-ekstre/*`).
- **Ocelot baypas edilecek, nginx doğrudan bağlayacak.** SignalR WebSocket
  kullanıyor; uzun ömürlü bağlantı gateway'lerin timeout ve buffering
  ayarlarıyla iyi geçinmiyor. Bu projede zaten uzun süren batch rotalarında
  Ocelot timeout sorunu yaşandı.
  Hedef: `wss://dijitalmasraf.com/agenthub` → nginx → CatalogService container'ı.
- **Ajanlar bellekte tutulacak.** Container yeniden başlarsa liste sıfırlanır,
  ajanlar birkaç saniyede yeniden bağlanır. Tek makine senaryosunda yeterli;
  veritabanına yazmaya gerek yok.

---

## 1. Hub

`CatalogService.Api` içinde `Features/AgentHub/` (veya repodaki dikey dilim
kalıbına uygun bir yer) altında:

**`AgentHub : Hub`** — yol `/agenthub`

Ajanın çağıracağı metotlar:

```
Task<KayitSonucu> Kaydol(AjanKaydiIstegi istek)
Task KalpAtisi()
```

`AjanKaydiIstegi`: `MakineId` (kararlı, makineye özgü), `MakineAdi`,
`AjanSurumu`, `IsletimSistemi`, `OrkaCalisiyorMu` (bool, nullable).

`KayitSonucu`: `Kabul` (bool), `Mesaj`, `SunucuSurumu`, `AsgariAjanSurumu`.

**Sürüm kontrolü baştan olsun.** Ajan sürümü sunucunun beklediği asgari sürümün
altındaysa kayıt reddedilsin ve mesajda "güncelleyin" densin. Şu an tek makine
var ama dağıtım Google Drive üzerinden yapılıyor; sürüm uyumsuzluğu sonradan
çok iş açar. Asgari sürüm yapılandırmadan okunsun.

**Yetkilendirme:** hub `[Authorize]` olsun, mevcut JWT şemasını kullansın.
Ajan bağlanırken token göndersin. Token yoksa/geçersizse bağlantı reddedilsin.

**Kimlik güvenliği:** kayıtta gelen `MakineId`'ye körü körüne güvenme; kaydın
sahibi olarak token'daki kullanıcı kimliği saklansın. İleride "kim hangi makineye
iş gönderebilir" kuralı buna dayanacak.

## 2. Bellekte ajan deposu

`IAjanDeposu` / `AjanDeposu` — singleton, thread-safe (`ConcurrentDictionary`).

Tutulacaklar: `ConnectionId`, `MakineId`, `MakineAdi`, `AjanSurumu`,
`KullaniciId`, `BaglantiZamani`, `SonKalpAtisi`, `OrkaCalisiyorMu`.

- `OnConnectedAsync` / `OnDisconnectedAsync` ile giriş-çıkış yönetilsin
- Aynı `MakineId` ile ikinci bağlantı gelirse eskisi düşürülsün (yeniden
  bağlanma sırasında hayalet kayıt kalmasın)
- `KalpAtisi()` `SonKalpAtisi`'nı güncellesin
- Belirli süre kalp atışı gelmeyen kayıtlar temizlensin (arka plan servisi veya
  okuma anında süzme — hangisi daha basitse)

## 3. Durum ucu

```
GET /api/catalog/agent/baglilar
```

Bağlı ajanların listesini dönsün: makine adı, makine kimliği, sürüm, bağlantı
zamanı, son kalp atışı, ORKA çalışıyor mu.

`[Authorize]` olsun. Bu uç **Ocelot üzerinden** geçebilir — sıradan bir HTTP
isteği, yalnız hub'ın WebSocket yolu baypas edilecek.

## 4. Yapılandırma ve nginx

`Program.cs`'e `AddSignalR()` ve `MapHub<AgentHub>("/agenthub")` eklensin.

**Nginx bloğunu ayrı bir dosyaya yaz** (`deploy/nginx-agenthub.conf` gibi) ve
`OZET.md`'ye "şunu şuraya ekle, şu komutu çalıştır" şeklinde net talimat koy.
Sunucuya erişimin yok; kullanıcı bunu elle uygulayacak.

Blokta olması gerekenler: `/agenthub` location, `proxy_pass` doğrudan
CatalogService container'ına, `Upgrade` ve `Connection` başlıklarının
geçirilmesi, `proxy_http_version 1.1`, yüksek `proxy_read_timeout` ve
`proxy_send_timeout`.

Docker compose'da yeni servis veya port açma — hub mevcut container içinde
yaşıyor.

## 5. Test istemcisi

Doğrulamayı kolaylaştırmak için küçük bir test istemcisi yaz (konsol uygulaması
veya bir test) — hub'a bağlanıp `Kaydol` çağırsın, sonra `baglilar` ucunda
göründüğü doğrulansın. Ajanın kendisini yazma, yalnız bağlantıyı kanıtlayan
asgari istemci.

---

## Testler

- Geçerli sürümle kayıt kabul ediliyor, depoda görünüyor
- Eski sürümle kayıt reddediliyor, mesaj anlaşılır
- Aynı `MakineId` ile ikinci bağlantı eskisini düşürüyor
- Bağlantı kopunca depodan siliniyor
- Kalp atışı `SonKalpAtisi`'nı güncelliyor
- Token'sız bağlantı reddediliyor
- `baglilar` ucu yetkisiz istekte 401 dönüyor

## Kabul kriterleri

1. Derleme temiz, tüm mevcut testler aynen geçiyor
2. Yukarıdaki testler yazıldı ve geçiyor
3. Hub yerelde ayağa kalkıyor; test istemcisi bağlanıp `baglilar` ucunda
   görünüyor — bunu **gerçekten çalıştırarak** doğrula, yalnız derlemeyle değil
4. Nginx bloğu ayrı dosyada, `OZET.md`'de uygulama talimatı var
5. Ocelot yapılandırması değişmedi (hub oradan geçmiyor)

Sonunda `OZET.md` ve `KARARLAR.md` güncelle.
