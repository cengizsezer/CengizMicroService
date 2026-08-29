using CatalogService.Api.Features.Ajanlar;
using CatalogService.Api.Features.Ajanlar.Dtos;
using CatalogService.Api.Features.Ajanlar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;

namespace CatalogService.UnitTests.Ajanlar
{
    /// <summary>
    /// Hub'ın kayıt kuralları. Gerçek bir soket kurulmuyor: <c>Hub.Context</c>
    /// yazılabilir olduğu için sahte bir bağlam yeterli — sınanan şey taşıma
    /// katmanı değil, kaydın kabul/ret kararı.
    /// </summary>
    public class AgentHubTests
    {
        private static AjanKaydiIstegi Istek(string surum = "1.0.0", string makineId = "MAK-1") => new()
        {
            MakineId = makineId,
            MakineAdi = "BANKA-PC",
            AjanSurumu = surum,
            IsletimSistemi = "Windows 11",
            OrkaCalisiyorMu = true
        };

        private static (AgentHub Hub, AjanDeposu Depo, SahteHubBaglami Baglam) Kur(
            AgentHubAyarlari? ayar = null, SahteSaat? saat = null,
            string connectionId = "c1", string? kullaniciId = "kullanici-1")
        {
            ayar ??= AjanTestVerisi.Ayarlar();
            var izleyici = new SabitAyar<AgentHubAyarlari>(ayar);
            var depo = new AjanDeposu(izleyici, saat ?? new SahteSaat());
            var baglam = new SahteHubBaglami(connectionId, kullaniciId);
            var hub = new AgentHub(depo, izleyici, NullLogger<AgentHub>.Instance) { Context = baglam };
            return (hub, depo, baglam);
        }

        [Fact]
        public async Task Gecerli_surumle_kayit_kabul_ediliyor_ve_depoda_gorunuyor()
        {
            var (hub, depo, _) = Kur(AjanTestVerisi.Ayarlar(asgari: "1.0.0"));

            var sonuc = await hub.Kaydol(Istek("1.0.0"));

            Assert.True(sonuc.Kabul);
            var ajan = Assert.Single(depo.Baglilar());
            Assert.Equal("MAK-1", ajan.MakineId);
            Assert.Equal("BANKA-PC", ajan.MakineAdi);
            Assert.Equal("1.0.0", ajan.AjanSurumu);
            Assert.Equal("Windows 11", ajan.IsletimSistemi);
            Assert.True(ajan.OrkaCalisiyorMu);
        }

        [Fact]
        public async Task Sonuc_her_zaman_sunucu_ve_asgari_surumu_tasiyor()
        {
            var (hub, _, _) = Kur(AjanTestVerisi.Ayarlar(asgari: "1.1.0", sunucu: "1.4.2"));

            var kabul = await hub.Kaydol(Istek("1.1.0"));
            var ret = await hub.Kaydol(Istek("1.0.0"));

            foreach (var sonuc in new[] { kabul, ret })
            {
                Assert.Equal("1.4.2", sonuc.SunucuSurumu);
                Assert.Equal("1.1.0", sonuc.AsgariAjanSurumu);
            }
        }

        [Fact]
        public async Task Eski_surumle_kayit_reddediliyor_ve_depoya_yazilmiyor()
        {
            var (hub, depo, _) = Kur(AjanTestVerisi.Ayarlar(asgari: "1.2.0"));

            var sonuc = await hub.Kaydol(Istek("1.1.9"));

            Assert.False(sonuc.Kabul);
            Assert.Contains("1.2.0", sonuc.Mesaj);
            Assert.Contains("güncelleyin", sonuc.Mesaj, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(depo.Baglilar());
        }

        [Fact]
        public async Task Okunamayan_surum_metni_reddediliyor()
        {
            var (hub, depo, _) = Kur();

            var sonuc = await hub.Kaydol(Istek("deneme-surumu"));

            Assert.False(sonuc.Kabul);
            Assert.Contains("Ajan sürümü okunamadı", sonuc.Mesaj);
            Assert.Empty(depo.Baglilar());
        }

        [Fact]
        public async Task MakineId_bos_ise_kayit_reddediliyor()
        {
            var (hub, depo, _) = Kur();

            var sonuc = await hub.Kaydol(Istek(makineId: "   "));

            Assert.False(sonuc.Kabul);
            Assert.Empty(depo.Baglilar());
        }

        [Fact]
        public async Task Kaydin_sahibi_token_daki_kullanici()
        {
            // İstekte kullanıcı alanı yok; olsaydı da ona güvenilmezdi. "Kim hangi
            // makineye iş gönderebilir" kuralı bu alana dayanacak.
            var (hub, depo, _) = Kur(kullaniciId: "smmm-42");

            await hub.Kaydol(Istek());

            Assert.Equal("smmm-42", Assert.Single(depo.Baglilar()).KullaniciId);
        }

        [Fact]
        public async Task Ayni_makineyle_ikinci_baglanti_eskisinin_soketini_kapatiyor()
        {
            var ayar = AjanTestVerisi.Ayarlar();
            var izleyici = new SabitAyar<AgentHubAyarlari>(ayar);
            var depo = new AjanDeposu(izleyici, new SahteSaat());

            var eskiBaglam = new SahteHubBaglami("c1");
            var eskiHub = new AgentHub(depo, izleyici, NullLogger<AgentHub>.Instance) { Context = eskiBaglam };
            await eskiHub.Kaydol(Istek());

            var yeniBaglam = new SahteHubBaglami("c2");
            var yeniHub = new AgentHub(depo, izleyici, NullLogger<AgentHub>.Instance) { Context = yeniBaglam };
            await yeniHub.Kaydol(Istek());

            Assert.True(eskiBaglam.Kesildi);
            Assert.False(yeniBaglam.Kesildi);
            Assert.Equal("c2", Assert.Single(depo.Baglilar()).ConnectionId);
        }

        [Fact]
        public async Task Kalp_atisi_son_atisi_guncelliyor()
        {
            var saat = new SahteSaat();
            var (hub, depo, _) = Kur(saat: saat);
            await hub.Kaydol(Istek());
            var ilk = depo.Baglilar()[0].SonKalpAtisi;

            saat.Ilerle(TimeSpan.FromSeconds(45));
            await hub.KalpAtisi();

            Assert.Equal(ilk.AddSeconds(45), depo.Baglilar()[0].SonKalpAtisi);
        }

        [Fact]
        public async Task Kaydolmamis_baglantidan_gelen_kalp_atisi_patlamiyor()
        {
            var (hub, depo, _) = Kur();

            await hub.KalpAtisi();

            Assert.Empty(depo.Baglilar());
        }

        [Fact]
        public async Task Baglanti_kopunca_depodan_siliniyor()
        {
            var (hub, depo, _) = Kur();
            await hub.Kaydol(Istek());

            await hub.OnDisconnectedAsync(null);

            Assert.Empty(depo.Baglilar());
        }

        [Fact]
        public void Hub_yetkilendirme_istiyor()
        {
            // Token'sız bağlantı reddi taşıma katmanında gerçekleşiyor; burada
            // sınanan, o kapının hiç konmadan unutulmamış olması.
            Assert.NotNull(typeof(AgentHub).GetCustomAttribute<AuthorizeAttribute>());
        }

        [Fact]
        public void Hub_yolu_agenthub()
        {
            // nginx bloğu ve ajan yapılandırması bu sabite bakıyor.
            Assert.Equal("/agenthub", AgentHub.Yol);
        }
    }
}
