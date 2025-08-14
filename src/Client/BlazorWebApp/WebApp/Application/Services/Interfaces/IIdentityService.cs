using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebApp.Domain.Models.User;

namespace WebApp.Application.Services.Interfaces
{
    public interface IIdentityService
    {
        Task<string> GetUserName();
        Task<string> GetUserToken();
        Task<bool> IsLoggedIn();

        // Artık Login bool değil, LoginResponseModel döndürüyor
        Task<LoginResponseModel?> Login(string username, string password, bool rememberMe);
        Task<bool> Register(string userName, string email, string password);
        Task<(string Username, string Password)> GetRememberedCredentials();

        void Logout();
    }
}