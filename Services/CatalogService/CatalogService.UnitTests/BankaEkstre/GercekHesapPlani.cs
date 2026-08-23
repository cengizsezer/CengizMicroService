using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Tur 2 kabul senaryolarının hesap planı. Kodlar ve adlar <b>gerçek ORKA planından</b>
    /// alındı; ölçülen özellikler korundu:
    ///
    /// <list type="bullet">
    /// <item><b>50 karakter kesme:</b> 6.128 kaydın 914'ü 48–50 karakter ve son kelimesi
    /// ortasından kopmuş. <c>120 B62</c> bu hâliyle duruyor ("… Ticaret Ano").</item>
    /// <item><b>Banka isimli cariler:</b> <c>320 1 10011 Ziraat Bank</c> — açıklamalarda
    /// gönderen banka adı geçtiği için indeksten çıkarılması gerekiyor.</item>
    /// <item><b>Firmanın kendi adını taşıyan gider hesapları:</b> <c>622 0 03 00</c>,
    /// <c>740 0</c> — cari grubunda olmadıkları için indekse girmemeli.</item>
    /// <item><b>Aynı carinin iki grup altındaki kopyası:</b> 159 + 329 çiftleri, adları birebir aynı.</item>
    /// <item><b>Gerçek aileler:</b> Park Plaza (4), Pardus Portföy fonları, Cms Jant.</item>
    /// </list>
    /// </summary>
    public static class GercekHesapPlani
    {
        /// <summary>Ölçülen dosyadaki hesap sahibi unvanı (bankanın en sık yazdığı biçim).</summary>
        public const string HesapSahibi = "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ";

        /// <summary>
        /// Kapsama kontrolüne takılmayan tek yazım. Ölçülen altı yazımın beşi ana unvanı
        /// kapsıyor ya da onun tarafından kapsanıyor; bu biri ("… VE SMMM A.Ş.") kapsamaz
        /// ve ancak takma ad olarak eklenince elenir.
        /// </summary>
        public const string TakmaAdlar = "ADAY BAĞIMSIZ DENETİM VE SMMM A.Ş.";

        /// <summary>Ölçülen altı yazımın tamamı (madde 4 / kabul kriteri 15).</summary>
        public static readonly string[] SahipYazimlari =
        {
            "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ",
            "ADAY BAĞIMSIZ DENETİM",
            "PKF ADAY BAĞIMSIZ DENETİM A.Ş.",
            "PKF ADAY",
            "ADAY BAĞIMSIZ DENETİM VE SMMM A.Ş.",
            "PKF ADAY BAĞIMSIZ DENETİM AŞ."
        };

        public static HesapPlaniKaydi Kayit(string kod, string ad) => new()
        {
            Kod = kod,
            Ad = ad,
            NormalizeAd = Normalizasyon.UnvanNormalize(ad),
            AnaGrup = Normalizasyon.AnaGrup(kod),
            BaslangicHarfi = Normalizasyon.BaslangicHarfi(kod),
            Aktif = true
        };

        public static List<HesapPlaniKaydi> Kur()
        {
            var plan = new List<HesapPlaniKaydi>
            {
                // --- Kesilmiş adlar (ORKA 50 karakterde kesiyor) ---
                Kayit("120 B62", "Baycan Elektrik Müteahhitlik Sanayi Ve Ticaret Ano"),
                Kayit("120 S97", "Solvia Yazılım Ve Danışmanlık Anonim Şti"),
                Kayit("120 I55", "İsgold Altın Rafinerisi Anonim Şti"),
                Kayit("120 H30", "Hakan Yetkili Müessese Anonim Şirketi"),

                // Gelen FAST satırlarında gönderenin bankası açıklamada geçiyor; bu iki cari
                // banka kayıt defteri katmanının fazla erken tetiklendiğini gösteren ölçülen
                // örnekler ("… Akbank T.A.Ş. MARBAŞ MENKUL DEĞERLER … hesabından …",
                // "… Türkiye İş Bankası A.Ş. DEMET DÖVİZ … hesabından …").
                Kayit("120 M40", "Marbaş Menkul Değerler Anonim Şti"),
                Kayit("120 D50", "Demet Döviz Yetkili Müessese Anonim Şirketi"),

                // --- Banka isimli cariler: indekse girmemeli ---
                Kayit("320 1 10011", "Ziraat Bank"),
                Kayit("320 1 10012", "Türkiye İş Bankası"),
                Kayit("320 1 10013", "Denizbank A.Ş."),
                Kayit("320 1 10014", "Akbank T.A.Ş."),
                Kayit("320 1 10015", "Enpara Bank"),

                // --- Firmanın kendi adını taşıyan gider hesapları: cari grubunda değil ---
                Kayit("622 0 03 00", "PKF Aday Bağımsız Denetim"),
                Kayit("740 0", "Bağımsız Denetim"),

                // --- Hesap sahibine benzeyen başka bir cari (ölçülen yanlış eşleşme hedefi) ---
                Kayit("120 B58", "Bağımsız Denetim Derneği"),

                // --- Aynı carinin iki grup altındaki kopyası (adlar birebir aynı) ---
                Kayit("159 B41", "Burak Günel"),
                Kayit("329 B41", "Burak Günel"),
                Kayit("159 Y10", "Yurtiçi Kargo"),
                Kayit("329 Y10", "Yurtiçi Kargo"),
                Kayit("159 A20", "Aras Kargo Yurtiçi Yurtdışı Taşımacılık"),
                Kayit("329 A20", "Aras Kargo Yurtiçi Yurtdışı Taşımacılık"),
                Kayit("159 Z01", "Zafer Genç"),
                Kayit("329 Z01", "Zafer Genç"),
                Kayit("159 U05", "Ufuk Çolak"),
                Kayit("329 U05", "Ufuk Çolak"),

                // --- Gerçek belirsizlik: adlar FARKLI ---
                Kayit("329 P04", "Park Plaza Yönetimi, Aidat"),
                Kayit("329 P05", "Park Plaza Yönetimi, Elektrik"),
                Kayit("329 P06", "Park Plaza Yönetimi, Su"),
                Kayit("329 P27", "Park Plaza 19. Kat"),
                Kayit("120 C10", "Cms Jant"),
                Kayit("120 C11", "Cms Jant Makina"),

                // --- Aynı soyadlı iki kişi (kabul kriteri 9) ---
                Kayit("120 K11", "Kemal Gülman"),
                Kayit("329 P90", "Polat Gülman"),

                // --- "… Tahsilatı" satıcıları (madde 5) ---
                Kayit("329 B43", "Belbim Elektronik Para Ve Ödeme"),
                Kayit("329 T06", "Turkcell Superonlıne"),
                Kayit("329 T01", "Türk Telekom A.Ş"),
                Kayit("329 T61", "Turknet İletişim Hizmetleri"),

                // --- Düşük skorda önerilen alakasız cariler (madde 6) ---
                Kayit("329 A33", "Adobe Systems Ireland"),
                Kayit("329 N21", "Novatek"),

                // --- Plaka anahtarı (madde 7): aynı plakanın iki hesabı ---
                Kayit("740 99 01 01 08", "34 Mrp 081 Araç Kira Bedeli"),
                Kayit("740 99 01 01 09", "34 Mrp 081 Araç Otopark Yakıt Vb."),

                // --- Kural grubu içindeki kişi muavinleri (Tur 3, madde 1) ---
                // Adlar ölçülen dosyadaki gerçek kişiler; kodlar gerçek plandaki biçimde.
                // "Abdülkadir Yılmaz" ile "Abdulkadir Sayıcı" difflib benzerliğinde 0.65
                // ile birbirine karışıyordu; önek yöntemi ikisini ayırır.
                Kayit("195 01 A20", "Abdülkadir Yılmaz"),
                Kayit("195 01 D06", "Dilara Kaya"),
                Kayit("195 01 H13", "İlyas Ömeroğlu"),
                Kayit("195 01 I02", "İlyas Yücel"),
                Kayit("195 01 M05", "Mesut Aktaş"),
                Kayit("195 01 E03", "Eda Budak"),

                // Kişi planda var ama kuralın grubunda değil: ortaklar altında.
                // Kural 195'e kilitlerse bu kayıt hiç bulunamaz (Tur 3, madde 1).
                Kayit("331 02", "Abdulkadir Sayıcı"),

                // --- Vergi eşlemesinin hedefleri ---
                Kayit("689 9 1", "Kanunen Kabul Edilmeyen Giderler"),
                Kayit("360 01 004", "Ödenecek Damga Vergisi"),
                Kayit("770 04 001", "Vergi Resim Ve Harçlar")
            };

            plan.AddRange(PardusFonlari());
            return plan;
        }

        /// <summary>
        /// Pardus Portföy ailesi. Gerçek dosyada 37 fon geçiyor; adlar birebir dosyadan
        /// alındı. Bir kısmı "Pardus Portföy Yönetimi A.Ş. …" ile başlıyor — ölçümde
        /// belirsizliği üreten n-gram bu ("PARDUS PORTFOY YONETIMI").
        /// </summary>
        private static IEnumerable<HesapPlaniKaydi> PardusFonlari()
        {
            var adlar = new[]
            {
                "Pardus Portföy Yönetimi Anonim Şirketi",
                "Pardus Portföy Yönetimi A.Ş. Sekizinci Girişim Sermayesi Yatı",
                "Pardus Portföy Yönetimi A.Ş. Üçüncü Girişim Sermayesi Yatırım",
                "Pardus Portföy Yönetimi A.Ş. Beşinci Girişim Sermayesi Yatırı",
                "Pardus Portföy Yönetimi A.Ş. Altıncı Girişim Sermayesi Yatırı",
                "Pardus Portföy Yönetimi A.Ş. Birinci Karma Girişim Sermayesi",
                "Pardus Portföy Para Piyasası (Tl) Fon",
                "Pardus Portföy Birinci Borçlanma Araçları (Tl) Fonu",
                "Pardus Portföy Bist 30 Dışı Şirketler Hisse Senedi (Tl) Fonu",
                "Pardus Portföy Birinci Değişken Fon",
                "Pardus Portföy Birinci Hisse Senedi (Tl) Fonu",
                "Pardus Portföy İkinci Değişken Fon",
                "Pardus Portföy İkinci Hisse Senedi (Tl) Fonu",
                "Pardus Portföy Katılım Hisse Senedi (Tl) Fonu",
                "Pardus Portföy Altın Fonu",
                "Pardus Portföy Birinci Fon Sepeti Fonu",
                "Pardus Portföy Birinci Katılım Fonu",
                "Pardus Portföy Bist 100 Dışı Şirketler Hisse Senedi (Tl) Fon",
                "Pardus Portföy Temettü Ödeyen Şirketler Hisse Senedi Fonu",
                "Pardus Portföy Sürdürülebilirlik Hisse Senedi (Tl) Fonu",
                "Pardus Portföy Evren Serbest Fon",
                "Pardus Portföy Ondördüncü Hisse Senedi Serbest (Tl) Fon",
                "Pardus Portföy Onikinci Hisse Senedi Serbest (Tl) Fon",
                "Pardus Portföy Bereket Hisse Senedi Serbest Fon",
                "Pardus Portföy Dördüncü Hisse Senedi Serbest (Tl) Fon",
                "Pardus Portföy Onüçüncü Hisse Senedi Serbest (Tl) Fon",
                "Pardus Portföy Ondokuzuncu Hisse Senedi Serbest Fon",
                "Pardus Portföy Onyedinci Hisse Senedi Serbest Fon",
                "Pardus Portföy Sanayi Şirketleri Hisse Senedi Serbest Fon",
                "Pardus Portföy Yirminci Hisse Senedi Serbest Fon",
                "Pardus Portföy Bist 100 Dışı Şirketler Hisse Senedi Serbest",
                "Pardus Portföy Sekizinci Hisse Senedi Serbest (Tl) Fon",
                "Pardus Portföy Altıncı Hisse Senedi Serbest (Tl) Fon",
                "Pardus Portföy Algo Etna İstatistiksel Arbitraj Serbest Fon",
                "Pardus Portföy Beşinci Hisse Senedi Serbest (Tl) Fon",
                "Pardus Portföy Bist 30 Dışı Şirketler Hisse Senedi Serbest",
                "Pardus Portföy Yedinci Hisse Senedi Serbest (Tl) Fon"
            };

            return adlar.Select((ad, i) => Kayit($"120 F{i + 1:00}", ad));
        }

        /// <summary>
        /// Banka kayıt defteri: ekstresi işlenen Vakıfbank hesabı + karşı taraf olarak
        /// bulunabilmesi gereken diğer bankalar (kabul kriterleri 7 ve 8).
        /// </summary>
        public static List<BankaHesabi> BankaHesaplari() => new()
        {
            Banka("Vakıfbank", "102 1 1 01", BankaEkstreTestOrtami.ParserTipi, "Vakıfbank Vadesiz Tl"),
            Banka("Denizbank", "102 1 3 02", null, "Denizbank Vadesiz Tl"),
            Banka("Türkiye İş Bankası", "102 1 5 01", null, "İş Bankası Vadesiz Tl", "İş Bankası, Türkiye İş Bankası"),
            Banka("Ziraat Bankası", "102 1 2 01", null, "Ziraat Vadesiz Tl"),
            Banka("Akbank", "102 1 4 01", null, "Akbank Vadesiz Tl"),
            Banka("Fibabanka", "102 1 6 01", null, "Fibabanka Vadesiz Tl", "Fibabank, Fibabanka"),
            Banka("Türk Ekonomi Bankası", "102 1 7 01", null, "Teb Vadesiz Tl", "Teb Maslak, Türk Ekonomi Bankası")
        };

        private static BankaHesabi Banka(string bankaAdi, string kod, string? parser, string hesapAdi,
                                         string? anahtarlar = null) => new()
        {
            BankaAdi = bankaAdi,
            HesapAdi = hesapAdi,
            OrkaHesapKodu = kod,
            ParserTipi = parser,
            EslestirmeAnahtarlari = anahtarlar,
            Aktif = true
        };

        /// <summary>Üretim seed'iyle aynı vergi kodu eşlemeleri.</summary>
        public static List<VergiKoduEslemesi> VergiKodlari() => new()
        {
            new() { VergiKodu = "9085", AnahtarKelime = "TRAFİK CEZ", HesapKodu = "689 9 1", HesapAdi = "Kanunen Kabul Edilmeyen Giderler", Sira = 10, Aktif = true },
            new() { VergiKodu = "0040", AnahtarKelime = "DAMGA", HesapKodu = "360 01 004", HesapAdi = "Ödenecek Damga Vergisi", Sira = 20, Aktif = true },
            new() { VergiKodu = "0033", AnahtarKelime = "BEYANNAME", HesapKodu = "770 04 001", HesapAdi = "Vergi Resim Ve Harçlar", Sira = 30, Aktif = true }
        };
    }
}
