# Örnek Dosyalar

Bu klasör **KDV Beyannamesi modülünün** parser ve XML generator geliştirilmesi için referans dosyaları içerir.

Geliştirme sırasında bu dosyaları örnek alarak:
- Excel parser'lar (mizan, yevmiye) bu yapılara göre kurulacak
- BDP XML üretici bu şablona göre üretim yapacak

---

## Dosyalar

### 📊 Ornek_KDV_Mizan.xlsx
Bir firmaya ait **dönem mizanı**. KDV beyannamesi için kritik hesaplar bu dosyadan çekilecek.

**Parser'ın bulması gereken hesaplar:**
| Hesap Kodu | Açıklama | XML'de Karşılığı |
|---|---|---|
| **391** | Hesaplanan KDV | `hesaplananKDV`, `toplamKDV` |
| **191** | İndirilecek KDV | `indirilecekKDVODToplamKDV` |
| **190** | Devreden KDV (önceki dönem) | Hesaplamaya dahil edilecek |
| **600** | Yurtiçi Satışlar | `toplamMatrah`, `teslimVeHizmetleriTeskilEdenBedelAylik` |

**Parser notları:**
- Kolon başlıklarını **isimle bul** (pozisyona güvenme): "Hesap Kodu", "Hesap Adı", "Borç", "Alacak", "Bakiye"
- Farklı firmalarda kolon sırası değişebilir
- Hesap kodu **string** olarak okunabilir (başında sıfır olabilir)

---

### 📋 Ornek_Yevmiye.xlsx
Bir aylık **yevmiye kaydı** örneği. Tab 2'deki karşılaştırma için kullanılacak.

**Parser'ın bulması gereken alanlar:**
- Fatura numarası (açıklama içinde veya ayrı kolonda olabilir)
- Tarih
- Açıklama
- Borç / Alacak tutarları
- Karşı hesap (varsa)

**Parser notları:**
- Fatura numarası bazen açıklama metninin içinde geçer (regex ile çek)
- Kolon başlıklarını isimle bul
- Tarih format'ı Excel formatında olabilir (datetime parse)

---

### 📄 Ornek_KDV_XML.xml
BDP'den export edilmiş **örnek KDV1_44 beyanname XML'i**.

**Önemli detaylar:**
- **Encoding: `ISO-8859-9`** (UTF-8 DEĞİL — Türkçe karakter için kritik)
- **XSD:** `KDV1_44.xsd` (versiyon 44)
- **Dosya adı formatı:** `{vdKodu}_{vergiNo}_KDV1_44_{ddMMyyyy}-{ddMMyyyy}.xml`

**Bu dosya `templates/kdv1_44_template.xml` olarak kopyalanacak** ve XML üretiminde şablon olarak kullanılacak.

**Format kuralları:**
- Tutarlar: `Decimal(2)` formatında (`2.00`, `10.00`, `38.00`)
- Sıfır tutarlar: bazıları `0`, bazıları `0.00` — şablona sadık kal
- Tarih: `ddMMyyyy` (ayraçsız, örn: `01042026`)
- Vergilendirme dönemi: `yyyyMMyyyyMM` (12 hane)
- Tab/girinti şablondaki gibi korunsun

---

## Veri Akışı

```
Mizan Excel  ──►  mizan_entries tablosu  ──┐
                                            ├──►  BDP XML Generator  ──►  KDV1_44 XML
Firma bilgileri (firmalar tablosu)  ───────┘
Düzenleyen bilgileri (app_settings) ───────┘

Yevmiye Excel  ──►  journal_entries tablosu  ──►  Tab 2 karşılaştırma (UI only)
Gelen Faturalar (worker)  ──►  incoming_invoices tablosu  ──►  Tab 2 karşılaştırma (UI only)
```

**ÖNEMLİ:** Gelen faturalar ve yevmiye XML'e DOĞRUDAN girmez. Sadece kontrol/karşılaştırma amaçlıdır. XML'in ana veri kaynağı **mizan**dır.

---

## XML Mapping Özeti

```
<genel>
  vdKodu                → firmalar.vd_kodu
  donem.yil, donem.ay   → Tab 4'teki dönem seçici
  mukellef.*            → firmalar tablosu (yetkili_adi, yetkili_soyadi, vkn, eposta vb.)
  hsv.*                 → mukellef ile aynı (sifat="kendisi")
  duzenleyen.*          → app_settings (PKF/SMMM bilgileri)

<ozel>
  hesaplananKDV         → mizan 391 bakiyesi
  toplamMatrah          → mizan 600 toplamı
  tevkifatUygulanmayanlar → mizan 600 oran bazlı kırılım
  indirilecekKDVODToplamKDV → mizan 191 bakiyesi
  sonrakiDonemeDevredenKDV  → hesaplanır (indirilecek > hesaplanan ise)
  odenmesiGerekenKDV    → hesaplanır (hesaplanan > indirilecek ise)
```

---

## Hesaplama Mantığı

```python
toplam_indirilecek = mizan_191_bakiye + mizan_190_onceki_donem_devreden
fark = mizan_391_bakiye - toplam_indirilecek

if fark > 0:
    odenmesiGerekenKDV = fark
    sonrakiDonemeDevredenKDV = 0
else:
    odenmesiGerekenKDV = 0
    sonrakiDonemeDevredenKDV = abs(fark)
```
