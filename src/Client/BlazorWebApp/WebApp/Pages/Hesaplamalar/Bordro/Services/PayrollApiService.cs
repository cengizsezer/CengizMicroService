using System.Net.Http.Json;
using WebApp.Pages.Hesaplamalar.Bordro.Model;

namespace WebApp.Pages.Hesaplamalar.Bordro.Services
{
    public class PayrollApiService : IPayrollApiService
    {
        private readonly HttpClient _httpClient;

        // Gateway'in genel catalog rotasından geçer (/catalog/{everything} -> Bearer'lı).
        // Eskiden /api/public/payroll idi: gateway'de yetki istemeyen AYRI bir rotası
        // vardı, o rota da controller'daki [AllowAnonymous] da kaldırıldı (KARARLAR §78).
        private const string Base = "/catalog/payroll";

        public PayrollApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<CalculatePayrollResponse?> CalculateAsync(
            CalculatePayrollRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{Base}/calculate",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<CalculatePayrollResponse>(cancellationToken: cancellationToken);
        }

        public async Task<PayrollCalculatorBootstrapDto?> GetBootstrapAsync(
            int year,
            CancellationToken cancellationToken = default)
        {
            return await _httpClient.GetFromJsonAsync<PayrollCalculatorBootstrapDto>(
                $"{Base}/bootstrap?year={year}",
                cancellationToken);
        }

        public async Task<PayrollParameterDto?> GetParametersAsync(
            int year,
            CancellationToken cancellationToken = default)
        {
            return await _httpClient.GetFromJsonAsync<PayrollParameterDto>(
                $"{Base}/parameters/{year}",
                cancellationToken);
        }


        public async Task<List<PayrollLawTypeDto>> GetLawTypesAsync(int year, CancellationToken cancellationToken = default)
        {
            return await _httpClient.GetFromJsonAsync<List<PayrollLawTypeDto>>(
                $"{Base}/law-types?year={year}",
                cancellationToken) ?? new List<PayrollLawTypeDto>();
        }

        public async Task<byte[]> ExportExcelAsync(CalculatePayrollRequest request, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{Base}/export-excel",
                request,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        public async Task<byte[]> ExportPdfAsync(CalculatePayrollRequest request, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{Base}/export-pdf",
                request,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        public async Task<DistributionComparisonResultDto?> CompareDistributionAsync(
            CompareDistributionRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{Base}/compare-distribution",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<DistributionComparisonResultDto>(cancellationToken: cancellationToken);
        }

        public async Task<byte[]> ExportComparisonExcelAsync(
            CompareDistributionRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{Base}/compare-distribution/export-excel",
                request,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        public async Task<byte[]> ExportComparisonPdfAsync(
            CompareDistributionRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{Base}/compare-distribution/export-pdf",
                request,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
    }
}
