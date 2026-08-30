# Görev: İş Gönderme ve İlerleme (C Adımı)

Bu görevi baştan sona, soru sormadan tamamla. Önce ilgili kodu oku, sonra
değiştir. Belirsizlik çıkarsa kararını `KARARLAR.md`'ye yaz ve devam et.

Mevcut testlerin tamamı aynen geçmeye devam etmeli.

---

## Kapsam

Sunucudan ajana iş gönderme, ajanın işi alması, ilerleme bildirmesi ve
tarayıcıda canlı görünmesi.

**Bu turda ORKA'ya dokunulmayacak.** Ajan işi alacak ve **sahte** olarak
çalıştıracak — adım adım bekleyip ilerleme bildirecek. Gerçek `GridDoldur` ve
ORKA akışı D adımında.

Amaç: iş akışının uçtan uca çalıştığını, ORKA'nın karmaşıklığı devreye girmeden
kanıtlamak.

---

## 1. İş modeli

Ajan listesi bellekte tutuluyor (kaybolması sorun değil). **İşler veritabanında
tutulacak** — sunucu yeniden başlarsa iş kaybolmamalı, geçmiş görülebilmeli.

```
AjanIsi
  Id
  AjanId                (hedef ajan)
  FirmaId               (hangi firmanın işi)
  IsTipi                ("SahteAktarim" bu turda, sonra "OrkayaAktar")
  Yuk                   (JSON — iş parametreleri)
  Durum                 (Bekliyor | Gonderildi | Calisiyor | Tamamlandi | Basarisiz | IptalEdildi | ZamanAsimi)
  IlerlemeYuzde
  IlerlemeMesaji
  ToplamAdim / TamamlananAdim
  OlusturanKullaniciId
  OlusturmaZamani
  GonderimZamani        (nullable)
  BaslamaZamani         (nullable)
  BitisZamani           (nullable)
  HataMesaji            (nullable)
  SonuçOzeti            (JSON, nullable)
```

## 2. Sunucu → ajan

Hub'a ajan tarafında dinlenecek metot ekle: `IsGonder(AjanIsi)`.

Uçlar:

```
POST /api/catalog/agent/is            → iş oluştur ve gönder
GET  /api/catalog/agent/is/{id}       → durum
GET  /api/catalog/agent/isler         → liste (firma ve durum filtreli)
POST /api/catalog/agent/is/{id}/iptal → iptal
```

Kurallar:

- **Hedef ajan bağlı değilse** iş `Bekliyor` kalsın; ajan bağlanınca bekleyen
  işler gönderilsin. Kullanıcıya "ajan bağlı değil, bağlanınca çalışacak" densin.
- **Aynı ajana aynı anda tek iş.** İkinci istek 409 dönsün ve çalışan işi
  belirtsin. Robot tek ORKA penceresiyle çalışıyor, paralel iş anlamsız.
- **Zaman aşımı:** `Calisiyor` durumundaki iş belirli süre ilerleme bildirmezse
  (yapılandırılabilir, varsayılan 15 dakika) `ZamanAsimi` işaretlensin.
- Ajan bağlantısı koparsa çalışan işi `Basarisiz` yap, mesajda sebebi yaz.

## 3. Ajan → sunucu

Hub'a ajanın çağıracağı metotlar:

```
Task IsBasladi(Guid isId)
Task IsIlerleme(Guid isId, int yuzde, string mesaj, int? tamamlananAdim)
Task IsBitti(Guid isId, bool basarili, string? hataMesaji, string? sonucOzetiJson)
```

**Yalnız kendi işini bildirebilsin** — `isId`'nin o ajana ait olduğu sunucuda
doğrulansın. Başka ajanın işini güncelleyememeli.

**Aynı bildirimin tekrarı zararsız olsun** (idempotent): ağ kopup yeniden
bağlandığında ajan son durumu tekrar gönderebilir.

## 4. Ajan tarafı — sahte çalıştırma

`--ajan` modunda `IsGonder` dinlensin.

Bu turda `IsTipi = "SahteAktarim"` için:
- `IsBasladi` bildir
- 10 adım boyunca her adımda 1 saniye bekle, `IsIlerleme` bildir
- `IsBitti(basarili: true)` ile bitir

Gerçek işi çalıştıran kısım **arayüz arkasında** olsun (`IIsCalistirici` gibi) —
D adımında ORKA uygulaması eklenecek, bağlantı katmanı değişmeyecek.

**Ajan kapanırken çalışan iş varsa** `IsBitti(basarili: false, "ajan kapatıldı")`
göndermeye çalışsın.

## 5. Tarayıcı tarafı

Banka Otomasyon > Aktar ekranında, çözülmüş bir ekstre için **"ORKA'ya Aktar"**
düğmesi (bu turda sahte iş gönderir).

- İş gönderilince durum kartı açılsın: ilerleme çubuğu, yüzde, mesaj, geçen süre
- **Durumu yoklamayla güncelle** (2 saniyede bir `GET .../is/{id}`).
  Tarayıcıya SignalR eklemek ikinci bir hub, Blazor WASM istemcisi ve ek nginx
  yapılandırması demek — kazancı bu aşamada yok. Sonra eklenebilir.
- İş bitince sonuç özeti görünsün; başarısızsa hata mesajı
- **İptal** düğmesi olsun
- Ajan bağlı değilse düğme uyarı göstersin ama yine de iş oluşturulabilsin

Ayrıca **Yönetim > Ajanlar** sayfasına son işler listesi eklensin.

---

## Testler

- İş oluşturuluyor, bağlı ajana gönderiliyor
- Ajan bağlı değilken iş `Bekliyor` kalıyor, bağlanınca gönderiliyor
- Aynı ajana ikinci iş 409 dönüyor
- Ajan başka ajanın işini güncelleyemiyor
- Aynı ilerleme bildirimi iki kez gelince durum bozulmuyor
- İlerleme gelmeyince zaman aşımı işaretleniyor
- Ajan bağlantısı kopunca çalışan iş `Basarisiz` oluyor
- İptal edilen iş ajana iptal bildiriyor
- Ajan tarafı: sahte iş 10 adım ilerleyip başarıyla bitiyor

## Kabul kriterleri

1. Derleme temiz, tüm mevcut testler aynen geçiyor
2. Migration üretildi ve uygulandı, `has-pending-model-changes` temiz
3. Yukarıdaki testler yazıldı ve geçiyor
4. **Uçtan uca gerçekten çalıştırılarak** doğrulandı: sunucu + ajan yerelde
   ayağa kaldırıldı, iş gönderildi, ilerleme geldi, durum ucu doğru sonucu
   döndü. Derleme yeterli değil.
5. Gateway ve nginx yapılandırması değişmedi

Sonunda `OZET.md` ve `KARARLAR.md` güncelle.
