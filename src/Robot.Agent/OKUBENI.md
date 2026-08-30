# PkfRobot — ORKA Masaüstü Otomasyon Ajanı

ORKA'yı klavye üzerinden süren konsol uygulaması. Adımlar JSON dosyalarında tanımlı,
yeni iş eklemek için kod değiştirmek gerekmez.

---

## Neden böyle tasarlandı

ORKA Delphi/VCL tabanlı ve UI Automation'a kapalı. FlaUInspect incelemesinde:

- Form içi kontroller `Pane` olarak görünüyor, `AutomationId` yok
- Grid (`TcxGridSite`) tek opak blok, satır/hücre okunamıyor
- **Ama pencere başlıkları okunabiliyor** → tüm doğrulama bunun üzerine kurulu
- Klavye navigasyonu çalışıyor (ok tuşları, F7, Ctrl+F)

Ofis testinde çıkan istisna: **Veri Transferi ekranı klavyeye kapalı.** Sol panel,
grid satırları ve "Transfere Başla" butonuna Tab geçmiyor, Ctrl+F / F6 yok, yazarak
arama yok. Orası için `Tikla` adımı eklendi — aşağıda.

Bu yüzden robot grid'e yazmaz; ORKA'nın kendi Excel aktarım kapısını kullanır.
Muhasebe mantığı DijitalMasraf'ta kalır, robot sadece "el" olur.

---

## Derleme (ev PC'si)

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Çıktı klasörü:

```
bin\Release\net8.0-windows\win-x64\publish\
```

İçinde tam olarak şunlar olur:

| Dosya | Boyut | Ofise kopyala? |
|---|---|---|
| `PkfRobot.exe` | ~163 MB | **Evet** — .NET runtime ve native DLL'ler içinde gömülü |
| `appsettings.json` | ~1 KB | **Evet** — ayarlar buradan okunur |
| `gorevler\` | 4 JSON | **Evet** — görev tanımları |
| `PkfRobot.pdb` | ~20 KB | Hayır — sadece hata ayıklama sembolleri |

Yani `publish\` klasörünü olduğu gibi kopyalamak en kolayı; `.pdb`'yi silebilirsin.

Exe'nin yanında **ayrı DLL çıkmaz**. Bunu sağlayan csproj'daki
`IncludeNativeLibrariesForSelfExtract` ayarıdır — bu ayar olmasaydı
`wpfgfx_cor3.dll`, `PresentationNative_cor3.dll` gibi 5 native dosya exe'nin
yanında ayrı ayrı durur ve kopyalanmadıklarında ofiste hata verirdi. Ayarı kaldırma.

Ofis PC'sinde **derleme yapma** — .NET SDK kurmaya gerek yok.

---

## Ajan modu (`--ajan`) — sunucuya bağlı kalma

ORKA otomasyonundan **ayrı** bir mod: sunucuya bağlanır ve bağlı kalır. Kalp
atışı gönderir, ORKA'nın açık olup olmadığını bildirir. Bu modda ORKA'ya
dokunmaz, görev çalıştırmaz.

```
PkfRobot.exe --ajan
```

**İlk çalıştırmada ajan anahtarını sorar.** Anahtarı panelden alırsın:
**Yönetim → Ajanlar → Yeni Ajan**. `pkfr_` ile başlar ve **bir kez** gösterilir —
o ekran kapandıktan sonra hiçbir yerden okunamaz, kaybolursa yenisini üretip
eskisini iptal edersin.

Girilen anahtar `%AppData%\PkfRobot\agent.dat` içine **Windows DPAPI ile
şifreli** yazılır; publish klasörüne değil, çünkü publish her güncellemede
üzerine yazılıyor. Bir daha sorulmaz.

| Dosya | Ne |
|---|---|
| `%AppData%\PkfRobot\agent.dat` | Ajan anahtarı, şifreli. Başka kullanıcı/makine okuyamaz. |
| `%AppData%\PkfRobot\makine.dat` | Makine kimliği (GUID). Her açılışta aynı kalsın diye saklanıyor. |
| `%AppData%\PkfRobot\logs\ajan-<tarih>.log` | Günlük log, 14 günden eskiler silinir. |

Anahtarı değiştirmek gerekirse:

```
PkfRobot.exe --anahtari-sifirla
```

**Sunucu adresleri** `appsettings.json > Ajan` bölümünde; Notepad ile
değiştirilebilir, derleme gerekmez.

### Ağ koparsa

Ajan kendiliğinden yeniden bağlanır: 5 sn, 10 sn, 30 sn, sonra dakikada bir,
sonsuza kadar. Gece ağ koparsa sabah bağlı olur. Log'a şu düşer:

```
[UYARI] Baglanti hatasi: ...
[BILGI] 5 sn sonra yeniden baglanilacak.
[BILGI] Hub'a baglanildi: BANKA-PC (...), surum 1.0.0, ORKA: acik.
```

Anahtar **iptal edilmişse** yeniden denemez, şunu yazıp durur:

```
Ajan anahtari gecersiz veya iptal edilmis. Yonetim > Ajanlar ekranindan yeni
anahtar uretin ve PkfRobot.exe --ajan --anahtari-sifirla ile girin.
```

Sunucu **sürümü eski** bulursa da denemez; yeni publish'i kopyalaman gerekir.

### Windows açılışında otomatik başlatma

Ajan oturum açıkken çalışmalı, o yüzden servis değil, **oturum açılışında
başlayan görev**. Yönetici komut satırında:

```
schtasks /Create /TN "PkfRobot Ajan" /TR "C:\PkfRobot\PkfRobot.exe --ajan" ^
  /SC ONLOGON /RL LIMITED /F
