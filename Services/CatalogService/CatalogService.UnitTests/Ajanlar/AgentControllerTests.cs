using CatalogService.Api.Features.Ajanlar;
using CatalogService.Api.Features.Ajanlar.Controllers;
using CatalogService.Api.Features.Ajanlar.Domain;
using CatalogService.Api.Features.Ajanlar.Dtos;
using CatalogService.Api.Features.Ajanlar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;

namespace CatalogService.UnitTests.Ajanlar
{
    public class AgentControllerTests
    {
        private static (AgentController Controller, AjanDeposu Depo) Kur(SahteSaat saat)
        {
            var depo = new AjanDeposu(new SabitAyar<AgentHubAyarlari>(AjanTestVerisi.Ayarlar()), saat);
            return (new AgentController(depo, NullLogger<AgentController>.Instance), depo);
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
                AjanId = "7",
                OrkaCalisiyorMu = false
            });

            var satir = Assert.Single(Liste(controller.Baglilar()));

            Assert.Equal("MAK-1", satir.MakineId);
            Assert.Equal("BANKA-PC", satir.MakineAdi);
            Assert.Equal("1.0.0", satir.AjanSurumu);
            Assert.Equal("Windows 11", satir.IsletimSistemi);
            Assert.Equal("7", satir.AjanId);
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
        public void Uc_yalniz_insan_tokenini_kabul_ediyor()
        {
            // Durum ucu yönetim ekranının kaynağı; ajanın kendi listesini okuması
            // için bir neden yok. Politikanın adı burada sabitleniyor, davranışı
            // AjanPolitikalariTests'te.
            var yetki = typeof(AgentController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.Equal(AjanPolitikalari.YalnizInsan, yetki!.Policy);
        }

        [Fact]
        public void Dusurme_ucu_admin_ve_insan_tokeni_istiyor()
        {
            var yetki = typeof(AgentController)
                .GetMethod(nameof(AgentController.Dusur))!
                .GetCustomAttribute<AuthorizeAttribute>();

            Assert.Equal(AjanPolitikalari.YalnizInsan, yetki!.Policy);
            Assert.Equal("Admin", yetki.Roles);
        }

        [Fact]
        public void Dusurme_ajanin_acik_baglantisini_kapatiyor()
        {
            var (controller, depo) = Kur(new SahteSaat());
            var kesildi = false;
            depo.Kaydet(new AjanKaydi
            {
                MakineId = "MAK-1",
                ConnectionId = "c1",
                MakineAdi = "BANKA-PC",
                AjanId = "7",
                BaglantiyiKes = () => kesildi = true
            });

            var kac = Assert.IsType<OkObjectResult>(controller.Dusur("7").Result).Value;

            Assert.Equal(1, kac);
            Assert.True(kesildi);
            Assert.Empty(Liste(controller.Baglilar()));
        }

        [Fact]
        public void Dusurme_baska_ajanin_baglantisina_dokunmuyor()
        {
            var (controller, depo) = Kur(new SahteSaat());
            depo.Kaydet(new AjanKaydi { MakineId = "MAK-1", ConnectionId = "c1", MakineAdi = "A", AjanId = "7" });

            var kac = Assert.IsType<OkObjectResult>(controller.Dusur("8").Result).Value;

            Assert.Equal(0, kac);
            Assert.Single(Liste(controller.Baglilar()));
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
