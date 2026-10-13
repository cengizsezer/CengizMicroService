using WebApp.Pages.DeclarationFollow;
using WebApp.Shared.Dto.Yonetim;

namespace WebApp.UnitTests.Beyannameler
{
    /// <summary>
    /// Beyanname firma seçenekleri Firmalarım'dan: filtrede yalnız aktifler (pasifler yalnız "Pasif firmaları da göster"
    /// ile, "(pasif)" ekiyle); formda yalnız aktifler + düzenlenen kaydın kendi pasif firması.
    /// </summary>
    public class BeyannameFirmaSecenekleriTests
    {
        private static readonly List<FirmaDto> Firmalar = new()
        {
            new FirmaDto { Id = 5, KisaAd = "LIME", Unvan = "LIME TEKSTİL", Aktif = true },
            new FirmaDto { Id = 3, KisaAd = "", Unvan = "DGR İÇ VE DIŞ TİC.", Aktif = true },
            new FirmaDto { Id = 4, KisaAd = "PKF ADAY", Unvan = "PKF ADAY BAĞIMSIZ DENETİM", Aktif = false },
            new FirmaDto { Id = 1, KisaAd = "PKF PROGROUP", Unvan = "PKF PROGROUP", Aktif = false }
        };

        [Fact]
        public void Filtre_yalniz_aktif_firmalar_ad_kisa_ad_yoksa_unvan()
        {
            var s = BeyannameFirmaSecenekleri.Filtre(Firmalar, pasifleriGoster: false);

            Assert.Equal(new[] { 3, 5 }, s.Select(x => x.Id));
            Assert.Equal(new[] { "DGR İÇ VE DIŞ TİC.", "LIME" }, s.Select(x => x.Ad));
            Assert.All(s, x => Assert.True(x.Aktif));
        }

        [Fact]
        public void Filtre_pasifleri_goster_ile_pasifler_sonda_isaretli()
        {
            var s = BeyannameFirmaSecenekleri.Filtre(Firmalar, pasifleriGoster: true);

            Assert.Equal(new[] { 3, 5, 4, 1 }, s.Select(x => x.Id));
            Assert.Equal("PKF ADAY (pasif)", s[2].Ad);
            Assert.False(s[2].Aktif);
        }

        [Fact]
        public void Form_yeni_kayitta_yalniz_aktifler_duzenlenen_kaydin_pasif_firmasi_eklenir()
        {
            Assert.Equal(new[] { 3, 5 }, BeyannameFirmaSecenekleri.Form(Firmalar, null).Select(x => x.Id));

            var duzenleme = BeyannameFirmaSecenekleri.Form(Firmalar, mevcutFirmaId: 4);
            Assert.Equal(new[] { 3, 5, 4 }, duzenleme.Select(x => x.Id));   // yalnız kendi pasif firması, PROGROUP yok
            Assert.Equal("PKF ADAY (pasif)", duzenleme.Last().Ad);
        }
    }
}
