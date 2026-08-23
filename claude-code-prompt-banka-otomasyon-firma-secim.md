# Görev: Banka Otomasyon — Firma Seçim Ekranı + Banka Hesapları CRUD

Bu görevi baştan sona, soru sormadan tamamla. Önce ilgili kodu oku, sonra değiştir.
Belirsizlik çıkarsa mevcut kalıbı izle, kararını `KARARLAR.md`'ye yaz ve devam et.

**Eşleştirme mantığına dokunma** — katman sırası, eşikler, benzersiz önek
algoritması, desenler, kurallar aynen kalsın. Bu görev yeniden adlandırma,
gezinme düzeni ve kaybolan bir ekranın geri getirilmesi. Mevcut testlerin
tamamı aynen geçmeye devam etmeli.

---

## 1. Yeniden adlandırma

- Menüde `Banka İşleme` → **`Banka Otomasyon`**
- Alt menüde `İşleme` → **`Aktar`**
- Sayfa başlıkları ve rotalar buna uysun: `/banka-otomasyon/...`
- Eski rotalar yeni rotalara yönlendirsin (kırık yer imi kalmasın)

---

## 2. Firma seçim ekranı

Modüle girince önce firma listesi gelsin.

**Önce `Raporlar` sayfasını incele** ve birebir aynı kalıbı kullan — kendi
tasarımını üretme. O sayfa firma listesini tablo halinde gösterip sağda `GİRİŞ`
düğmesiyle firmaya giriyor; aynısı burada da olsun.

Tablo kolonları:

| Kolon | İçerik |
|---|---|
| Firma | Unvan |
| VKN | Vergi numarası |
| Hesap planı | Kayıt sayısı, yoksa "yüklenmedi" |
| Banka hesabı | Tanımlı hesap sayısı |
| Onay bekleyen | Tüm bankalar toplamı |
| — | `GİRİŞ` düğmesi |

Hesap planı yüklenmemiş firma "kurulum gerekli" olarak işaretlensin ama yine de
girilebilsin.

### Firma içi yapı

`GİRİŞ` sonrası iki sekme:

- **Aktar** — banka sekmeleri, hesap kartları, ekstre yükleme, onay ekranı
  (mevcut hali)
- **Tanımlar** — firma geneli tanımlar + banka sekmesi altında o bankanın
  kuralları (mevcut hali)

Sayfanın üstünde hangi firmada olunduğu sürekli görünsün, firma listesine dönüş
bağlantısı olsun.

### Tenant bağlamı gerçekten değişmeli

Seçilen firma, API'ye giden tenant değerini belirlemeli. Sayfa "PKF Aday"
gösterirken istek SMMM tenant'ıyla giderse veri yanlış firmaya yazılır.

**Testle doğrula:** seçili firma Aday iken yapılan hesap planı içe aktarımı
Aday'ın kayıtlarına yazılıyor mu.

Üstteki genel FİRMA DEĞİŞTİR bağlamıyla çelişki olursa sayfadaki seçim kazansın
ve kullanıcıya uyarı gösterilsin. Seçim oturum boyunca hatırlansın, her sekme
değişiminde tekrar sorulmasın.

**Yükleme ve içe aktarım onaylarında firma adı yazsın** — "PKF Aday için 287
satır yüklenecek" gibi. Yanlış firmaya veri girmeye karşı son savunma.

---

## 3. Banka hesapları CRUD'u geri gelsin

Kapsül düzenlemesinde kayboldu. Şu an "Bu bankanın kuralları → Ayrıştırıcı
ayarları" bölümü hesapları listeliyor ama **yalnız** ayrıştırıcı seçimi ve katman
bayrakları düzenlenebiliyor.

Düzenlenemez durumda olanlar: banka adı, hesap adı, ORKA hesap kodu, hesap tipi,
para birimi, IBAN, eşleştirme anahtarları. Kaybolan düğmeler: **Yeni hesap**,
**Toplu içe aktar**, **Örnek şablon indir**.

### Yeri

Tam CRUD **Tanımlar** sayfasına, hesap planının altına dönsün. Gerekçe: banka
hesabı tanımı bankaya değil firmaya ait bir kayıttır; ayrıca yeni bir banka
eklerken henüz o bankanın sekmesi yoktur, kapsülün içinden erişilemez.

"Bu bankanın kuralları → Ayrıştırıcı ayarları" bölümü kalsın (ayrıştırıcı ve
katman bayrakları için pratik), ama her satırda tam düzenlemeye götüren bir
bağlantı olsun.

### Somut ihtiyaç

Banka adları tutarsız girilmiş: hem `İş Bankası` hem `İŞ BANKASI`, hem `Ziraat`
hem `Ziraat Bankası` var. Bu yüzden sekme sayısı 9 çıkıyor, oysa 8 banka var.

Bu sadece görüntü sorunu değil: **"aynı banka önceliği" kuralı `BankaAdi`
üzerinden çalışıyor.** Aynı bankanın hesapları farklı yazımlarda olunca sistem
onları ayrı bankalar sanıyor ve bankalar arası eşleştirme bozuluyor.
Kullanıcının bunu düzeltebilmesi gerekiyor.

### Tutarsızlığı baştan önle

`BankaAdi` alanına yazarken **mevcut banka adlarından öneri göster**
(autocomplete). Kullanıcı listede olmayan yeni bir yazım girerse uyarı çıksın:
"Bu ad mevcut hiçbir hesapla eşleşmiyor, yeni bir banka sekmesi açılacak."

---

## Kabul kriterleri

1. Derleme temiz, tüm mevcut testler aynen geçiyor
2. Migration gerekiyorsa üretildi ve uygulandı, `has-pending-model-changes` temiz
3. Menü `Banka Otomasyon` → `Aktar` / `Tanımlar`; eski rotalar yönlendiriyor
4. Firma seçim ekranı `Raporlar` kalıbında; iki firma da listeleniyor, sayaçlar
   doğru
5. Seçilen firma tenant bağlamını gerçekten değiştiriyor (test yazıldı)
6. Banka hesapları tam CRUD Tanımlar'da; yeni hesap, toplu içe aktar, şablon
   indir düğmeleri çalışıyor
7. `BankaAdi` autocomplete çalışıyor ve yeni yazımda uyarı veriyor
8. Ayrıştırıcı ayarları bölümünden tam düzenlemeye geçiş bağlantısı var
9. Yükleme ve içe aktarım onaylarında firma adı görünüyor

Sonunda `OZET.md` ve `KARARLAR.md` güncelle.
