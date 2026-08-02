using CatalogService.Api.Features.Muhasebe.Dtos;
using CatalogService.Api.Features.Muhasebe.Services;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.Api.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.Muhasebe
{
    /// <summary>
    /// Masraf merkezi tanım uçları. Hesap planındaki kural 8 ile aynı çizgide:
    /// silme yok, kullanılmayan merkez pasife çekilir.
    /// </summary>
    public class MasrafMerkeziServiceTests
    {
        private static MasrafMerkeziService Servis(CatalogContext db) => new(db);

        private static MasrafMerkeziYazDto Yaz(string kod, string ad) => new() { Kod = kod, Ad = ad };

        [Fact]
        public async Task Ekleme_KoduVeAdiKirpiyor_TenantOtomatikDoluyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();

            var eklenen = await Servis(db).CreateAsync(Yaz("  IDR  ", "  İdari İşler  "));

            Assert.Equal("IDR", eklenen.Kod);
            Assert.Equal("İdari İşler", eklenen.Ad);
            Assert.True(eklenen.Aktif);

            var kayit = await db.MasrafMerkezleri.FirstAsync(m => m.Id == eklenen.MasrafMerkeziId);
            Assert.Equal(MuhasebeTestOrtami.TenantNo, kayit.TenantNo);
        }

        [Fact]
        public async Task AyniKod_IkinciKezEklenemiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            await servis.CreateAsync(Yaz("IDR", "İdari İşler"));

            var hata = await Assert.ThrowsAsync<DuplicateRecordException>(
                () => servis.CreateAsync(Yaz("IDR", "İdare")));

            Assert.Contains("IDR", hata.Message);
            Assert.Equal(1, await db.MasrafMerkezleri.CountAsync());
        }

        [Fact]
        public async Task PasifMerkezinKodu_YenidenKullanilamiyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var eklenen = await servis.CreateAsync(Yaz("IDR", "İdari İşler"));
            await servis.PasifeAlAsync(eklenen.MasrafMerkeziId);

            await Assert.ThrowsAsync<DuplicateRecordException>(() => servis.CreateAsync(Yaz("IDR", "İdari İşler")));
        }

        [Theory]
        [InlineData("", "İdari İşler", "kod")]
        [InlineData("   ", "İdari İşler", "kod")]
        [InlineData("IDR", "", "ad")]
        [InlineData("ONBIRHANE12", "İdari İşler", "kod")]
        public async Task GecersizGirdi_KuralHatasiVeriyor(string kod, string ad, string alan)
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();

            var hata = await Assert.ThrowsAsync<MuhasebeKuralException>(
                () => Servis(db).CreateAsync(Yaz(kod, ad)));

            Assert.Equal(alan, hata.Field);
            Assert.Equal(0, await db.MasrafMerkezleri.CountAsync());
        }

        [Fact]
        public async Task Liste_VarsayilanOlarakYalnizcaAktifleriDonuyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var idari = await servis.CreateAsync(Yaz("IDR", "İdari İşler"));
            await servis.CreateAsync(Yaz("SAT", "Satış"));
            await servis.PasifeAlAsync(idari.MasrafMerkeziId);

            var aktifler = await servis.GetHepsiAsync();
            Assert.Equal(new[] { "SAT" }, aktifler.Select(m => m.Kod));

            var hepsi = await servis.GetHepsiAsync(pasifDahil: true);
            Assert.Equal(new[] { "IDR", "SAT" }, hepsi.Select(m => m.Kod));
            Assert.False(hepsi.Single(m => m.Kod == "IDR").Aktif);
        }

        [Fact]
        public async Task PasifeAlma_TekrarCagrildiginda_AyniSonucuVeriyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();
            var servis = Servis(db);

            var eklenen = await servis.CreateAsync(Yaz("IDR", "İdari İşler"));

            var ilk = await servis.PasifeAlAsync(eklenen.MasrafMerkeziId);
            var ikinci = await servis.PasifeAlAsync(eklenen.MasrafMerkeziId);

            Assert.False(ilk!.Aktif);
            Assert.False(ikinci!.Aktif);
        }

        [Fact]
        public async Task OlmayanMerkez_PasifeAlmadaNullDonuyor()
        {
            using var db = await MuhasebeTestOrtami.HazirContextAsync();

            Assert.Null(await Servis(db).PasifeAlAsync(9999));
            Assert.Null(await Servis(db).GetByIdAsync(9999));
        }
    }
}
