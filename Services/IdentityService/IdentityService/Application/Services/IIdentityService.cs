using IdentityService.Application.Models;

namespace IdentityService.Application.Services
{
    public interface IIdentityService
    {
        Task<LoginResponseModel> LoginAsync(LoginRequestModel requestModel);
        Task<bool> RegisterAsync(RegisterRequestModel model);
        Task<LoginResponseModel> RefreshTokenAsync(RefreshTokenRequestModel model);

        Task<List<FirmaDto>> GetUserFirmsAsync(int userId);

        Task<LoginResponseModel> SelectTenantAsync(int userId, string tenantNo);
    }
}