```

Görevi **anahtarı giren kullanıcıyla aynı hesapta** çalıştır — DPAPI kullanıcıya
bağlı, başka hesapta `agent.dat` çözülemez ve anahtar yeniden sorulur.

Daha basiti: exe'ye kısayol oluştur, hedefine ` --ajan` ekle, kısayolu
`%AppData%\Microsoft\Windows\Start Menu\Programs\Startup` içine at.

### Anahtar log'a düşmez

Ajan anahtarı hiçbir log satırına yazılmaz; yazılsa bile `pkfr_***` olarak
maskelenir. Aynısı ajan token'ı için de geçerli. Görev adımlarında ise adında
`sifre`, `anahtar`, `token` ya da `agent` geçen her değer `***` olarak yazılır.

---

## İlk çalıştırma sırası

### 1. Probe — hiçbir tuşa basmaz

```
PkfRobot.exe --probe
```

Ekrandaki pencereleri listeler. Bir şey ters gittiğinde önce bunu çalıştır.

**Hiç pencere okunamıyorsa:** ORKA yönetici modunda çalışıyor olabilir.
`PkfRobot.exe`'yi de sağ tık → Yönetici olarak çalıştır.

### 1b. Kalibre — koordinat ölçmek için

```
PkfRobot.exe --kalibre
```

Fareyi hedefin üzerine getir, oranı oku, JSON'a yapıştır. Ctrl+C ile çık.
Detay: aşağıdaki "Fare ile tıklama" bölümü.

### 2. Temel zincir

```
PkfRobot.exe --gorev gorevler\01-orka-ac-firma-sec.json --sifre GIZLI
```

ORKA açılıp firmaya giriyor mu? `C:\RobotLog\` altındaki ekran görüntülerine bak.

### 3. Modül navigasyonu

```
PkfRobot.exe --gorev gorevler\02-modul-ve-sekme.json --sifre GIZLI ^
  --degisken modulSagOk=7 ^
  --degisken sekmeAdi="Veri Transferi"
