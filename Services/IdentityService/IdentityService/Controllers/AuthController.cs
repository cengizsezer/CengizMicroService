using IdentityService.Application.Models;
using IdentityService.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IdentityService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IIdentityService _identityService;

        public AuthController(IIdentityService identityService)
        {
            _identityService = identityService;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestModel model)
        {
            var result = await _identityService.LoginAsync(model);
            if (result is null) return Unauthorized("Kullanıcı adı veya şifre hatalı.");

            // ÖNEMLİ: Login cevabında Role ve Firmalar dolu gelecek.
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

        [AllowAnonymous]
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestModel model)
        {
            var result = await _identityService.RefreshTokenAsync(model);
            return Ok(result);
        }

        // İstemci login sonrası profil + firmalar için kullanır
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

            var firms = await _identityService.GetUserFirmsAsync(userId);
            return Ok(new
            {
                username = User.Identity?.Name,
                role = User.FindFirstValue(ClaimTypes.Role),
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
