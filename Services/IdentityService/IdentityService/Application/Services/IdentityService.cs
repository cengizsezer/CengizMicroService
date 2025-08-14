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

        public async Task<LoginResponseModel?> LoginAsync(LoginRequestModel model)
        {
            var user = await _userManager.Users
                .Include(u => u.UserFirmalar)
                .ThenInclude(uf => uf.Firma)
                .FirstOrDefaultAsync(u => u.UserName == model.Username);

            if (user is null) return null;

            var check = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!check.Succeeded) return null;

            var token = GenerateJwtToken(user);
            var refresh = GenerateRefreshToken();

            user.RefreshToken = refresh;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            var firms = user.UserFirmalar
                .Select(uf => new FirmaDto { Ad = uf.Firma.Ad, Vkn = uf.Firma.Vkn, FirmaNo = uf.Firma.FirmaNo })
                .ToList();

            return new LoginResponseModel
            {
                Username = user.UserName!,
                Token = token,
                RefreshToken = refresh,
                Role = user.Role,
                Firmalar = firms
            };
        }

        public async Task<List<FirmaDto>> GetUserFirmsAsync(int userId)
        {
            return await _context.UserFirms
                .Where(x => x.UserId == userId)
                .Select(x => new FirmaDto { Ad = x.Firma.Ad, Vkn = x.Firma.Vkn, FirmaNo = x.Firma.FirmaNo })
                .ToListAsync();
        }
        public async Task<bool> RegisterAsync(RegisterRequestModel model)
        {
            var existingUser = await _userManager.FindByNameAsync(model.UserName);
            if (existingUser != null)
                return false;

            var user = new User
            {
                UserName = model.UserName,
                Email = model.Email,
                Role = model.Role ?? "User"
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            return result.Succeeded;
        }

        public async Task<LoginResponseModel> RefreshTokenAsync(RefreshTokenRequestModel model)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.UserName == model.UserName &&
                u.RefreshToken == model.RefreshToken &&
                u.RefreshTokenExpiryTime > DateTime.UtcNow);

            if (user == null)
                throw new UnauthorizedAccessException("Refresh token geçersiz veya süresi dolmuş.");

            var newAccessToken = GenerateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            return new LoginResponseModel
            {
                Username = user.UserName,
                Token = newAccessToken,
                RefreshToken = newRefreshToken
            };
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Role, user.Role)
            };

            // Firma claim ekleme (istersen)
            var firmaIds = user.UserFirmalar.Select(uf => uf.FirmaId.ToString()).ToArray();
            claims.Add(new Claim("firms", string.Join(",", firmaIds)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        }
    }
}