```

Sağ ok sayısını buradan değiştirebilirsin, JSON'a dokunmadan.

### 4. Banka transferi (DryRun)

```
PkfRobot.exe --gorev gorevler\03-banka-transferi.json --sifre GIZLI ^
  --degisken dosyaYolu="C:\RobotGiris\102-1-1-VAK-01_202607.xlsx" ^
  --degisken hesapKodu="102 1 1 VAK-01"
```

`DryRun: true` olduğu için **Kaydet'e basmaz**. Kaydet ekranına kadar gidip durur.

### 5. Canlı mod — sadece defalarca temiz çalıştıktan sonra

```
PkfRobot.exe --gorev ... --canli
```

Önce sahte bir test firmasında dene.

---

## Ayarlar

`appsettings.json` — Notepad ile düzenlenebilir, derleme gerekmez.

| Ayar | Ne işe yarar |
|---|---|
| `Ajan.TokenUcu` / `Ajan.HubAdresi` | `--ajan` modunun bağlandığı sunucu (anahtar burada değil) |
| `Ajan.KalpAtisiSaniye` | Canlılık bildirimi aralığı — varsayılan 30 |
| `Ajan.OrkaSurecAdi` | ORKA açık mı kontrolü için süreç adı (`OrkaPath`'teki exe'nin uzantısız hâli) |
| `OrkaPath` | ORKA exe'sinin yolu |
| `DryRun` | `true` iken Kaydet adımları atlanır |
| `OtomatikOneGetir` | Tuş göndermeden önce ORKA'yı öne getirir — varsayılan `true`, kapatma |
| `Zamanlama.AdimBeklemeMs` | Robot hızlı gidiyorsa artır |
| `Zamanlama.PencereTimeoutSn` | Yavaş günlerde artır |
| `Pencereler.*` | Beklenen pencere başlıkları (içerir mantığı) |
| `BeklenmeyenPencereler` | Bu başlıklar çıkarsa robot durur |

---

## Odak güvencesi (`OtomatikOneGetir`)

Ofis testinde robot ORKA yerine `cmd` penceresine yazdı — ORKA ön planda değildi
ve **şifre cmd'ye gitti**. Artık `Tus`, `Yaz`, `TemizleYaz`, `Kisayol` ve
`OnayGerekir` adımlarından **önce** odak kontrol ediliyor:

- Odaktaki pencere ORKA'nın **process'ine** aitse hiçbir şey yapılmaz.
- Değilse ORKA öne getirilir (ana ekran → giriş ekranı → şube seçim sırasıyla).
- Log'a `Yaz: odak ORKA'da degildi -> '...' one getiriliyor.` satırı düşer.

Kontrol **başlığa değil process'e** bakıyor. Sebebi: ORKA'nın kendi açtığı "Excel
Dosyasını Seçiniz" diyaloğu ayrı bir pencere; başlığa bakılsaydı robot ondan odağı
çalıp ana pencereye dönerdi ve dosya yolu yanlış yere yazılırdı.

Bu sayede her görevin başına elle `BeklePencere` koymak gerekmiyor.

`Tikla` bu listede yok çünkü zaten hedef pencereyi kendisi öne getiriyor —
üstüne ana pencereyi öne almak, tıklanacak popup'ı arkada bırakırdı.

ORKA process'i `OrkaPath`'teki exe adından ve config'deki bilinen pencere
başlıklarından tespit edilip 5 saniyeliğine önbelleğe alınır; her adımda process
listesi taranmaz.

---

## Alt pencere araması

ORKA'nın **"Firma Şifresini Giriniz."** popup'ı masaüstünün doğrudan çocuğu değil,
ana pencerenin alt penceresi. Sadece üst seviyeye bakıldığı sürece ekranda
dururken bile bulunamıyor, 40 sn timeout oluyordu.

Pencere araması artık iki aşamalı:

