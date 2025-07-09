using IdentityService.Application.Models;
using IdentityService.Application.Services;
using Microsoft.AspNetCore.Mvc;


namespace IdentityService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {

        private readonly IdentityService.Application.Services.IIdentityService identityService;

        public AuthController(IdentityService.Application.Services.IIdentityService identityService)
        {
            this.identityService = identityService;
        }


        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginRequestModel loginRequestModel)
        {
            var result = await identityService.Login(loginRequestModel);

            return Ok(result);
        }
    }
}