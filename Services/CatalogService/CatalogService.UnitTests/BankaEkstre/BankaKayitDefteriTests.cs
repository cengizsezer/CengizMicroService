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
    /// transferdir. Buradaki ham açıklamalar gerçek dosyadan birebir alınmıştır.
    /// </summary>
    public class BankaKayitDefteriTests
    {
        private const string HesapSahibi = "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ";

        // ---- Gerçek dosyadan birebir ham açıklamalar ----

        private const string DenizbankHesabina =
            "DENİZBANK HESABINA (PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ VADESİZ HESABINDAN " +
            "DENİZBANK A.Ş. - IBAN MERKEZ SUBE ŞUBESİ NEZDİNDEKİ TR450013400001964210100008 NO'LU " +
            "PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ HESABINA YAPILAN 1414049 SORGU NO'LU EFT)";

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
                Hesap(5, "Ziraat", "102 1 5 01")
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
        /// Bağlam gerçek zincirden kurulur: unvan çıkarıcı çalıştırılır, "hesap sahibi elendi"
        /// bayrağı ve şablon oradan gelir. Elle bayrak set edilseydi test asıl sorunu
        /// (katmanın hiç açılmaması) atlardı.
        /// </summary>
        private static SatirBaglami Baglam(string hamAciklama, string islemTipi, Yon yon)
        {
            var unvan = new UnvanCikarici().Cikar(hamAciklama, BankaEkstreTestOrtami.Desenler(), HesapSahibi);

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
                new BankaHesabi { FirmaId = firma, BankaAdi = "Ziraat", OrkaHesapKodu = "102 1 5 01", Aktif = true });

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
            Assert.Equal("102 1 5 01", Bul("HESAPLAR ARASI E.F.T. ZİRAAT BANKASI").OnerilenHesapKodu);

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
