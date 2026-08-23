using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>
    /// Banka hesabı CRUD'unun eşleştirmeye bakan tarafı: ayrıştırıcı isteğe bağlı
    /// (hesapların çoğuna ekstre yüklenmiyor) ve eşleştirme anahtarları temiz saklanıyor.
    /// </summary>
    public class BankaHesabiServiceTests
    {
        private static BankaHesabiService Servis(CatalogContext db)
            => new(db, new EkstreParserSecici(new IEkstreParser[] { new VakifbankVadesizParser() }),
                   BankaEkstreTestOrtami.Kapsam());

        private static BankaHesabiYazDto Yaz(string kod = "102 1 1 04", string banka = "Vakıfbank",
                                             string parser = "", string? anahtarlar = null) => new()
        {
            BankaAdi = banka,
            HesapAdi = "Vakıfbank, Vadeli Tl - Otomatik Süpürme Hesabı",
            EslestirmeAnahtarlari = anahtarlar,
            OrkaHesapKodu = kod,
            ParserTipi = parser,
            ParaBirimi = "TRY",
            Aktif = true
        };

        [Fact]
        public async Task Ayristiricisiz_hesap_kaydedilebilir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            var dto = await Servis(db).CreateAsync(Yaz(anahtarlar: "Otomatik Süpürme"));

            Assert.Equal(string.Empty, dto.ParserTipi);
            Assert.Null(db.EkstreBankaHesaplari.Single().ParserTipi);
        }

        [Fact]
        public async Task Tanimsiz_ayristirici_reddedilir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            var hata = await Assert.ThrowsAsync<BankaEkstreKuralException>(
                () => Servis(db).CreateAsync(Yaz(parser: "ZIRAAT_VADESIZ")));

            Assert.Equal(nameof(BankaHesabiYazDto.ParserTipi), hata.Field);
        }

        [Fact]
        public async Task Anahtarlar_temizlenerek_saklanir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            var dto = await Servis(db).CreateAsync(
                Yaz(anahtarlar: " Otomatik   Süpürme , Süpürme ,, otomatik süpürme "));

            Assert.Equal("Otomatik Süpürme, Süpürme", dto.EslestirmeAnahtarlari);
        }

        [Fact]
        public async Task Bos_anahtar_null_saklanir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            var olusan = await Servis(db).CreateAsync(Yaz(anahtarlar: "Süpürme"));
            var guncel = await Servis(db).UpdateAsync(olusan.Id, Yaz(anahtarlar: "   "));

            Assert.Null(guncel!.EslestirmeAnahtarlari);
            Assert.Null(db.EkstreBankaHesaplari.Single().EslestirmeAnahtarlari);
        }

        [Fact]
        public async Task Ayristirici_secilirse_kaydedilir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            var dto = await Servis(db).CreateAsync(Yaz(parser: BankaEkstreTestOrtami.ParserTipi));

            Assert.Equal(BankaEkstreTestOrtami.ParserTipi, dto.ParserTipi);
            Assert.Equal(BankaEkstreTestOrtami.ParserTipi, db.EkstreBankaHesaplari.Single().ParserTipi);
        }

        [Fact]
        public void Anahtar_onerisi_hesap_adindan_uretilir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            Assert.Equal("Otomatik Süpürme",
                Servis(db).AnahtarOner("Vakıfbank, Vadeli Tl - Otomatik Süpürme Hesabı", "Vakıfbank"));
        }
    }
}
