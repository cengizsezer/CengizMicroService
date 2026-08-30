using IdentityService.Application.Models.Agent;
using IdentityService.Domain.Entities;
using IdentityService.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace IdentityService.Application.Services.Agent
{
    /// <summary>
    /// Ajan anahtarlarının üretimi, doğrulanması ve ajan token'ının basılması.
    ///
    /// <b>Ham anahtar hiçbir yerde durmuyor:</b> üretildiği anda hash'lenip
    /// veritabanına hash'i yazılıyor, ham hâli yalnız oluşturma yanıtında bir kez
    /// dönüyor. Log satırlarına da girmiyor — başarısız denemede bile yazılan şey
    /// yalnızca önek.
    ///
    /// Hash için ASP.NET Identity'nin <see cref="IPasswordHasher{TUser}"/>'ı
    /// kullanılıyor: repoda parolalar da (UserManager üzerinden) onunla tutuluyor,
    /// yani tuz/iterasyon kararı tek yerde kalıyor. Düz SHA256 bilerek
    /// kullanılmadı — anahtar bir paroladır.
    /// </summary>
    public class AjanKimlikServisi : IAjanKimlikServisi
    {
        /// <summary>
        /// Kullanıcı token'ı 20 dakika; ajan token'ı 8 saat. Süresiz değil: iptal
        /// edilen bir ajanın elindeki token en fazla bu kadar yaşasın.
        /// </summary>
        public static readonly TimeSpan TokenOmru = TimeSpan.FromHours(8);

        private readonly IdentityDbContext _db;
        private readonly IPasswordHasher<Ajan> _hashleyici;
        private readonly IConfiguration _config;
        private readonly TimeProvider _saat;
        private readonly ILogger<AjanKimlikServisi> _log;

        public AjanKimlikServisi(
            IdentityDbContext db,
            IPasswordHasher<Ajan> hashleyici,
            IConfiguration config,
            TimeProvider saat,
            ILogger<AjanKimlikServisi> log)
        {
            _db = db;
            _hashleyici = hashleyici;
            _config = config;
            _saat = saat;
            _log = log;
        }

        public async Task<YeniAjanYaniti> OlusturAsync(
            YeniAjanIstegi istek, int olusturanKullaniciId, CancellationToken ct = default)
        {
            var ad = (istek?.Ad ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(ad))
                throw new ArgumentException("Ajan adı zorunlu.", nameof(istek));

            var hamAnahtar = AjanAnahtari.Uret();

            var ajan = new Ajan
            {
                Ad = ad,
                AnahtarOnEki = AjanAnahtari.OnEkiCikar(hamAnahtar),
                OlusturanKullaniciId = olusturanKullaniciId,
                OlusturmaZamani = _saat.GetUtcNow().UtcDateTime,
                GecerlilikBitisi = istek!.GecerlilikBitisi,
                Aktif = true
            };
            ajan.AnahtarHash = _hashleyici.HashPassword(ajan, hamAnahtar);

            _db.Ajanlar.Add(ajan);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Ajan oluşturuldu: {Ad} (#{Id}, önek {OnEk}), kullanıcı {KullaniciId}",
                ajan.Ad, ajan.Id, ajan.AnahtarOnEki, olusturanKullaniciId);

            return new YeniAjanYaniti
            {
                Id = ajan.Id,
                Ad = ajan.Ad,
                Anahtar = hamAnahtar,
                AnahtarOnEki = ajan.AnahtarOnEki
            };
        }

        public async Task<List<AjanListeSatiri>> ListeleAsync(CancellationToken ct = default)
        {
            var simdi = _saat.GetUtcNow().UtcDateTime;

            // Ajanı oluşturan kullanıcının adı Ajan tablosunda değil User
            // tablosunda: sahip kayıttan okunuyor.
            var satirlar = await (
                from a in _db.Ajanlar.AsNoTracking()
                join u in _db.Users.AsNoTracking() on a.OlusturanKullaniciId equals u.Id into sahip
                from u in sahip.DefaultIfEmpty()
                orderby a.Ad
                select new AjanListeSatiri
                {
                    Id = a.Id,
                    Ad = a.Ad,
                    AnahtarOnEki = a.AnahtarOnEki,
                    OlusturanKullaniciId = a.OlusturanKullaniciId,
                    OlusturanKullaniciAdi = u != null ? u.UserName : null,
                    OlusturmaZamani = a.OlusturmaZamani,
                    SonKullanim = a.SonKullanim,
                    GecerlilikBitisi = a.GecerlilikBitisi,
                    Aktif = a.Aktif,
                    IptalZamani = a.IptalZamani,
                    IptalNedeni = a.IptalNedeni
                }).ToListAsync(ct);

            foreach (var satir in satirlar)
                satir.Durum = DurumMetni(satir.Aktif, satir.GecerlilikBitisi, simdi);

            return satirlar;
        }

        public async Task<bool> IptalEtAsync(int id, string neden, CancellationToken ct = default)
        {
            var ajan = await _db.Ajanlar.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (ajan is null) return false;

            // Zaten iptalliyse ilk iptalin zamanı ve nedeni korunuyor; ikinci
            // çağrı kaydın tarihçesini bozmasın.
            if (ajan.Aktif)
            {
                ajan.Aktif = false;
                ajan.IptalZamani = _saat.GetUtcNow().UtcDateTime;
                ajan.IptalNedeni = string.IsNullOrWhiteSpace(neden) ? null : neden.Trim();
                await _db.SaveChangesAsync(ct);

                _log.LogInformation("Ajan iptal edildi: {Ad} (#{Id}) — {Neden}", ajan.Ad, ajan.Id, ajan.IptalNedeni);
            }

            return true;
        }

        public async Task<AjanTokenYaniti?> TokenUretAsync(string hamAnahtar, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(hamAnahtar)) return null;

            hamAnahtar = hamAnahtar.Trim();
            var onEk = AjanAnahtari.OnEkiCikar(hamAnahtar);

            // Önek yalnız adayları daraltıyor; kararı hash veriyor.
            var adaylar = await _db.Ajanlar
                .Where(a => a.AnahtarOnEki == onEk)
                .ToListAsync(ct);

            var ajan = adaylar.FirstOrDefault(a =>
                _hashleyici.VerifyHashedPassword(a, a.AnahtarHash, hamAnahtar) != PasswordVerificationResult.Failed);

            if (ajan is null)
            {
                // Anahtarın kendisi loglanmıyor; önek, aynı denemenin
                // tekrarlandığını görmeye yetiyor.
                _log.LogWarning("Ajan token isteği reddedildi: anahtar tanınmadı (önek {OnEk})", onEk);
                return null;
            }

            var simdi = _saat.GetUtcNow().UtcDateTime;

            if (!ajan.Aktif)
            {
                _log.LogWarning("Ajan token isteği reddedildi: {Ad} (#{Id}) iptal edilmiş", ajan.Ad, ajan.Id);
                return null;
            }

            if (ajan.GecerlilikBitisi is { } bitis && bitis <= simdi)
            {
                _log.LogWarning("Ajan token isteği reddedildi: {Ad} (#{Id}) anahtarının süresi dolmuş", ajan.Ad, ajan.Id);
                return null;
            }

            ajan.SonKullanim = simdi;
            await _db.SaveChangesAsync(ct);

            var bitisZamani = simdi.Add(TokenOmru);
            return new AjanTokenYaniti
            {
                Token = Bas(ajan, simdi, bitisZamani),
                GecerlilikBitisiUtc = bitisZamani,
                AjanId = ajan.Id,
                AjanAdi = ajan.Ad
            };
        }

        /// <summary>
        /// Ajan token'ı. Kullanıcı token'ıyla aynı imza / issuer / audience — onu
        /// doğrulayan servisler bunu da doğrulayabilsin. Ayrım claim'lerde:
        /// <c>sub</c> bir kullanıcı değil ajan, ve <c>ajan_id</c> yalnız burada var.
        /// </summary>
        private string Bas(Ajan ajan, DateTime simdi, DateTime bitis)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, $"ajan-{ajan.Id}"),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(AjanClaimleri.Tip, AjanClaimleri.AjanTipi),
                new(AjanClaimleri.AjanId, ajan.Id.ToString()),
                new(AjanClaimleri.AjanAdi, ajan.Ad)
            };

            var signingKey = _config["Jwt:SigningKey"] ?? _config["Jwt:Key"];
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                notBefore: simdi,
                expires: bitis,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string DurumMetni(bool aktif, DateTime? gecerlilikBitisi, DateTime simdi)
        {
            if (!aktif) return "İptal";
            if (gecerlilikBitisi is { } bitis && bitis <= simdi) return "Süresi doldu";
            return "Aktif";
        }
    }
}
