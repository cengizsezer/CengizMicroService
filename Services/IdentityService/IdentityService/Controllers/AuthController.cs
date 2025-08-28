using IdentityService.Application.Models;
using IdentityService.Application.Services;
using IdentityService.Domain.Entities;
using IdentityService.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IdentityService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IIdentityService _identityService;
        private readonly IdentityDbContext _context;
        private readonly UserManager<User> _userManager;

        public AuthController(
            IIdentityService identityService,
            IdentityDbContext context,
            UserManager<User> userManager)
        {
            _identityService = identityService;
            _context = context;
            _userManager = userManager;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestModel model)
        {
            var result = await _identityService.LoginAsync(model);
            if (result is null) return Unauthorized("Kullanıcı adı veya şifre hatalı.");
            // Not: Token boş gelebilir; tenant seçimi sonrası üretilecek.
            return Ok(result);
        }

        // Refresh token ile tenant seçimi (access token gerekmez)
        [AllowAnonymous]
        [HttpPost("select-tenant")]
        public async Task<IActionResult> SelectTenant([FromBody] SelectTenantRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.UserName) ||
                string.IsNullOrWhiteSpace(req.TenantNo) ||
                string.IsNullOrWhiteSpace(req.RefreshToken))
                return BadRequest("Eksik bilgi.");

            // Refresh token doğrula
            var rt = await _context.RefreshTokens
                .FirstOrDefaultAsync(x =>
                    x.Token == req.RefreshToken &&
                    !x.IsRevoked &&
                    !x.IsUsed &&
                    x.ExpiresAtUtc > DateTime.UtcNow);

            if (rt is null) return Unauthorized("Refresh token geçersiz veya süresi dolmuş.");

            var user = await _userManager.FindByNameAsync(req.UserName);
            if (user is null || user.Id != rt.UserId)
                return Unauthorized("Kullanıcı / token eşleşmiyor.");

            // Access token üret (tenant, role, permission claim'leri ile)
            var res = await _identityService.SelectTenantAsync(user.Id, req.TenantNo);
            return Ok(res);
        }

        [AllowAnonymous]
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestModel model)
        {
            var result = await _identityService.RefreshTokenAsync(model);
            return Ok(result);
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestModel model)
        {
            var ok = await _identityService.RegisterAsync(model);
            if (!ok) return BadRequest("Bu kullanıcı zaten var.");
            return Ok(new RegisterResponseModel { Success = true, Message = "Kayıt başarılı" });
        }

        // Aktif profil bilgileri
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

            var firms = await _identityService.GetUserFirmsAsync(userId);
            var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            var perms = User.FindAll("perm").Select(c => c.Value).ToList();
            var tenantNo = User.FindFirst("tn")?.Value;

            return Ok(new
            {
                username = User.Identity?.Name,
                tenant = tenantNo,
                roles,
                permissions = perms,
                firms
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("admin-test")]
        public IActionResult AdminOnly()
        {
            return Ok("Bu endpoint sadece Admin rolüne sahip kullanıcılara aittir.");
        }
    }
  
}
