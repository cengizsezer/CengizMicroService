using CatalogService.Api.Features.BankaEkstre;
using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.Muhasebe;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Madde 3: banka kayıt defteri katmanı (Katman 2).
    ///
    /// Katman pratikte hiç çalışmıyordu. Girişi tek bir koşula bağlıydı: eşleşen açıklama
    /// şablonunun <c>BankalarArasi</c> demesi. Şablonlar işlem tipiyle eşleşiyor, gerçek
    /// dosyada ise hesaplar arası EFT'lerin işlem tipi "Hesaba giden EFT" / "Gelen EFT
    /// Otomatik Yatan"; "hesaplar arası" ifadesi açıklamada geçiyor. Seed'deki
    /// "Hesaplar Arası EFT" şablonu bu yüzden hiçbir satıra uymuyordu ve katman hiç
    /// çağrılmadan satır "çözülemedi" kalıyordu.
    ///
    /// İkinci giriş eklendi: karşı taraf olarak hesap sahibinin kendi unvanı çıkarsa
    /// (<see cref="SatirBaglami.HesapSahibiElendi"/>) satır kendi hesapları arası bir
    /// transferdir.
    ///
    /// Üçüncü giriş, o bayrağa hiç güvenmeyen giden EFT kalıbı: "… VADESİZ HESABINDAN
    /// (banka) … ŞUBESİ NEZDİNDEKİ …" + banka adlı unvan. Karşı tarafı veren desen
    /// veritabanından silinmiş ya da sırası kaymışsa bayrak kalkmıyor ve "DENİZBANK
    /// HESABINA" / "İŞ BANKASI" satırları cari katmanlarına düşüyordu.
    ///
    /// Buradaki ham açıklamalar gerçek dosyadan birebir alınmıştır.
    /// </summary>
    public class BankaKayitDefteriTests
    {
        private const string HesapSahibi = "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ";

        // ---- Gerçek dosyadan birebir ham açıklamalar ----

        private const string DenizbankHesabina =
            "DENİZBANK HESABINA (PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ VADESİZ HESABINDAN " +
            "DENİZBANK A.Ş. - IBAN MERKEZ SUBE ŞUBESİ NEZDİNDEKİ TR450013400001964210100008 NO'LU " +
            "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ HESABINA YAPILAN 1414049 SORGU NO'LU EFT)";

        // Aynı gövde, parantez öncesi metin başka bir banka: gerçek dosyada iki kez geçiyor.
        private const string IsBankasi =
            "İŞ BANKASI  (PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ VADESİZ HESABINDAN " +
            "TÜRKİYE İŞ BANKASI A.Ş. - IBAN MERKEZ ŞUBE ŞUBESİ NEZDİNDEKİ TR310006400000110083399663 " +
            "NO'LU PKF ADAY BAĞIMSIZ DENETİM A.Ş.   HESABINA YAPILAN 1414525 SORGU NO'LU EFT)";

        // DBS ödemesi: gövde IsBankasi ile birebir aynı kalıpta, tek fark parantez öncesi
        // metindeki "DBS … NO.LU ABONE". Banka aracı, para aboneye (tedarikçiye) gidiyor.
        private const string IsBankasiDbs =
            "İŞ BANKASI DBS - BORUSANPRE - 879382 NO.LU ABONE / İŞ BANKASI (PKF ADAY BAĞIMSIZ " +
            "DENETİM ANONİM ŞİRKETİ VADESİZ HESABINDAN TÜRKİYE İŞ BANKASI A.Ş. - IBAN MERKEZ ŞUBE " +
            "ŞUBESİ NEZDİNDEKİ TR360006400000110083430904 NO'LU PKF ADAY BAĞIMSIZ DENETİM ANONİM " +
            "ŞİRKETİ HESABINA YAPILAN 8906612 SORGU NO'LU EFT)";

        private const string TebMaslak =
            "HESAPLAR ARASI EFT TEB MASLAK (PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ VADESİZ " +
            "HESABINDAN TÜRK EKONOMİ BANKASI A.Ş. - IBAN MERKEZ SUBESI ŞUBESİ NEZDİNDEKİ " +
            "TR590003200000000154386387 NO'LU PKF ADAY BAĞIMSIZ DENETİM A.Ş. HESABINA YAPILAN " +
            "6347902 SORGU NO'LU EFT)";

        private const string OtomatikSupurme =
            "otomatik süpürme pkf aday / TR40 0001 5001 5800 7298 4901 00 nolu PKF ADAY BAĞIMSIZ " +
            "DENETİM ANONİM ŞİRKETİ hesabından TR37 0001 5001 5801 8031 9306 76 nolu PKF ADAY " +
            "BAĞIMSIZ DENETİM ANONİM ŞİRKETİ hesabına 2026072200617949 referans nolu havale yapılmıştır.";

        private const string VakifbankDenizbank =
            "HESAPLAR ARASI E.F.T. VAKIFBANK/DENİZBANK A.Ş.-0134-90001-24795 sorgu numaralı " +
            "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ tarafından PKF ADAY BAĞIMSIZ DENETİM " +
            "ANONİM ŞİRKETİ tarafına gelen EFT ";

        private const string HesaplarArasiVirman =
            "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ TR37 0001 5001 5801 8031 9306 76 nolu " +
            "hesabından TR40 0001 5001 5800 7298 4901 00 nolu hesabına 2026072400633213 referans nolu virman";

        // Döviz alış/satış: karşı hesabı yalnız IBAN veriyor, açıklamada banka adı geçmiyor.
        // İlk IBAN ekstrenin kendi hesabı (TR40); karşı taraf ikinci IBAN.
        private const string DovizAlis =
            "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ TR40 0001 5001 5800 7298 4901 00 nolu hesabından " +
            "TR80 0001 5001 5804 8013 1394 00 nolu hesabına 2026081700750734 referans nolu döviz alış " +
            "(484796,8 TL (10000,0 USD karşılığı)) (İşlem Kuru : 48,47968)";

        private const string DovizSatis =
            "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ TR80 0001 5001 5804 8013 1394 00 nolu hesabından " +
            "TR40 0001 5001 5800 7298 4901 00 nolu hesabına 2026081800441877 referans nolu döviz satış " +
            "(703,0 USD (33418,71 TL karşılığı)) (İşlem Kuru : 47,53729)";

        /// <summary>Ekstresi işlenen hesabın IBAN'ı; dosyanın künyesindeki değer.</summary>
        private const string IslenenIban = "TR400001500158007298490100";

        // Karşı taraf başka biri: katman açılmamalı.
        private const string ZaferGenc =
            "ZAFER GENÇ (PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ VADESİZ HESABINDAN YAPI VE " +
            "KREDİ BANKASI A.Ş. - IBAN MERKEZ SUBE ŞUBESİ NEZDİNDEKİ TR170006701000000080282909 " +
            "NO'LU ZAFER GENÇ HESABINA YAPILAN 6527772 SORGU NO'LU EFT)";

        // Denizbank adı geçiyor ama para başka bir firmadan geliyor: katman açılmamalı.
        private const string RtaDenizbankUzerinden =
            "CARİ HESABA MAHSUBEN/DENİZBANK A.Ş.-0134-90001-63612 sorgu numaralı RTA " +
            "LABORATUVARLARI BİYOLOJİK ÜRÜNLER İLAÇ VE MAKİ tarafından PKF ADAY BAĞIMSIZ " +
            "DENETİM A.Ş. tarafına gelen EFT ";

        private const int IslenenId = 1;

        private readonly HesapEslestirici _eslestirici = new();

        // ---- Kayıt defteri ----

        private static BankaHesabi Hesap(int id, string banka, string kod, string? anahtarlar = null) => new()
        {
            FirmaId = BankaEkstreTestOrtami.FirmaId,
            Id = id,
            BankaAdi = banka,
            OrkaHesapKodu = kod,
            EslestirmeAnahtarlari = anahtarlar,
            Aktif = true
        };

        /// <param name="ayniBankadaIkinciHesap">
        /// Aynı bankada ikinci bir ayırt edilemez hesap; belirsizlik senaryosu için.
        /// </param>
        private static EslestirmeVerisi Veri(bool ayniBankadaIkinciHesap = false)
        {
            var hesaplar = new List<BankaHesabi>
            {
                Hesap(IslenenId, "Vakıfbank", "102 1 1 01"),
                Hesap(2, "Vakıfbank", "102 1 1 04", "Otomatik Süpürme, Süpürme"),
                Hesap(3, "Denizbank", "102 1 3 02"),
                Hesap(4, "TEB", "102 1 32 87", "TEB Maslak"),
                // Ziraat ve İş Bankası kodları gerçek plandaki gibi ayrı; "102 1 5 01"
                // İş Bankası'nın kodu (bkz. GercekHesapPlani).
                Hesap(5, "Ziraat", "102 1 2 01"),
                Hesap(7, "Türkiye İş Bankası", "102 1 5 01", "İş Bankası, Türkiye İş Bankası"),
                // Kullanıcının defterindeki gerçek DBS hesabı. Eşleştirme HESAP ADINA
                // bakmadığı için ("İş Bankası, Dbs Tl - 3430904, Borusan") DBS satırlarını
                // adı yüzünden çekmez; yalnız BankaAdi ile genel yarışa girer ve 7 numaranın
                // daha uzun anahtarına yenilir (bkz. KARARLAR §81).
                Hesap(10, "İş Bankası", "102 1 5 06")
            };

            if (ayniBankadaIkinciHesap)
                hesaplar.Add(Hesap(6, "Vakıfbank", "102 1 1 05"));

            return new EslestirmeVerisi
            {
                BankaHesaplari = hesaplar,
                IslenenBankaHesabiId = IslenenId
            };
        }

        /// <summary>
        /// Döviz senaryosunun kayıt defteri: ekstresi işlenen TL hesabı ve iki döviz hesabı.
        /// Karşı hesabı yalnız IBAN ayırt ediyor — üçü de aynı bankada ve açıklamada banka
        /// adı hiç geçmiyor.
        ///
        /// IBAN'lar bilerek farklı biçimlerde: ekstresi işlenen hesapta boşluklu (kullanıcı
        /// Tanımlar'a böyle girmiş), USD hesabında bitişik. Karşılaştırma iki tarafı da
        /// rakamlara indirdiği için ikisi de tutmalı.
        /// </summary>
        private static EslestirmeVerisi DovizVerisi()
        {
            var islenen = Hesap(IslenenId, "Vakıfbank", "102 1 1 01");
            islenen.Iban = "TR40 0001 5001 5800 7298 4901 00";

            var usd = Hesap(9, "Vakıfbank", "102 2 1 02");
            usd.Iban = "TR800001500158048013139400";

            return new EslestirmeVerisi
            {
                // 102 2 1 01 (EUR) IBAN'sız: banka adıyla ayırt edilemez, yalnız IBAN çözer.
                BankaHesaplari = new List<BankaHesabi> { islenen, Hesap(8, "Vakıfbank", "102 2 1 01"), usd },
                IslenenBankaHesabiId = IslenenId
            };
        }

        /// <summary>
        /// Bağlam gerçek zincirden kurulur: unvan çıkarıcı çalıştırılır, "hesap sahibi elendi"
        /// bayrağı ve şablon oradan gelir. Elle bayrak set edilseydi test asıl sorunu
        /// (katmanın hiç açılmaması) atlardı.
        /// </summary>
        /// <param name="desenler">
        /// Varsayılan üretim listesi; karşı tarafı veren desenin kayıtlı olmadığı senaryo
        /// için <see cref="KarsiTarafDeseniSiz"/> verilir.
        /// </param>
        private static SatirBaglami Baglam(string hamAciklama, string islemTipi, Yon yon,
                                           IReadOnlyList<UnvanDeseni>? desenler = null)
        {
            var unvan = new UnvanCikarici().Cikar(
                hamAciklama, desenler ?? BankaEkstreTestOrtami.Desenler(), HesapSahibi);

            return new SatirBaglami
            {
                IslemTipi = islemTipi,
                HamAciklama = hamAciklama,
                Yon = yon,
                Unvan = unvan.Unvan,
                HesapSahibiElendi = unvan.HesapSahibiElendi,
                KarsiIban = Normalizasyon.IbanBul(hamAciklama),
                Sablon = new AciklamaUretici().SablonBul(islemTipi, BankaEkstreTestOrtami.Sablonlar())
            };
        }

        // ---- Kullanıcının bildirdiği üç satır ----

        [Fact]
        public void Denizbank_hesabina_giden_eft_denizbank_hesabina_eslesir()
        {
            var sonuc = _eslestirici.Coz(Baglam(DenizbankHesabina, "Hesaba giden EFT", Yon.Cikan), Veri());

            Assert.Equal("102 1 3 02", sonuc.HesapKodu);
            Assert.Equal(KaynakKatman.BankaKayitDefteri, sonuc.Katman);
            Assert.Equal(SatirDurum.Otomatik, sonuc.Durum);
        }

        [Fact]
        public void Hesaplar_arasi_eft_teb_maslak_teb_hesabina_eslesir()
        {
            var sonuc = _eslestirici.Coz(Baglam(TebMaslak, "Hesaba giden EFT", Yon.Cikan), Veri());

            Assert.Equal("102 1 32 87", sonuc.HesapKodu);
            Assert.Equal(KaynakKatman.BankaKayitDefteri, sonuc.Katman);
        }

        [Fact]
        public void Otomatik_supurme_pkf_aday_supurme_hesabina_eslesir()
        {
            var sonuc = _eslestirici.Coz(
                Baglam(OtomatikSupurme, "Otomatik Süpürme İşlemleri Virman", Yon.Cikan), Veri());

            Assert.Equal("102 1 1 04", sonuc.HesapKodu);
            Assert.Equal(KaynakKatman.BankaKayitDefteri, sonuc.Katman);
        }

        // ---- Aynı banka önceliği ----

        [Fact]
        public void Ekstrenin_kendi_bankasi_karsi_taraf_sayilmaz()
        {
            // "VAKIFBANK/DENİZBANK" — Vakıfbank biziz. Aynı turda yarışsalardı ikisi de
            // 9 karakterle berabere kalır ve satır gereksiz yere onaya düşerdi.
            var sonuc = _eslestirici.Coz(
                Baglam(VakifbankDenizbank, "Gelen EFT Otomatik Yatan", Yon.Giren), Veri());

            Assert.Equal("102 1 3 02", sonuc.HesapKodu);
        }

        [Fact]
        public void Baska_banka_gecmiyorsa_ayni_bankanin_hesaplari_aranir()
        {
            // "Hesaplararası Virman" açıklamasında hiçbir banka adı yok; ayrım ekstrenin
            // kendi bankasından geliyor. Vakıfbank'ta işlenen dışında tek hesap var.
            var sonuc = _eslestirici.Coz(Baglam(HesaplarArasiVirman, "Virman", Yon.Giren), Veri());

            Assert.Equal("102 1 1 04", sonuc.HesapKodu);
            Assert.Equal(KaynakKatman.BankaKayitDefteri, sonuc.Katman);
        }

        [Fact]
        public void Ayni_banka_icinde_birden_fazla_aday_varsa_satir_onaya_duser()
        {
            var sonuc = _eslestirici.Coz(
                Baglam(HesaplarArasiVirman, "Virman", Yon.Giren), Veri(ayniBankadaIkinciHesap: true));

            Assert.Equal(SatirDurum.OnayBekliyor, sonuc.Durum);
            Assert.Equal(2, sonuc.Adaylar.Count);
            Assert.Contains(sonuc.Adaylar, a => a.Kod == "102 1 1 04");
            Assert.Contains(sonuc.Adaylar, a => a.Kod == "102 1 1 05");
        }

        [Fact]
        public void Anahtar_tutuyorsa_ayni_bankada_birden_fazla_hesap_olsa_da_cozulur()
        {
            // Süpürme hesabının anahtarı metinde geçtiği için belirsizlik oluşmaz.
            var sonuc = _eslestirici.Coz(
                Baglam(OtomatikSupurme, "Otomatik Süpürme İşlemleri Virman", Yon.Cikan),
                Veri(ayniBankadaIkinciHesap: true));

            Assert.Equal("102 1 1 04", sonuc.HesapKodu);
            Assert.Equal(SatirDurum.Otomatik, sonuc.Durum);
        }

        // ---- Kendi hesabına giden EFT: karşı taraf deseni devrede değilken ----

        /// <summary>
        /// Karşı tarafı veren desenin ("NO'LU … HESABINA") kayıtlı olmadığı desen listesi.
        /// Desenler veritabanında ve ekrandan düzenlenebilir olduğu için üretimde bu desen
        /// eksik ya da sırası kaymış olabiliyor; o zaman hesap sahibi hiç elenmiyor ve unvan
        /// parantez öncesi serbest metinden geliyor ("DENİZBANK HESABINA", "İŞ BANKASI").
        /// </summary>
        private static List<UnvanDeseni> KarsiTarafDeseniSiz()
            => BankaEkstreTestOrtami.Desenler().Where(d => d.Sira != 55).ToList();

        /// <summary>
        /// Gerçek dosyadaki iki satır: "… VADESİZ HESABINDAN (banka) … ŞUBESİ NEZDİNDEKİ …"
        /// kalıbı + banka adlı unvan (koşul c). (a) tutmuyor — metinde "hesaplar arası"
        /// geçmiyor; (b) de tutmuyor — hesap sahibi elenmemiş. Düzeltmeden önce "İŞ BANKASI"
        /// unvanı benzerlik katmanına düşüp 0.43 ile "İstanbul Ticaret Odası"na eşleşiyordu.
        /// </summary>
        [Theory]
        [InlineData(DenizbankHesabina, "102 1 3 02")]
        [InlineData(IsBankasi, "102 1 5 01")]
        public void Kendi_hesabina_giden_eft_karsi_taraf_deseni_olmadan_da_cozulur(
            string hamAciklama, string beklenenKod)
        {
            var baglam = Baglam(hamAciklama, "Hesaba giden EFT", Yon.Cikan, KarsiTarafDeseniSiz());

            // (a) da (b) de tutmuyor: katmanı yalnız gövdenin kalıbı açıyor.
            Assert.False(baglam.HesapSahibiElendi);

            var sonuc = _eslestirici.Coz(baglam, Veri());

            Assert.Equal(beklenenKod, sonuc.HesapKodu);
            Assert.Equal(KaynakKatman.BankaKayitDefteri, sonuc.Katman);
            Assert.Equal(SatirDurum.Otomatik, sonuc.Durum);
        }

        /// <summary>Aynı iki satır, desenlerin tamamı kayıtlıyken de aynı koda gitmeli.</summary>
        [Theory]
        [InlineData(DenizbankHesabina, "102 1 3 02")]
        [InlineData(IsBankasi, "102 1 5 01")]
        public void Kendi_hesabina_giden_eft_tum_desenlerle_de_cozulur(string hamAciklama, string beklenenKod)
        {
            var sonuc = _eslestirici.Coz(Baglam(hamAciklama, "Hesaba giden EFT", Yon.Cikan), Veri());

            Assert.Equal(beklenenKod, sonuc.HesapKodu);
            Assert.Equal(KaynakKatman.BankaKayitDefteri, sonuc.Katman);
        }

        [Fact]
        public void Kalip_tek_basina_katmani_acmaz_unvan_da_banka_olmali()
        {
            // ZAFER GENÇ satırı da "… VADESİZ HESABINDAN YAPI VE KREDİ BANKASI A.Ş. …
            // ŞUBESİ NEZDİNDEKİ …" kalıbında; ayrım unvanın banka olup olmadığından geliyor.
            var baglam = Baglam(ZaferGenc, "Hesaba giden EFT", Yon.Cikan, KarsiTarafDeseniSiz());

            Assert.Equal("ZAFER GENÇ", baglam.Unvan);
            Assert.Equal(BankaEslesmesi.Yok, _eslestirici.BankaEslesmesiBul(baglam, Veri()));
        }

        [Fact]
        public void Kalip_yoksa_banka_adli_unvan_tek_basina_katmani_acmaz()
        {
            // "HESAPLAR ARASI" da yok, kalıp da yok: açıklamada yalnız gönderenin bankası
            // geçiyor. Katman açılsaydı satır Denizbank'a yazılırdı.
            var baglam = Baglam("DENİZBANK A.Ş. - IBAN MERKEZ SUBE ŞUBESİ NEZDİNDEKİ TR45 hesabına ödeme",
                                "Hesaba giden EFT", Yon.Cikan);

            Assert.Equal(BankaEslesmesi.Yok, _eslestirici.BankaEslesmesiBul(baglam, Veri()));
        }

        // ---- Döviz alış/satış: karşı hesabı IBAN veriyor ----

        /// <summary>
        /// Metinde iki IBAN var ve ilki ekstrenin kendi hesabı; tek IBAN okunduğunda döviz
        /// alış satırı karşı tarafsız kalıyor, banka adı da geçmediği için (üç hesap da
        /// Vakıfbank) satır yanlış döviz hesabına düşüyordu.
        /// </summary>
        [Theory]
        [InlineData(DovizAlis)]
        [InlineData(DovizSatis)]
        public void Doviz_satirlari_iban_ile_dogru_doviz_hesabina_gider(string hamAciklama)
        {
            var baglam = Baglam(hamAciklama, "Döviz Alış", Yon.Cikan);
            var sonuc = _eslestirici.Coz(baglam, DovizVerisi());

            Assert.Equal("102 2 1 02", sonuc.HesapKodu);
            Assert.Equal(KaynakKatman.BankaKayitDefteri, sonuc.Katman);
            Assert.Equal(SatirDurum.Otomatik, sonuc.Durum);
        }

        [Fact]
        public void Ekstrenin_kendi_ibani_karsi_hesap_sayilmaz()
        {
            var veri = DovizVerisi();

            // Kayıtta boşluklu duran IBAN da rakamlara iner; iki taraf aynı anahtarda buluşur.
            Assert.Equal(Normalizasyon.IbanAnahtar(IslenenIban), veri.IslenenIbanAnahtari);

            // Döviz alış satırında kendi IBAN'ı (TR40) ilk sırada geçiyor. Elenmeseydi arama
            // orada durur, karşı taraftaki TR80 hiç denenmezdi.
            var sonuc = _eslestirici.Coz(Baglam(DovizAlis, "Döviz Alış", Yon.Cikan), veri);

            Assert.Equal("102 2 1 02", sonuc.HesapKodu);
            Assert.NotEqual("102 1 1 01", sonuc.HesapKodu);
        }

        // ---- DBS ödemesi: banka aracı, para aboneye gidiyor ----

        /// <summary>
        /// DBS satırının gövdesi <see cref="IsBankasi"/> ile birebir aynı kalıpta ve unvan
        /// yine banka adlı; koşul (c) bu yüzden açılıyor ve satır İş Bankası hesabına
        /// yazılıyordu. Oysa banka yalnız aracı: para Borusan'a gidiyor.
        /// </summary>
        [Fact]
        public void Dbs_odemesi_kayit_defteri_katmanini_acmaz()
        {
            var baglam = Baglam(IsBankasiDbs, "Hesaba giden EFT", Yon.Cikan, KarsiTarafDeseniSiz());

            // Katmanı yalnız (c) açabilirdi: (a) ifadesi yok, (b) bayrağı kalkmıyor.
            Assert.False(baglam.HesapSahibiElendi);
            Assert.Equal(BankaEslesmesi.Yok, _eslestirici.BankaEslesmesiBul(baglam, Veri()));
        }

        /// <summary>
        /// Farkı yaratan tek şeyin DBS/ABONE olduğunun kanıtı: aynı metinden bu iki kelime
        /// çıkarılınca koşul (c) yine açılıyor ve satır banka hesabına gidiyor.
        /// </summary>
        [Fact]
        public void Dbs_ve_abone_kelimeleri_cikinca_ayni_govde_kayit_defterine_gider()
        {
            var isaretsiz = IsBankasiDbs.Replace(" DBS ", " ").Replace(" ABONE ", " ");

            var sonuc = _eslestirici.Coz(
                Baglam(isaretsiz, "Hesaba giden EFT", Yon.Cikan, KarsiTarafDeseniSiz()), Veri());

            Assert.Equal("102 1 5 01", sonuc.HesapKodu);
            Assert.Equal(KaynakKatman.BankaKayitDefteri, sonuc.Katman);
        }

        /// <summary>
        /// Kullanıcı DBS hesabına "DBS" eşleştirme anahtarı tanımlasa bile satır kayıt
        /// defterine düşmez: (c) hiç açılmadığı için anahtar aramasına sıra gelmiyor.
        /// </summary>
        [Fact]
        public void Dbs_anahtari_tanimli_olsa_bile_dbs_satiri_kayit_defterine_dusmez()
        {
            var veri = Veri();
            veri.BankaHesaplari.First(h => h.Id == 10).EslestirmeAnahtarlari = "Dbs, Borusan";

            var baglam = Baglam(IsBankasiDbs, "Hesaba giden EFT", Yon.Cikan, KarsiTarafDeseniSiz());

            Assert.Equal(BankaEslesmesi.Yok, _eslestirici.BankaEslesmesiBul(baglam, veri));
        }

        /// <summary>
        /// DBS hesabı defterde dururken DBS <b>olmayan</b> İş Bankası satırı eskisi gibi
        /// 102 1 5 01'e gitmeli: iki hesap da "İŞ BANKASI" metnini tutuyor ama uzun anahtar
        /// ("Türkiye İş Bankası") kazanıyor, satır belirsizliğe düşmüyor.
        /// </summary>
        [Fact]
        public void Dbs_hesabi_defterdeyken_normal_is_bankasi_satiri_bozulmaz()
        {
            var sonuc = _eslestirici.Coz(
                Baglam(IsBankasi, "Hesaba giden EFT", Yon.Cikan, KarsiTarafDeseniSiz()), Veri());

            Assert.Equal("102 1 5 01", sonuc.HesapKodu);
            Assert.Equal(SatirDurum.Otomatik, sonuc.Durum);
        }

        // ---- Katman yanlış açılmamalı ----

        [Fact]
        public void Karsi_taraf_baska_biriyse_katman_acilmaz()
        {
            // Açıklamada "YAPI VE KREDİ BANKASI" geçiyor ama para Zafer Genç'e gidiyor.
            var baglam = Baglam(ZaferGenc, "Hesaba giden EFT", Yon.Cikan);

            Assert.False(baglam.HesapSahibiElendi);
            Assert.Equal("ZAFER GENÇ", baglam.Unvan);
            Assert.Equal(BankaEslesmesi.Yok, _eslestirici.BankaEslesmesiBul(baglam, Veri()));
        }

        [Fact]
        public void Banka_adi_gecen_normal_cari_eftinde_katman_acilmaz()
        {
            // Para RTA Laboratuvarları'ndan Denizbank üzerinden geliyor; karşı taraf o firma.
            // Katman açılsaydı satır 102 1 3 02'ye yazılırdı.
            var baglam = Baglam(RtaDenizbankUzerinden, "Gelen EFT Otomatik Yatan", Yon.Giren);

            Assert.False(baglam.HesapSahibiElendi);
            Assert.Equal(BankaEslesmesi.Yok, _eslestirici.BankaEslesmesiBul(baglam, Veri()));
        }

        [Fact]
        public void Hesap_sahibi_unvani_girilmemisse_eski_davranis_surer()
        {
            // Bayrak hesap sahibi unvanından türüyor; alan boşken katman yalnız şablonla açılır.
            var unvan = new UnvanCikarici().Cikar(DenizbankHesabina, BankaEkstreTestOrtami.Desenler());

            Assert.False(unvan.HesapSahibiElendi);
        }

        [Fact]
        public void Kayit_defteri_bos_ise_satir_dusmez()
        {
            var veri = new EslestirmeVerisi { IslenenBankaHesabiId = IslenenId };

            var sonuc = _eslestirici.Coz(Baglam(DenizbankHesabina, "Hesaba giden EFT", Yon.Cikan), veri);

            Assert.Equal(SatirDurum.Cozulemedi, sonuc.Durum);
        }

        // ---- Uçtan uca, gerçek dosyayla ----

        private static EkstreService Servis(CatalogContext db)
        {
            var secici = new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() });
            return new EkstreService(db, secici, new UnvanCikarici(), new AciklamaUretici(),
                                     new HesapEslestirici(), new HesapEslesmeService(db, BankaEkstreTestOrtami.Kapsam()),
                                     new SabitKullanici(), BankaEkstreTestOrtami.Kapsam());
        }

        /// <summary>Üretimdeki seed + gerçek kayıt defteri.</summary>
        private static async Task<(CatalogContext Db, int HesapId)> HazirlaAsync()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            await BankaEkstreSeed.SeedAsync(db);

            var islenen = new BankaHesabi
            {
                FirmaId = BankaEkstreTestOrtami.FirmaId,
                BankaAdi = "Vakıfbank",
                HesapAdi = "VAKIFBANK VADESIZ TL",
                OrkaHesapKodu = "102 1 1 01",
                ParserTipi = VakifbankVadesizParser.Tip,
                Iban = "TR400001500158007298490100",
                HesapSahibiUnvani = HesapSahibi,
                Aktif = true
            };

            const int firma = BankaEkstreTestOrtami.FirmaId;

            db.EkstreBankaHesaplari.AddRange(
                islenen,
                new BankaHesabi
                {
                    FirmaId = firma,
                    BankaAdi = "Vakıfbank",
                    HesapAdi = "Vakıfbank, Vadeli Tl - Otomatik Süpürme Hesabı",
                    OrkaHesapKodu = "102 1 1 04",
                    EslestirmeAnahtarlari = "Otomatik Süpürme, Süpürme",
                    Aktif = true
                },
                new BankaHesabi { FirmaId = firma, BankaAdi = "Denizbank", OrkaHesapKodu = "102 1 3 02", Aktif = true },
                new BankaHesabi { FirmaId = firma, BankaAdi = "TEB", OrkaHesapKodu = "102 1 32 87", EslestirmeAnahtarlari = "TEB Maslak", Aktif = true },
                new BankaHesabi { FirmaId = firma, BankaAdi = "Ziraat", OrkaHesapKodu = "102 1 2 01", Aktif = true },
                new BankaHesabi { FirmaId = firma, BankaAdi = "Vakıfbank", HesapAdi = "Vakıf Bank Eur", OrkaHesapKodu = "102 2 1 01", Aktif = true },
                new BankaHesabi
                {
                    FirmaId = firma,
                    BankaAdi = "Vakıfbank",
                    HesapAdi = "Vakıf Bank Usd",
                    OrkaHesapKodu = "102 2 1 02",
                    Iban = "TR800001500158048013139400",
                    Aktif = true
                },
                new BankaHesabi
                {
                    FirmaId = firma,
                    BankaAdi = "Türkiye İş Bankası",
                    OrkaHesapKodu = "102 1 5 01",
                    EslestirmeAnahtarlari = "İş Bankası, Türkiye İş Bankası",
                    Aktif = true
                });

            await db.SaveChangesAsync();
            return (db, islenen.Id);
        }

        [Fact]
        public async Task Gercek_dosyada_hesaplar_arasi_satirlar_kayit_defterinden_cozulur()
        {
            var (db, hesapId) = await HazirlaAsync();
            using var _ = db;

            using var dosya = BankaEkstreTestOrtami.GercekEkstre();
            var yukleme = await Servis(db).YukleAsync(hesapId, dosya, "Vakıfbank_Hesap_Ekstresi.xlsx");

            var satirlar = db.EkstreSatirlari.Where(s => s.EkstreYuklemeId == yukleme.Id).ToList();
            Assert.Equal(287, satirlar.Count);

            EkstreSatiri Bul(string onek) =>
                satirlar.First(s => s.HamAciklama.StartsWith(onek, StringComparison.Ordinal));

            Assert.Equal("102 1 3 02", Bul("DENİZBANK HESABINA").OnerilenHesapKodu);
            Assert.Equal("102 1 32 87", Bul("HESAPLAR ARASI EFT TEB MASLAK").OnerilenHesapKodu);
            Assert.Equal("102 1 1 04", Bul("otomatik süpürme pkf aday").OnerilenHesapKodu);
            Assert.Equal("102 1 3 02", Bul("HESAPLAR ARASI E.F.T. VAKIFBANK/DENİZBANK").OnerilenHesapKodu);
            Assert.Equal("102 1 2 01", Bul("HESAPLAR ARASI E.F.T. ZİRAAT BANKASI").OnerilenHesapKodu);
            Assert.Equal("102 1 5 01", Bul("İŞ BANKASI  (").OnerilenHesapKodu);

            // Döviz alış/satış: karşı hesabı yalnız IBAN veriyor; ilk IBAN kendi hesabımız.
            var dovizAlis = satirlar.First(s => s.HamAciklama.Contains("döviz alış", StringComparison.Ordinal));
            Assert.Equal("102 2 1 02", dovizAlis.OnerilenHesapKodu);
            Assert.Equal("TR800001500158048013139400", dovizAlis.KarsiIban);

            Assert.Equal("102 2 1 02",
                satirlar.First(s => s.HamAciklama.Contains("döviz satış", StringComparison.Ordinal)).OnerilenHesapKodu);

            // Karşı tarafı gerçek bir cari olan satır kayıt defterine düşmemeli.
            Assert.NotEqual(KaynakKatman.BankaKayitDefteri, Bul("ZAFER GENÇ").KaynakKatman);
        }

        [Fact]
        public async Task Gercek_dosyada_kayit_defteri_katmani_gorunur_sayida_satir_cozer()
        {
            // Düzeltmeden önce bu sayı sıfırdı: katman hiç çağrılmıyordu.
            var (db, hesapId) = await HazirlaAsync();
            using var _ = db;

            using var dosya = BankaEkstreTestOrtami.GercekEkstre();
            var yukleme = await Servis(db).YukleAsync(hesapId, dosya, "Vakıfbank_Hesap_Ekstresi.xlsx");

            var defterden = db.EkstreSatirlari
                .Count(s => s.EkstreYuklemeId == yukleme.Id && s.KaynakKatman == KaynakKatman.BankaKayitDefteri);

            Assert.True(defterden >= 30, $"Kayıt defterinden çözülen satır sayısı beklenenden az: {defterden}");
        }
    }
}
