using WebApp.Application.Services;
using WebApp.Application.Services.Interfaces;
using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.UnitTests.FirmaPortfoyu
{
    /// <summary>
    /// Firma Portföyü: okuyucu sonucundan sunucuya giden ana hesap satırları. Yalnız okuyucunun kabul ettiği 3 haneli
    /// kodlar; borç/alacak bakiye seviye-1 düğümünden; alt kırılım gönderilmez.
    /// </summary>
    public class PortfoyMizanSatirlariTests
    {
        private static HesapDugumu D(string kod, decimal borc, decimal alacak, decimal? bakiye = null)
            => HesapDugumu.Olustur(kod, "ad " + kod, borc, alacak, bakiye ?? borc - alacak)!;

        [Fact]
        public void Ana_hesaplar_dugumden_borc_alacak_ile_alt_kirilimlar_gonderilmez()
        {
            var sonuc = new MizanParseResult
            {
                Rows =
                {
                    new MizanExcelRow { Kod = "102", Ad = "BANKALAR", CariDonem = 400m },
                    new MizanExcelRow { Kod = "600", Ad = "YURTİÇİ SATIŞLAR", CariDonem = -900m }
                },
                HesapDugumleri =
                {
                    D("102", 500m, 100m), D("102 1", 500m, 100m), D("102 1 01", 500m, 100m),
                    D("600", 0m, 900m), D("600 1", 0m, 900m)
                }
            };

            var satirlar = PortfoyMizanSatirlari.Hazirla(sonuc);

            Assert.Equal(new[] { "102", "600" }, satirlar.Select(s => s.HesapKodu));
            Assert.Equal(500m, satirlar[0].BorcBakiye);
            Assert.Equal(100m, satirlar[0].AlacakBakiye);
            Assert.Equal("BANKALAR", satirlar[0].HesapAdi);
            Assert.Equal(900m, satirlar[1].AlacakBakiye);
        }

        [Fact]
        public void Basit_bicimde_bakiye_isaretine_gore_borca_ya_da_alacaga_yazilir()
        {
            var sonuc = new MizanParseResult
            {
                Rows =
                {
                    new MizanExcelRow { Kod = "100", CariDonem = 250m },
                    new MizanExcelRow { Kod = "600", CariDonem = -250m },
                    new MizanExcelRow { Kod = "120", CariDonem = 0m }
                },
                HesapDugumleri = { D("100", 0m, 0m, 250m), D("600", 0m, 0m, -250m), D("120", 0m, 0m, 0m) }
            };

            var satirlar = PortfoyMizanSatirlari.Hazirla(sonuc);

            Assert.Equal((250m, 0m), (satirlar[0].BorcBakiye, satirlar[0].AlacakBakiye));
            Assert.Equal((0m, 250m), (satirlar[1].BorcBakiye, satirlar[1].AlacakBakiye));
            Assert.Equal((0m, 0m), (satirlar[2].BorcBakiye, satirlar[2].AlacakBakiye));
        }
    }
}
