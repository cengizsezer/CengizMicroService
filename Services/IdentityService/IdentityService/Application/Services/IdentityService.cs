using IdentityService.Application.Models;
using IdentityService.Domain.Entities;
using IdentityService.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace IdentityService.Application.Services
{
    public class IdentityService : IIdentityService
    {
        private readonly IdentityDbContext _context;
        private readonly IConfiguration _configuration;

        public IdentityService(IdentityDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<LoginResponseModel> Login(LoginRequestModel requestModel)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == requestModel.Username);

            if (user == null || !VerifyPassword(requestModel.Password, user.PasswordHash))
                throw new UnauthorizedAccessException("Geçersiz kullanıcı adı veya şifre.");

            var accessToken = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            return new LoginResponseModel
            {
                Username = user.Username,
                Token = accessToken,
                RefreshToken = refreshToken
            };
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Username),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds,
                notBefore: DateTime.UtcNow
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        }

        private bool VerifyPassword(string password, string storedHash)
        {
            return BCrypt.Net.BCrypt.Verify(password, storedHash);
        }



        public async Task<bool> Register(RegisterRequestModel model)
        {
            var existingUser = await _context.Users.AnyAsync(u => u.Username == model.UserName);
            if (existingUser)
                return false;
            Console.WriteLine($"Login isteği geldi. Email: {model.UserName}");
            var user = new User
            {
                Username = model.UserName,
                Email = model.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                Role = model.Role ?? "User"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<LoginResponseModel> RefreshToken(RefreshTokenRequestModel model)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.Username == model.UserName &&
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
                Username = user.Username,
                Token = newAccessToken,
                RefreshToken = newRefreshToken
            };
        }
    }

}