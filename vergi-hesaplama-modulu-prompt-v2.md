# Görev: Firma Kontrol — Kurumlar Vergisi Hesaplama sekmesi

## Çalışma şekli — önce bunu oku

Bu görevi **baştan sona kesintisiz** tamamla. Faz aralarında onay bekleme, soru sorma.

- Belirsizlik çıkarsa en makul kararı ver, kodda gerekçesini yaz ve ilerleme
  dosyasına "verilen karar" olarak not et. Durup sorma.
- Her fazın sonunda ilerleme tablosunu güncelle ve devam et.
- **Yalnızca Firma Kontrol modülü ve yeni eklenecek vergi dosyalarına dokun.**
  Muhasebe modülüne, diğer sayfalara, ortak altyapıya dokunma.
- Her faz sonunda derlemenin temiz olduğunu ve testlerin geçtiğini doğrula.
  Kırık bırakma.
- Sonunda tek bir özet yaz: ne yapıldı, hangi kararlar verildi, ne doğrulanamadı.

---

## Bağlam

DijitalMasraf'ın Firma Kontrol modülünde "Vergi Hesaplaması" sekmesi var ama
basit: K.K.E.G, geçmiş yıl zararları, iştirak istisnası, bağış. Bu sekmeyi
kurumlar vergisi beyannamesinin tam yapısına çeviriyoruz.

Kapsam **yalnızca kurumlar vergisi mükellefleri**. Gelir vergisi, basit usul,
işletme hesabı ayrı bir modülde ele alınacak; bu görevde onlara ait hiçbir şey
yapılmayacak. Ancak veri modeli ileride onları taşıyabilecek şekilde kurulacak
(`MukellefiyetTuru` alanı).

Kullanıcı tek kişi (PKF admin). Onay akışı, çok kullanıcılı yetki, versiyonlama
bu fazda yok.

Mevcut Firma Kontrol modülünün konvansiyonlarını takip et; yeni mimari desen
icat etme. Başlamadan önce mevcut modülü incele.

---

## Çekirdek kural 1 — İki ayrı istisna grubu

İstisna ve indirimler iki gruba ayrılır ve aralarında geçmiş yıl zararları durur.
Bu sıralama sonucu doğrudan değiştirir:

```
    Ticari bilanço kârı / zararı (690)
  + Kanunen kabul edilmeyen giderler (KKEG)
  ─────────────────────────────────────────────
  = Kâr ve ilaveler toplamı
  − Zarar olsa dahi indirilecek istisna ve indirimler
  ─────────────────────────────────────────────
  = Kâr / Zarar
  − Geçmiş yıl zararları (yıl yıl, 5 yıllık sınır)
  − Kazancın bulunması halinde indirilecek istisna ve indirimler
  ─────────────────────────────────────────────
  = MATRAH
  × Kurumlar vergisi oranı
  ─────────────────────────────────────────────
  = Hesaplanan kurumlar vergisi
  − Mahsuplar (tevkifat, geçici vergi, yurt dışı vergi)
  ─────────────────────────────────────────────
  = Ödenecek / iade edilecek vergi
```

**Grup 2 (zarar olsa dahi):** matrahı negatife çekebilir, oluşan zarar gelecek
yıllara devreder.

**Grup 3 (kazanç varsa):** matrahı sıfırın altına indiremez. Kalan kısım kural
olarak yanar; istisnası Ar-Ge ve nakdi sermaye indirimidir (devreder).

---

## Çekirdek kural 2 — KKEG'in iki türü

KKEG neden eklenir: gider defterlere yazılmış (770, 760, 689), 690 ticari kârı
azaltmış, ama vergi kanunu o gideri kabul etmiyor. Beyannamede geri eklenir.

Ancak iki farklı davranış var:

**a) Matrahı artıran KKEG (kalıcı fark).** Vergi cezaları, örtülü sermaye faizi,
binek oto kısıtlaması. Geri eklenir, matrahı doğrudan artırır.

**b) İstisnaya ilişkin KKEG (nötr).** İstisna kapsamındaki bir faaliyet içinde
(teknopark, serbest bölge, yurt dışı şube) oluşan KKEG. Ticari kâra eklenir ama
**aynı tutar istisna kazancını da büyütür**, dolayısıyla matraha net etkisi
sıfırdır.

Bu ayrım veri modelinde ve ekranda ayrı ayrı görünmeli:

- `VergiKalemi.IstisnayaIliskinMi BIT` — kalem (b) türündeyse `true`
- `VergiKalemi.BagliIstisnaKalemiId INT NULL` — hangi istisna kalemini büyüteceği
- Hesaplama motoru: `(b)` türü bir kaleme tutar girildiğinde, bağlı istisna
  kaleminin efektif tutarı `girilen istisna + ilişkili KKEG` olarak hesaplanır
- Ekranda `(b)` grubunun altında geri dönüş satırı: "Teknopark istisnası
  18.000,00 → 21.100,00 olarak hesaplanacak"
- İlaveler bölümünün altında iki toplam: **ham ilave toplamı** (beyannameye
  yazılan) ve **matraha etki eden kısım**

---

## Veri modeli

