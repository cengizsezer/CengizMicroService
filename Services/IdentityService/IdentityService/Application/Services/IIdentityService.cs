using IdentityService.Application.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityService.Application.Services
{
    public interface IIdentityService
    {

        Task<LoginResponseModel> Login(LoginRequestModel requestModel);
        Task<bool> Register(RegisterRequestModel model);
        Task<LoginResponseModel> RefreshToken(RefreshTokenRequestModel model);
    }

}