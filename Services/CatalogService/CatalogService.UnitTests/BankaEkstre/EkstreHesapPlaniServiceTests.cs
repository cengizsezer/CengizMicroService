using CatalogService.Api.Features.BankaEkstre.Services;
using ClosedXML.Excel;

namespace CatalogService.UnitTests.BankaEkstre
{
    /// <summary>ORKA hesap planı xlsx içe aktarımı: kolon eşleme, upsert ve kod formatının korunması.</summary>
    public class EkstreHesapPlaniServiceTests
    {
        private static MemoryStream Dosya(params (string Kod, string Ad)[] satirlar)
        {
            using var kitap = new XLWorkbook();
            var sayfa = kitap.Worksheets.Add("Hesap Planı");

            sayfa.Cell(1, 1).Value = "ORKA HESAP PLANI";
            sayfa.Cell(3, 1).Value = "Hesap Kodu";
            sayfa.Cell(3, 2).Value = "Hesap Adı";

            var satirNo = 4;
            foreach (var (kod, ad) in satirlar)
            {
                sayfa.Cell(satirNo, 1).Value = kod;
                sayfa.Cell(satirNo, 2).Value = ad;
                satirNo++;
            }

            var akis = new MemoryStream();
            kitap.SaveAs(akis);
            akis.Position = 0;
            return akis;
        }

        [Fact]
        public async Task Ice_aktarim_kodlari_bosluklu_saklar()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            var servis = new EkstreHesapPlaniService(db);

            using var dosya = Dosya(("120 D22", "DAĞI GİYİM SANAYİ"), ("329 K08", "KEMAL TEKSTİL"));
            var sonuc = await servis.IceAktarAsync(dosya);

            Assert.Equal(2, sonuc.Okunan);
            Assert.Equal(2, sonuc.Eklenen);

            var kayit = db.EkstreHesapPlani.Single(h => h.Kod == "120 D22");
            Assert.Equal("120", kayit.AnaGrup);
            Assert.Equal("D", kayit.BaslangicHarfi);
            Assert.Equal("DAGI GIYIM", kayit.NormalizeAd);
        }

        [Fact]
        public async Task Var_olan_kod_guncellenir_silinmez()
        {
            var veritabani = $"plan-{Guid.NewGuid()}";

            using (var db = BankaEkstreTestOrtami.YeniContext(veritabani))
            {
                using var ilk = Dosya(("120 D22", "DAGI GIYIM"), ("120 Z01", "ZETA"));
                await new EkstreHesapPlaniService(db).IceAktarAsync(ilk);
            }

            using (var db = BankaEkstreTestOrtami.YeniContext(veritabani))
            {
                using var ikinci = Dosya(("120 D22", "DAĞI GİYİM SANAYİ"));
                var sonuc = await new EkstreHesapPlaniService(db).IceAktarAsync(ikinci);

                Assert.Equal(1, sonuc.Guncellenen);
                Assert.Equal(0, sonuc.Eklenen);

                // Dosyada olmayan kod silinmez; geçmiş eşleşmeler kırılmasın.
                Assert.Equal(2, db.EkstreHesapPlani.Count());
                Assert.Equal("DAĞI GİYİM SANAYİ", db.EkstreHesapPlani.Single(h => h.Kod == "120 D22").Ad);
            }
        }

        [Fact]
        public async Task Baslik_yoksa_acik_hata_verir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            using var kitap = new XLWorkbook();
            var sayfa = kitap.Worksheets.Add("Sayfa1");
            sayfa.Cell(1, 1).Value = "120 D22";
            sayfa.Cell(1, 2).Value = "DAGI GIYIM";

            using var akis = new MemoryStream();
            kitap.SaveAs(akis);
            akis.Position = 0;

            var hata = await Assert.ThrowsAsync<InvalidDataException>(
                () => new EkstreHesapPlaniService(db).IceAktarAsync(akis));

            Assert.Contains("Hesap Kodu", hata.Message);
        }

        [Fact]
        public async Task Arama_ana_gruba_gore_daraltir()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();
            var servis = new EkstreHesapPlaniService(db);

            using var dosya = Dosya(("120 D22", "DAĞI GİYİM"), ("329 D05", "DAĞITIM AŞ"));
            await servis.IceAktarAsync(dosya);

            var girenler = await servis.AraAsync("D", "120", 10);
            var kod = Assert.Single(girenler);
            Assert.Equal("120 D22", kod.Kod);

            Assert.Equal(2, (await servis.AraAsync("D", null, 10)).Count);
        }
    }
}
