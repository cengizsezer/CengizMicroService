using System.Net.Http.Json;
using WebApp.Application.Services.Interfaces;
using WebApp.Shared.Dto;

namespace WebApp.Application.Services
{
    public class VehicleService : IVehicleService
    {
        private readonly HttpClient _httpClient;
        private const string Base = "/catalog/vehicles";

        public VehicleService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        public async Task<bool> DeleteAllAsync()
    => (await _httpClient.DeleteAsync($"{Base}/alldelete")).IsSuccessStatusCode;
        public async Task<List<VehicleDto>> GetAllAsync()
            => await _httpClient.GetFromJsonAsync<List<VehicleDto>>(Base)
               ?? new List<VehicleDto>();

        public Task<VehicleDto?> GetAsync(int id)
            => _httpClient.GetFromJsonAsync<VehicleDto>($"{Base}/{id}");

        public async Task<bool> CreateAsync(VehicleDto model)
            => (await _httpClient.PostAsJsonAsync(Base, model)).IsSuccessStatusCode;

        public async Task<bool> UpdateAsync(VehicleDto model)
            => (await _httpClient.PutAsJsonAsync($"{Base}/{model.Id}", model)).IsSuccessStatusCode;

        public async Task<bool> DeleteAsync(int id)
            => (await _httpClient.DeleteAsync($"{Base}/{id}")).IsSuccessStatusCode;

        public async Task<bool> ImportAsync(List<VehicleDto> items)
            => (await _httpClient.PostAsJsonAsync($"{Base}/import", items)).IsSuccessStatusCode;

        public Task<byte[]> ExportExcelAsync()
            => _httpClient.GetByteArrayAsync($"{Base}/export");
    }
}