```sql
CREATE TABLE VergiKalemi (
    Id                    INT IDENTITY PRIMARY KEY,
    Kod                   VARCHAR(20)  NOT NULL UNIQUE,
    Ad                    NVARCHAR(200) NOT NULL,
    Grup                  TINYINT NOT NULL,       -- 1=KKEG 2=ZararOlsaDahi 3=KazancVarsa 4=Mahsup
    AltGrup               NVARCHAR(100) NULL,
    KanunMaddesi          NVARCHAR(100) NULL,
    Aciklama              NVARCHAR(1000) NULL,
    Hatirlatma            NVARCHAR(1000) NULL,
    OranBilgisi           NVARCHAR(200) NULL,
    UstSinirTuru          TINYINT NULL,           -- 0=Yok 1=KurumKazanciYuzdesi 2=SabitTutar
    UstSinirDeger         DECIMAL(9,4) NULL,
    DevredebilirMi        BIT NOT NULL DEFAULT 0,
    IstisnayaIliskinMi    BIT NOT NULL DEFAULT 0, -- KKEG türü (b)
    BagliIstisnaKalemiId  INT NULL REFERENCES VergiKalemi(Id),
    AsgariMatrahtanDuser  BIT NOT NULL DEFAULT 0, -- KVK 32/C
    MukellefiyetTuru      TINYINT NOT NULL DEFAULT 2,  -- 1=GV 2=KV 3=İkisi
    SiraNo                SMALLINT NOT NULL,
    SistemKalemi          BIT NOT NULL DEFAULT 1,
    Aktif                 BIT NOT NULL DEFAULT 1
);

CREATE TABLE VergiHesaplama (
    Id                    INT IDENTITY PRIMARY KEY,
    FirmaId               INT NOT NULL,
    DonemYil              SMALLINT NOT NULL,
    TicariKar             DECIMAL(19,2) NOT NULL,
    KvOrani               DECIMAL(5,2) NOT NULL DEFAULT 25.00,
    IndirimliOran         DECIMAL(5,2) NULL,
    IndirimliOranMatrahi  DECIMAL(19,2) NULL,
    AsgariKvHesapla       BIT NOT NULL DEFAULT 1,
    Notlar                NVARCHAR(2000) NULL,
    GuncellemeT           DATETIME2 NOT NULL,
    CONSTRAINT UQ_VergiHesaplama UNIQUE (FirmaId, DonemYil)
);

CREATE TABLE VergiHesaplamaSatir (
    Id              INT IDENTITY PRIMARY KEY,
    HesaplamaId     INT NOT NULL REFERENCES VergiHesaplama(Id) ON DELETE CASCADE,
    VergiKalemiId   INT NOT NULL REFERENCES VergiKalemi(Id),
    Tutar           DECIMAL(19,2) NOT NULL DEFAULT 0,
    OncekiDonem     DECIMAL(19,2) NULL,
    Aciklama        NVARCHAR(500) NULL,
    CONSTRAINT UQ_HesaplamaKalem UNIQUE (HesaplamaId, VergiKalemiId)
);

CREATE TABLE GecmisYilZarari (
    Id            INT IDENTITY PRIMARY KEY,
    HesaplamaId   INT NOT NULL REFERENCES VergiHesaplama(Id) ON DELETE CASCADE,
    ZararYili     SMALLINT NOT NULL,
    ZararTutari   DECIMAL(19,2) NOT NULL,
    MahsupEdilen  DECIMAL(19,2) NOT NULL DEFAULT 0
);
```

Parasal alanların tamamı `decimal`. `double` kullanma.

---

## Kalem listesi (seed)

`SeedFiles/vergi-kalemleri-kv.json`. Aşağıdaki kalemler `Aciklama` ve
`Hatirlatma` alanları dolu olarak seed edilecek.

### Grup 1 — KKEG (matrahı artıran)

| Kod | Ad | Kanun | Hatırlatma |
|---|---|---|---|
| KKEG-01 | Ayrılan yedek akçeler | KVK 11/1-a | Her türlü yedek akçe KKEG'dir |
| KKEG-02 | Kurumlar vergisi ve gelir vergisi | KVK 11/1-d | 691 hesabındaki karşılık dahil |
| KKEG-03 | Vergi cezaları, gecikme faizi ve zamları | KVK 11/1-d | Gecikme faizi ve gecikme zammı da KKEG |
| KKEG-04 | Örtülü sermaye üzerinden ödenen faiz ve kur farkı | KVK 11/1-b, 12 | Ortak borcu özsermayenin 3 katını aşıyor mu? |
| KKEG-05 | Transfer fiyatlandırması yoluyla örtülü kazanç dağıtımı | KVK 11/1-c, 13 | İlişkili kişi işlemlerinde emsale uygunluk |
| KKEG-06 | Finansman gider kısıtlaması | GVK 41/9, KVK 11/1-i | Yabancı kaynak özkaynağı aşıyorsa aşan kısma isabet eden finansman giderinin %10'u |
| KKEG-07 | Binek otomobil amortisman kısıtlaması | GVK 40/7 | Yıllık iktisap bedeli sınırını aşan kısmın amortismanı |
| KKEG-08 | Binek otomobil gider kısıtlaması | GVK 40/5 | Giderlerin %70'i indirilebilir, %30'u KKEG |
| KKEG-09 | Binek otomobil kiralama kısıtlaması | GVK 40/1 | Aylık kira sınırını aşan kısım |
| KKEG-10 | Esas faaliyet dışı taşıt, tekne, uçak giderleri | KVK 11/1-f | İşletme faaliyetiyle ilgisi yoksa tamamı KKEG |
| KKEG-11 | Basın yoluyla işlenen fiillerden doğan tazminatlar | KVK 11/1-g | |
| KKEG-12 | Alkol ve tütün ürünleri reklam giderleri | KVK 11/1-ğ | |
| KKEG-13 | Ödenmemiş SGK primleri | 5510 md.88 | Beyanname verme tarihine kadar ödenmemişse KKEG |
| KKEG-14 | Şartları taşımayan şüpheli alacak karşılıkları | VUK 323 | Dava veya icra takibi yoksa karşılık ayrılamaz |
| KKEG-15 | Kıdem tazminatı karşılığı | VUK | Ödenmeyen karşılık KKEG; ödendiğinde indirilir |
| KKEG-16 | VUK'a uygun olmayan amortisman ve değerleme farkları | VUK | Faydalı ömür ve yöntem kontrolü |
| KKEG-17 | Bağış ve yardımların indirilemeyen kısmı | KVK 10 | Sınırı aşan bağışlar buraya yazılır |
| KKEG-18 | Kanunen kabul edilmeyen diğer giderler | — | Serbest metin |

