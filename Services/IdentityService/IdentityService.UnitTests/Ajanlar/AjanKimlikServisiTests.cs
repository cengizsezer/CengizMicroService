using IdentityService.Application.Models.Agent;
using IdentityService.Application.Services.Agent;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace IdentityService.UnitTests.Ajanlar
{
    /// <summary>
    /// Ajan anahtarının üretimi ve token'a çevrilmesi.
    ///
    /// Buradaki en önemli test <see cref="Ham_anahtar_veritabaninda_hicbir_alanda_durmuyor"/>:
    /// anahtarın saklanmaması bu modülün varlık sebebi — ofisteki makine fiziksel
    /// olarak erişilebilir bir yerde, ve sunucu tarafında da kaybolan bir anahtarı
    /// geri veremiyor olmamız gerekiyor.
    /// </summary>
    public class AjanKimlikServisiTests
    {
        private static YeniAjanIstegi Istek(string ad = "Ofis Banka PC", DateTime? bitis = null)
            => new() { Ad = ad, GecerlilikBitisi = bitis };

        [Fact]
        public async Task Anahtar_uretiliyor_ve_yalniz_hash_i_saklaniyor()
        {
            using var db = AjanTestKurulumu.Db();
            var servis = AjanTestKurulumu.Servis(db, new SahteSaat());

            var yeni = await servis.OlusturAsync(Istek(), olusturanKullaniciId: 3);

            var kayit = await db.Ajanlar.SingleAsync();
            Assert.StartsWith(AjanAnahtari.OnEk, yeni.Anahtar);
            Assert.Equal(yeni.Anahtar[..AjanAnahtari.OnEkUzunlugu], kayit.AnahtarOnEki);
            Assert.NotEqual(yeni.Anahtar, kayit.AnahtarHash);
            Assert.NotEmpty(kayit.AnahtarHash);
            Assert.Equal(3, kayit.OlusturanKullaniciId);
            Assert.True(kayit.Aktif);
        }

        [Fact]
        public async Task Ham_anahtar_veritabaninda_hicbir_alanda_durmuyor()
        {
            using var db = AjanTestKurulumu.Db();
            var servis = AjanTestKurulumu.Servis(db, new SahteSaat());

            var yeni = await servis.OlusturAsync(Istek(), olusturanKullaniciId: 1);

            // Anahtarın önek dışındaki kısmı hiçbir metin alanında geçmemeli.
            var govde = yeni.Anahtar[AjanAnahtari.OnEkUzunlugu..];
            var kayit = await db.Ajanlar.SingleAsync();
            var metinler = new[] { kayit.Ad, kayit.AnahtarHash, kayit.AnahtarOnEki, kayit.IptalNedeni ?? "" };

            Assert.All(metinler, m => Assert.DoesNotContain(govde, m, StringComparison.Ordinal));
        }

        [Fact]
        public async Task Her_anahtar_farkli()
        {
            using var db = AjanTestKurulumu.Db();
            var servis = AjanTestKurulumu.Servis(db, new SahteSaat());

            var a = await servis.OlusturAsync(Istek("A"), 1);
            var b = await servis.OlusturAsync(Istek("B"), 1);

            Assert.NotEqual(a.Anahtar, b.Anahtar);
        }

        [Fact]
        public async Task Gecerli_anahtarla_token_aliniyor()
        {
            var saat = new SahteSaat();
            using var db = AjanTestKurulumu.Db();
            var servis = AjanTestKurulumu.Servis(db, saat);
            var yeni = await servis.OlusturAsync(Istek(), 1);

            var token = await servis.TokenUretAsync(yeni.Anahtar);

            Assert.NotNull(token);
            Assert.Equal(yeni.Id, token!.AjanId);
            Assert.Equal("Ofis Banka PC", token.AjanAdi);
            Assert.Equal(saat.GetUtcNow().UtcDateTime.AddHours(8), token.GecerlilikBitisiUtc);
        }

        [Fact]
        public async Task Token_ajan_claimlerini_ve_sekiz_saatlik_omru_tasiyor()
        {
            var saat = new SahteSaat();
            using var db = AjanTestKurulumu.Db();
            var servis = AjanTestKurulumu.Servis(db, saat);
            var yeni = await servis.OlusturAsync(Istek(), 1);

            var token = await servis.TokenUretAsync(yeni.Anahtar);
            var jeton = new JwtSecurityTokenHandler().ReadJwtToken(token!.Token);

            Assert.Equal(AjanClaimleri.AjanTipi, jeton.Payload[AjanClaimleri.Tip]);
            Assert.Equal(yeni.Id.ToString(), jeton.Payload[AjanClaimleri.AjanId]);

            // sub bir kullanıcı değil: ajan token'ı insan token'ının yerine geçmesin.
            Assert.Equal($"ajan-{yeni.Id}", jeton.Payload["sub"]);

            Assert.Equal(TimeSpan.FromHours(8), AjanKimlikServisi.TokenOmru);
            Assert.Equal(8 * 60 * 60, (long)(EpochSaniye(jeton, "exp") - EpochSaniye(jeton, "nbf")));
        }

        [Fact]
        public async Task Token_ayni_imza_issuer_ve_audience_ile_dogrulanabiliyor()
        {
            // Kullanıcı token'ını doğrulayan servisler ajan token'ını da
            // doğrulayabilmeli; ayrım imzada değil claim'lerde.
            using var db = AjanTestKurulumu.Db();
            var servis = AjanTestKurulumu.Servis(db, new SahteSaat(DateTimeOffset.UtcNow));
            var yeni = await servis.OlusturAsync(Istek(), 1);
            var token = await servis.TokenUretAsync(yeni.Anahtar);

            var sonuc = await new JsonWebTokenHandler().ValidateTokenAsync(token!.Token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = AjanTestKurulumu.Issuer,
                ValidateAudience = true,
                ValidAudience = AjanTestKurulumu.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AjanTestKurulumu.ImzaAnahtari)),
                ValidateLifetime = true
            });

            Assert.True(sonuc.IsValid, sonuc.Exception?.Message);
        }

        [Fact]
        public async Task Gecersiz_anahtar_reddediliyor()
        {
            using var db = AjanTestKurulumu.Db();
            var servis = AjanTestKurulumu.Servis(db, new SahteSaat());
            await servis.OlusturAsync(Istek(), 1);

            Assert.Null(await servis.TokenUretAsync(AjanAnahtari.Uret()));
            Assert.Null(await servis.TokenUretAsync("saçma"));
            Assert.Null(await servis.TokenUretAsync(""));
        }

        [Fact]
        public async Task Oneki_tutan_ama_govdesi_tutmayan_anahtar_reddediliyor()
        {
            // Önek yalnız aday daraltıyor; kararı hash veriyor.
            using var db = AjanTestKurulumu.Db();
            var servis = AjanTestKurulumu.Servis(db, new SahteSaat());
            var yeni = await servis.OlusturAsync(Istek(), 1);

            var sahte = yeni.Anahtar[..AjanAnahtari.OnEkUzunlugu] + "bambaskabirgovde";

            Assert.Null(await servis.TokenUretAsync(sahte));
        }

        [Fact]
        public async Task Iptal_edilmis_anahtarla_token_alinamiyor()
        {
            var saat = new SahteSaat();
            using var db = AjanTestKurulumu.Db();
            var servis = AjanTestKurulumu.Servis(db, saat);
            var yeni = await servis.OlusturAsync(Istek(), 1);

            Assert.True(await servis.IptalEtAsync(yeni.Id, "Makine değişti"));

            Assert.Null(await servis.TokenUretAsync(yeni.Anahtar));

            var kayit = await db.Ajanlar.SingleAsync();
            Assert.False(kayit.Aktif);
            Assert.Equal("Makine değişti", kayit.IptalNedeni);
            Assert.Equal(saat.GetUtcNow().UtcDateTime, kayit.IptalZamani);
        }

        [Fact]
        public async Task Ikinci_iptal_ilk_iptalin_kaydini_bozmuyor()
        {
            var saat = new SahteSaat();
            using var db = AjanTestKurulumu.Db();
            var servis = AjanTestKurulumu.Servis(db, saat);
            var yeni = await servis.OlusturAsync(Istek(), 1);
            await servis.IptalEtAsync(yeni.Id, "İlk neden");
            var ilkZaman = (await db.Ajanlar.SingleAsync()).IptalZamani;

            saat.Ilerle(TimeSpan.FromHours(2));
            await servis.IptalEtAsync(yeni.Id, "İkinci neden");

            var kayit = await db.Ajanlar.SingleAsync();
            Assert.Equal("İlk neden", kayit.IptalNedeni);
            Assert.Equal(ilkZaman, kayit.IptalZamani);
        }

        [Fact]
        public async Task Olmayan_ajan_iptal_edilemiyor()
        {
            using var db = AjanTestKurulumu.Db();
            var servis = AjanTestKurulumu.Servis(db, new SahteSaat());

            Assert.False(await servis.IptalEtAsync(404, "yok"));
        }

        [Fact]
        public async Task Suresi_dolmus_anahtarla_token_alinamiyor()
        {
            var saat = new SahteSaat();
            using var db = AjanTestKurulumu.Db();
            var servis = AjanTestKurulumu.Servis(db, saat);
            var bitis = saat.GetUtcNow().UtcDateTime.AddDays(1);
            var yeni = await servis.OlusturAsync(Istek(bitis: bitis), 1);

            Assert.NotNull(await servis.TokenUretAsync(yeni.Anahtar));

            saat.Ilerle(TimeSpan.FromDays(1));

            Assert.Null(await servis.TokenUretAsync(yeni.Anahtar));
        }

        [Fact]
        public async Task Gecerlilik_bitisi_bos_birakilan_anahtar_suresiz()
        {
            var saat = new SahteSaat();
            using var db = AjanTestKurulumu.Db();
            var servis = AjanTestKurulumu.Servis(db, saat);
            var yeni = await servis.OlusturAsync(Istek(), 1);

            saat.Ilerle(TimeSpan.FromDays(365));

            Assert.NotNull(await servis.TokenUretAsync(yeni.Anahtar));
        }

        [Fact]
        public async Task Ayni_anahtarla_iki_kez_token_alinabiliyor()
        {
            // Ajan yeniden başladığında ya da bağlantı koptuğunda aynı anahtarla
            // yeniden token alacak; anahtar tek kullanımlık değil.
            var saat = new SahteSaat();
            using var db = AjanTestKurulumu.Db();
            var servis = AjanTestKurulumu.Servis(db, saat);
            var yeni = await servis.OlusturAsync(Istek(), 1);

            var ilk = await servis.TokenUretAsync(yeni.Anahtar);
            saat.Ilerle(TimeSpan.FromHours(9));
            var ikinci = await servis.TokenUretAsync(yeni.Anahtar);

            Assert.NotNull(ilk);
            Assert.NotNull(ikinci);
            Assert.NotEqual(ilk!.GecerlilikBitisiUtc, ikinci!.GecerlilikBitisiUtc);
        }

        [Fact]
        public async Task Son_kullanim_token_alindikca_ilerliyor()
        {
            var saat = new SahteSaat();
            using var db = AjanTestKurulumu.Db();
            var servis = AjanTestKurulumu.Servis(db, saat);
            var yeni = await servis.OlusturAsync(Istek(), 1);
            Assert.Null((await db.Ajanlar.SingleAsync()).SonKullanim);

            saat.Ilerle(TimeSpan.FromMinutes(5));
            await servis.TokenUretAsync(yeni.Anahtar);

            Assert.Equal(saat.GetUtcNow().UtcDateTime, (await db.Ajanlar.SingleAsync()).SonKullanim);
        }

        [Fact]
        public async Task Liste_durumu_sunucuda_karara_baglaniyor()
        {
            var saat = new SahteSaat();
            using var db = AjanTestKurulumu.Db();
            var servis = AjanTestKurulumu.Servis(db, saat);

            await servis.OlusturAsync(Istek("Aktif olan"), 1);
            var iptalli = await servis.OlusturAsync(Istek("Bir iptalli"), 1);
            await servis.OlusturAsync(Istek("Suresi dolan", bitis: saat.GetUtcNow().UtcDateTime.AddHours(1)), 1);
            await servis.IptalEtAsync(iptalli.Id, "Makine hurdaya çıktı");

            saat.Ilerle(TimeSpan.FromHours(2));
            var liste = await servis.ListeleAsync();

            Assert.Equal(new[] { "Aktif olan", "Bir iptalli", "Suresi dolan" }, liste.Select(x => x.Ad));
            Assert.Equal(new[] { "Aktif", "İptal", "Süresi doldu" }, liste.Select(x => x.Durum));
        }

        [Fact]
        public async Task Liste_ham_anahtari_dondurmuyor()
        {
            using var db = AjanTestKurulumu.Db();
            var servis = AjanTestKurulumu.Servis(db, new SahteSaat());
            var yeni = await servis.OlusturAsync(Istek(), 1);

            var satir = Assert.Single(await servis.ListeleAsync());

            Assert.Equal(yeni.AnahtarOnEki, satir.AnahtarOnEki);
            Assert.DoesNotContain(yeni.Anahtar[AjanAnahtari.OnEkUzunlugu..], satir.AnahtarOnEki, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Adi_bos_ajan_olusturulamiyor()
        {
            using var db = AjanTestKurulumu.Db();
            var servis = AjanTestKurulumu.Servis(db, new SahteSaat());

            await Assert.ThrowsAsync<ArgumentException>(() => servis.OlusturAsync(Istek("   "), 1));
        }

        private static long EpochSaniye(JwtSecurityToken jeton, string ad)
            => Convert.ToInt64(jeton.Payload[ad]);
    }
}
