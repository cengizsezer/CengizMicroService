using System.Threading.Tasks;
using WebApp.Domain.Models.User;

namespace WebApp.Application.Services.Interfaces
{
    public interface IIdentityService
    {
        Task<string> GetUserName();
        Task<string> GetAccessToken();
        Task<string> GetRefreshToken();
        Task<bool> IsLoggedIn();

        // 1) Kullanıcıyı doğrula: refresh + firmalar gelir (access token yok)
        Task<LoginResponseModel?> Login(string username, string password, bool rememberMe);

        // 2) Firma seç: seçilen firma (tenant) için access token üret
        Task<LoginResponseModel> SelectTenant(string firmaNo);

        // 3) Token saklama / header set etme
        Task StoreTokens(string accessToken, string? refreshToken = null);
        Task<RegisterResponseModel?> Register(string userName, string email, string password);
        Task<(string Username, string Password)> GetRememberedCredentials();
        void Logout();
    }
}
