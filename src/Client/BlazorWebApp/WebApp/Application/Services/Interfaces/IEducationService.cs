using WebApp.Domain.Models;
using WebApp.Shared.Dto;
using WebApp.Shared.Dto.Education;

namespace WebApp.Application.Services.Interfaces
{
    public interface IEducationService
    {
        Task<PaginatedItemsViewModel<EducationItemDto>> GetAsync(int pageIndex = 0, int pageSize = 20, string? q = null, string orderBy = "createdAtDesc");
        Task<EducationItemDto?> GetByIdAsync(int id);
        Task<EducationItemDto?> CreateAsync(CreateEducationItemDto dto, CancellationToken ct = default);
        Task<EducationItemDto?> UpdateAsync(int id, UpdateEducationItemDto dto, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    }
}
