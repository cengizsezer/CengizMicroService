using WebApp.Shared.Dto.Admin;
using WebApp.Shared.Dto.Sovos;

namespace WebApp.Application.Services.Interfaces;

public interface ISovosAdminService
{
    Task<PageDto<SovosCompanyListItemDto>> GetCompaniesAsync(
        int pageIndex = 0, int pageSize = 50, string? q = null);

    Task<SovosCompanyEditDto?> GetCompanyByIdAsync(int id);

    Task<(bool ok, string? err)> CreateCompanyAsync(NewSovosCompanyDto dto);

    Task<bool> UpdateCompanyAsync(int id, SovosCompanyEditDto dto);

    Task<bool> ChangePasswordAsync(int id, SovosCompanyPasswordDto dto);

    Task<bool> DeleteCompanyAsync(int id);

    Task<(bool ok, string? message)> RunNowAsync(int companyId);
}
