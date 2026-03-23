using System.Net.Http.Json;
using WebApp.Application.Services.Interfaces;
using CatalogService.Api.Features.Declarations.Dtos;
using WebApp.Extensions;
using WebApp.Shared.Dto.DeclarationFollow;

namespace WebApp.Application.Services
{
    public class DeclarationApiService : IDeclarationApiService
    {
        private readonly HttpClient _httpClient;

        // gateway prefix
        private const string Prefix = "/catalog/declarations";

        public DeclarationApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<CompanyMonthlySummaryDto>> GetMonthlySummaryAsync(
            int year,
            int month,
            string? tenantNo = null,
            string? declarationType = null)
        {
            var url = $"{Prefix}/monthly-summary?year={year}&month={month}";

            if (!string.IsNullOrWhiteSpace(tenantNo))
                url += $"&tenantNo={Uri.EscapeDataString(tenantNo)}";

            if (!string.IsNullOrWhiteSpace(declarationType))
                url += $"&declarationType={Uri.EscapeDataString(declarationType)}";

            return await _httpClient.GetResponseAsync<List<CompanyMonthlySummaryDto>>(url);
        }

        public async Task<YearlyTaxSummaryDto?> GetYearlySummaryAsync(
            int year,
            string? tenantNo = null)
        {
            var url = $"{Prefix}/yearly-summary?year={year}";

            if (!string.IsNullOrWhiteSpace(tenantNo))
                url += $"&tenantNo={Uri.EscapeDataString(tenantNo)}";

            return await _httpClient.GetResponseAsync<YearlyTaxSummaryDto>(url);
        }

        public async Task<int?> CreateAsync(CreateDeclarationRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync(Prefix, request);

            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<CreateDeclarationResponse>();
            return result?.Id;
        }

        public async Task<bool> UpdateAsync(int id, UpdateDeclarationRequest request)
        {
            var response = await _httpClient.PutAsJsonAsync($"{Prefix}/{id}", request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var response = await _httpClient.DeleteAsync($"{Prefix}/{id}");
            return response.IsSuccessStatusCode;
        }
    }
}