using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre
{
    /// <summary>
    /// Dört bankanın (Vakıfbank, İş Bankası, Akbank, Ziraat) açıklama şablonları, unvan
    /// desenleri ve sabit kuralları. Bu üç tablo kasıtlı olarak veritabanındadır: yeni
    /// banka eklerken kod değişmez, yalnız buraya (veya arayüzden) satır eklenir.
    ///
    /// Satır bazında idempotent: aynı (ParserTipi + desen) kaydı ikinci kez eklenmez,
    /// mevcut kayıtların üzerine yazılmaz — kullanıcı düzenlemesi korunur.
    /// İçerik banka bazlı referans olduğundan tenant'tan bağımsız tek sefer çalışır.
    ///
    /// Her bankanın satırları kendi <c>...Sablonlari/Desenleri/Kurallari</c> metodunda
    /// durur; ekleme mantığı (sıra numarası, tekrar kontrolü) ortak.
    /// </summary>
    public static class BankaEkstreSeed
    {
        private const string Vakifbank = VakifbankVadesizParser.Tip;
        private const string IsBankasi = IsBankasiVadesizParser.Tip;
        private const string Akbank = AkbankVadesizParser.Tip;
        private const string Ziraat = ZiraatVadesizParser.Tip;

        /// <summary>
        /// Açıklamanın sonundaki "… Tahsilatı" ifadesinden satıcı adı. Kaçışları bozulmasın
        /// diye ayrı sabitte durur (test ortamı da aynı sabiti kullanır).
        /// </summary>
        public const string TahsilatDeseni =
            @"([A-ZÇĞİÖŞÜ][A-Za-zÇĞİÖŞÜçğıöşü/\s]*?)\s*(?:Temsilci|Bayi|Abone|Fatura|Ses/Data/ICT)?\s*Tahsilat[ıi]\s*$";

        // ---- İş Bankası unvan desenleri ----
        // Karşı tarafın adı yıldızla ayrılmış alanlarda. Kaçışları bozulmasın diye
        // sabitte durur; test ortamı da aynı sabitleri kullanır.

        /// <summary>
        /// Baştaki unvan: <c>UNVAN*0111*…</c>. İkinci alanın <b>dört haneli banka kodu</b>
        /// olması şart koşuldu — "Havale" satırlarında unvan başta değil sonda duruyor ve
        /// orada ikinci alan bir kod değil, metin:
        /// <c>2. FATURA BEDELİ ÖDEMESİ*OPAT OTOMOTİV …</c>. Kod çıpası sayesinde iki kalıp
        /// işlem tipine bakmadan, yalnız veriden ayrılıyor (şablon tablosunda desenleri
        /// işlem tipine bağlayan bir alan yok).
        /// </summary>
        public const string IsBankasiBastakiUnvan = @"^([^*]{4,}?)\*\d{4}(?:\*|$)";

        /// <summary>
        /// Sondaki unvan: son yıldızdan sonrası ("Havale" gövdesi). En az iki kelime
        /// aranıyor — tek kelimelik kuyruklar ("FAST", "8792586") unvan değil.
        /// </summary>
        public const string IsBankasiSondakiUnvan = @"\*([^*]{4,}?\s[^*]{2,})$";

        /// <summary>Banka kodu okunamayan satırlarda baştaki alan; en sonda denenir.</summary>
        public const string IsBankasiBastakiAlan = @"^([^*]{4,}?)\*";

        // ---- Akbank unvan desenleri ----

        /// <summary>Bankalar arası EFT gövdesi; yakalanan ad hesap sahibinin kendisi, elenir.</summary>
        public const string AkbankBankalarArasi = @"^EFT:\s*(.+?)\s+HESAPLAR ARASI";

        /// <summary>Mobil kanal gövdesi: <c>7777/MBL-6973644-UNVAN-…</c>.</summary>
        public const string AkbankMobilUnvan = @"^\d+/MBL-\d+-([^-]{4,})-";

        /// <summary>Dekont numarasıyla başlayan gövde: <c>8425183-UNVAN-…</c>.</summary>
        public const string AkbankDekontUnvan = @"^\d+-([^-]{4,})-";

        // ---- Ziraat unvan desenleri ----

        /// <summary>Bankalar arası gövde: <c>Gönd: PKF ADAY … HESAPLAR ARASI …</c> (hesap sahibi, elenir).</summary>
        public const string ZiraatBankalarArasi = @"^Gönd:\s*(.+?)\s+HESAPLAR ARASI";

        /// <summary>IBAN'dan sonra, eğik çizgiler arasındaki ad: <c>…/TR38…-BURAK GÜNEL/2026…</c>.</summary>
        public const string ZiraatIbanSonrasiUnvan = @"/TR\d{24}-([^/]{4,})/";

        /// <summary>Açıklamanın sonundaki büyük harfli unvan: <c>… 1. Taksit HANEDAN DÖVİZ VE ALTIN TİC</c>.</summary>
        public const string ZiraatSondakiUnvan = @"([A-ZÇĞİÖŞÜ][A-ZÇĞİÖŞÜ\s\.]{8,})$";

        public static async Task SeedAsync(CatalogContext db, CancellationToken ct = default)
        {
            // Kategoriler önce: kural satırlarına atanabilmeleri için Id'lerinin oluşması
            // gerekiyor, bu yüzden kendi SaveChanges'ini yapar.
            await KategorileriSeedAsync(db, ct);

            await SablonlariSeedAsync(db, ct);
            await DesenleriSeedAsync(db, ct);
            await KurallariSeedAsync(db, ct);
            await VergiKodlariSeedAsync(db, ct);
            await db.SaveChangesAsync(ct);

            // Atama en sonda: yeni eklenen satırlar da kategorisini alsın.
            await KategorileriAtaAsync(db, ct);
        }

        // ---- İşlem kategorileri ----

        /// <summary>
        /// Dört bankanın gerçek verisinden ölçülen kategori listesi: (ad, varsayılan ana
        /// hesap grubu). Kategoriler bankadan ve firmadan bağımsız; ana grup, ekstre
        /// satırının hangi kategoriye düştüğünü belirleyen tek bağdır.
        ///
        /// Sıra numarası dizideki sıradan üretilir ve mevcut kayıtlar güncellenmez; bu
        /// yüzden yeni kategoriler <b>sona</b> ekleniyor — araya girseler kurulu
        /// veritabanlarıyla yeni kurulumlar farklı sırada görünürdü.
        /// </summary>
        public static readonly (string Ad, string AnaGrup)[] Kategoriler =
        {
            ("Hesaplar arası", "102"),
            ("Müşteri tahsilatı", "120"),
            ("Tedarikçi ödemesi", "329"),
            ("Grup içi cari", "136"),
            ("Diğer alacak", "159"),
            ("Personel iş avansı", "195"),
            ("Personel maaş avansı", "196"),
            ("Banka gideri", "770"),
            ("Araç/hizmet gideri", "740"),
            ("Finansman gideri", "780"),
            ("Kredi", "300"),
            ("Kredi kartı", "309"),
            ("Ortaklar", "331"),
            ("Diğer borç", "336"),
            ("Vergi borcu", "360"),
            ("SGK", "361"),
            ("KKEG", "689"),
            // İş Bankası / Akbank / Ziraat ekstrelerinde çıkan, Vakıfbank'ta hiç görülmeyen
            // iki grup: yatırım hesabı hareketleri (118) ve tahsile verilen çekler (101).
            ("Menkul kıymet", "118"),
            ("Alınan çekler", "101")
        };

        /// <summary>
        /// Açıklama şablonlarının kategorisi. Şablonda hesap kodu yok — kategori ancak
        /// işlemin niteliğinden okunabilir; bu yüzden tek yer burada elle yazılıyor.
        /// Kural/vergi/kişi satırları kodlarının ana grubundan türetiliyor.
        ///
        /// Anahtar şablonun <see cref="AciklamaSablonu.IslemTipiDeseni"/> alanı; bankalar
        /// arasında ortak (aynı desen iki bankada da geçerse aynı kategoriyi alır).
        /// Yönü belirsiz desenler (İş Bankası "EFT", "FAST", "Havale") kasıtlı olarak
        /// listede yok: aynı desen hem tahsilat hem ödeme olabiliyor, kategori satırın
        /// kendi hesap kodundan okunuyor.
        /// </summary>
        private static readonly Dictionary<string, string> SablonKategorileri = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Gelen EFT Otomatik Yatan"] = "Müşteri tahsilatı",
            ["Tös Hesaba Havale"] = "Müşteri tahsilatı",
            ["Alınan havale"] = "Müşteri tahsilatı",
            ["Gelen FAST Anlık Ödeme"] = "Müşteri tahsilatı",
            ["Gelen EFT Ödeme"] = "Müşteri tahsilatı",
            ["FAST Anlık Ödeme"] = "Tedarikçi ödemesi",
            ["Hesaba giden EFT"] = "Tedarikçi ödemesi",
            ["Gönderilen havale"] = "Tedarikçi ödemesi",
            ["Otomatik Süpürme İşlemleri Virman"] = "Hesaplar arası",
            ["Virman"] = "Hesaplar arası",
            ["Hesaplar Arası EFT"] = "Hesaplar arası",
            ["HGS Bakiye Yükle"] = "Araç/hizmet gideri",
            ["Otoyolu Bakiye Yükle"] = "Araç/hizmet gideri",
            ["MKK Masrafı"] = "Banka gideri",
            ["DIT Yp transfer"] = "Banka gideri",
            ["Vergi Tahsilatı"] = "Vergi borcu",
            ["Kredi Kartı Borç Öde"] = "Kredi kartı",

            // İş Bankası
            ["Para Transferi"] = "Hesaplar arası",
            ["Vadeli - Faiz Verme"] = "Hesaplar arası",
            ["Yatırım"] = "Hesaplar arası",
            ["Kredi"] = "Kredi",
            ["Ücret"] = "Banka gideri",
            ["Para Çekme"] = "Kredi kartı",
            ["Kredi Kartı"] = "Kredi kartı",
            ["Çek"] = "Alınan çekler",

            // Akbank
            ["VADELİ HESABA TRANSFER"] = "Hesaplar arası",
            ["HESAP AÇILIŞI"] = "Hesaplar arası",
            ["KISMİ ÖDEME"] = "Hesaplar arası",
            ["VİRMAN"] = "Hesaplar arası",
            ["Kredi Kartı Ödemesi"] = "Kredi kartı",
            ["DBS ODM"] = "Tedarikçi ödemesi",
            ["FATURA ÖDEME"] = "Tedarikçi ödemesi",
            ["Artı Para Faizi"] = "Finansman gideri",

            // Ziraat
            ["No.lu Vds."] = "Hesaplar arası",
            ["FT. ÖDEMESİ"] = "Tedarikçi ödemesi",
            ["KMV Matrahı"] = "Banka gideri"
        };

        private static async Task KategorileriSeedAsync(CatalogContext db, CancellationToken ct)
        {
            var mevcut = await db.EkstreIslemKategorileri.Select(k => k.Ad).ToListAsync(ct);
            var kayitli = new HashSet<string>(mevcut, StringComparer.OrdinalIgnoreCase);

            var sira = 0;
            foreach (var (ad, anaGrup) in Kategoriler)
            {
                sira += 10;
                if (!kayitli.Add(ad)) continue;

                db.EkstreIslemKategorileri.Add(new IslemKategorisi
                {
                    Ad = ad,
                    VarsayilanAnaGrup = anaGrup,
                    Sira = sira,
                    Aktif = true
                });
            }

            await db.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Kategorisi <b>boş</b> olan kayıtlara kategori yazar; dolu olana dokunmaz —
        /// kullanıcının ekrandan verdiği karar korunur (seed'in genel kuralı).
        ///
        /// Kod taşıyan kayıtlar (sabit kural, vergi kodu, kişi yönlendirme) kategorilerini
        /// <b>hesap kodunun ana grubundan</b> alır. Elle yazılmış bir eşleme listesi
        /// tutulmadı: ana grup zaten kategorinin tanımı ve ekstre satırının etiketi de aynı
        /// yoldan bulunuyor — iki liste ayrışırsa aynı hesap kuralda bir, satırda başka
        /// kategori gösterirdi.
        /// </summary>
        private static async Task KategorileriAtaAsync(CatalogContext db, CancellationToken ct)
        {
            var kategoriler = await db.EkstreIslemKategorileri.ToListAsync(ct);
            if (kategoriler.Count == 0) return;

            var adaGore = kategoriler
                .GroupBy(k => k.Ad, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            var anaGrubaGore = kategoriler
                .Where(k => !string.IsNullOrWhiteSpace(k.VarsayilanAnaGrup))
                .GroupBy(k => k.VarsayilanAnaGrup!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

            int? KodunKategorisi(string? hesapKodu)
            {
                var anaGrup = Normalizasyon.AnaGrup(hesapKodu);
                return anaGrup.Length > 0 && anaGrubaGore.TryGetValue(anaGrup, out var id) ? id : null;
            }

            var degisti = false;

            foreach (var kural in await db.EkstreSabitKurallar.Where(k => k.IslemKategorisiId == null).ToListAsync(ct))
            {
                kural.IslemKategorisiId = KodunKategorisi(kural.HesapKodu);
                degisti |= kural.IslemKategorisiId is not null;
            }

            foreach (var vergi in await db.EkstreVergiKodlari.Where(v => v.IslemKategorisiId == null).ToListAsync(ct))
            {
                vergi.IslemKategorisiId = KodunKategorisi(vergi.HesapKodu);
                degisti |= vergi.IslemKategorisiId is not null;
            }

            foreach (var kisi in await db.EkstreKisiYonlendirmeleri.Where(k => k.IslemKategorisiId == null).ToListAsync(ct))
            {
                kisi.IslemKategorisiId = KodunKategorisi(kisi.HesapKodu);
                degisti |= kisi.IslemKategorisiId is not null;
            }

            foreach (var sablon in await db.EkstreAciklamaSablonlari.Where(s => s.IslemKategorisiId == null).ToListAsync(ct))
            {
                if (!SablonKategorileri.TryGetValue(sablon.IslemTipiDeseni, out var ad)) continue;
                if (!adaGore.TryGetValue(ad, out var id)) continue;

                sablon.IslemKategorisiId = id;
                degisti = true;
            }

            if (degisti) await db.SaveChangesAsync(ct);
        }

        // ---- Açıklama şablonları ----

        /// <summary>Tek bir açıklama şablonu satırı ekler; sıra numarasını çağrı sırası verir.</summary>
        private delegate void SablonEkleyici(string islemTipi, string sablon, bool bankalarArasi = false,
                                             EslesmeTuru tur = EslesmeTuru.Tam);

        private static async Task SablonlariSeedAsync(CatalogContext db, CancellationToken ct)
        {
            await SablonlariSeedAsync(db, Vakifbank, VakifbankSablonlari, ct);
            await SablonlariSeedAsync(db, IsBankasi, IsBankasiSablonlari, ct);
            await SablonlariSeedAsync(db, Akbank, AkbankSablonlari, ct);
            await SablonlariSeedAsync(db, Ziraat, ZiraatSablonlari, ct);
        }

        private static async Task SablonlariSeedAsync(
            CatalogContext db, string parserTipi, Action<SablonEkleyici> icerik, CancellationToken ct)
        {
            var mevcut = await db.EkstreAciklamaSablonlari
                .Where(s => s.ParserTipi == parserTipi)
                .Select(s => s.IslemTipiDeseni)
                .ToListAsync(ct);

            var kayitli = new HashSet<string>(mevcut, StringComparer.OrdinalIgnoreCase);
            var sira = 0;

            void Ekle(string islemTipi, string sablon, bool bankalarArasi = false, EslesmeTuru tur = EslesmeTuru.Tam)
            {
                sira += 10;
                if (!kayitli.Add(islemTipi)) return;

                db.EkstreAciklamaSablonlari.Add(new AciklamaSablonu
                {
                    ParserTipi = parserTipi,
                    IslemTipiDeseni = islemTipi,
                    EslesmeTuru = tur,
                    Sablon = sablon,
                    BankalarArasi = bankalarArasi,
                    Sira = sira,
                    Aktif = true
                });
            }

            icerik(Ekle);
        }

        private static void VakifbankSablonlari(SablonEkleyici Ekle)
        {
            // Gelen para
            Ekle("Gelen EFT Otomatik Yatan", "Gelen Eft - {UNVAN}");
            Ekle("Tös Hesaba Havale", "Gelen Eft - {UNVAN}");
            Ekle("Alınan havale", "Gelen Eft - {UNVAN}");
            Ekle("Gelen FAST Anlık Ödeme", "Gelen Eft - {UNVAN}");
            Ekle("Gelen EFT Ödeme", "Gelen Eft - {UNVAN}");

            // Giden para
            Ekle("FAST Anlık Ödeme", "Giden Eft - {UNVAN}");
            Ekle("Hesaba giden EFT", "Giden Eft - {UNVAN}");
            Ekle("Gönderilen havale", "Giden Eft - {UNVAN}");

            // Bankalar arası: unvan yerine banka adı kullanılır, Katman 3 burada devreye girer.
            Ekle("Otomatik Süpürme İşlemleri Virman", "Otomatik Süpürme Pkf Aday", bankalarArasi: true);
            Ekle("Virman", "Hesaplararası Virman - {HESAP}", bankalarArasi: true);
            Ekle("Hesaplar Arası EFT", "Hesaplar Arası Eft - {BANKA}", bankalarArasi: true, tur: EslesmeTuru.Icerir);

            // Sabit giderler
            Ekle("HGS Bakiye Yükle", "Hgs Bakiye Yüklemesi - {PLAKA}");
            Ekle("Otoyolu Bakiye Yükle", "Hgs Bakiye Yüklemesi - {PLAKA}", tur: EslesmeTuru.Icerir);
            Ekle("MKK Masrafı", "Banka Gideri");
            // Gerçek işlem tipi "DIT Yp transfer para çek işlemi"; TAM eşleşme tutmuyor.
            Ekle("DIT Yp transfer", "Banka Gideri", tur: EslesmeTuru.Icerir);
            Ekle("Vergi Tahsilatı", "Vergi Ödemesi - {VERGI}");
            Ekle("Kredi Kartı Borç Öde", "Kredi Kartı Borç Ödemesi");
        }

        /// <summary>
        /// İş Bankası'nın "İşlem Tipi" kolonu okunabilir ve az sayıda değer alıyor
        /// (ölçümde 11 tip); şablonlar TAM eşleşmeyle o kolona bağlanır.
        ///
        /// Vakıfbank'tan farkı: aynı işlem tipi hem gelen hem giden olabiliyor ("EFT" 242
        /// satırın hem tahsilat hem ödeme tarafı). Şablon tablosunda yön alanı yok, bu
        /// yüzden yön <c>{YON}</c> yer tutucusuyla veriden geliyor.
        /// </summary>
        private static void IsBankasiSablonlari(SablonEkleyici Ekle)
        {
            Ekle("EFT", "{YON} Eft - {UNVAN}");
            Ekle("FAST", "{YON} Eft - {UNVAN}");
            Ekle("Havale", "{YON} Eft - {UNVAN}");

            // Kendi hesapları arası hareketler: karşı hesabı banka kayıt defteri çözer.
            Ekle("Para Transferi", "Hesaplararası Virman - {HESAP}", bankalarArasi: true);
            Ekle("Vadeli - Faiz Verme", "Hesaplararası Virman - {HESAP}", bankalarArasi: true);
            Ekle("Yatırım", "Yatırım Hesabına/Hesabından Aktarma", bankalarArasi: true);

            Ekle("Kredi", "Kredi No: {KREDI}");
            Ekle("Ücret", "Banka Gideri");
            Ekle("Para Çekme", "Kredi Kartı Ödemesi");
            Ekle("Kredi Kartı", "Kredi Kartı Borç Ödemesi");
            Ekle("Çek", "Çek Tahsilatı - {UNVAN}");
        }

        /// <summary>
        /// Akbank ekstresinde <b>işlem tipi kolonu yok</b>: şablonlar açıklamada geçen
        /// ifadeyle eşleşir (<see cref="EslesmeTuru.Icerir"/>), çünkü
        /// <see cref="AciklamaUretici.SablonBul"/> önce ham açıklamayı tarıyor.
        ///
        /// Sıra önemli: "VADELİ HESABA TRANSFER" satırı "VİRMAN" kelimesini de içeriyor
        /// (<c>7777/MBL-VİRMAN-VADELİ HESABA TRANSFER …</c>), dar ifade önce denenmeli.
        /// </summary>
        private static void AkbankSablonlari(SablonEkleyici Ekle)
        {
            Ekle("HESAPLAR ARASI EFT", "Hesaplar Arası Eft - {BANKA}", bankalarArasi: true, tur: EslesmeTuru.Icerir);
            Ekle("VADELİ HESABA TRANSFER", "Vadeli Hesaba Transfer", bankalarArasi: true, tur: EslesmeTuru.Icerir);
            Ekle("HESAP AÇILIŞI", "Vadeli Hesap Açılışı", bankalarArasi: true, tur: EslesmeTuru.Icerir);
            Ekle("KISMİ ÖDEME", "Hesaplararası Virman - {HESAP}", bankalarArasi: true, tur: EslesmeTuru.Icerir);
            Ekle("VİRMAN", "Hesaplararası Virman - {HESAP}", bankalarArasi: true, tur: EslesmeTuru.Icerir);

            Ekle("Kredi Kartı Ödemesi", "Kredi Kartı Borç Ödemesi", tur: EslesmeTuru.Icerir);
            // DBS (doğrudan borçlandırma) satırları bankalar arası DEĞİL, tedarikçi ödemesi.
            Ekle("DBS ODM", "Dbs Ödemesi - {UNVAN}", tur: EslesmeTuru.Icerir);
            Ekle("FATURA ÖDEME", "Fatura Ödemesi - {UNVAN}", tur: EslesmeTuru.Icerir);
            Ekle("Artı Para Faizi", "Finansman Gideri", tur: EslesmeTuru.Icerir);
        }

        /// <summary>
        /// Ziraat de işlem tipi kolonu vermiyor; şablonlar açıklamadan eşleşir.
        ///
        /// "E.F.T." ile "EFT" aynı dosyada iki türlü yazılıyor; şablon eşleşmesi
        /// <see cref="Normalizasyon.KisaltmaNormalize"/>'dan geçtiği için tek desen ikisini
        /// de yakalar.
        /// </summary>
        private static void ZiraatSablonlari(SablonEkleyici Ekle)
        {
            Ekle("HESAPLAR ARASI EFT", "Hesaplar Arası Eft - {BANKA}", bankalarArasi: true, tur: EslesmeTuru.Icerir);
            // "707-62286065-5003 No.lu Vds. Hes.tan …": kendi vadeli hesapları arası aktarım.
            // Karşı hesap, banka kayıt defterindeki eşleştirme anahtarlarından bulunur.
            Ekle("No.lu Vds.", "Vadeli Hesap Virmanı - {HESAP}", bankalarArasi: true, tur: EslesmeTuru.Icerir);
            Ekle("FT. ÖDEMESİ", "Giden Eft - {UNVAN}", tur: EslesmeTuru.Icerir);
            Ekle("KMV Matrahı", "Banka Gideri", tur: EslesmeTuru.Icerir);
            // Bankalar arası olmayan gönderiler; en sonda denenir.
            Ekle("Gönd:", "{YON} Eft - {UNVAN}", tur: EslesmeTuru.Icerir);
        }

        // ---- Unvan desenleri ----

        private delegate void DesenEkleyici(string desen, int sira, string aciklama);

        private static async Task DesenleriSeedAsync(CatalogContext db, CancellationToken ct)
        {
            await DesenleriSeedAsync(db, Vakifbank, VakifbankDesenleri, ct);
            await DesenleriSeedAsync(db, IsBankasi, IsBankasiDesenleri, ct);
            await DesenleriSeedAsync(db, Akbank, AkbankDesenleri, ct);
            await DesenleriSeedAsync(db, Ziraat, ZiraatDesenleri, ct);
        }

        private static async Task DesenleriSeedAsync(
            CatalogContext db, string parserTipi, Action<DesenEkleyici> icerik, CancellationToken ct)
        {
            var mevcut = await db.EkstreUnvanDesenleri
                .Where(d => d.ParserTipi == parserTipi)
                .Select(d => d.Desen)
                .ToListAsync(ct);

            var kayitli = new HashSet<string>(mevcut, StringComparer.Ordinal);

            void Ekle(string desen, int sira, string aciklama)
            {
                if (!kayitli.Add(desen)) return;

                db.EkstreUnvanDesenleri.Add(new UnvanDeseni
                {
                    ParserTipi = parserTipi,
                    Desen = desen,
                    GrupNo = 1,
                    Sira = sira,
                    Aktif = true,
                    Aciklama = aciklama
                });
            }

            icerik(Ekle);
        }

        private static void VakifbankDesenleri(DesenEkleyici Ekle)
        {
            // Sıra ölçülen kapsamaya göre: en çok yakalayan desen önce denenir. Sıra numarası
            // elle verilir — araya desen eklenince mevcut kayıtların sırası değişmemeli
            // (seed mevcut satırların üzerine yazmaz, yalnız eksikleri ekler).
            // Açıklamanın SONUNDAKİ satıcı adı. Diğer desenlerden ÖNCE denenmeli: aynı
            // metinde "Ad Soyad/Unvan:" alanı da var ve mevcut desenler oraya takılıyordu.
            //
            // Yakalama, rakam ve iki nokta içermeyen bir ifadedir; kuyruğundaki genel ekler
            // (Temsilci, Bayi, Abone, Ses/Data/ICT) yakalamanın dışında bırakılır:
            //   "…,Belbim Temsilci Tahsilatı"                    → Belbim        (329 B43)
            //   "…Tutar:2.764,90  SuperonlineTahsilatı"          → Superonline   (329 T06)
            //   "…Tarihi:19.08.2026 Türk Telekom Ses/Data/ICT …" → Türk Telekom  (329 T01)
            //   "…Son Ödeme Tarihi:23.07.2026 Turknet Tahsilatı" → Turknet       (329 T61)
            // Rakam ve iki nokta dışlandığı için "Abone No:22912623" ve "Fatura No:…"
            // alanları yakalamaya giremez.
            Ekle(TahsilatDeseni, 5, "Açıklama sonundaki satıcı adı: \"… Tahsilatı\" (5 satır)");
            Ekle(@"sorgu numaralı (.+?) tarafından", 10, "Gelen EFT gövdesi (ölçümde 120 satır)");
            Ekle(@"nolu ([A-ZÇĞİÖŞÜ0-9][^/]{4,70}?) hesab", 20, "\"... nolu X hesabına\" kalıbı (72 satır)");
            // Giden FAST/EFT gövdesi: "… hesabından <GÖNDEREN BANKA> <KARŞI TARAF> hesabına
            // giden FAST ödemesi". Karşı taraf gönderen bankanın adından sonra, "hesabına"
            // kelimesinden önce durur. Banka adı değişken (Türkiye Garanti Bankası A.Ş.,
            // Akbank T.A.Ş., Denizbank A.Ş. …); ortak tek çıpa "A.Ş." kuyruğu olduğu için
            // banka adının kendisi serbest bırakılıp yalnız bu kuyruk aranır — tembel
            // eşleşme ilk "A.Ş."yi (gönderen bankanınkini) tutar, karşı tarafın kendi
            // "A.Ş." eki unvanın içinde kalır ("Denizbank A.Ş. YURTİÇİ KARGO A.Ş.").
            //
            // "sorgu no'lu \S+ (.+)$" deseninden ÖNCE gelmeli: o desen bu satırlarda
            // açıklamanın kalanını (banka adı + karşı taraf + gövde) tek parça yakalayıp
            // unvan sanıyordu; gerçek dosyada 38 satır bu yüzden karşı tarafsız kalıyordu.
            Ekle(@"(?:[Hh]esabından|HESABINDAN)\s+[^()]{0,70}?(?<![A-ZÇĞİÖŞÜa-zçğıöşü])A\.?Ş\.?\s*[-–]?\s*([^()]{3,120}?)\s+(?:[Hh]esabına|HESABINA)",
                 25, "Giden FAST/EFT: banka adından sonraki karşı taraf (38 satır)");
            Ekle(@"sorgu no'lu \S+ (.+)$", 30, "Sorgu numarasından sonra kalan metin (32 satır)");
            Ekle(@"nolu ([A-ZÇĞİÖŞÜ][A-ZÇĞİÖŞÜ0-9.\s&]{4,60})", 40, "Büyük harfli unvan (12 satır)");
            Ekle(@"^([A-ZÇĞİÖŞÜ0-9][^/]{4,60}?)\s*/\s*[A-ZÇĞİÖŞÜ]", 50, "Eğik çizgi öncesi unvan (6 satır)");
            // Giden EFT gövdesi: "… NEZDİNDEKİ (IBAN) NO'LU (KARŞI TARAF) HESABINA YAPILAN …".
            // Karşı tarafı veren tek desen bu; olmadan parantez öncesi serbest metin
            // ("DENİZBANK HESABINA", "ZAFER GENÇ") unvan sanılıyordu. Hesap sahibinin kendi
            // adı buraya düştüğünde satırın kendi hesapları arası olduğu da anlaşılır.
            Ekle(@"NO'LU ([A-ZÇĞİÖŞÜ][^()]{4,70}?) HESABINA", 55, "Giden EFT karşı tarafı (15 satır)");
            Ekle(@"^(.+?)\s*\(", 60, "Parantez öncesi metin (~30 satır)");
        }

        /// <summary>
        /// İş Bankası açıklaması yıldızla ayrılmış alanlardan oluşuyor:
        /// <c>UNVAN*0111*GÖVDE*REFERANS*KANAL</c>. "Havale" tipinde unvan sonda duruyor;
        /// iki kalıp işlem tipiyle değil, ikinci alanın dört haneli banka kodu olup
        /// olmamasıyla ayrılıyor (bkz. <see cref="IsBankasiBastakiUnvan"/>).
        /// </summary>
        private static void IsBankasiDesenleri(DesenEkleyici Ekle)
        {
            Ekle(IsBankasiBastakiUnvan, 10, "Baştaki unvan: UNVAN*0111*… (EFT/FAST gövdesi)");
            Ekle(IsBankasiSondakiUnvan, 20, "Sondaki unvan: son yıldızdan sonrası (Havale gövdesi)");
            Ekle(IsBankasiBastakiAlan, 30, "Banka kodu okunamayan satırlarda baştaki alan");
        }

        /// <summary>
        /// Akbank açıklaması tire ile ayrılmış: <c>{kanal}-{no}-UNVAN-{gövde}</c>.
        /// <c>7777/MBL-</c> ve <c>DBS ODM/</c> önekleri gürültü; desenler öneki
        /// <b>tüketerek</b> yazıldı, böylece unvan aranmadan önce ayrıca temizlik
        /// yapmaya gerek kalmıyor.
        /// </summary>
        private static void AkbankDesenleri(DesenEkleyici Ekle)
        {
            Ekle(AkbankBankalarArasi, 10, "Bankalar arası EFT: hesap sahibinin kendi unvanı (elenir)");
            Ekle(AkbankMobilUnvan, 20, "Mobil kanal: 7777/MBL-{no}-UNVAN-{gövde}");
            Ekle(AkbankDekontUnvan, 30, "Dekont numarasıyla başlayan gövde: {no}-UNVAN-{gövde}");
        }

        /// <summary>
        /// Ziraat açıklamalarında unvan üç yerde olabiliyor. IBAN'ın <b>önündeki</b> ad
        /// karşı tarafın bankasıdır ("Enpara Bank A.Ş./TR38…"), unvan değil — o kalıp için
        /// kasıtlı olarak desen tanımlanmadı; IBAN'dan sonraki ad zaten karşı tarafı veriyor.
        /// </summary>
        private static void ZiraatDesenleri(DesenEkleyici Ekle)
        {
            Ekle(ZiraatBankalarArasi, 10, "Bankalar arası gönderi: hesap sahibinin kendi unvanı (elenir)");
            Ekle(ZiraatIbanSonrasiUnvan, 20, "IBAN'dan sonraki ad: …/TR38…-BURAK GÜNEL/…");
            Ekle(ZiraatSondakiUnvan, 30, "Açıklama sonundaki büyük harfli unvan");
        }

        // ---- Sabit kurallar (Katman 4) ----

        private delegate void KuralEkleyici(
            string desen, string kod, string ad,
            EslesmeTuru tur = EslesmeTuru.Tam,
            KuralKapsami kapsam = KuralKapsami.IslemTipi,
            bool unvanCikarilsin = true,
            bool altHesapGerekli = false,
            string? anaGruplar = null);

        private static async Task KurallariSeedAsync(CatalogContext db, CancellationToken ct)
        {
            await KurallariSeedAsync(db, Vakifbank, VakifbankKurallari, ct);
            await KurallariSeedAsync(db, IsBankasi, IsBankasiKurallari, ct);
            await KurallariSeedAsync(db, Akbank, AkbankKurallari, ct);
            await KurallariSeedAsync(db, Ziraat, ZiraatKurallari, ct);

            await GenelAvansiCokGruplaYukseltAsync(db, ct);
        }

        private static async Task KurallariSeedAsync(
            CatalogContext db, string parserTipi, Action<KuralEkleyici> icerik, CancellationToken ct)
        {
            var mevcut = await db.EkstreSabitKurallar
                .Where(k => k.ParserTipi == parserTipi)
                .Select(k => k.IslemTipiDeseni)
                .ToListAsync(ct);

            var kayitli = new HashSet<string>(mevcut, StringComparer.OrdinalIgnoreCase);
            var sira = 0;

            void Ekle(
                string desen, string kod, string ad,
                EslesmeTuru tur = EslesmeTuru.Tam,
                KuralKapsami kapsam = KuralKapsami.IslemTipi,
                bool unvanCikarilsin = true,
                bool altHesapGerekli = false,
                string? anaGruplar = null)
            {
                sira += 10;
                if (!kayitli.Add(desen)) return;

                db.EkstreSabitKurallar.Add(new SabitKural
                {
                    ParserTipi = parserTipi,
                    IslemTipiDeseni = desen,
                    Kapsam = kapsam,
                    EslesmeTuru = tur,
                    HesapKodu = kod,
                    HesapAdi = ad,
                    Guven = 0.95m,
                    UnvanCikarilsin = unvanCikarilsin,
                    AltHesapGerekli = altHesapGerekli,
                    AnaGruplar = anaGruplar,
                    Sira = sira,
                    Aktif = true
                });
            }

            icerik(Ekle);
        }

        private static void VakifbankKurallari(KuralEkleyici Ekle)
        {
            // Personel avansı: desen ham açıklamada aranır ve kural öğrenme katmanından
            // önce çalışır. Karşı taraf bir cari değil, ödeme yapılan kişidir; bu yüzden
            // unvan çıkarılmaz (çıkarılsaydı unvan benzerliği katmanı kişiyi 329 altında
            // bir cariye eşlerdi) ve yalnız ana grup verilir — kişi muavinini kullanıcı seçer.
            void Avans(string desen, string kod, string ad, string? anaGruplar = null)
                => Ekle(desen, kod, ad, EslesmeTuru.Icerir, KuralKapsami.Aciklama, unvanCikarilsin: false,
                        altHesapGerekli: true, anaGruplar: anaGruplar);

            // Kodlar boşluklu ORKA formatında; ana hesap seviyesinde bırakıldı, muavin
            // kırılımı firmadan firmaya değiştiği için arayüzden düzenlenmeli.

            // Sıra önemli: "Maaş Avansı" tek başına "Avans"tan önce denenmeli, yoksa genel
            // desen tutar ve 196 yerine ayrıştırılamayan bir grup seçilirdi. Sıra numaraları
            // Ekle içinde onar onar artıyor: İş Avansı 10, İş Avans 20, Masraf Ödemesi 30,
            // Maaş Avansı 40, genel Avans 50 — dar ifadelerin hepsi genelden önce.
            Avans("İş Avansı", "195", "İş Avansları");
            // Gerçek dosyada kısaltılmış hâli de geçiyor ("İş Avans").
            Avans("İş Avans", "195", "İş Avansları");
            Avans("Masraf Ödemesi", "195", "İş Avansları");
            Avans("Maaş Avansı", "196", "Personel Avansları");
            // Genel "Avans": hangi avans olduğunu söylemiyor, iş avansı da olabilir maaş
            // avansı da. Bu yüzden tek grup değil, İKİ ana grup birden taranır; ikisinin
            // toplamında tek kişi muavini varsa satır otomatik çözülür, birden fazlaysa
            // hepsi aday olarak onaya düşer.
            Avans("Avans", "196", "Personel Avansları", anaGruplar: "195, 196");

            // Banka masrafı sayılan işlemlerin tamamı banka komisyonu muavinine gider:
            // MKK masrafı, kambiyo muamele vergisi, DIT yp transfer, komisyon ve BSMV.
            // Ölçümde bunlar ana grupta (770) kalıyor ve "otomatik" damgasıyla yanlış
            // hesaba yazılıyordu.
            //
            // Eşleşme türü İÇERİR: gerçek işlem tipleri seed'deki kısa adın uzun hâli
            // ("DIT Yp transfer para çek işlemi", "Kambiyo Muameleleri Vergisi Tahsilatı").
            // TAM eşleşme ile kural hiçbir satıra uymuyordu.
            //
            // Bu satırlar zaten 770 ile kurulmuş bir veritabanında GÜNCELLENMEZ — seed
            // mevcut kayda dokunmaz (kullanıcı düzenlemesini ezmemek için). Eski kurulumda
            // kod arayüzden düzeltilir.
            Ekle("MKK Masrafı", "770 03 005", "Banka Komisyonu", EslesmeTuru.Icerir);
            Ekle("Kambiyo", "770 03 005", "Banka Komisyonu", EslesmeTuru.Icerir);
            Ekle("DIT Yp transfer", "770 03 005", "Banka Komisyonu", EslesmeTuru.Icerir);
            Ekle("Komisyon", "770 03 005", "Banka Komisyonu", EslesmeTuru.Icerir);
            Ekle("BSMV", "770 03 005", "Banka Komisyonu", EslesmeTuru.Icerir);
            // "Masraf" genel bir kelime (masraf ödemesi, kart masrafı…); banka komisyonuna
            // değil ana gruba bırakıldı, kullanıcı Tanımlar'dan daraltır.
            Ekle("Masraf", "770", "Genel Yönetim Giderleri", EslesmeTuru.Icerir);
            Ekle("HGS Bakiye Yükle", "740", "Hizmet Üretim Maliyeti");
            Ekle("Otoyolu Bakiye Yükle", "740", "Hizmet Üretim Maliyeti", EslesmeTuru.Icerir);
        }

        /// <summary>
        /// İş Bankası kuralları. Açıklama kapsamlı kurallar <b>tüm katmanlardan önce</b>
        /// çalışıp satırı kapatıyor; bu yüzden yalnız karşı tarafın kimliğinden bağımsız,
        /// niteliği kesin satırlar için yazıldı (kredi anaparası, banka ücreti, finansman
        /// gideri, kredi kartı virmanı). 120/329 gibi cari gerektiren gruplara açıklama
        /// kuralı konmadı — konsaydı unvan katmanları hiç çalışmazdı.
        ///
        /// İki koşullu kurallar regex ile yazıldı: <see cref="EslesmeTuru.Regex"/> ham metinde
        /// arandığı için Türkçe büyük harf tuzağına girmemek üzere sorunlu harfler nokta ile
        /// bırakıldı (<c>TAHS.LAT</c>).
        /// </summary>
        private static void IsBankasiKurallari(KuralEkleyici Ekle)
        {
            void Aciklama(string desen, string kod, string ad, EslesmeTuru tur = EslesmeTuru.Icerir,
                          bool altHesapGerekli = false)
                => Ekle(desen, kod, ad, tur, KuralKapsami.Aciklama, altHesapGerekli: altHesapGerekli);

            // Kredi taksitinin anapara bacağı krediyi kapatır (300), faiz bacağı gider (780).
            // Öğrenme anahtarı kredi numarasını taşıdığı için (Normalizasyon.KrediAnahtar)
            // her kredinin muavini ayrı öğrenilebiliyor.
            Aciklama(@"KRED.\s*NO.*ANAPARA\s+TAHS.LAT", "300", "Banka Kredileri", EslesmeTuru.Regex,
                     altHesapGerekli: true);
            Aciklama(@"KMH.*KULLANDIRIM\s+.CRET", "770 03 005", "Banka Komisyonu", EslesmeTuru.Regex);

            Aciklama("EFT Ücret", "770 03 005", "Banka Komisyonu");
            Aciklama("MKK SAKLAMA KOMİSYONU", "770 03 005", "Banka Komisyonu");
            Aciklama("YATIRIM HESABI SAKLAMA ÜCR", "770 03 005", "Banka Komisyonu");

            // Erken ödeme farkı ve BSMV finansman gideri; Vakıfbank'ta BSMV komisyon
            // muavinine gidiyordu, İş Bankası ölçümünde 780'e düşüyor (parser bazlı ayrım).
            Aciklama("ERKN.ODEM", "780", "Finansman Giderleri");
            Aciklama("BSMV", "780", "Finansman Giderleri");

            Aciklama("CH/KKH VİRMAN", "309", "Kredi Kartları");
            Aciklama("KRE.KART BORÇ ÖDEME", "309", "Kredi Kartları");

            // Ölçülen muavinler; hesap planı firmadan firmaya değişirse arayüzden düzeltilir.
            Aciklama("YATIRIM HESABINA/HESABINDAN AKTARMA", "102 1 5 04", "Yatırım Hesabı");
            Aciklama("NET FAİZ BED.OTO.VİRMAN", "102 1 5 07", "Vadeli Hesap");
            // KMH borcunun kapatılması bir banka hesabından diğerine; muavin firmaya bağlı,
            // bu yüzden yalnız ana grup verilip satır onaya bırakılıyor.
            Aciklama("KMH BORCUNA KARŞILIK", "102", "Bankalar", altHesapGerekli: true);

            Aciklama("SGK", "361", "Ödenecek Sosyal Güvenlik Kesintileri");

            // İşlem tipi kolonundan çözülenler.
            Ekle("Ücret", "770 03 005", "Banka Komisyonu", EslesmeTuru.Icerir);
            Ekle("Para Çekme", "309", "Kredi Kartları");
            Ekle("Kredi Kartı", "309", "Kredi Kartları");
            Ekle("Çek", "101", "Alınan Çekler");
        }

        /// <summary>
        /// Akbank kuralları. İşlem tipi kolonu olmadığı için <b>hepsi</b> açıklama
        /// kapsamlı. Bankalar arası satırlara (hesap açılışı, kısmi ödeme, virman) kural
        /// yazılmadı: karşı hesabı banka kayıt defteri katmanı çözüyor ve açıklama kuralı
        /// o katmandan önce çalışıp satırı kapatırdı.
        /// </summary>
        private static void AkbankKurallari(KuralEkleyici Ekle)
        {
            void Aciklama(string desen, string kod, string ad, bool altHesapGerekli = false)
                => Ekle(desen, kod, ad, EslesmeTuru.Icerir, KuralKapsami.Aciklama,
                        altHesapGerekli: altHesapGerekli);

            // DBS ve fatura ödemeleri tedarikçiye gider; muavin, açıklamadan çıkarılan
            // unvanla 329 içinde aranır, bulunamazsa satır ana grupla onaya düşer.
            Aciklama("DBS ODM", "329", "Satıcılar", altHesapGerekli: true);
            Aciklama("FATURA ÖDEME", "329", "Satıcılar", altHesapGerekli: true);

            Aciklama("Artı Para Faizi", "780", "Finansman Giderleri");
            Aciklama("Kredi Kartı Ödemesi", "309", "Kredi Kartları");
            Aciklama("SGK", "361", "Ödenecek Sosyal Güvenlik Kesintileri");
        }

        /// <summary>
        /// Ziraat kuralları. Üç bankanın en az bilgi vereni: satırların 173'ü kendi
        /// hesapları arası ve karşı hesap, açıklamadaki <b>alt hesap numarasından</b>
        /// bulunuyor (62286065-5010 → vadeli kasa). O numaralar firmaya özel olduğu için
        /// seed'e yazılamaz; banka hesabının "eşleştirme anahtarları" alanına Tanımlar
        /// ekranından girilir.
        /// </summary>
        private static void ZiraatKurallari(KuralEkleyici Ekle)
        {
            void Aciklama(string desen, string kod, string ad, bool altHesapGerekli = false)
                => Ekle(desen, kod, ad, EslesmeTuru.Icerir, KuralKapsami.Aciklama,
                        altHesapGerekli: altHesapGerekli);

            // "0707GKDS26000982 Ref,USD 2000TL.92919,40 KMV Matrahı İnternet Döviz Satış
            // İşlemi" — kambiyo muamele vergisi, banka komisyonu muavinine gider.
            Aciklama("KMV Matrahı", "770 03 005", "Banka Komisyonu");
            Aciklama("FT. ÖDEMESİ", "329", "Satıcılar", altHesapGerekli: true);
            Aciklama("SGK", "361", "Ödenecek Sosyal Güvenlik Kesintileri");
        }

        /// <summary>
        /// Genel "Avans" kuralı çoklu ana gruptan önce kurulmuş veritabanlarında tek gruplu
        /// (196) duruyor. Seed mevcut kayıtların üzerine yazmaz — kullanıcı düzenlemesi
        /// korunsun diye — ama bu satırın tek gruplu kalması kuralın <b>eksik</b> çalışması
        /// demek: iş avansı da olabilen genel bir avans satırında 195 hiç taranmaz.
        ///
        /// Bu yüzden tek seferlik, <b>dar</b> bir yükseltme yapılır: kayıt hâlâ seed'in
        /// bıraktığı hâldeyse (kod 196, liste boş, alt hesap bekleniyor) listeye "195, 196"
        /// yazılır. Kullanıcı kodu ya da listeyi elle değiştirdiyse kayda dokunulmaz.
        /// </summary>
        private static async Task GenelAvansiCokGruplaYukseltAsync(CatalogContext db, CancellationToken ct)
        {
            const string genelAvans = "Avans";

            var kayit = await db.EkstreSabitKurallar.FirstOrDefaultAsync(
                k => k.ParserTipi == Vakifbank &&
                     k.IslemTipiDeseni == genelAvans &&
                     k.Kapsam == KuralKapsami.Aciklama &&
                     k.AltHesapGerekli &&
                     k.HesapKodu == "196" &&
                     k.AnaGruplar == null, ct);

            if (kayit is not null) kayit.AnaGruplar = "195, 196";
        }

        // ---- Vergi kodu eşlemeleri ----

        /// <summary>
        /// Vergi tahsilatı satırlarında karşı hesap metnin içeriğine göre değişiyor: gerçek
        /// dosyadaki 5 vergi satırı dört farklı hesaba gitmiş. Ölçülen kodlar tohumlanır,
        /// kalanı kullanıcı Tanımlar ekranından ekler.
        ///
        /// Eşleşmeyen bir kod (ölçümde "0010/KURUMLAR V.") satırı onaya düşürür; tahmin edilmez.
        /// </summary>
        private static async Task VergiKodlariSeedAsync(CatalogContext db, CancellationToken ct)
        {
            var mevcut = await db.EkstreVergiKodlari
                .Select(v => (v.VergiKodu ?? string.Empty) + "|" + (v.AnahtarKelime ?? string.Empty))
                .ToListAsync(ct);

            var kayitli = new HashSet<string>(mevcut, StringComparer.OrdinalIgnoreCase);
            var sira = 0;

            void Ekle(string? kod, string? kelime, string hesapKodu, string hesapAdi)
            {
                sira += 10;
                if (!kayitli.Add((kod ?? string.Empty) + "|" + (kelime ?? string.Empty))) return;

                db.EkstreVergiKodlari.Add(new VergiKoduEslemesi
                {
                    VergiKodu = kod,
                    AnahtarKelime = kelime,
                    HesapKodu = hesapKodu,
                    HesapAdi = hesapAdi,
                    Sira = sira,
                    Aktif = true
                });
            }

            // Trafik cezası kanunen kabul edilmeyen giderdir; plaka anahtarı bu satırlarda
            // ayrıca araç hesabını da aday olarak öne çıkarır.
            Ekle("9085", "TRAFİK CEZ", "689 9 1", "Kanunen Kabul Edilmeyen Giderler");
            Ekle("0040", "DAMGA", "360 01 004", "Ödenecek Damga Vergisi");
            Ekle("0033", "BEYANNAME", "770 04 001", "Vergi Resim Ve Harçlar");
        }
    }
}