### Grup 1b — İstisnaya ilişkin KKEG (nötr)

`IstisnayaIliskinMi = true`, `BagliIstisnaKalemiId` ilgili istisnaya bağlı.

| Kod | Ad | Bağlı istisna | Hatırlatma |
|---|---|---|---|
| KKEGI-01 | Teknopark faaliyetine ilişkin KKEG | IST-17 | Aynı tutar teknopark istisnasını büyütür |
| KKEGI-02 | Serbest bölge faaliyetine ilişkin KKEG | IST-16 | Aynı tutar serbest bölge istisnasını büyütür |
| KKEGI-03 | Yurt dışı şube faaliyetine ilişkin KKEG | IST-09 | |
| KKEGI-04 | Yurt dışı inşaat işlerine ilişkin KKEG | IST-10 | |
| KKEGI-05 | Diğer istisna kazançlara ilişkin KKEG | — | Bağlı istisna kullanıcı tarafından seçilir |

### Grup 2 — Zarar olsa dahi indirilecek istisna ve indirimler

| Kod | Ad | Kanun | Hatırlatma |
|---|---|---|---|
| IST-01 | İştirak kazançları istisnası | KVK 5/1-a | Tam mükellef kurumdan alınan kâr payı |
| IST-02 | Yurt dışı iştirak kazançları istisnası | KVK 5/1-b | En az %50 iştirak, 1 yıl elde tutma, %15 vergi yükü |
| IST-03 | Yurt dışı iştirak hissesi satış kazancı istisnası | KVK 5/1-c | Tam mükellef AŞ; en az 2 yıl aktifte |
| IST-04 | Emisyon primi kazancı istisnası | KVK 5/1-ç | Nominal üzerindeki ihraç bedeli farkı |
| IST-05 | Yatırım fonu ve ortaklığı portföy işletmeciliği kazancı | KVK 5/1-d | |
| IST-06 | İştirak hissesi satış kazancı istisnası (%75) | KVK 5/1-e | En az 2 tam yıl aktifte; kazanç 5 yıl fonda tutulmalı |
| IST-07 | Taşınmaz satış kazancı istisnası (eski iktisaplar) | KVK 5/1-e | 15.07.2023 öncesi iktisap edilenler için; sonrası kaldırıldı |
| IST-08 | Bankalara/TMSF'ye borçlu olanların taşınmaz satış kazancı | KVK 5/1-f | |
| IST-09 | Yurt dışı şube kazançları istisnası | KVK 5/1-g | En az %15 vergi yükü; transfer şartı |
| IST-10 | Yurt dışı inşaat, onarım, montaj ve teknik hizmet kazançları | KVK 5/1-h | Genel sonuç hesaplarına intikal ettirilmiş olmalı |
| IST-11 | Eğitim ve rehabilitasyon merkezi kazanç istisnası | KVK 5/1-ı | 5 hesap dönemi; Bakanlık ruhsatı |
| IST-12 | Risturn istisnası (kooperatifler) | KVK 5/1-i | |
| IST-13 | Sat-kirala-geri al işlemlerinden doğan kazanç | KVK 5/1-j | |
| IST-14 | Varlık kiralama şirketlerine devirden doğan kazanç | KVK 5/1-k | |
| IST-15 | Sınai mülkiyet hakları satış/kiralama kazancı | KVK 5/B | Patent veya faydalı model; değerleme raporu |
| IST-16 | Serbest bölge kazanç istisnası | 3218 gç.3 | Üretim faaliyeti; imalatçı olmayanlarda süre sınırı |
| IST-17 | Teknoloji geliştirme bölgesi kazanç istisnası | 4691 gç.2 | Bölgede üretilen yazılım/Ar-Ge kazancı; girişim fonu yükümlülüğü |
| IST-18 | Türk Uluslararası Gemi Sicili kazanç istisnası | 4490 md.12 | |
| IST-19 | Yatırım indirimi istisnası | GVK gç.61, 69 | Devreden yatırım indirimi; endeksleme |
| IST-20 | Diğer indirim ve istisnalar | — | Serbest metin |

### Grup 3 — Kazancın bulunması halinde indirilecek

