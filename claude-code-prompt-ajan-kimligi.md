# Görev: Ajan Kimliği — Uzun Ömürlü Anahtar

Bu görevi baştan sona, soru sormadan tamamla. Önce ilgili kodu oku, sonra değiştir.
Belirsizlik çıkarsa mevcut kalıbı izle, kararını `KARARLAR.md`'ye yaz ve devam et.

Mevcut testlerin tamamı aynen geçmeye devam etmeli.

---

## Sorun

Kullanıcı token'ı **20 dakika** ömürlü (`nbf`/`exp` farkı 1200 saniye, prod'da
ölçüldü). Ajan (PkfRobot) ofisteki makinede günlerce bağlı kalacak. Kullanıcı
token'ıyla bağlanırsa 20 dakikada bir düşer.

Ayrıca kullanıcı parolasını veya uzun ömürlü kullanıcı token'ını ofisteki
makinede tutmak istemiyoruz — o makine fiziksel olarak erişilebilir bir yerde.

## Çözüm

Ajana özel, kullanıcıdan bağımsız bir kimlik. Sunucuda üretilir, bir kez
gösterilir, ajan saklar. İptal edilebilir.

---

## 1. Ajan kaydı (IdentityService)

Yeni tablo — repodaki mevcut kalıba uygun bir yere:

```
Ajan
  Id
  Ad                    (kullanıcının verdiği ad: "Ofis Banka PC")
  AnahtarHash           (ham anahtar SAKLANMAZ)
  AnahtarOnEki          (ilk 8 karakter — listede tanımak için)
  OlusturanKullaniciId
  OlusturmaZamani
  SonKullanim           (nullable)
  GecerlilikBitisi      (nullable — süresiz de olabilir)
  Aktif
  IptalZamani           (nullable)
  IptalNedeni           (nullable)
```

**Anahtar üretimi:**
- Kriptografik olarak güvenli rastgele, en az 32 bayt
- Kullanıcıya **bir kez** gösterilir, sonra bir daha gösterilemez
- Veritabanında yalnız hash saklanır — parola gibi. Repoda parola hash'leme
  nasıl yapılıyorsa aynı yaklaşımı kullan (BCrypt / ASP.NET Identity hasher);
  düz SHA256 kullanma.
- Öneki (`pkfr_` gibi) olsun ki bir yere yapıştırıldığında ne olduğu anlaşılsın

## 2. Ajan token'ı alma ucu

```
POST /api/identity/agent/token
Body: { AjanAnahtari }
```

Anahtar geçerliyse **ajana özel bir JWT** üretilir:

- Ömür **8 saat** (kullanıcı token'ından uzun ama süresiz değil)
- `sub` = ajan kimliği, kullanıcı kimliği değil
- Ayırt edici bir claim: `typ: agent` veya `role: agent`
- `SonKullanim` güncellenir

Bu uç `[AllowAnonymous]` olacak (ajanın elinde token yok, anahtar var) ama
**hız sınırı** uygulansın — anahtar denemesine karşı. Başarısız denemeler
loglansın.

## 3. Hub tarafı (CatalogService)

`AgentHub` şu an `[Authorize]` — kullanıcı token'ını da ajan token'ını da kabul
ediyor.

- Hub'a yalnız **ajan token'ıyla** bağlanılabilsin (`typ: agent` claim'i şart)
- Kullanıcı token'ıyla hub bağlantısı reddedilsin — ajan olmayan bir istemcinin
  ajan gibi davranmasını engeller
- `AjanDeposu`'nda saklanan `KullaniciId` yerine artık `AjanId` tutulsun; ajanı
  oluşturan kullanıcı `Ajan` tablosundan okunur

`GET /api/catalog/agent/baglilar` ucu **kullanıcı token'ıyla** çalışmaya devam
etsin — o insan tarafı.

## 4. Yönetim ekranı

Yönetim altında **Ajanlar** sayfası:

- Kayıtlı ajanların listesi: ad, anahtar öneki, oluşturma zamanı, son kullanım,
  bağlı mı (hub'daki durumla birleştir), durum
- **Yeni ajan** — ad girilir, anahtar üretilir ve **bir kez** gösterilir.
  "Bu anahtarı şimdi kopyalayın, bir daha gösterilmeyecek" uyarısı net olsun.
- **İptal** — ajan devre dışı bırakılır, neden yazılır. İptal edilen ajanın
  açık hub bağlantısı da düşürülsün.
- Süresi dolan veya iptal edilen ajanlar görsel olarak ayrışsın

## 5. Test istemcisi

`AgentHubTestClient`'a `--ajan-anahtari` seçeneği ekle: anahtarla token alıp
onunla bağlansın. Mevcut `--token` seçeneği kalsın.

---

## Testler

- Anahtar üretiliyor, hash saklanıyor, ham anahtar veritabanında yok
- Geçerli anahtarla token alınıyor; `typ: agent` claim'i var, ömür 8 saat
- Geçersiz anahtar reddediliyor
- İptal edilmiş anahtarla token alınamıyor
- Süresi dolmuş anahtarla token alınamıyor
- Kullanıcı token'ıyla hub bağlantısı reddediliyor
- Ajan token'ıyla hub bağlantısı kabul ediliyor
- `baglilar` ucu kullanıcı token'ıyla çalışıyor, ajan token'ıyla değil
- Aynı anahtarla iki kez token alınabiliyor (yeniden bağlanma senaryosu)

## Kabul kriterleri

1. Derleme temiz, tüm mevcut testler aynen geçiyor
2. Migration üretildi ve uygulandı, `has-pending-model-changes` temiz
3. Yukarıdaki testler yazıldı ve geçiyor
4. Ham anahtar hiçbir yerde saklanmıyor ve loglanmıyor — bunu açıkça doğrula
5. Gateway ve nginx yapılandırması değişmedi (yeni uç mevcut `/auth/` veya
   `/api/` kurallarından geçiyorsa dokunma; geçmiyorsa `OZET.md`'ye talimat yaz)

Sonunda `OZET.md` ve `KARARLAR.md` güncelle. Ajan anahtarının nasıl üretilip
ajana verileceğini adım adım yaz — kullanıcı bunu elle yapacak.
