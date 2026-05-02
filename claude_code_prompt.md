# Claude Code İçin Prompt: Firma Kontrol Sayfası (Radzen Blazor)

## Proje Bağlamı

Türk muhasebe pratiğine yönelik bir **Kurumlar Vergisi Hazırlık Uygulaması**'nın frontend'ini yapıyoruz. Şu anda **sadece UI/mock data** ile çalışacağız, backend yok. Tasarım netleştiğinde backend bağlanacak.

**Teknoloji:**
- Radzen Blazor (https://blazor.radzen.com/)
- .NET tarafı sen seç (Blazor Server / WebAssembly / Web App fark etmez, en uygun olanı kullan)
- Mock data: in-memory C# listeleri/recordlar olarak

## Genel Akış

```
┌─────────────────────────────────────────────┐
│ Sol Menü (Sidebar)                          │
│  • Firma Kontrol  ← şimdilik sadece bu      │
│                                             │
│  Ana İçerik:                                │
│  ┌────────────────────────────────────┐     │
│  │ FIRMA CAROUSEL (3-4 firma kartı)   │     │
│  │  ◀  [Firma Kartı]  ▶               │     │
│  │  ●●○○ (sayfalama noktaları)        │     │
│  │       [GİRİŞ butonu]               │     │
│  └────────────────────────────────────┘     │
│                                             │
│  Giriş'e tıklayınca ↓                       │
│                                             │
│  ┌────────────────────────────────────┐     │
│  │ TAB BAR                            │     │
│  │ [Firma Kontrol] (şu an tek tab)    │     │
│  │                                    │     │
│  │ Kategori bazlı checklist:          │     │
│  │  ▼ Dönen Varlıklar (11)            │     │
│  │  ▼ Duran Varlık (3)                │     │
│  │  ▼ Kısa V.Y (4)                    │     │
│  │  ▼ Bilanço İşlemi (6)              │     │
│  │  ▼ Gelir Tablosu (3)               │     │
│  │  ▼ Beyanname Sonuç (16)            │     │
│  │  ▼ Ticaret Sicil (3)               │     │
│  └────────────────────────────────────┘     │
└─────────────────────────────────────────────┘
```

## Sayfa 1: Firma Seçim (Carousel)

**Komponent:** `RadzenCarousel`

**Her firma kartında olacaklar:**
- Firma adı (büyük, kalın)
- Vergi no
- Sektör
- Dönem (örn: "2025 Hesap Dönemi")
- Logo placeholder veya avatar
- Küçük bir özet bilgi (toplam kontrol / tamamlanan)
- **GİRİŞ** butonu (RadzenButton, mavi, alt kısımda)

**Mock 3 firma:**
```csharp
1. Furkan Mühendislik A.Ş.    | VKN: 1234567890 | İnşaat
2. Derya Gıda Ltd. Şti.       | VKN: 9876543210 | Gıda
3. Altuner Otomotiv A.Ş.      | VKN: 5556667778 | Otomotiv
4. Yayla Kuruyemiş Ltd. Şti.  | VKN: 1112223334 | Perakende
```

**Carousel davranışı:**
- Sol/sağ ok ile gezinme
- Alt kısımda nokta indikatörler (●●○○)
- Her seferinde tek kart görünür (resimde olduğu gibi)
- Otomatik kayma KAPALI olsun (kullanıcı kontrolü)

## Sayfa 2: Firma Kontrol Tab Sayfası

**Üst Bar:**
- "← Firma Seçim'e Dön" butonu (sol üst)
- Seçili firma adı (orta, başlık)
- Tab bar (`RadzenTabs`)

**Tek Tab: "Firma Kontrol"**

**İçerik:** 7 kategori, her biri **collapsible/açılır panel** (`RadzenPanel` veya `RadzenFieldset`).

Her kategorinin başlığında:
- Kategori adı
- Tamamlanma sayacı: `(3/11 kontrol edildi)`
- Sorun varsa kırmızı badge: `(2 sorun)`

### Her Kontrol Maddesi Şu Yapıda Olacak

```
┌──────────────────────────────────────────────────────┐
│ ☐ [Soru metni buraya gelecek]                        │
│   Durum: [Bekliyor ▼] [Kontrol Edildi / Sorun Var]   │
│   Açıklama: [textarea - not girebilir]                │
└──────────────────────────────────────────────────────┘
```

**Davranışlar:**

1. **Tick atılınca (☑):**
   - Soru metninin üstü çizilir (`text-decoration: line-through`)
   - Renk grileşir
   - Kategori sayacı +1 olur

2. **Durum dropdown'ı 3 değer alır:**
   - `Bekliyor` (default - nötr renk)
   - `Kontrol Edildi` (yeşil arka plan)
   - `Sorun Var` (**kırmızı arka plan, dikkat çekici** — kart'ın tamamı hafif kırmızıya boyanır)

3. **Açıklama alanı (RadzenTextArea):**
   - Her zaman görünür (collapse içinde)
   - Placeholder: "Not / açıklama girin..."
   - Özellikle "Sorun Var" durumunda doldurulması teşvik edilir

4. **"Sorun Var" durumu:**
   - Kontrol kartının arka planı `#FEE2E2` veya `#FEF2F2` (hafif kırmızı tint)
   - Sol kenarında 4px kalınlığında kırmızı border
   - Kategori başlığında kırmızı sayaç badge

## Mock Data — Tüm Kontrol Soruları

Aşağıdaki listeyi **olduğu gibi** mock data olarak kullan. Her soruya unique ID ver.

```csharp
public record ControlItem(
    int Id,
    string Category,      // "Dönen Varlıklar", "Duran Varlık", vb.
    string Question,      // Soru metni
    bool IsChecked,       // Tick durumu
    ControlStatus Status, // enum: Pending, Verified, HasIssue
    string? Note          // Açıklama
);

public enum ControlStatus
{
    Pending,    // Bekliyor
    Verified,   // Kontrol Edildi
    HasIssue    // Sorun Var
}
```

### Kategori 1: Dönen Varlıklar (11 madde)

1. Dövizli kasa bankası çek cari hesap vb var mı, değerlemesi yapılmış mı? Kasa efektif döviz alış, diğer hesaplar döviz alış kuru ile değerle.
2. Kasa sorunu var mı? (Kasanın olağandan fazla bakiye vermesi) Dönem içinde kasanın eksiye düştüğü günler var mı? Örnek kasa sayfasına bak.
3. 30.000 TL üzeri nakit ödeme var mı, varsa ilişkisi nedir? Cari hesap ise detayı kontrol edilmeli.
4. 101-103 Hesapların devri vadeye göre uygun mu? 31.12 hafta sonuna denk geliyor ise, çeklerin devri Ocak ve sonraki aylara ait olacak şekilde olmalıdır.
5. Banka bakiyelerin 31.12 tarihindeki bakiyesini vermesi gerekir. POS bakiyesi ise banka ile anlaşma bakiyesine uygun olmalı. Örneğin ertesi gün tahsil anlaşması var ise hafta tatil gününe göre 30.12 ve 31.12 bakiyesi devir olmalı. 30 günlük tahsilat anlaşması var ise 1 Aralık - 31 Aralık tarihleri arası tahsilat bakiyesinin kalması gerekir.
6. Verilen çekler hesabı hazır değerler grubunda eksi karakterli çalışan bir hesaptır. Hazır değerler hesap grubunu eksiye düşürmüyor ise verilen çekler bu grupta kalabilir. Eksiye düşürmesi halinde 321 Borç Senetleri Hesabına alınmalı. İhtiyari olarak her zaman 321 Hesaba alınabilir; bu durumda 101-Alınan çeklerin 121 Alacak Senetleri Hesabına alınması gerekir.
7. Yeni yıla devir eden stok miktarı/stok tutarı firmanın mutlaka bilgisi olmalı. Devren stok tutarı mutabakat/bilgi dahilinde devir etmeli.
8. Geçici vergi beyanında atlanmış ve geçici vergi sonrası şüpheli duruma düşmüş alacak var mı, karşılık ayrılması gereken alacak var mı?
9. Dönem sonu itibari ile 120-340 / 320-159 hesap kontrolleri yapılmalı. Finansman gider kısıtlamasına tabi olmasa bile rakamların neden avans hesabında olması gerektiği sorgulanmalı ve ona göre avans hesapları alınmalı.
10. Dönem sonu itibari ile 180 hesap kalmamalı, gelecek yıla ait giderler 280 hesapta takip edilmeli. Açılış fişinden sonra 280 hesaplar 180 hesaplara taşınmalı.
11. Devreden KDV mutlaka Aralık beyanı ile kontrol edilmeli. Geçici vergi beyanından sonra düzeltme verilmiş olabilir. Devir var ise 190 hesap ile son KDV beyanı check edilmeli.

### Kategori 2: Duran Varlık (3 madde)

1. Kredi ile alınmış varlık var mı, ilk yıl faiz ve kur farkları maliyet yazılmış mı, kontrol edilmeli. Araç, bina, arsa, makine alımlarında ilk yıl (31.12'ye kadar olan süre) oluşan bu farklar varlıkların maliyetine yazılır.
2. 25'li grup ile demirbaş tablosu toplamı 257+268 hesap toplamı demirbaş tablosu eşit mi kontrol edilmeli. Şirket dönem içinde bir adres değişikliği yaptıysa 264-Özel maliyet hesabının bakiyesi ve ona ilişkin 268-Amortisman hesapları kontrol edilerek direkt olarak gider yazılmalıdır.
3. Varlık satışı nedeni ile 549-Özel Fonların durumu kontrol edilmeli. Dönem içinde fona konu varlık varsa amortisman mahsubu ve üç yıl hesabı kontrol edilmelidir.

### Kategori 3: Kısa V.Y (Kısa Vadeli Yabancı Kaynaklar) (4 madde)

1. Örnek ortaklar cari hesabına bakılabilir. Ortak hesabı şirkete borçlu ise kar payı ve adat faizi hesaplanma durumu kontrol edilmelidir.
2. Aralık ayına ait KDV, Muhtasar, GEKAP beyanları 360 hesaplar ile uyumlu mu, ödenmeyen vergiler 368 hesaba alınmış mı kontrol edilmeli.
3. 361 bakiyesi Aralık bildirgesi ile uyumlu mu kontrol edilmeli.
4. Cari hesap, kredi vb hesapların kur değerlemesi yapılmış mı, varsa finansman gider hesaplaması kapsamında değerlendirilmiş mi?

### Kategori 4: Bilanço İşlemi (6 madde)

1. Geçici vergi beyanından sonra gelecek yıl için dava açılacak firma var ise kurumları yaparken gelecek yıla doğru devriden olunmalı. Örneğin x firmasının 2025 yılından 2026 yılına devir eden bakiyesi olası alacak davası için uygun mu kontrol edilmelidir.
2. Şirket bir grup firması ise grup firmaları cari hesapları mutabakatı yapılmalı.
3. Genel olarak cari hesap kontrolleri yapılmış mı, özellikle PKF Muhasebe ile mutabakat yapılmalı.
4. Genel mizan kontrolleri neticesinde şirketin sermaye artırımına ihtiyaç var mı?
5. İlişkili kişiler ile transfer fiyatlandırmasına konu işlem var mı, ilişkili kişilerin kim olduğu Transfer Fiyatlandırması sayfasında mevcuttur.
6. Şirket bu yıl mı açılmış, sermaye taahhüt kayıtları var mı, sermaye hesapları doğru mu, ortakları doğru açılmış mı?

### Kategori 5: Gelir Tablosu (3 madde)

1. Faiz ve fon gelirlerine ilişkin banka yazıları temin edilmeli, kayıtlar ile uyumu 642-645-193 hesaplar ile kontrol edilmeli.
2. 12 Aylık KDV beyanları toplamı gelir tablosu brüt satışlarına eşit mi, eşit değil ise farklar açıklanabilir mi?
3. Kredi faizleri ve banka mevduat faizleri: Taksitli kredilerde faiz giderleri, ödeme tarihine göre ilgili döneme tahakkuk ettirilmelidir. Örneğin ödeme tarihi 10 Ocak ise, 31.12 kapanışı nedeniyle faiz tutarının 20 günlük kısmı içinde bulunulan döneme aittir. Bu durumda 20 günlük faiz için 780 hesaba borç, 381 hesaba alacak kaydı yapılır. Faiz tutarı 50.000 TL ise hesaplama: 50.000 / 30 x 20 = 33.333,33 TL. Aynı uygulama faiz gelirleri için de geçerlidir.

### Kategori 6: Beyanname Sonuç (16 madde)

1. Ödenmeyen SGK prim var mı, gider hesaplarından çıkarılıp KKEG yapılmış mı, daha önce KKEG yapılıp bu döneme indirim konusu yapılacak SGK ödemesi var mı?
2. Ödenen/ödenmeyen geçici vergilerin durumu kontrol edilmeli. Ödenmeyen geçici vergiler kurumlar beyanında indirim konusu yapılmamalı.
3. Kurumlar vergisi iadesi mi çıkıyor, iade çıkıyor ise GEKSİS raporuna uygun kontroller yapılmalı (Brüt satış kontrolleri, ortaklar adat kontrolleri).
4. Verilen geçici vergi beyanı sonrası kurumlar vergisi ödemesi çıkıyor mu, %10'luk matrah artışı söz konusu mu?
5. Kar zarar değişiyor mu, değişimin nedeni kontrol edilmeli.
6. 4. Dönem geçiciye göre KKEG değişiyor mu, değişiyor ise nedeni kontrol edilmeli.
7. Dönem net karı veya zararı gelir tablosunda ve bilançoda eşit mi? Kar çıkmış ise ödenen vergi gelir tablosundan kardan düşülmüş mü? (K-DÖNEM KARI VERGİ VE DİĞER YASAL YÜKÜMLÜLÜK KARŞILIKLARI (-))
8. 370 hesap ile ödenen/ödenecek vergi eşit mi, peşin ödenen vergiler (193 HESAP - 371 eşitlenmiş mi?), 370-371 farkı son dönem geçici kadar mı?
9. Muhasebe uygulama tebliği uyarınca firmanın net satışları ve aktif toplamı ek mali tablo doldurulmasını zorunlu kılıyor mu? (AKTİF TOP: 240.560.700 TL / NET SATIŞ: 534.574.300 TL)
10. 7326 / 6111 / 7440 SK matrah artırım kayıtları kontrolü.
11. 7326 / 6111 / 7440 SK işleminden kaynaklanan 689 hesapta KKEG var mı?
12. İstisna işlemi var mı, istisna işlemi kaynaklı zarar var mı, indirimli kurumlar uygulanmış mı? Arsa, daire satışı kaynaklı fon kaydı yapılmış mı, gerekli kurallara uyulmuş mu, yeminli raporuna ihtiyaç var mı?
13. Bilanço ve Gelir Tablosu dipnotları doldurulmuş mu?
14. Yabancı para pozisyonu dolduruldu mu?
15. Örtülü sermaye kontrolü yapıldı mı, ortaklara yürütülen adat var mı, beyannamenin arka sayfasına yazılmış mı? Transfer fiyatlandırmasına konu işlem beyannameye yazılmış mı?
16. Beyannameye geriye dönük 5 yıllık geçmiş yıl zararı yazıldı mı? 2025 kurumlar beyanında 24-23-22-21-20 yılları zararı mahsup edilebilir. Matrah artırımı dolayısıyla ilgili yılların zararının tamamı değil %50'si zarar olarak yazılır. Firmanın YMM'si var ise ilgili beyanname bölümünde bu bilgi yazılmış mı? Beyannameye aktarılan önceki yıl bilanço ve gelir tablosu aktarımı doğru mu?

### Kategori 7: Ticaret Sicil (3 madde)

1. Şirkette hisse devri, sermaye artırımı yapılmış mı, yapılan bu işlemler şirket kayıtlarına yansımış mı, kayıtlar ve gerçek faydalanıcı bildirimi güncellenmeli, kontrol edilmeli.
2. Şirketin adres değişikliği var mı, var ise kurumlar vergisi beyanı hangi vergi dairesine veriliyor kontrol edilmeli. Şube veya depo açılışı var mı, beyana yazılmış mı?
3. Nakit sermaye artışı yapılmış mı, ne tutarda yapılmış, nakit sermaye artış indirim hakkı var mı, gerekli dilekçe ve banka dekontları hazırlanıp vergi dairesine sunulmuş mu? Sermaye indirimi ilgili tablo hazırlanmış mı?

## Stil ve UX Detayları

**Renk Paleti (Radzen default temayı kullan ama):**
- Ana renk: Mavi (Radzen primary)
- Sorun var: `#DC2626` (kırmızı), arka plan `#FEE2E2`
- Kontrol edildi: `#16A34A` (yeşil), arka plan `#DCFCE7`
- Bekliyor: nötr gri

**Animasyonlar:**
- Tick atılınca üstü çizme: `transition: all 0.3s ease`
- Sorun var seçilince arka plan rengi yumuşak geçişle değişsin
- Carousel geçişleri Radzen default

**Responsive:**
- Carousel kartı: max 600px width, ortalı
- Kontrol listesi kategorileri tek sütun (geniş ekranda dahi okunabilirlik için)

## State Yönetimi

- **Şu an için** state'i in-memory tut (sayfa yenilenince sıfırlansın, sorun değil)
- Her firma için ayrı ControlItem listesi olmalı (firma değişince kontroller resetlenmeli VEYA firma bazlı ayrı tutulmalı — ayrı tutmak daha mantıklı)
- Backend bağlanınca aynı modeli HTTP üzerinden çekecek şekilde kurgula (servis katmanı oluştur ama şimdilik mock implementation)

## Klasör Yapısı Önerisi

```
/Pages
  /FirmaKontrol
    Index.razor              ← Carousel sayfası
    FirmaDetay.razor         ← Tab'lı detay sayfası
/Components
  /FirmaKontrol
    FirmaCard.razor          ← Carousel'deki tek firma kartı
    KontrolKategori.razor    ← Açılır kategori paneli
    KontrolMaddesi.razor     ← Tek bir checkbox satırı
/Models
  Firma.cs
  ControlItem.cs
  ControlStatus.cs
/Services
  IFirmaService.cs
  MockFirmaService.cs        ← Mock implementation
```

## Kabul Kriterleri

- [ ] Sol menüde "Firma Kontrol" linki görünür
- [ ] Tıklayınca 4 firma kartlı carousel açılır
- [ ] Carousel'de sağ/sol ok ile gezinme çalışır
- [ ] Her kartta "Giriş" butonu var
- [ ] Giriş'e basınca o firmanın detay sayfasına geçer
- [ ] Detay sayfasında 7 kategori collapsible olarak listelenir
- [ ] Toplam 46 kontrol maddesi doğru kategorilere dağılmış
- [ ] Her madde için: tick + durum dropdown (3 seçenek) + açıklama alanı çalışır
- [ ] Tick atılınca soru metni üstü çizilir
- [ ] "Sorun Var" seçilince madde kartı kırmızı tint alır
- [ ] Kategori başlıklarında "X/Y kontrol edildi" sayacı dinamik güncellenir
- [ ] Sorun varsa kategori başlığında kırmızı badge gözükür
- [ ] "Firma seçimine dön" butonu çalışır

## Notlar

- Backend yok, hiçbir API çağrısı yapma. Tüm data hardcoded mock olarak sunulsun.
- Sayfa yenilenince state sıfırlanması kabul edilebilir (persistence yok).
- Kod temiz, okunaklı, küçük komponentlere bölünmüş olsun. Sonra başka tablar ekleyeceğiz.
- Dosyayı çalışır halde teslim et, derleme hatası olmasın.
- Tasarım netleştikten sonra başka tab'lar (Mizan, Bilanço, Gelir Tablosu vb.) eklenecek — kodu buna hazır kuracak şekilde yaz (tab eklemek kolay olsun).