1. **Üst seviye** pencereler — hızlı yol, çoğu durumda buraya düşer.
2. Bulamazsa **ORKA process'ine ait pencerelerin alt pencereleri** (`Window` ve
   `Pane` tipleri; Delphi formundaki binlerce alt kontrol taranmaz).

Alt pencerede bulunursa log'a şu düşer:

```
Alt pencerede bulundu: 'Firma Sifresini Giriniz.'  (kok pencere: 'ORKA_0001 ...')
```

Yani firma şifresi popup'ı için artık sabit `Bekle` yerine gerçek bekleme yazılabilir:

```json
{ "Tip": "BeklePencere", "Deger": "Giriniz", "Not": "Firma sifresi popup'i" },
{ "Tip": "Yaz", "Deger": "{firmaSifre}", "Not": "firma sifresi" }
```

`Bekle: 3000` gibi sabit süreler yavaş günde kırılır; `BeklePencere` kırılmaz.

---

## Şifreler

ORKA **iki kez** şifre sorar: giriş ekranında bir kez, firma açılırken bir kez daha.
İkisi ayrı ayarlanır.

| | Giriş şifresi | Firma şifresi |
|---|---|---|
| Göreve geçen değişken | `{sifre}` | `{firmaSifre}` |
| 1. öncelik | `--sifre xxx` | `--degisken firmaSifre=xxx` |
| 2. öncelik | `ORKA_SIFRE` ortam değişkeni | `ORKA_FIRMA_SIFRE` ortam değişkeni |
| 3. öncelik | `appsettings.json > Giris.Sifre` | `appsettings.json > Giris.FirmaSifresi` |

Üstteki bulunursa alttakine bakılmaz.

**Komut satırına şifre yazmaktan kaçın.** Ofis testinde şifre `cmd` penceresinin
**başlığında** göründü ve robotun aldığı ekran görüntülerine düştü. Log'da şifre
maskeleniyor ama ekran görüntüsü maskelenemiyor. Ortam değişkeni başlıkta görünmez:

```
set ORKA_SIFRE=xxx
set ORKA_FIRMA_SIFRE=yyy
PkfRobot.exe --gorev gorevler\01-orka-ac-firma-sec.json
```

`set` ile verilen değer sadece o cmd penceresinde geçerlidir, pencere kapanınca gider.

**Log'da maskeleme:** `Deger` ya da `Not` alanında **"sifre" geçen her adım** log'a
`***` olarak yazılır (büyük/küçük harf duyarsız). Yani `{sifre}` kadar `{firmaSifre}`
de maskelenir; hata satırlarında da maskelenir. Yeni bir şifre alanı eklersen adını
içinde "sifre" geçecek şekilde koy, maskeleme kendiliğinden çalışsın.

Ekran görüntüsü maskelenemez — bu yüzden şifreyi komut satırına yazma.

---

## Adım tipleri (görev JSON'u yazarken)

| Tip | Ne yapar |
|---|---|
| `OrkaBaslat` | ORKA'yı çalıştırır, zaten açıksa atlar |
| `BeklePencere` | Başlığı `Deger` içeren pencere gelene kadar bekler |
| `Dogrula` | O pencere yoksa hata verir, devam etmez |
| `Yaz` | Metin yazar (`{degisken}` destekler) |
| `TemizleYaz` | Ctrl+A sonra yazar (dolu arama kutuları için) |
| `Tus` | `Deger` tuşuna `Adet` kez basar |
| `Kisayol` | `CTRL+F`, `ALT+K` gibi kombinasyon |
| `Tikla` | Pencereye göreli `X`/`Y` oranına fare ile sol tık |
| `Bekle` | `Sayi` milisaniye bekler |
| `EkranGoruntusu` | İsimli görüntü alır |
| `OnayGerekir` | **DryRun'da atlanır** — Kaydet adımları buraya |
| `Log` | Log'a not düşer |

