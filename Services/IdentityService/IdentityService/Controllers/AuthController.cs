using Azure.Core;
using EventBus.Base.Abstraction;
using IdentityService.Application.Models;
using IdentityService.Application.Services;
using IdentityService.Domain.Entities;
using IdentityService.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using IdentityService.IntegrationEvents.Event;
using IdentityService.Application.Models.Admin;

namespace IdentityService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IIdentityService _identityService;
        private readonly IdentityDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IEventBus _eventBus;
        public AuthController(
            IIdentityService identityService,
            IdentityDbContext context,
            UserManager<User> userManager
            , IEventBus eventBus)
        {
            _identityService = identityService;
            _context = context;
            _userManager = userManager;
            _eventBus = eventBus;
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
           
            var result = await _identityService.RegisterAsync(model); // => RegisterResult
            
            if (result.Success)
            {
                // Publish SUCCESS
                var registerSuccessEvent = new UserRegisterSuccessIntegrationEvent(result.Email);
                _eventBus.Publish(registerSuccessEvent);

                return Ok(new RegisterResponseModel { Success = true, Message = "Kayıt başarılı" });
            }
            else
            {
                // Publish FAILED (ör: zaten var, policy fail, vb.)
                var registerFailedEvent= new UserRegisterFailedIntegrationEvent(result.Email, result.ErrorMessage ?? "Kayıt başarısız");
                _eventBus.Publish(registerFailedEvent);


                return BadRequest(result.ErrorMessage ?? "Kayıt başarısız");
            }
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


        [HttpGet("users")]
        public async Task<ActionResult<PageDto<UserListItemDto>>> GetUsers(
    [FromQuery] int p = 0, [FromQuery] int ps = 50, [FromQuery] string? q = null)
        {
            var usersQ = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                usersQ = usersQ.Where(x =>
                    x.UserName!.Contains(q) ||
                    (x.Email != null && x.Email.Contains(q)));
            }

            var count = await usersQ.CountAsync();

            var users = await usersQ
                .OrderBy(x => x.UserName)
                .Skip(p * ps).Take(ps)
                .Select(u => new UserListItemDto
                {
                    Id = u.Id,
                    DisplayName = u.UserName ?? "",
                    Email = u.Email ?? "",

                    // İlk tenant adı (aktif firma gibi göstermek için)
                    FirmName = (
                        from ut in _context.UserTenants
                        join t in _context.Tenants on ut.TenantId equals t.Id
                        where ut.UserId == u.Id
                        select t.Ad
                    ).FirstOrDefault(),

                    // Kullanıcının tüm rolleri (tenant fark etmeksizin benzersiz)
                    Roles = (
                        from utr in _context.UserTenantRoles
                        join r in _context.RolesApp on utr.RoleId equals r.Id
                        where utr.UserId == u.Id
                        select r.Name
                    ).Distinct().ToArray()
                })
                .AsNoTracking()
                .ToListAsync();

            return Ok(new PageDto<UserListItemDto>
            {
                PageIndex = p,
                PageSize = ps,
                Count = count,
                Data = users
            });
        }
    }
  
}
