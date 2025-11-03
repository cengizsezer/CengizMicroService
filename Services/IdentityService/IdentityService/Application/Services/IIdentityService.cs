using IdentityService.Application.Models;
using IdentityService.Application.Models.Register;

namespace IdentityService.Application.Services
{
    public interface IIdentityService
    {
        Task<LoginResponseModel> LoginAsync(LoginRequestModel requestModel);
        Task<RegisterResult> RegisterAsync(RegisterRequestModel model);
        Task<LoginResponseModel> RefreshTokenAsync(RefreshTokenRequestModel model);

        Task<List<FirmaDto>> GetUserFirmsAsync(int userId);

        Task<LoginResponseModel> SelectTenantAsync(int userId, string tenantNo);
    }
}
