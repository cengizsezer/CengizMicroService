using WebApp.Shared.Dto;

namespace WebApp.Application.Services.Interfaces
{
    public interface IVehicleService
    {
        Task<List<VehicleDto>> GetAllAsync();
        Task<VehicleDto?> GetAsync(int id);
        Task<bool> CreateAsync(VehicleDto model);
        Task<bool> UpdateAsync(VehicleDto model);
        Task<bool> DeleteAsync(int id);
        Task<bool> ImportAsync(List<VehicleDto> items);
        Task<byte[]> ExportExcelAsync();

        Task<bool> DeleteAllAsync();
    }
}
