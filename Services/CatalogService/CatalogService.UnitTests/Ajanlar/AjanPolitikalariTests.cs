using CatalogService.Api.Features.Ajanlar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CatalogService.UnitTests.Ajanlar
{
    /// <summary>
    /// Hub'a yalnız ajan, durum ucuna yalnız insan girebilsin kuralı.
    ///
    /// Politikalar niyet ifadesi değil davranış: burada gerçekten
    /// <see cref="IAuthorizationService"/> kurulup değerlendiriliyor.
    ///
    /// Son iki test token'ın basılıp doğrulandığı yolun tamamını dolaşıyor.
    /// Sebebi: kararın dayandığı claim'in doğrulamadan sağ çıkacağı
    /// <b>varsayılamaz</b> — JwtBearer gelen kısa claim adlarının bir kısmını uzun
    /// URI'lere çeviriyor. Token IdentityService'teki gibi
    /// <see cref="JwtSecurityTokenHandler"/> ile basılıyor, .NET 8'de JwtBearer'ın
    /// kullandığı <see cref="JsonWebTokenHandler"/> ile doğrulanıyor.
    /// </summary>
    public class AjanPolitikalariTests
    {
        private const string ImzaAnahtari = "super_secret_dev_key_32bytes_minimum";
        private const string Issuer = "identityserver.tr";
        private const string Audience = "identityclient.tr";

        private static IAuthorizationService Yetkilendirme()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthorization(AjanPolitikalari.Ekle);
            return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
        }

        private static ClaimsPrincipal Ajan(string ajanId = "7") =>
            new(new ClaimsIdentity(new[]
            {
                new Claim(AjanKimligi.TipClaim, AjanKimligi.AjanTipi),
                new Claim(AjanKimligi.AjanIdClaim, ajanId)
            }, "Test"));

        private static ClaimsPrincipal Kullanici(params string[] izinler)
        {
            var claimler = new List<Claim>
            {
                new("sub", "42"),
                new(ClaimTypes.NameIdentifier, "42"),
                new("tn", "0001")
            };

            claimler.AddRange(izinler.Select(i => new Claim(AjanPolitikalari.IzinClaim, i)));

            return new ClaimsPrincipal(new ClaimsIdentity(claimler, "Test"));
        }

        [Fact]
        public async Task Hub_politikasi_ajan_tokenini_geciriyor()
        {
            var sonuc = await Yetkilendirme().AuthorizeAsync(Ajan(), null, AjanPolitikalari.YalnizAjan);

            Assert.True(sonuc.Succeeded);
        }

        [Fact]
        public async Task Hub_politikasi_kullanici_tokenini_reddediyor()
        {
            // Ajan olmayan bir istemcinin ajan gibi kaydolup iş emri beklemesini
            // engelleyen kural.
            var sonuc = await Yetkilendirme().AuthorizeAsync(Kullanici(), null, AjanPolitikalari.YalnizAjan);

            Assert.False(sonuc.Succeeded);
        }

        [Fact]
        public async Task Durum_ucu_kullanici_tokeniyle_calisiyor()
        {
            var sonuc = await Yetkilendirme().AuthorizeAsync(Kullanici(), null, AjanPolitikalari.YalnizInsan);

            Assert.True(sonuc.Succeeded);
        }

        [Fact]
        public async Task Durum_ucu_ajan_tokenini_reddediyor()
        {
            var sonuc = await Yetkilendirme().AuthorizeAsync(Ajan(), null, AjanPolitikalari.YalnizInsan);

            Assert.False(sonuc.Succeeded);
        }

        [Fact]
        public async Task Yonetim_politikasi_izinli_kullaniciyi_geciriyor()
        {
            var sonuc = await Yetkilendirme().AuthorizeAsync(
                Kullanici(AjanPolitikalari.AjanYonetimiDuzenle), null, AjanPolitikalari.YonetimiDuzenle);

            Assert.True(sonuc.Succeeded);
        }

        [Fact]
        public async Task Yonetim_politikasi_yalniz_gorme_iznini_reddediyor()
        {
            // Listeyi görmek bağlantı düşürme hakkı vermiyor.
            var sonuc = await Yetkilendirme().AuthorizeAsync(
                Kullanici(AjanPolitikalari.AjanYonetimiGoruntule), null, AjanPolitikalari.YonetimiDuzenle);

            Assert.False(sonuc.Succeeded);
        }

        [Fact]
        public async Task Yonetim_politikasi_admin_rolunu_izin_yerine_saymiyor()
        {
            // Yetki artık rolde değil izinde: rol adı tek başına kapıyı açmıyor.
            var admin = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("sub", "42"),
                new Claim("role", "Admin"),
                new Claim(ClaimTypes.Role, "Admin")
            }, "Test"));

            var sonuc = await Yetkilendirme().AuthorizeAsync(admin, null, AjanPolitikalari.YonetimiDuzenle);

            Assert.False(sonuc.Succeeded);
        }

        [Fact]
        public async Task Yonetim_politikasi_izinli_ajan_tokenini_reddediyor()
        {
            // Ajan token'ı bu claim'i hiç taşımıyor ama taşısa bile geçmemeli:
            // politika insan şartını da koşuyor.
            var ajanAmaIzinli = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(AjanKimligi.TipClaim, AjanKimligi.AjanTipi),
                new Claim(AjanKimligi.AjanIdClaim, "7"),
                new Claim(AjanPolitikalari.IzinClaim, AjanPolitikalari.AjanYonetimiDuzenle)
            }, "Test"));

            var sonuc = await Yetkilendirme().AuthorizeAsync(ajanAmaIzinli, null, AjanPolitikalari.YonetimiDuzenle);

            Assert.False(sonuc.Succeeded);
        }

        [Fact]
        public async Task Kimligi_dogrulanmamis_istek_iki_politikadan_da_gecemiyor()
        {
            var anonim = new ClaimsPrincipal(new ClaimsIdentity());

            var hub = await Yetkilendirme().AuthorizeAsync(anonim, null, AjanPolitikalari.YalnizAjan);
            var uc = await Yetkilendirme().AuthorizeAsync(anonim, null, AjanPolitikalari.YalnizInsan);

            Assert.False(hub.Succeeded);
            Assert.False(uc.Succeeded);
        }

        [Fact]
        public async Task Basilan_ajan_tokeni_dogrulamadan_gecince_hala_ajan()
        {
            var kimlik = await TokendanKimlik(new[]
            {
                new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, "ajan-7"),
                new Claim(AjanKimligi.TipClaim, AjanKimligi.AjanTipi),
                new Claim(AjanKimligi.AjanIdClaim, "7")
            });

            // Asıl sınanan: ajan_id doğrulamadan adı değişmeden çıkıyor.
            Assert.Equal("7", AjanKimligi.AjanId(kimlik));

            var hub = await Yetkilendirme().AuthorizeAsync(kimlik, null, AjanPolitikalari.YalnizAjan);
            var uc = await Yetkilendirme().AuthorizeAsync(kimlik, null, AjanPolitikalari.YalnizInsan);

            Assert.True(hub.Succeeded);
            Assert.False(uc.Succeeded);
        }

        [Fact]
        public async Task Basilan_kullanici_tokeni_dogrulamadan_gecince_ajan_sayilmiyor()
        {
            var kimlik = await TokendanKimlik(new[]
            {
                new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, "42"),
                new Claim("tn", "0001"),
                new Claim("role", "Admin")
            });

            Assert.False(AjanKimligi.AjanMi(kimlik));

            var hub = await Yetkilendirme().AuthorizeAsync(kimlik, null, AjanPolitikalari.YalnizAjan);
            var uc = await Yetkilendirme().AuthorizeAsync(kimlik, null, AjanPolitikalari.YalnizInsan);

            Assert.False(hub.Succeeded);
            Assert.True(uc.Succeeded);
        }

        /// <summary>
        /// Verilen claim'lerle token basar, CatalogService'in
        /// <c>TokenValidationParameters</c>'ının aynısıyla doğrular ve ortaya çıkan
        /// kimliği döner.
        ///
        /// Doğrulama <see cref="JsonWebTokenHandler"/> ile yapılıyor: .NET 8'de
        /// JwtBearer varsayılan olarak bunu kullanıyor.
        /// <see cref="JwtSecurityTokenHandler"/> bu çözümde okuma yapamıyor —
        /// <c>System.IdentityModel.Tokens.Jwt</c> 7.0.3 ile
        /// <c>Microsoft.IdentityModel.*</c> 8.x yan yana geliyor ve eski okuyucu
        /// <c>iss</c>/<c>exp</c>/<c>nbf</c>'i düşürüyor. Üretimde bu yola hiç
        /// girilmiyor; token basmak (WriteToken) etkilenmiyor.
        /// </summary>
        private static async Task<ClaimsPrincipal> TokendanKimlik(IEnumerable<Claim> claims)
        {
            var anahtar = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ImzaAnahtari));
            var jeton = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: new SigningCredentials(anahtar, SecurityAlgorithms.HmacSha256));

            var yazili = new JwtSecurityTokenHandler().WriteToken(jeton);

            // JwtBearerOptions.MapInboundClaims varsayılanı true; kısa adların
            // çevrilmesi bu testin asıl konusu olduğu için burada da açık.
            var okuyucu = new JsonWebTokenHandler { MapInboundClaims = true };

            var sonuc = await okuyucu.ValidateTokenAsync(yazili, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = anahtar,
                ValidateLifetime = true
            });

            Assert.True(sonuc.IsValid, sonuc.Exception?.Message);
            return new ClaimsPrincipal(sonuc.ClaimsIdentity);
        }
    }
}
