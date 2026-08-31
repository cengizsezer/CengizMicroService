using IdentityService.Application.Services.Agent;
using IdentityService.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Reflection;

namespace IdentityService.UnitTests.Ajanlar
{
    /// <summary>
    /// Uçların adresi ve kapıları. Adresler gateway yapılandırmasına bağlı:
    /// <c>/auth/{everything}</c> kuralı zaten varken bu ön ek seçildiği için
    /// Ocelot'a yeni satır eklemek gerekmedi. Ön ek değişirse bu testler düşer —
    /// düşmesi de doğrusu.
    /// </summary>
    public class AjanUclariTests
    {
        [Fact]
        public void Token_ucu_gateway_in_auth_kuralindan_geciyor()
        {
            var rota = typeof(AgentAuthController).GetCustomAttribute<RouteAttribute>();

            Assert.Equal("api/auth/agent", rota!.Template);
        }

        [Fact]
        public void Token_ucu_anonim()
        {
            // Ajanın elinde token yok, anahtar var: bu uç kapalı olamaz.
            Assert.NotNull(typeof(AgentAuthController).GetCustomAttribute<AllowAnonymousAttribute>());
        }

        [Fact]
        public void Token_ucunda_hiz_siniri_var()
        {
            // Anonim uçta tek koruma anahtarın kendisi; sınır, deneme selinin
            // servisi PBKDF2 hesaplarıyla boğmasını engelliyor.
            var sinir = typeof(AgentAuthController)
                .GetMethod(nameof(AgentAuthController.Token))!
                .GetCustomAttribute<EnableRateLimitingAttribute>();

            Assert.Equal(AjanHizSiniri.Politika, sinir!.PolicyName);
        }

        [Fact]
        public void Yonetim_ucu_admin_onekinin_disinda()
        {
            // /auth/admin/{everything} gateway kuralı yola role=Admin şartı koyuyor;
            // uç orada kalsaydı izin tabanlı yetki hiç konuşulmadan 403 olurdu.
            var rota = typeof(AgentYonetimController).GetCustomAttribute<RouteAttribute>();

            Assert.Equal("api/auth/agents", rota!.Template);
        }

        [Fact]
        public void Yonetim_ucu_rol_degil_izin_istiyor()
        {
            var yetki = typeof(AgentYonetimController).GetCustomAttribute<AuthorizeAttribute>();

            Assert.Null(yetki!.Roles);
            Assert.Equal(AjanYetkileri.GoruntulePolitikasi, yetki.Policy);
        }

        [Theory]
        [InlineData(nameof(AgentYonetimController.Olustur))]
        [InlineData(nameof(AgentYonetimController.IptalEt))]
        public void Anahtar_ureten_ve_iptal_eden_uclar_ayri_izin_istiyor(string metot)
        {
            // Listeyi görmek anahtar üretme hakkı vermiyor: yazan uçlar Edit izninde.
            var yetki = typeof(AgentYonetimController)
                .GetMethod(metot)!
                .GetCustomAttribute<AuthorizeAttribute>();

            Assert.Equal(AjanYetkileri.DuzenlePolitikasi, yetki!.Policy);
        }

        [Fact]
        public void Izin_anahtarlari_catalog_tarafiyla_ayni()
        {
            // İki serviste ayrı ayrı yazılı; adlar birlikte değişmeli.
            Assert.Equal("AjanYonetimi.View", AjanYetkileri.Goruntule);
            Assert.Equal("AjanYonetimi.Edit", AjanYetkileri.Duzenle);
            Assert.Equal("perm", AjanYetkileri.IzinClaim);
        }
    }
}
