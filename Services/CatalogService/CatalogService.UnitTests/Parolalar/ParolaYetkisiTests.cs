using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using CatalogService.Api.Features.Ajanlar;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.Parolalar;
using CatalogService.Api.Features.Parolalar.Controllers;
using CatalogService.Api.Features.Parolalar.Domain;
using CatalogService.Api.Features.Parolalar.Dtos;
using CatalogService.Api.Features.Parolalar.Services;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.Ajanlar;
using CatalogService.UnitTests.BankaEkstre;
using CatalogService.UnitTests.Muhasebe;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using JsonWebTokenHandler = Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler;
using Microsoft.IdentityModel.Tokens;

namespace CatalogService.UnitTests.Parolalar
{
    /// <summary>
    /// Parolalar sunucu yetkisi: tüm uçlar insan + pkf rolü ister. Rol claim'i IdentityService'in bastığı gibi
    /// (ClaimTypes.Role + "role") Catalog'un doğrulamasından geçince tanınır. Rolsüz kullanıcı 403 alır ve erişim
    /// kaydına "reddedildi" yazılır (şifresiz). Firmalarım global olduğu için kapsam (kiracı/kullanıcı) yok.
    /// </summary>
    public class ParolaYetkisiTests
    {
        private const string Anahtar = "parola-yetki-testi-icin-yalniz-test-anahtari-32+";
        private const string Issuer = "test-issuer";
        private const string Audience = "test-audience";

        private static IAuthorizationService Yetkilendirme()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthorization(AjanPolitikalari.Ekle);
            services.AddAuthorization(ParolaYetkisi.Ekle);
            return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
        }

