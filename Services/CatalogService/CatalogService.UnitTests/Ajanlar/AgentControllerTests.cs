using CatalogService.Api.Features.Ajanlar;
using CatalogService.Api.Features.Ajanlar.Controllers;
using CatalogService.Api.Features.Ajanlar.Domain;
using CatalogService.Api.Features.Ajanlar.Dtos;
using CatalogService.Api.Features.Ajanlar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace CatalogService.UnitTests.Ajanlar
{
    public class AgentControllerTests
    {
        private static (AgentController Controller, AjanDeposu Depo) Kur(SahteSaat saat)
        {
            var depo = new AjanDeposu(new SabitAyar<AgentHubAyarlari>(AjanTestVerisi.Ayarlar()), saat);
            return (new AgentController(depo), depo);
        }

        private static List<BagliAjanDto> Liste(ActionResult<List<BagliAjanDto>> sonuc)
            => Assert.IsType<List<BagliAjanDto>>(Assert.IsType<OkObjectResult>(sonuc.Result).Value);

        [Fact]
        public void Ajan_yokken_bos_liste_doner()
        {
            var (controller, _) = Kur(new SahteSaat());

            Assert.Empty(Liste(controller.Baglilar()));
        }

        [Fact]
        public void Bagli_ajanin_butun_alanlari_donuyor()
        {
            var saat = new SahteSaat();
            var (controller, depo) = Kur(saat);
            depo.Kaydet(new AjanKaydi
            {
                MakineId = "MAK-1",
                ConnectionId = "c1",
                MakineAdi = "BANKA-PC",
                AjanSurumu = "1.0.0",
                IsletimSistemi = "Windows 11",
                KullaniciId = "smmm-42",
                OrkaCalisiyorMu = false
            });

            var satir = Assert.Single(Liste(controller.Baglilar()));

            Assert.Equal("MAK-1", satir.MakineId);
            Assert.Equal("BANKA-PC", satir.MakineAdi);
            Assert.Equal("1.0.0", satir.AjanSurumu);
            Assert.Equal("Windows 11", satir.IsletimSistemi);
            Assert.Equal("smmm-42", satir.KullaniciId);
            Assert.Equal(saat.GetUtcNow(), satir.BaglantiZamani);
            Assert.Equal(saat.GetUtcNow(), satir.SonKalpAtisi);
            Assert.False(satir.OrkaCalisiyorMu);
        }

        [Fact]
        public void Kalp_atisi_kesilen_ajan_listede_cikmaz()
        {
            var saat = new SahteSaat();
            var (controller, depo) = Kur(saat);
            depo.Kaydet(new AjanKaydi { MakineId = "MAK-1", ConnectionId = "c1", MakineAdi = "BANKA-PC" });

            saat.Ilerle(TimeSpan.FromSeconds(120));

            Assert.Empty(Liste(controller.Baglilar()));
        }

        [Fact]
        public void Uc_yetkilendirme_istiyor()
        {
            Assert.NotNull(typeof(AgentController).GetCustomAttribute<AuthorizeAttribute>());
        }

        [Fact]
        public void Uc_adresi_ocelot_un_catalog_kuralina_uyuyor()
        {
            // /catalog/{everything} -> /api/catalog/{everything}: gateway yapılandırması
            // değişmeden geçebilmesi bu ön eke bağlı.
            var rota = typeof(AgentController).GetCustomAttribute<RouteAttribute>();
            Assert.Equal("api/catalog/agent", rota!.Template);
        }
    }
}
