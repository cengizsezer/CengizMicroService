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

        private const string Prefix = "/catalog/declarations";

        public DeclarationApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<CompanyMonthlySummaryDto>> GetMonthlySummaryAsync(
            int year,
            int month,
            int? firmaId = null,
            string? declarationType = null,
            bool pasifleriGoster = false)
        {
            var query = new List<string>
            {
                $"year={year}",
                $"month={month}"
            };

            // Firma = Firmalarım (FirmaId). Pasif firmanın kayıtları yalnız pasifleriGoster ile.
            if (firmaId.HasValue)
                query.Add($"firmaId={firmaId.Value}");

            if (pasifleriGoster)
                query.Add("pasifleriGoster=true");

            if (!string.IsNullOrWhiteSpace(declarationType))
                query.Add($"declarationType={Uri.EscapeDataString(declarationType)}");

            var url = $"{Prefix}/monthly-summary?{string.Join("&", query)}";

            return await _httpClient.GetResponseAsync<List<CompanyMonthlySummaryDto>>(url);
        }

        // GetYearlySummaryAsync KALDIRILDI (09.09.2026): tek çağıran Beyanname Takip
        // sayfasındaki "Bu Yıl Toplam Vergi" kartıydı, kart kaldırıldı. Sunucudaki
        // /catalog/declarations/yearly-summary ucu duruyor; gerekirse sarmalayıcı
        // birkaç satırla geri yazılır.

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