        /// <summary>IdentityService.GenerateJwtToken gibi basar, Catalog gibi (JsonWebTokenHandler, MapInboundClaims) doğrular.</summary>
        private static async Task<ClaimsPrincipal> Kullanici(params string[] roller)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, "42"),
                new(JwtRegisteredClaimNames.UniqueName, "ayse"),
                new(ClaimTypes.NameIdentifier, "42"),
                new(ClaimTypes.Name, "ayse"),
                new("tn", "500")
            };
            foreach (var r in roller)
            {
                claims.Add(new Claim(ClaimTypes.Role, r));
                claims.Add(new Claim("role", r));
            }
            return await Dogrula(claims);
        }

        private static async Task<ClaimsPrincipal> Dogrula(IEnumerable<Claim> claims)
        {
            var anahtar = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Anahtar));
            var yazili = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                Issuer, Audience, claims, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(20),
                new SigningCredentials(anahtar, SecurityAlgorithms.HmacSha256)));

            var sonuc = await new JsonWebTokenHandler { MapInboundClaims = true }.ValidateTokenAsync(yazili, new TokenValidationParameters
            {
                ValidIssuer = Issuer,
                ValidAudience = Audience,
                IssuerSigningKey = anahtar
            });
            Assert.True(sonuc.IsValid, sonuc.Exception?.Message);
            return new ClaimsPrincipal(sonuc.ClaimsIdentity);
        }

        [Fact]
        public async Task Pkf_rollu_kullanici_gecer_rolsuz_ve_baska_rollu_kullanici_gecemez()
        {
            var y = Yetkilendirme();

            Assert.True((await y.AuthorizeAsync(await Kullanici("pkf"), null, ParolaYetkisi.Politika)).Succeeded);
            Assert.True((await y.AuthorizeAsync(await Kullanici("MaliIsler", "pkf"), null, ParolaYetkisi.Politika)).Succeeded);

            Assert.False((await y.AuthorizeAsync(await Kullanici(), null, ParolaYetkisi.Politika)).Succeeded);
            Assert.False((await y.AuthorizeAsync(await Kullanici("MaliIsler"), null, ParolaYetkisi.Politika)).Succeeded);
            Assert.False((await y.AuthorizeAsync(await Kullanici("Admin"), null, ParolaYetkisi.Politika)).Succeeded);
            Assert.False((await y.AuthorizeAsync(await Kullanici("PKFX"), null, ParolaYetkisi.Politika)).Succeeded);
            Assert.False((await y.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, ParolaYetkisi.Politika)).Succeeded);
        }

        [Fact]
        public async Task Ajan_tokeni_pkf_rolu_tasisa_bile_gecemez()
        {
            var ajan = await Dogrula(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, "ajan-7"),
                new Claim(AjanKimligi.TipClaim, AjanKimligi.AjanTipi),
                new Claim(AjanKimligi.AjanIdClaim, "7"),
                new Claim("role", "pkf")
            });

            Assert.False((await Yetkilendirme().AuthorizeAsync(ajan, null, ParolaYetkisi.Politika)).Succeeded);
        }

        [Fact]
        public void Controllerin_tum_uclari_parola_politikasinda_acik_kapi_yok()
        {
            var tip = typeof(ParolalarController);
            var sinif = tip.GetCustomAttributes<AuthorizeAttribute>().ToList();
            Assert.Equal(ParolaYetkisi.Politika, Assert.Single(sinif).Policy);

            var uclar = tip.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes().Any(a => a is Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute))
                .ToList();
            Assert.Equal(5, uclar.Count);   // liste, ekle, güncelle, sil, alan
            Assert.All(uclar, m =>
            {
                Assert.Empty(m.GetCustomAttributes<AllowAnonymousAttribute>());
                Assert.Empty(m.GetCustomAttributes<AuthorizeAttribute>());   // sınıfınkini gevşeten ek kural yok
            });
            Assert.Empty(tip.GetCustomAttributes<AllowAnonymousAttribute>());
        }

        // ---- 403 → "reddedildi" erişim kaydı ----

        private sealed class SahteKimlikDogrulama : IAuthenticationService
        {
            public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.NoResult());
            public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) { context.Response.StatusCode = 401; return Task.CompletedTask; }
            public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) { context.Response.StatusCode = 403; return Task.CompletedTask; }
            public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;
            public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
        }

        private static (CatalogContext Db, ParolaService Servis) Ortam()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            db.Firmalar.Add(new Firma { Id = 5, Unvan = "LIME LTD.", KisaAd = "LIME", VergiKimlikNo = "2222222222", Aktif = true });
            db.SaveChanges();
            var servis = new ParolaService(db, new ParolaSifreleyici(new EphemeralDataProtectionProvider()),
                                           new SahteSaat(new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero)), new SabitKullanici(23));
            return (db, servis);
        }

        private static DefaultHttpContext Istek(IParolaService servis, bool parolaUcu, string? id, string? alan)
        {
            var sp = new ServiceCollection()
                .AddSingleton(servis)
                .AddSingleton<IAuthenticationService, SahteKimlikDogrulama>()
                .BuildServiceProvider();
            var ctx = new DefaultHttpContext { RequestServices = sp };
            ctx.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
                new EndpointMetadataCollection(new AuthorizeAttribute { Policy = parolaUcu ? ParolaYetkisi.Politika : null }), "uc"));
            if (id is not null) ctx.Request.RouteValues["id"] = id;
            if (alan is not null) ctx.Request.RouteValues["alan"] = alan;
            return ctx;
        }

        private static AuthorizationPolicy Politika() => new AuthorizationPolicyBuilder().RequireRole(ParolaYetkisi.Rol).Build();

        [Fact]
        public async Task Rolsuz_kullanici_alan_ucunda_403_alir_reddedildi_kaydi_yazilir_sifresiz()
        {
            var (db, servis) = Ortam();
            var kayit = await servis.EkleAsync(new ParolaKaydetDto
            {
                Tur = ParolaTuru.DijitalVergiDairesi, FirmaId = 5, KullaniciAdi = "10000005", Parola = "Gizli-P-1", Sifre = "Gizli-S-2"
            });

            var ctx = Istek(servis, parolaUcu: true, kayit.Id.ToString(), "parola");
            var sonrakiCagrildi = false;
            await new ParolaReddiKaydedici(NullLogger<ParolaReddiKaydedici>.Instance)
                .HandleAsync(_ => { sonrakiCagrildi = true; return Task.CompletedTask; }, ctx, Politika(), PolicyAuthorizationResult.Forbid());

            Assert.Equal(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
            Assert.False(sonrakiCagrildi);                                       // controller'a hiç ulaşılmadı

            var red = await db.ParolaErisimKayitlari.AsNoTracking().SingleAsync(k => k.Islem == ParolaIslemleri.Reddedildi);
            Assert.Equal(kayit.Id, red.ParolaId);
            Assert.Equal("parola", red.Alan);
            Assert.Equal(ParolaTuru.DijitalVergiDairesi, red.Tur);
            Assert.Equal("LIME", red.FirmaAdi);
            Assert.Equal(23, red.KullaniciId);
            Assert.Equal("test", red.Kullanici);

            var json = System.Text.Json.JsonSerializer.Serialize(await db.ParolaErisimKayitlari.ToListAsync());
            Assert.DoesNotContain("Gizli-P-1", json);
            Assert.DoesNotContain("Gizli-S-2", json);
        }

        [Fact]
        public async Task Rolsuz_kullanici_listede_403_kayit_id_siz_reddedildi()
        {
            var (db, servis) = Ortam();
            var ctx = Istek(servis, parolaUcu: true, id: null, alan: null);

            await new ParolaReddiKaydedici(NullLogger<ParolaReddiKaydedici>.Instance)
                .HandleAsync(_ => Task.CompletedTask, ctx, Politika(), PolicyAuthorizationResult.Forbid());

            Assert.Equal(403, ctx.Response.StatusCode);
            var red = await db.ParolaErisimKayitlari.SingleAsync();
            Assert.Equal(ParolaIslemleri.Reddedildi, red.Islem);
            Assert.Equal(0, red.ParolaId);
            Assert.Null(red.Alan);
            Assert.Equal(string.Empty, red.FirmaAdi);
        }

        [Fact]
        public async Task Pkf_kullanici_icin_istek_controllera_gecer_reddedildi_kaydi_yok()
        {
            var (db, servis) = Ortam();
            var ctx = Istek(servis, parolaUcu: true, "1", "parola");
            var sonrakiCagrildi = false;

            await new ParolaReddiKaydedici(NullLogger<ParolaReddiKaydedici>.Instance)
                .HandleAsync(_ => { sonrakiCagrildi = true; return Task.CompletedTask; }, ctx, Politika(), PolicyAuthorizationResult.Success());

            Assert.True(sonrakiCagrildi);
            Assert.Equal(200, ctx.Response.StatusCode);
            Assert.Empty(await db.ParolaErisimKayitlari.ToListAsync());
        }

        [Fact]
        public async Task Baska_uclardaki_403_parola_erisim_kaydina_yazilmaz()
        {
            var (db, servis) = Ortam();
            var ctx = Istek(servis, parolaUcu: false, "1", null);

            await new ParolaReddiKaydedici(NullLogger<ParolaReddiKaydedici>.Instance)
                .HandleAsync(_ => Task.CompletedTask, ctx, Politika(), PolicyAuthorizationResult.Forbid());

            Assert.Equal(403, ctx.Response.StatusCode);
            Assert.Empty(await db.ParolaErisimKayitlari.ToListAsync());
        }
    }
}