| Kod | Ad | Kanun | Üst sınır | Hatırlatma |
|---|---|---|---|---|
| IND-01 | Ar-Ge indirimi | 5746 md.3 | — | Harcamanın %100'ü; devreder |
| IND-02 | Tasarım indirimi | 5746 md.3 | — | Tasarım merkezi belgesi |
| IND-03 | Sponsorluk — amatör spor | KVK 10/1-b | Kurum kazancının %5'i | Amatör dalda %100 |
| IND-04 | Sponsorluk — profesyonel spor | KVK 10/1-b | Kurum kazancının %5'i | Profesyonel dalda %50 |
| IND-05 | Bağış ve yardımlar (genel) | KVK 10/1-c | Kurum kazancının %5'i | Makbuz şartı |
| IND-06 | Eğitim ve sağlık tesisi bağışları | KVK 10/1-ç | Sınırsız | Okul, sağlık tesisi, yurt, huzurevi |
| IND-07 | Kültür ve turizm amaçlı bağışlar | KVK 10/1-d | Sınırsız | Bakanlıkça desteklenen projeler |
| IND-08 | Cumhurbaşkanınca başlatılan yardım kampanyaları | KVK 10/1-e | Sınırsız | Afet kampanyaları |
| IND-09 | Kızılay ve Yeşilay'a nakdi bağışlar | KVK 10/1-f | Sınırsız | Yalnızca nakdi; iktisadi işletmeleri hariç |
| IND-10 | Girişim sermayesi fonu | KVK 10/1-g | Kurum kazancının %10'u | Öz sermayenin %20'sini aşamaz |
| IND-11 | Korumalı işyeri indirimi | KVK 10/1-h | — | Engelli istihdamı; ücretin %100'ü |
| IND-12 | Nakdi sermaye artışı faiz indirimi | KVK 10/1-ı | — | Nakdi sermaye artırdınız mı? TCMB ticari kredi faiz oranının %50'si; 5 hesap dönemi; devreder |
| IND-13 | Yurt dışına verilen hizmetlerde indirim | KVK 10/1-ğ | — | Mimarlık, yazılım, çağrı merkezi, sağlık, eğitim; kazancın %80'i; Türkiye'ye transfer şartı |
| IND-14 | Teknogirişim sermaye desteği | 5746 md.3 | — | |
| IND-15 | Bireysel katılım yatırımcısı indirimi | GVK gç.82 | — | Melek yatırımcı lisansı |
| IND-16 | Diğer indirimler | — | — | Serbest metin |

### Grup 4 — Mahsuplar

| Kod | Ad | Kanun | Hatırlatma |
|---|---|---|---|
| MAH-01 | Yurt içinde kesilen vergiler (tevkifat) | KVK 34 | Mevduat faizi, repo, kira stopajı |
| MAH-02 | Yurt dışında ödenen vergiler | KVK 33 | Mahsup sınırı: yurt dışı kazanca isabet eden Türkiye vergisi |
| MAH-03 | Ödenen geçici vergi | KVK 34/3 | Tahakkuk etmiş ve ödenmiş olmalı |
| MAH-04 | İndirimli kurumlar vergisi | KVK 32/A | Yatırım teşvik belgesi kapsamında |

---

## Vergi oranı

Varsayılan %25, ekranda değiştirilebilir:

- Banka, finansal kiralama, faktoring, elektronik ödeme, varlık yönetimi, sigorta
  ve emeklilik şirketleri, sermaye piyasası kurumları → %30
- İhracat kazancı → 5 puan indirimli
- Sanayi sicil belgeli üretim faaliyeti kazancı → 1 puan indirimli
- İlk defa halka arz edilen kurumlar → 2 puan indirimli (5 hesap dönemi)

İndirimli oran uygulanacaksa `IndirimliOran` ve `IndirimliOranMatrahi` ayrı
girilir; kalan matraha genel oran uygulanır.

---

## Yurt içi asgari kurumlar vergisi (KVK 32/C)

Paralel hesaplama:

```
Asgari matrah = Ticari kâr + KKEG − (AsgariMatrahtanDuser = true olan istisnalar)
Asgari vergi  = Asgari matrah × %10
Ödenecek      = MAX(normal hesaplanan vergi, asgari vergi)
```

`AsgariMatrahtanDuser` bayrağı seed'de yalnızca ilgili istisnalarda `true`
işaretlenecek. Emin olmadığın kalemlerde `false` bırak ve ilerleme dosyasına
"teyit edilmeli" notu düş.

İlk defa faaliyete başlayan kurumlar 3 hesap dönemi muaftır — ekranda bir
onay kutusuyla kapatılabilsin.

---

## Ekran

Sekme adı **Vergi Hesaplaması**, Gelir Tablosu'nun sağında.

Tek kolon, yukarıdan aşağı beyanname sırası. Her bölüm katlanabilir.

### Bölüm sırası ve görsel ayrım

1. **Ticari bilanço kârı (690)** — gelir tablosundan gelir, düzenlenemez, gri kutu
2. **İlaveler (+)** — iki alt kutu:
   - *Matrahı artıran KKEG* — nötr çerçeve, kendi toplamı, altyazı "Kalıcı fark ·
     vergi yükünü doğrudan artırır"
   - *İstisnaya ilişkin KKEG* — yeşil çerçeve, kendi toplamı, altyazı "Aynı tutar
     istisna kazancına eklenir · matraha net etkisi yok". Tutar girilen her kalemin
     altında geri dönüş satırı: "Teknopark istisnası 18.000,00 → 21.100,00"
   - Bölüm altında iki toplam: **ham ilave toplamı** ve **matraha etki eden kısım**
3. **Kâr ve ilaveler toplamı** — ara toplam çizgisi
4. **Zarar olsa dahi indirilecek istisnalar (−)** — mavi çerçeve, başlık altyazısı
   "Matrahı negatife çekebilir, oluşan zarar devreder"
5. **Kâr / Zarar** — ara toplam
6. **Geçmiş yıl zararları (−)** — yıl yıl, devreden tutar gösterimi, 5 yıllık sınır
7. **Kazanç varsa indirilecek indirimler (−)** — sarı çerçeve, başlık altyazısı
   "Matrahı sıfırın altına indiremez"
8. **Matrah** — vurgulu ara toplam
9. **Vergi hesabı** — iki kolon yan yana: Normal / Asgari 32/C, uygulanan yeşil
   vurgulu, altta "yüksek olan uygulanır" açıklaması
10. **Mahsuplar (−)**
11. **Ödenecek kurumlar vergisi** — vurgulu sonuç

