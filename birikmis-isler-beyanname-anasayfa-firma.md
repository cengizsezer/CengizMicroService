# Birikmiş İşler — Beyannameler, Anasayfa, Firma Bilgileri, Tema

Bu dosya dört ayrı iş içeriyor. **Hepsini birden çalıştırma** — her bölümü ayrı
tur olarak ver, sırayla. Her turda mevcut testlerin tamamı aynen geçmeye devam
etmeli, eşleştirme ve hesaplama mantığına dokunulmamalı.

---

# 1. Beyannameler → Takip + Özet

Şu an `Beyannameler` ve `Beyanname Takip` ayrı menü satırları. Bunları tek üst
sayfada topla:

```
Beyannameler
  ├── Takip      (mevcut Beyanname Takip ekranı, değişmeden)
  └── Özet       (yeni)
```

Hesaplamalar sayfasındaki `HesaplamaSekmesi` + `DynamicComponent` kalıbını
izle — yeni sekme eklemek bir bileşen ve bir kayıt satırı olsun.

## Özet sekmesi — firma / beyanname matrisi

Satırlar firmalar, kolonlar beyanname türleri, kesişimde durum. Gerçek bir örnek
(kullanıcının Excel'de elle tuttuğu hâli):

| Sıra | Firma Adı | Vergi Kimlik No | KDV (1 No.lu) `0015` | KDV Tevkifat (2 No.lu) `4017` | Gelir Vergisi Stopajı `0003` | Kurumlar Vergisi `0010` | SGK Primi `4101` | Toplam |
|---|---|---|---|---|---|---|---|---|
| 1 | ALPHA AHŞAP | 7721471008 | ✓ | | | | | 1 |
| 3 | CİTADEL GAYRİMENKUL | 7280624888 | ✓ | ✓ | ✓ | | ✓ | 4 |

Kurallar:

- Beyanname türü kolonları **sabit yazılmasın**, mevcut beyanname türü
  tanımlarından türetilsin. Kolon başlığında tür adı ve altında vergi kodu.
- Dönem seçimi (yıl + ay) üstte; matris o döneme göre dolsun.
- **Her hücre tıklanabilir** — tıklanınca o firmanın o beyannamesinin detayına
  gidilsin.
- Hücre durumu görsel olarak ayrışsın: beyanname yok / hazırlandı / onaylandı /
  ödendi.
- Satır sonunda firma bazlı toplam, sütun sonunda tür bazlı toplam.

## PDF ekleme ve görüntüleme

Her beyanname kaydına dosya bağlanabilsin:

- **Tahakkuk** (PDF)
- **Beyanname** (PDF)
- **Dekont** (PDF) — yalnız ödendi işaretlenince istensin

Matriste ve takip listesinde her belge için küçük bir PDF ikonu olsun; ikon
belge varsa dolu, yoksa soluk. Tıklanınca **tarayıcı içinde görüntülensin**
(indirme zorunlu olmasın).

Depolamada mevcut altyapıyı kullan — repoda zaten dosya yükleme/saklama var
(`Job`/`JobAttachment` gibi), önce onu incele ve aynı kalıbı izle; yeni bir
mekanizma icat etme. Bulduğunu `KARARLAR.md`'ye yaz.

Dosya boyutu ve tipi doğrulansın (yalnız PDF, makul bir üst sınır).

---

# 2. Anasayfa

Uygulama açılınca doğrudan `Şirket` ekranı geliyor. Bunun yerine bir **Anasayfa**
olsun; giriş sonrası varsayılan rota o olsun.

İçerik — hepsi tıklanabilir, ilgili sayfaya götürsün:

- Bu ay bekleyen beyanname sayısı ve toplam vergi
- Onay bekleyen banka ekstresi satırı (firma bazlı)
- Yaklaşan son ödeme tarihleri
- Hızlı erişim: son kullanılan firmalar

Sayılar mevcut servislerden okunsun, yeni hesaplama yazma. Veri yoksa boş
durum mesajı anlaşılır olsun.

---

# 3. Firma Bilgileri ekranı

`Yönetim > Firmalarım` altında, her firma için bilgi tutulan bir ekran. Amaç:
kullanıcının sık ihtiyaç duyduğu bilgilere anında erişmesi.

Bölümler:

**Sicil bilgileri** — unvan, VKN, vergi dairesi, ticaret sicil no, MERSİS no,
kuruluş tarihi, adres, NACE kodu, e-posta, telefon, sermaye

**Ortaklık bilgileri** — ortak adı, TCKN/VKN, pay tutarı, pay oranı, ortaklık
başlangıç tarihi. Birden fazla satır; toplam pay oranı %100 değilse uyarı.

**İmza yetkilileri** — ad, TCKN, görev/unvan, temsil şekli (münferit/müşterek),
yetki başlangıç ve bitiş tarihi. Süresi dolmuş yetkili görsel olarak ayrışsın.

Her bölüm ayrı kaydedilebilsin, hepsi tek formda olmasın. Firma bazlı kapsam
Banka Otomasyon'daki mekanizmayla aynı olsun (`FirmaId`).

Belge eklenebilsin (imza sirküleri, vergi levhası, faaliyet belgesi) — 1.
bölümdeki PDF altyapısıyla aynı.

---

# 4. Sol menü teması

Sol menü şu an beyaz; gözü yoruyor. Koyu temaya çevir — siyaha yakın ama saf
siyah değil, yumuşak bir koyu gri/lacivert.

- Menü metinleri açık renkte, seçili satır belirgin
- Kontrast erişilebilirlik açısından yeterli olsun
- Sağdaki içerik alanı açık kalsın, yalnız sol menü koyulaşsın
- Renk değerleri tek bir yerde (CSS değişkeni) tanımlansın, dağıtık olmasın

---

## Her tur için kabul kriterleri

1. Derleme temiz, tüm mevcut testler aynen geçiyor
2. Migration gerekiyorsa üretildi ve uygulandı
3. Yeni servis/hesaplama varsa birim testi yazıldı
4. Ekranlar tarayıcıda denenmese bile, sunucu tarafı testli

Sonunda `OZET.md` ve `KARARLAR.md` güncelle.
