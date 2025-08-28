using IdentityService.Application.Models;
using IdentityService.Domain.Entities;
using IdentityService.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace IdentityService.Application.Services
{
    public class IdentityService : IIdentityService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IdentityDbContext _context;
        private readonly IConfiguration _configuration;

        public IdentityService(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IdentityDbContext context,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _configuration = configuration;
        }

        // 1) LOGIN: yalnızca kullanıcıyı doğrula + refresh üret + firma listesini dön
        // (Access token'ı tenant seçimiyle üreteceğiz.)
        public async Task<LoginResponseModel?> LoginAsync(LoginRequestModel model)
        {
            Console.WriteLine($"[ID] DB = {_context.Database.GetDbConnection().DataSource}/{_context.Database.GetDbConnection().Database}");

            var user = await _userManager.Users
                .Include(u => u.UserTenants).ThenInclude(ut => ut.Tenant)
                .FirstOrDefaultAsync(u => u.UserName == model.Username);

            if (user is null) return null;

            var check = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
            if (!check.Succeeded) return null;

            // Refresh – cihaz/IP eklemek istersen parametre al.
            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = GenerateRefreshToken(),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(14),
                DeviceId = "web",
                IpAddress = null
            };
            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            var firms = user.UserTenants
                .Select(ut => new FirmaDto
                {
                    Ad = ut.Tenant.Ad,
                    Vkn = ut.Tenant.Vkn ?? "",
                    FirmaNo = ut.Tenant.FirmaNo
                })
                .ToList();

            return new LoginResponseModel
            {
                Username = user.UserName!,
                Token = "", // tenant seçince üretilecek
                RefreshToken = refreshToken.Token,
                Role = "", // legacy alan (kullanılmıyor)
                Firmalar = firms
            };
        }

        // 2) Kullanıcının firma listesi
        public async Task<List<FirmaDto>> GetUserFirmsAsync(int userId)
        {
            return await _context.UserTenants
                .Where(x => x.UserId == userId)
                .Select(x => new FirmaDto
                {
                    Ad = x.Tenant.Ad,
                    Vkn = x.Tenant.Vkn ?? "",
                    FirmaNo = x.Tenant.FirmaNo
                })
                .ToListAsync();
        }

        // 3) REGISTER: basit hali
        public async Task<bool> RegisterAsync(RegisterRequestModel model)
        {
            var existingUser = await _userManager.FindByNameAsync(model.UserName);
            if (existingUser != null) return false;

            var user = new User
            {
                UserName = model.UserName,
                Email = model.Email,
                // Role alanı legacy; asıl rol/izin yönetimi UserTenantRole üzerinden yapılacak.
                LegacyRole = model.Role ?? "User"
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            return result.Succeeded;
        }

        // 4) REFRESH: rotasyon + reuse koruması (minimum)
        public async Task<LoginResponseModel> RefreshTokenAsync(RefreshTokenRequestModel model)
        {
            var rt = await _context.RefreshTokens
                .FirstOrDefaultAsync(x =>
                    x.Token == model.RefreshToken &&
                    !x.IsRevoked &&
                    !x.IsUsed &&
                    x.ExpiresAtUtc > DateTime.UtcNow);

            if (rt is null) throw new UnauthorizedAccessException("Refresh token geçersiz veya süresi dolmuş.");

            var user = await _userManager.FindByNameAsync(model.UserName)
                       ?? throw new UnauthorizedAccessException();

            // reuse engelle
            rt.IsUsed = true;
            await _context.SaveChangesAsync();

            // yeni refresh üret
            var newRt = new RefreshToken
            {
                UserId = user.Id,
                Token = GenerateRefreshToken(),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(14),
                DeviceId = rt.DeviceId,
                IpAddress = rt.IpAddress
            };
            _context.RefreshTokens.Add(newRt);
            await _context.SaveChangesAsync();

            // Access token tenant seçimi ile verilecek; burada boş bırakıyoruz.
            return new LoginResponseModel
            {
                Username = user.UserName!,
                Token = "",
                RefreshToken = newRt.Token,
                Role = user.LegacyRole // legacy
            };
        }

        // 5) TENANT SEÇ: asıl access token'ı üret
        public async Task<LoginResponseModel> SelectTenantAsync(int userId, string tenantNo)
        {
            var user = await _userManager.Users
                .Include(u => u.UserTenants)
                    .ThenInclude(ut => ut.Tenant)
                .Include(u => u.UserTenants)
                    .ThenInclude(ut => ut.Roles)
                        .ThenInclude(utr => utr.Role)
                            .ThenInclude(r => r.Permissions)
                                .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new UnauthorizedAccessException();

            var membership = user.UserTenants.FirstOrDefault(x => x.Tenant.FirmaNo == tenantNo);
            if (membership is null)
                throw new UnauthorizedAccessException("Bu firmaya erişiminiz yok.");

            var roles = membership.Roles.Select(r => r.Role.Name).Distinct().ToList();
            // Eğer henüz rol/izin tablosunu seed etmediysen aşağıdaki satır, legacy tekli role alanını da ekler
            if (!roles.Any() && !string.IsNullOrWhiteSpace(user.LegacyRole))
                roles.Add(user.LegacyRole);

            var perms = membership.Roles
                .SelectMany(r => r.Role.Permissions.Select(p => p.Permission.Key))
                .Distinct()
                .ToList();

            // Legacy tekli role için basit default izinler (opsiyonel)
            if (!perms.Any() && roles.Contains("MaliIsler"))
                perms.AddRange(new[] { "Expense.View", "Expense.Edit", "Beyanname.View" });
            if (!perms.Any() && roles.Contains("Ilkyardim"))
                perms.Add("Ilkyardim.View");

            var access = GenerateJwtToken(user, tenantNo, roles, perms);

            // En güncel geçerli refresh'i döndür (UI saklıyorsa gerek yok)
            var lastRefresh = await _context.RefreshTokens
                .Where(x => x.UserId == user.Id && !x.IsRevoked && !x.IsUsed && x.ExpiresAtUtc > DateTime.UtcNow)
                .OrderByDescending(x => x.Id)
                .Select(x => x.Token)
                .FirstOrDefaultAsync() ?? "";

            return new LoginResponseModel
            {
                Username = user.UserName!,
                Token = access,
                RefreshToken = lastRefresh,
                Role = user.LegacyRole, // legacy
                Firmalar = user.UserTenants.Select(ut => new FirmaDto
                {
                    Ad = ut.Tenant.Ad,
                    Vkn = ut.Tenant.Vkn ?? "",
                    FirmaNo = ut.Tenant.FirmaNo
                }).ToList()
            };
        }

        // ==== PRIVATE HELPERS ====

        // Yeni: tenant + role + permission claim'li token
        private string GenerateJwtToken(User user, string tenantNo, IEnumerable<string> roles, IEnumerable<string> permissions)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.UserName!),
                new("tn", tenantNo)
            };

            foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));
            foreach (var p in permissions) claims.Add(new Claim("perm", p));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(20), // kısa ömürlü access
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Eski nötr token (geri uyumluluk için istersen kalsın)
        private string GenerateJwtToken(User user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.UserName!)
            };
            if (!string.IsNullOrWhiteSpace(user.LegacyRole))
                claims.Add(new Claim(ClaimTypes.Role, user.LegacyRole));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string GenerateRefreshToken()
            => Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    }
}