Yeni iş eklemek: `gorevler\` altına yeni JSON. Kod değişmez.

### Her adımda kullanılabilen ek alanlar

| Alan | Ne işe yarar |
|---|---|
| `TimeoutSn` | Bu adıma özel pencere bekleme süresi. Verilmezse `Zamanlama.PencereTimeoutSn` geçerli. Tek yavaş adım yüzünden genel timeout'u büyütmeye gerek yok. |
| `Deger` içinde `\|` | Birden fazla aday başlık: `"Veri Transferi\|Transfer Islemleri"`. Sırayla denenir, ilk bulunan kabul edilir. ORKA sürümden sürüme başlık değiştirdiği için tek başlığa bağlanmak kırılgan. |

Timeout olduğunda log'a **hem denenen tüm adaylar hem de o an ekranda olan
pencere başlıkları** yazılır — "neyi aradı, ne vardı" tek bakışta görülür.

---

## Fare ile tıklama (`Tikla`)

Veri Transferi ekranındaki sol panel, grid satırları ve "Transfere Başla" butonu
klavyeyle erişilemiyor. O kontroller UIA'ya da kapalı olduğu için tıklanacak
eleman bulunamıyor — geriye tek yol **koordinat** kalıyor.

```json
{ "Tip": "Tikla", "X": 0.125, "Y": 0.300, "Not": "Sol panel - Banka Ekstresi" }
```

**X ve Y piksel değil, orandır (0.0 – 1.0).** Pencerenin sol/üst kenarından
itibaren, pencere genişliğine/yüksekliğine oranlanmış konum:

```
mutlak X = pencere.Sol + pencere.Genislik * X
mutlak Y = pencere.Ust + pencere.Yukseklik * Y
```

Piksel yerine oran kullanılmasının sebebi: ekran çözünürlüğü ya da pencere boyu
değişince piksel kayar, oran kaymaz. Ofisteki PC ile evdeki PC farklı çözünürlükte
olsa bile aynı JSON çalışır.

| | Anlamı |
|---|---|
| `X: 0.0` / `Y: 0.0` | Pencerenin sol üst köşesi |
| `X: 0.5` / `Y: 0.5` | Tam orta |
| `X: 1.0` / `Y: 1.0` | Sağ alt köşe |

Aralık dışı bir değer (örn. piksel yazmak: `"X": 240`) adımı hataya düşürür —
sessizce ekranın dışına tıklamaz.

**Hedef pencere:** `Deger` boşsa `appsettings.json > Pencereler.AnaEkran`
kullanılır. Başka bir pencereye (modal diyalog gibi) tıklanacaksa başlığı
`Deger`'e yaz. Pencere önce beklenir, öne getirilir ve büyütülür — oranın
anlamlı olması için pencerenin **tam ekran** olması şart.

### Oranı nasıl ölçersin — `--kalibre` (en hızlı yol)

```
PkfRobot.exe --kalibre
```

Saniyede bir fare imlecinin ORKA penceresine göre oranını ekrana yazar:

```
=== KALIBRE MODU ===
Olculen pencere: basligi 'ORKA_' iceren pencere
Fareyi hedefin uzerine getir, asagidaki orani JSON'a yapistir.
Cikmak icin Ctrl+C.