### Davranış

1. 690 gelir tablosundan gelir, düzenlenemez, gelir tablosu değişince güncellenir
2. Kanun maddesi kalem adının yanında monospace ve soluk
3. Hatırlatma metni: tutar girilmiş kalemlerde her zaman görünür, boş kalemlerde
   bilgi ikonuna tıklayınca açılır
4. Üst sınırlı kalemlerde canlı uyarı: sınır aşılırsa satır kırmızıya döner,
   aşan tutar hesaplanır, "KKEG-17'ye taşı" butonu çıkar
5. Grup 3 toplamı kalan kazancı aşarsa uyarı; indirim kalan kazanç kadar uygulanır;
   devredebilir kalemlerde devreden tutar ayrıca gösterilir
6. Sıfır tutarlı kalemler varsayılan gizli; "tüm kalemleri göster" anahtarı
7. Kalem arama kutusu: kod, ad, kanun maddesi
8. Her satıra kullanıcı notu girilebilir
9. Otomatik kayıt yok; kaydedilmemiş değişiklikle sayfadan çıkarken uyarı
10. Tarih ve tutar biçimi `tr-TR`

### Kalem yönetimi ekranı

`/firma-kontrol/vergi-kalemleri`

- Gruba göre sekmeli liste
- Yeni kalem ekleme, düzenleme, pasife alma
- `SistemKalemi = true` olanlarda kod ve grup kilitli; ad, açıklama, hatırlatma,
  sıra no, üst sınır düzenlenebilir
- Kullanıcının eklediği kalemler tamamen düzenlenebilir, kullanılmamışsa silinebilir
- Sürükle-bırak sıralama

---

## Excel dışa aktarım

Beyanname formatına yakın tek sayfa: kalem kodu, ad, kanun maddesi, tutar,
kullanıcı notu. Ara toplamlar ve matrah satırları vurgulu.

Sunucu tarafında ClosedXML ile gerçek `.xlsx` ucu aç. CSV kullanma.

---

## Kabul kriterleri

- [ ] 690 gelir tablosundan geliyor, düzenlenemiyor
- [ ] İstisnaya ilişkin KKEG girildiğinde bağlı istisna tutarı otomatik büyüyor
- [ ] İstisnaya ilişkin KKEG'in matraha net etkisi sıfır
- [ ] İlaveler bölümünde ham toplam ve matraha etki eden toplam ayrı gösteriliyor
- [ ] Grup 2 indirimleri matrahı negatife çekebiliyor
- [ ] Grup 3 indirimleri matrahı sıfırın altına indiremiyor
- [ ] Geçmiş yıl zararları en eski yıldan başlayarak mahsup ediliyor
- [ ] 5 yıldan eski zarar mahsup edilemiyor, uyarı veriliyor
- [ ] Bağış %5 sınırı aşıldığında uyarı çıkıyor ve aşan tutar hesaplanıyor
- [ ] Devredebilir kalemlerde kullanılmayan tutar ayrı gösteriliyor
- [ ] Asgari kurumlar vergisi paralel hesaplanıyor, yüksek olan uygulanıyor
- [ ] Vergi oranı değiştirilebiliyor, indirimli oran ayrı matrah alabiliyor
- [ ] Kalem ekleme/düzenleme çalışıyor, sistem kalemlerinin kodu kilitli
- [ ] Sıfır tutarlı kalemler gizlenebiliyor
- [ ] Kaydedilmemiş değişiklikle çıkarken uyarı veriliyor
- [ ] Excel çıktısı tüm kalemleri ve ara toplamları içeriyor

---

## Fazlar

Aralarında durmadan, sırayla tamamla. Her fazın sonunda ilerleme tablosunu
güncelle ve devam et.

**Faz 1 — Veri katmanı.** Tablolar, migration, seed dosyası (yukarıdaki tüm
kalemler, açıklama ve hatırlatma metinleri dolu).

**Faz 2 — Hesaplama motoru ve API.** Beyanname sırası, grup kuralları, KKEG
ayrımı ve istisna geri beslemesi, üst sınır kontrolleri, asgari kurumlar vergisi.
Birim testleri — kabul kriterlerinin her maddesi için en az bir test.

**Faz 3 — Vergi Hesaplaması sekmesi.** Ekran, katlanabilir bölümler, hatırlatma
metinleri, canlı uyarılar, geri dönüş satırları.

**Faz 4 — Kalem yönetimi ekranı ve Excel dışa aktarım.**

---

## İlerleme

| Faz | Durum | Not |
|---|---|---|
| 1 Veri katmanı | ✅ | Aşağıya bakınız |
| 2 Hesaplama motoru ve API | ✅ | Aşağıya bakınız |
| 3 Vergi Hesaplaması sekmesi | ✅ | Aşağıya bakınız |
| 4 Kalem yönetimi ve Excel | ✅ | Aşağıya bakınız |

### Faz 1 — Veri katmanı

