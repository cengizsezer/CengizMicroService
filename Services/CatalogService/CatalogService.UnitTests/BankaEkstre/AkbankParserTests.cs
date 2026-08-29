using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Akbank ayrıştırıcısı. Vakıfbank'tan iki farkı sınanır: <b>işlem tipi kolonu yok</b>
    /// (satırın niteliği yalnız açıklamadan okunuyor) ve yön için iki sinyal var
    /// (Borç/Alacak kolonu + tutarın işareti) — ikisi çapraz doğrulanıyor.
    ///
    /// Ham açıklamalar gerçek 7 aylık ekstreden birebir alınmıştır.
    /// </summary>
    public class AkbankParserTests
    {
        private readonly AkbankVadesizParser _parser = new();
        private readonly HesapEslestirici _eslestirici = new();

        // ---- Gerçek dosyadan ham açıklamalar ----

        private const string DbsOdemesi = "DBS ODM/5089567/0801422766";

        private const string FaturaOdemesi = "FATURA ÖDEME/VF000011536945";

        private const string DiscAkademi =
            "8425183-DISC Akademi Eğitim Ve Yazılım A.Ş.-100 kredi + eğitim bedeli";

        private const string HesaplarArasiTeb =
            "7777/MBL-6973644-Pkf Aday Bağımsız Denetim Anonim Şirketi-HESAPLAR ARASI EFT - TEB";

        private const string EftHesaplarArasi =
            "EFT: PKF ADAY BAĞIMSIZ DENETİM ANONİM ŞİRKETİ HESAPLAR ARASI EFT - Akbank";

        private const string VadeliHesabaTransfer =
            "7777/MBL-VİRMAN-VADELİ HESABA TRANSFER 0698-0268799";

        private const int IslenenId = 1;

        // ---- Dosya ve kolonlar ----

        [Fact]
        public void Basliklar_isimle_bulunur_ve_satirlar_ayrisir()
        {
            using var dosya = UcBankaTestOrtami.AkbankEkstresi(
                new object?[] { "27.08.2026", -1500.00m, "B", DbsOdemesi, "5089567" },
                new object?[] { "27.08.2026", 25000.00m, "A", DiscAkademi, "8425183" });

            var sonuc = _parser.Ayristir(dosya);

            Assert.Empty(sonuc.Uyarilar);
            Assert.Equal(2, sonuc.Satirlar.Count);
            Assert.Equal(6, sonuc.AciklamaKolonu);
            Assert.Equal(11, sonuc.Satirlar[0].KaynakSatirNo);
        }

        [Fact]
        public void Yon_borc_alacak_kolonundan_okunur()
        {
            using var dosya = UcBankaTestOrtami.AkbankEkstresi(
                new object?[] { "27.08.2026", -1500.00m, "B", DbsOdemesi, "5089567" },
                new object?[] { "27.08.2026", 25000.00m, "A", DiscAkademi, "8425183" });

            var satirlar = _parser.Ayristir(dosya).Satirlar;

            Assert.Equal(Yon.Cikan, satirlar[0].Yon);
            Assert.Equal(1500.00m, satirlar[0].Tutar);
            Assert.Equal(Yon.Giren, satirlar[1].Yon);
        }

        [Fact]
        public void Kolon_ile_isaret_celisirse_kolon_kazanir_ve_uyarilir()
        {
            // Tutar artı ama kolon "B" diyor: dosya beklenen biçimde değil. Yön kolondan
            // alınır (işarete güvenilseydi tüm satırlar "giren" okunup 120/329 kararı ters
            // giderdi), çelişki de sessiz kalmaz.
            using var dosya = UcBankaTestOrtami.AkbankEkstresi(
                new object?[] { "27.08.2026", 1500.00m, "B", DbsOdemesi, "5089567" });

            var sonuc = _parser.Ayristir(dosya);

            Assert.Equal(Yon.Cikan, sonuc.Satirlar.Single().Yon);
            Assert.Contains(sonuc.Uyarilar, u => u.Contains("çelişti"));
        }

        [Fact]
        public void Islem_tipi_bos_birakilir()
        {
            using var dosya = UcBankaTestOrtami.AkbankEkstresi(
                new object?[] { "27.08.2026", -1500.00m, "B", DbsOdemesi, "5089567" });

            var satir = _parser.Ayristir(dosya).Satirlar.Single();

            // Kolon yok; uydurma bir işlem tipi türetilseydi unvansız satırların öğrenme
            // anahtarı "ISLEM:<uydurma>" olur ve ilgisiz satırları da çözerdi.
            Assert.Equal(string.Empty, satir.IslemTipi);
            Assert.Equal("5089567", satir.Referans);
        }

        // ---- Unvan çıkarma ----

        [Fact]
        public void Dekont_numarasindan_sonraki_unvan_yakalanir()
        {
            var sonuc = Cikar(DiscAkademi);

            Assert.Equal("DISC Akademi Eğitim Ve Yazılım A.Ş.", sonuc.Unvan);
        }

        [Theory]
        [InlineData(HesaplarArasiTeb)]
        [InlineData(EftHesaplarArasi)]
        public void Bankalar_arasi_satirlarda_hesap_sahibi_elenir(string aciklama)
        {
            var sonuc = Cikar(aciklama);

            Assert.Null(sonuc.Unvan);
            Assert.True(sonuc.HesapSahibiElendi);
        }

        [Fact]
        public void Gurultu_onekli_satirdan_unvan_uydurulmaz()
        {
            // "DBS ODM/…" ve "FATURA ÖDEME/…" satırlarında karşı tarafın adı hiç yazmıyor;
            // desenler öneki tüketerek yazıldığı için gürültü unvan sanılmıyor.
            Assert.Null(Cikar(DbsOdemesi).Unvan);
            Assert.Null(Cikar(FaturaOdemesi).Unvan);
        }

        private static UnvanSonuc Cikar(string aciklama)
            => new UnvanCikarici().Cikar(aciklama, UcBankaTestOrtami.AkbankDesenleri(),
                                         UcBankaTestOrtami.HesapSahibi);

        // ---- Katman ayrımı: DBS bankalar arası değil ----

        [Fact]
        public void Dbs_satiri_banka_kayit_defterine_dusmez_cari_katmanina_gider()
        {
            var baglam = Baglam(DbsOdemesi, Yon.Cikan);

            // Katman 2'nin iki girişi de kapalı: şablon bankalar arası demiyor ve
            // karşı taraf olarak hesap sahibinin kendi adı çıkmadı.
            Assert.False(baglam.Sablon!.BankalarArasi);
            Assert.False(baglam.HesapSahibiElendi);
            Assert.Null(_eslestirici.BankaBul(baglam, Veri()));

            // Satır tedarikçiye gidiyor; muavin açıklamadan çıkarılamadığı için onaya düşer.
            var sonuc = _eslestirici.Coz(baglam, Veri());

            Assert.Equal(KaynakKatman.SabitKural, sonuc.Katman);
            Assert.Equal("329", sonuc.HesapKodu);
            Assert.Equal(SatirDurum.OnayBekliyor, sonuc.Durum);
        }

        [Fact]
        public void Hesaplar_arasi_satir_banka_kayit_defterine_duser()
        {
            // Karşılaştırma noktası: aynı bankanın gerçek bir bankalar arası satırında
            // katman açılıyor ve karşı hesap TEB olarak bulunuyor.
            var baglam = Baglam(HesaplarArasiTeb, Yon.Cikan);

            Assert.True(baglam.Sablon!.BankalarArasi);

            var banka = _eslestirici.BankaBul(baglam, Veri());

            Assert.NotNull(banka);
            Assert.Equal("102 1 32 87", banka!.OrkaHesapKodu);
        }

        [Fact]
        public void Vadeli_hesaba_transfer_dar_sablonu_kazanir()
        {
            // Satır hem "VİRMAN" hem "VADELİ HESABA TRANSFER" içeriyor; dar ifade önce
            // denenmezse genel virman şablonu tutardı.
            var baglam = Baglam(VadeliHesabaTransfer, Yon.Cikan);

            Assert.Equal("VADELİ HESABA TRANSFER", baglam.Sablon!.IslemTipiDeseni);
        }

        private SatirBaglami Baglam(string hamAciklama, Yon yon)
        {
            var uretici = new AciklamaUretici();
            var unvan = Cikar(hamAciklama);

            var baglam = new SatirBaglami
            {
                // Akbank'ta işlem tipi kolonu yok: şablon eşleşmesi ham açıklamadan.
                IslemTipi = string.Empty,
                HamAciklama = hamAciklama,
                Yon = yon,
                Unvan = unvan.Unvan,
                HesapSahibiElendi = unvan.HesapSahibiElendi
            };

            baglam.Sablon = uretici.SablonBul(baglam.IslemTipi, UcBankaTestOrtami.AkbankSablonlari(), hamAciklama);
            return baglam;
        }

        private static EslestirmeVerisi Veri() => new()
        {
            BankaHesaplari = new List<BankaHesabi>
            {
                new() { Id = IslenenId, FirmaId = BankaEkstreTestOrtami.FirmaId, BankaAdi = "Akbank", OrkaHesapKodu = "102 1 4 01", Aktif = true },
                new() { Id = 4, FirmaId = BankaEkstreTestOrtami.FirmaId, BankaAdi = "TEB", OrkaHesapKodu = "102 1 32 87", Aktif = true }
            },
            SabitKurallar = UcBankaTestOrtami.AkbankKurallari(),
            IslenenBankaHesabiId = IslenenId,
            HesapSahibi = UcBankaTestOrtami.HesapSahibi
        };
    }
}