X: 0.043  Y: 0.318   (mutlak: 82, 305)   "X": 0.043, "Y": 0.318
X: 0.417  Y: 0.926   (mutlak: 800, 890)  "X": 0.417, "Y": 0.926
```

Fareyi hedefin üzerine getir, satırın sağındaki `"X": ..., "Y": ...` parçasını
doğrudan görev JSON'una yapıştır. **Ctrl+C** ile çıkılır.

- Hiçbir tuşa basmaz, tıklamaz — sadece okur.
- ORKA penceresi yoksa uyarı yazar ama **çıkmaz**, açılmasını bekler.
- Fare pencerenin dışındaysa satırın sonunda `<< FARE PENCERENIN DISINDA` uyarısı çıkar.
- Ondalık ayraç her zaman **nokta**dır (Türkçe locale'de virgül basıp JSON'u bozmasın diye).

### Alternatif: ekran görüntüsünden ölçme

1. `PkfRobot.exe --probe` → `C:\RobotLog\...\probe-ekran.png` tam ekran görüntü.
2. Görüntüde hedefin pikselini ölç, ekran genişliğine böl.
   Örnek: 1920 genişlikte 240. piksel → `240 / 1920 = 0.125`
3. `gorevler\04-tikla-kalibrasyon.json` içindeki X/Y'yi Notepad ile değiştir, çalıştır.
4. `tiklama-oncesi.png` / `tiklama-sonrasi.png` karşılaştır.

Pencere tam ekran olduğu için ekran görüntüsündeki oran ≈ pencere oranıdır.

> **DryRun tıklamayı engellemez.** `DryRun` sadece `OnayGerekir` (Kaydet)
> adımlarını atlar. `Tikla` her hâlükârda gerçekten tıklar. Önce test firmasında dene.

Her `Tikla` adımında log'a pencerenin ölçüleri ve hesaplanan mutlak nokta yazılır:

```
[ADIM 07] Tikla -> Sol panel - Banka Ekstresi (oran 0,125 x 0,3)
Pencere 'ORKA_0001 ...': sol=0 ust=0 genislik=1920 yukseklik=1080 -> tiklanan nokta: (240, 324)
```

---

## Doğrulanması gereken noktalar

Bu değerler tahmindir, ofiste ölçülmeli:

- [ ] Giriş ekranında şifre alanı zaten odaklı mı, yoksa Tab gerekiyor mu?
- [ ] Modül ekranında **wrap** var mı? (son tile'dan sağa basınca başa dönüyor mu)
- [ ] Sağ ok sayısı `7` — başka firmada yetki farklıysa değişir
- [x] "Transfere Başla" için `ALT+T` — **çalışmıyor**, klavye geçmiyor. `Tikla` kullan.
- [ ] Sol panel ve grid satırı için X/Y oranları (`--kalibre` ile ölç)
- [ ] Firma şifresi popup'ı ekrandayken `--probe` sonrası `BeklePencere: "Giriniz"` bulunuyor mu
      (alt pencere araması bunun için eklendi, gerçek ORKA'da doğrulanmalı)
- [ ] `OtomatikOneGetir`: ORKA arkadayken bir görev başlat — log'da
      `odak ORKA'da degildi -> ... one getiriliyor` satırı çıkmalı ve tuşlar ORKA'ya gitmeli
- [ ] Excel dosya seçim diyaloğu açıkken robot ondan odağı ÇALMAMALI
      (aynı process olduğu için çalmaması lazım, ofiste teyit et)
- [ ] "Kaydet" için `ALT+K` çalışıyor mu?
- [ ] Hesap Planı'nda tam kod mu, kısa kod mu (`VAK-01`) tek sonuç veriyor?

---

## Bilinen kısıtlar

**Ekran kilitliyken çalışmaz.** UI Automation kilitli oturumda çalışmıyor.
Robot çalışacak PC'de oturum açık kalmalı.

**Mükerrer kayıt riski.** Bu sürümde kontrol yok. Canlı moda geçmeden önce
DijitalMasraf tarafında "bu dosya bu firmaya bu dönemde aktarıldı mı" kontrolü eklenmeli.

**Tuş sayıları yetkiye bağlı.** Kapalı modüller odak sırasını değiştirir.
Firma bazlı ayarlanabilir olmalı.

---

## Sorun çıkarsa

`C:\RobotLog\<tarih>_<gorev>\` klasörünü zip'le. İçinde:
- `log.txt` — her adım, ne beklendi, ne bulundu
- `adim-NN-*.png` — her adımın ekran görüntüsü

Bu klasör hangi adımda ne olduğunu görmek için yeterli olmalı.
