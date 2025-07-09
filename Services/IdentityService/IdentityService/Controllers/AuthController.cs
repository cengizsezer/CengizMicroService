using IdentityService.Application.Models;
using IdentityService.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

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

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestModel loginRequestModel)
        {
            var result = await _identityService.Login(loginRequestModel);

            if (result == null)
                return Unauthorized("Kullanıcı adı veya şifre hatalı.");

            return Ok(result);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestModel model)
        {
            var result = await _identityService.Register(model);
            Console.WriteLine($"OnRegister: {model.UserName}  + {model.Password}");
            if (!result)
                return BadRequest("Bu kullanıcı zaten var.");

            return Ok(new RegisterResponseModel { Success = true, Message = "Kayıt başarılı" });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestModel model)
        {
            try
            {
                var result = await _identityService.RefreshToken(model);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("admin-test")]
        public IActionResult AdminOnly()
        {
            return Ok("Bu endpoint sadece Admin rolüne sahip kullanıcılara aittir.");
        }
    }
}
