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
    /// <c>/auth/{everything}</c> ve <c>/auth/admin/{everything}</c> kuralları zaten
    /// varken bu ön ekler seçildiği için Ocelot'a yeni satır eklemek gerekmedi.
    /// Ön ek değişirse bu testler düşer — düşmesi de doğrusu.
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
        public void Yonetim_ucu_admin_istiyor_ve_admin_kuralindan_geciyor()
        {
            var rota = typeof(AdminAgentController).GetCustomAttribute<RouteAttribute>();
            var yetki = typeof(AdminAgentController).GetCustomAttribute<AuthorizeAttribute>();

            Assert.Equal("api/auth/admin/agents", rota!.Template);
            Assert.Equal("Admin", yetki!.Roles);
        }
    }
}