Oluşturulan dosyalar (hepsi `CatalogService.Api` içinde, Firma Kontrol feature'ı altında):

- `Features/FirmaKontrol/Domain/{VergiEnums,VergiKalemi,VergiHesaplama,VergiHesaplamaSatir,GecmisYilZarari}.cs`
- `Infrastructure/EntityConfigurations/{VergiKalemi,VergiHesaplama,VergiHesaplamaSatir,GecmisYilZarari}EntityTypeConfiguration.cs`
- `Infrastructure/Setup/SeedFiles/vergi-kalemleri-kv.json` — 63 kalem
- `Features/FirmaKontrol/VergiKalemSeed.cs`
- `Migrations/20260802151528_AddVergiHesaplamaTablolari`

Doğrulama: migration gerçek veritabanına uygulandı, seed çalıştı → **63 kalem**
(Grup 1: 23 = 18 KKEG + 5 istisnaya ilişkin, Grup 2: 20, Grup 3: 16, Grup 4: 4).
Açıklama veya hatırlatma alanı boş kalan kalem yok; 4 istisnaya ilişkin KKEG bağlı
istisnasına bağlandı.

Verilen kararlar:

- **Kalem katalogu firmadan bağımsız (tenant'a bağlı değil).** Mevcut Firma Kontrol
  tabloları da `TenantNo` kullanmıyor, `FirmaId` ile çalışıyor; katalog ise tüm firmalar
  için ortak olduğundan hiçbir sahiplik alanı almadı.
- **Eski `FirmaKontrolVergiler` tablosuna dokunulmadı.** Yeni beyanname yapısı ayrı
  tablolarda; eski basit panelin verisi geriye dönük olarak yerinde duruyor.
- **Seed eklemeli ve idempotent:** her açılışta yalnızca eksik kodlar eklenir, mevcut
  kayıtların metinleri ezilmez. Kullanıcı sistem kaleminin adını/açıklamasını
  değiştirebildiği için seed onu geri almamalı.
- **Bağlı istisna JSON'da kodla veriliyor** (`bagliIstisnaKod`), Id seed sırasında çözülüyor;
  böylece Id'ler dosyaya gömülmedi.
- **KKEGI-05 (diğer istisna kazançlara ilişkin KKEG) bağlantısız bırakıldı** — hangi
  istisnayı büyüteceği firmaya göre değişir, kalem yönetimi ekranından seçilir.
  Bağlantısızken motor onu matrahı artıran KKEG gibi işler (hatırlatma metnine yazıldı).

### Faz 2 — Hesaplama motoru ve API

Oluşturulan dosyalar:

- `Features/FirmaKontrol/Services/VergiHesaplamaMotoru.cs` — saf hesaplama (DB bilmez)
- `Features/FirmaKontrol/Services/{IVergiBeyannameService,VergiBeyannameService}.cs`
- `Features/FirmaKontrol/Dtos/VergiBeyannameDtos.cs`
- `Features/FirmaKontrol/Controllers/VergiBeyannameController.cs`
- `CatalogService.UnitTests/FirmaKontrol/{VergiTestKatalogu,VergiHesaplamaMotoruTests,VergiBeyannameServiceTests}.cs`

Uçlar (`api/catalog/firma-kontrol/vergi`):

```
GET    /kalemler[?pasifDahil=true]      GET /kalemler/{id}
POST   /kalemler                        PUT /kalemler/{id}
PATCH  /kalemler/{id}/pasif             DELETE /kalemler/{id}
POST   /kalemler/sirala
GET    /{firmaId}/{donemYil}            POST /onizle          POST /{firmaId}
```

Testler: **143/143** geçiyor (98 önceki + 45 vergi: 24 motor + 21 servis).

Verilen kararlar:

- **Motor saf fonksiyon:** veritabanı bilmez, hiçbir türetilmiş değer yazılmaz. Matrah,
  hesaplanan vergi, asgari vergi ve ödenecek vergi her istekte yeniden üretilir.
- **Katalogu çağıran belirler; motor `Aktif`'e göre süzmez.** İlk sürümde motor pasif
  kalemleri eliyordu; bu, kayıtlı bir beyannamedeki kalem sonradan pasife alınınca matrahı
  sessizce değiştiriyordu (birim test bunu yakaladı). Servis artık "aktif kalemler +
  beyannamede geçmiş pasif kalemler" kümesini veriyor.
- **`KurumKazanci` (yüzdesel üst sınırların tabanı)** = ticari bilanço kârı − iştirak
  kazançları istisnası (IST-01) − geçmiş yıl zararı mahsubu. KVK 10 uygulamasındaki tanım
  esas alındı; KKEG bu tabana **girmez**. Kod IST-01 kodunu arar — kod değişirse taban da
  değişir (kalem yönetiminde sistem kaleminin kodu kilitli olduğu için güvenli).
- **Bağlı istisnası olmayan istisnaya ilişkin KKEG**, büyütecek istisna bulamadığı için
  matrahı artıran KKEG gibi işlenir ve kullanıcıya uyarı üretilir (KKEGI-05 senaryosu).
- **Asgari matrah** = ticari kâr + **ham** ilave toplamı − (`AsgariMatrahtanDuser` işaretli
  Grup 2 ve Grup 3 kalemlerinin efektif tutarları). İstisnaya ilişkin KKEG burada da nötr
  kalır, çünkü büyüttüğü istisna (teknopark/serbest bölge) asgari matrahtan düşen kalemdir.
- **Asgari KV oranı (%10) ve zarar mahsup sınırı (5 yıl) kodda sabit.** Bunlar kaleme değil
  hesabın kendisine ait olduğu için `VergiKalemi` tablosunda karşılıkları yok; motorda
  adlandırılmış sabit olarak duruyor ve yorumda mevzuat dayanağı yazılı.
- **Geçmiş yıl zararında `MahsupEdilen` kullanıcıdan alınmaz**, motor hesaplar ve kayıt
  sırasında yazılır. Sınır dışı (5 yıldan eski) zarar mahsup edilmez ve uyarı üretilir.
- **Sıfır tutarlı ve notu boş satırlar saklanmaz;** beyanname tablosu yalnızca dolu kalemleri
  tutar. Ekrandaki "tüm kalemleri göster" anahtarı katalogdan beslenir.
- **`POST /onizle` kaydetmeden hesaplar.** Ekran her değişiklikte bunu çağırabilir; otomatik
  kayıt yoktur (şartname gereği).

⚠️ **Teyit edilmesi gereken mevzuat noktaları:**

- `AsgariMatrahtanDuser` (KVK 32/C) 11 kalemde `true` işaretlendi: IST-01, IST-04, IST-12,
  IST-13, IST-14, IST-16, IST-17, IND-01, IND-02, IND-10, IND-11. Emin olunmayan kalemlerde
  `false` bırakıldı (özellikle **IST-05 yatırım fonu/ortaklığı portföy kazancı** kapsamı
  koşullu olduğu için işaretlenmedi). Liste yürürlükteki KVK 32/C metniyle karşılaştırılmalı.
- Kalemlerin oran ve tavanları yıllara göre değişir; seed'e yalnızca **oransal** sınırlar
  yazıldı (bağış %5, girişim sermayesi fonu %10). Binek otomobil tutar sınırları, finansman
  gider kısıtlaması oranı gibi **tutarsal/yıllık** değerler seed'e yazılmadı; bunlar kalem
  yönetimi ekranından girilmeli.

Her faz bitiminde bu tabloyu güncelle: durum ✅, nota oluşturulan dosyalar,
verilen kararlar ve teyit edilmesi gereken mevzuat noktaları.

---

## Uyulacak kurallar

- Mevcut Firma Kontrol modülünün konvansiyonlarını takip et
- Türkçe alan ve sınıf adları
- Parasal alanlar `decimal`, gösterim `tr-TR`
- Oranlar ve tutarsal sınırlar koda gömülmesin, `VergiKalemi` tablosundan okunsun
- İş kuralları servis katmanında; ekran kontrolleri yalnızca kullanıcı deneyimi için
- Hata mesajları Türkçe ve eyleme dönük
- Yalnızca Firma Kontrol modülü ve yeni vergi dosyalarına dokun

### Faz 3 — Vergi Hesaplaması sekmesi

Oluşturulan dosyalar:

- `WebApp/Pages/FirmaKontrol/Components/VergiBeyannamePanel.razor` — 11 bölümlü ekran
- `WebApp/Shared/Dto/FirmaKontrol/VergiBeyannameDtos.cs`
- `WebApp/Application/Services/FirmaKontrol/{IVergiBeyannameApiClient,VergiBeyannameApiClient}.cs`
- `Detay.razor`: sekme içeriği eski `VergiHesabiPanel` yerine yeni panele bağlandı

| Ekran maddesi | Durum |
|---|---|
| 11 bölüm, beyanname sırası, katlanabilir | ✅ |
| 690 gelir tablosundan, düzenlenemez, gri kutu | ✅ |
| İlaveler: nötr + yeşil alt kutular, iki ayrı toplam | ✅ |
| İstisnaya ilişkin KKEG'de geri dönüş satırı ("18.000,00 → 21.100,00") | ✅ |
| Grup 2 mavi, Grup 3 sarı çerçeve + altyazılar | ✅ |
| Geçmiş yıl zararları yıl yıl, devreden gösterimi, 5 yıl uyarısı | ✅ |
| Matrah vurgulu ara toplam | ✅ |
| Vergi hesabı iki kolon (Normal / Asgari), uygulanan yeşil | ✅ |
| Kanun maddesi monospace + soluk | ✅ |
| Hatırlatma: tutar girilince otomatik, boşta bilgi ikonuyla | ✅ |
| Üst sınır aşımında satır kırmızı + "KKEG-17'ye taşı" düğmesi | ✅ |
| Devreden / yanan tutar gösterimi | ✅ |
| Sıfır tutarlı kalemler gizli + "tüm kalemleri göster" | ✅ |
| Kalem arama (kod, ad, kanun maddesi) | ✅ |
| Satır başına kullanıcı notu | ✅ |
| Otomatik kayıt yok; kaydedilmemiş değişiklikle ayrılırken uyarı | ✅ |
| tr-TR tutar biçimi | ✅ |

Faz 3 kararları:

- **Hesap ekranda yapılmıyor.** Her değişiklikte `POST /vergi/onizle` çağrılır; matrah, sınır
  aşımı, devreden tutar ve uyarılar sunucudan gelir. Ekranda hiçbir vergi kuralı kopyalanmadı.
- **Kaydedilmemiş değişiklik uyarısı `NavigationManager.RegisterLocationChangingHandler`
  ile yapıldı** — uygulama içi geçişleri yakalar, ek JS dosyası ve `index.html` değişikliği
  gerektirmez. Tarayıcı sekmesini kapatma/yenileme uyarısı için `beforeunload` gerekir;
  o ortak dosyalara dokunmak gerekeceği için yapılmadı.
- **Dönem yılı şimdilik cari yıl** (`DateTime.Now.Year`); mevcut vergi paneli de böyle
  çalışıyordu. Geçmiş dönem seçimi ayrı bir iş.
- **"KKEG-17'ye taşı"** düğmesi kaynak kalemi üst sınıra çeker ve aşan tutarı KKEG-17'ye
  ekler; ardından yeniden hesaplatır.

### Faz 4 — Kalem yönetimi ve Excel

Oluşturulan dosyalar:

- `WebApp/Pages/FirmaKontrol/VergiKalemleri.razor` — `/firma-kontrol/vergi-kalemleri`
- `WebApp/Pages/FirmaKontrol/Components/VergiKalemFormDialog.razor`
- `Features/FirmaKontrol/Services/VergiBeyannameExcel.cs` — ClosedXML ile `.xlsx`
- Uç: `GET /vergi/{firmaId}/{donemYil}/excel`

| Madde | Durum |
|---|---|
| Gruba göre sekmeli liste | ✅ 4 sekme |
| Yeni kalem ekleme, düzenleme, pasife alma | ✅ |
| Sistem kaleminde kod ve grup kilitli, kalan alanlar açık | ✅ (sunucu da zorluyor) |
| Kullanıcı kalemi tamamen düzenlenebilir, kullanılmamışsa silinebilir | ✅ |
| Sıralama | ⚠️ yukarı/aşağı düğmeleriyle + toplu kaydet (sürükle-bırak değil) |
| Excel: beyanname formatına yakın tek sayfa | ✅ |
| Kalem kodu, ad, kanun maddesi, tutar, not kolonları | ✅ |
| Ara toplam ve matrah satırları vurgulu | ✅ |
| Sunucu tarafında ClosedXML, gerçek `.xlsx` | ✅ |

Faz 4 kararları:

- **Sürükle-bırak yerine yukarı/aşağı düğmeleri.** Projede kullanılan Radzen sürümünde hazır
  bir sürükle-bırak liste bileşeni yok; harici kitaplık eklemek "yeni mimari desen icat etme"
  kuralına aykırı olurdu. Sıra numaraları yeniden atanıp `POST /kalemler/sirala` ile topluca
  kaydediliyor — davranış aynı, etkileşim farklı. Sürükle-bırak isteniyorsa ayrıca ele alınmalı.
- **Excel kayıtlı beyannameden üretilir.** Kaydedilmemiş değişiklik varsa ekran önce
  kaydetmeyi önerir; böylece dosya ile veritabanı hep aynı şeyi söyler.
- **Excel'de Grup 2 ve 3'te efektif tutar yazılır** (Grup 2'de ilişkili KKEG ile büyütülmüş,
  Grup 3'te uygulanabilen kısım); girilen tutar ile farkı not kolonunda açıklanır.
- **Tutarı sıfır olan kalemler Excel'e yazılmaz;** çıktı beyanname gibi yalnızca dolu
  kalemleri gösterir.

### Uçtan uca doğrulama (gerçek stack, 02.08.2026)

CatalogService (:5004) + yerel SQL Server. Migration uygulandı, seed çalıştı.
**34/34 doğrulama geçti:**

| Doğrulama | Sonuç |
|---|---|
| Katalog: 63 kalem, grup dağılımı 23/20/16/4 | ✅ |
| KKEGI-01 → IST-17 bağı, IND-05 %5 tavanı, 11 adet 32/C işareti | ✅ |
| Sistem kaleminin kodu/grubu kilitli, silinemiyor (409) | ✅ |
| Kullanıcı kalemi ekleme, tekrarlı kod reddi, geçersiz bağlı istisna reddi | ✅ |
| İstisnaya ilişkin KKEG bağlı istisnayı büyütüyor (18.000 → 21.100) | ✅ |
| Aynı KKEG'in matraha net etkisi sıfır (982.000 = 982.000) | ✅ |
| Ham ilave / matraha etki eden ayrımı | ✅ |
| Bağış %5 tavanı: 50.000 uygulanıp 30.000 aşım + uyarı | ✅ |
| Grup 3 matrahı sıfırın altına indirmiyor, devreden ayrı | ✅ |
| Zarar mahsubu en eskiden, 5 yıldan eski reddediliyor + uyarı | ✅ |
| Asgari KV paralel hesap, yüksek olan uygulanıyor | ✅ |
| İndirimli oran ayrı matrah (230.000) | ✅ |
| Kaydetme, upsert, sıfır satır saklanmaması, mahsubun motordan yazılması | ✅ |
| Excel: gerçek `.xlsx` (PK imzası), doğru content-type, kayıtsız dönemde 404 | ✅ |
| Excel içeriği: tüm bölüm başlıkları, ara toplamlar, kalem satırları, notlar | ✅ |

Test verisi temizlendi: `VergiHesaplamalar` 0, satır 0, zarar 0, katalog 63 sistem kalemi, 0 pasif.

⚠️ **Doğrulama sırasında oluşan ve giderilen veri bozulması:** test betiği UTF-8 BOM'suz
yazıldığı için PowerShell 5.1 Türkçe karakterleri bozarak IND-05 kaleminin adını
veritabanında bozdu. Seed dosyasındaki doğru metinle onarıldı ve tüm katalog tarandı
(bozuk kayıt kalmadı). Bu bir uygulama hatası değil, test aracı hatasıydı.

### Doğrulanamayanlar

- **Ekranların görsel davranışı.** Tarayıcı otomasyonu bu oturumda kullanılamadı; iki ekranın
  (Vergi Hesaplaması paneli ve kalem yönetimi) render'ı, katlanabilir bölümler, renk kodlu
  çerçeveler, geri dönüş satırı ve "KKEG-17'ye taşı" düğmesi gözle görülmedi. Bağlandıkları
  API sözleşmesi ve hesap sonuçları gerçek veriyle birebir doğrulandı.
- **Excel dosyasının Excel'de açılışı.** Dosyanın gerçek xlsx olduğu (zip imzası, ClosedXML
  üretimi) ve içeriğindeki metinler doğrulandı, ancak Microsoft Excel ile açılıp biçimlerin
  (vurgular, kolon genişlikleri) beklendiği gibi göründüğü kontrol edilmedi.
- **Mevzuat doğruluğu.** Kalem listesi, oranlar ve 32/C kapsamı bir mali müşavir tarafından
  yürürlükteki mevzuatla karşılaştırılmalıdır (yukarıdaki "teyit edilmesi gereken" notları).
