# Görev: Anasayfa — Firma Bilgi Paneli

Bu görevi baştan sona, soru sormadan tamamla. Önce mevcut anasayfayı ve
`/yonetim/firmalar/{id}/bilgiler` ekranını oku — firma bilgileri tabloları zaten
var, yeniden yazma.

Mevcut testlerin tamamı aynen geçmeye devam etmeli.

---

## Amaç

Uygulama açılınca sorumlu olunan 8-10 firmanın bilgilerine anında erişilen bir
ekran. Kullanıcı belirli bir bilgiyi hızlı bulmak istiyor: "Citadel'in vergi
dairesi ne", "Progroup'ta kim imza atabilir", "şu firmanın MERSİS no".

Mevcut anasayfadaki sayaç kartları (bekleyen beyanname, onay bekleyen ekstre,
yaklaşan ödemeler) **kaldırılmasın** — firma paneli onların üstüne, ana içerik
olarak gelsin. Sayaçlar altta veya yan kolonda daha kompakt dursun.

---

## Düzen — master-detail

Sol tarafta firma listesi, sağda seçili firmanın bilgileri. Kart ızgarası değil:
10 firma × 4 bölüm ekrana sığmaz, sürekli kaydırma gerekir.

### Sol liste

- Firma adı ve VKN (VKN tek satır altta, mono yazı tipi, soluk)
- Seçili firma vurgulu (sol kenarda renkli çizgi + hafif zemin)
- Üstte arama kutusu: firma adı veya VKN ile süzer
- **Uyarı göstergesi:** firmada dikkat gerektiren bir durum varsa satırın sağında
  küçük bir uyarı simgesi. Kullanıcı firmaya tıklamadan sorunu görebilmeli:
  - İmza yetkisinin süresi 60 günden az kalmışsa
  - Ortaklık pay oranları toplamı %100 değilse
  - Zorunlu sicil alanları boşsa

### Sağ panel — dört bölüm, alt alta

**Mükellefiyet:** vergi dairesi, VKN, mükellefiyet türleri, e-fatura/e-defter
durumu, işe başlama tarihi, NACE kodu

**Sicil:** ticaret sicil no, MERSİS no, sermaye, adres

**Ortaklık:** tablo — ad, TCKN/VKN (maskeli), pay tutarı, pay oranı. Altta toplam
pay oranı; %100 değilse kırmızı.

**İmza yetkilileri:** tablo — ad, görev/unvan, temsil şekli, yetki bitiş tarihi.
Süresi dolmuş veya dolmak üzere olan satır sarı zeminde, kalan gün sayısıyla.

**Belgeler:** en altta, mevcut PDF altyapısıyla. İmza sirküleri, vergi levhası,
faaliyet belgesi vb. Var olanlar dolu ikonla, ekleme için kesikli çerçeveli
düğme.

### Ayrıntılar

- TCKN'ler **maskeli** gösterilsin (`1234****901`), tıklanınca açılsın
- Alan boşsa "—" göster, satırı gizleme — eksik olduğu görünsün
- Her bölümün sağ üstünde "Düzenle" bağlantısı, mevcut firma bilgileri ekranına
  götürsün. Bu ekran **okuma odaklı**, düzenleme orada yapılsın.
- Bilgi yoğunluğu önemli: bu bir muhasebeci aracı, boşluktan çok veri görünsün

---

## Veri

Firma bilgileri tabloları zaten var (Firma Bilgileri ekranı turunda eklendi).
Yeni tablo açma.

Eksik alan varsa ekle — özellikle mükellefiyet bölümü için: mükellefiyet
türleri, e-fatura durumu, e-defter durumu, işe başlama tarihi. Bunlar `Firma`
veya firma bilgileri tablosunda yoksa nullable alan olarak eklensin.

Tek bir uçtan tüm firmaların özeti + seçili firmanın detayı gelsin; her firma
için ayrı istek atma.

## Kapsam

Firma bazlı ayrım korunsun ama bu ekran **tüm firmaları** gösteriyor — kullanıcı
`pkfadmin` hepsinden sorumlu. Banka Otomasyon'daki `FirmaId` mekanizmasını
kullan, oturum bağlamı kurma.

---

## Testler

- Firma listesi tüm firmaları döndürüyor
- Uyarı göstergesi doğru hesaplanıyor: süresi dolan yetki, %100 olmayan pay
  toplamı, eksik zorunlu alan
- Arama firma adı ve VKN ile süzüyor
- Firma detayı doğru firmadan geliyor (kapsam izolasyonu)

## Kabul kriterleri

1. Derleme temiz, tüm mevcut testler aynen geçiyor
2. Migration gerekiyorsa üretildi ve uygulandı
3. Yukarıdaki testler yazıldı ve geçiyor
4. Mevcut sayaç kartları kaybolmadı
5. Ekran tek istekle yükleniyor, firma başına ayrı çağrı yok

Sonunda `OZET.md` ve `KARARLAR.md` güncelle.
