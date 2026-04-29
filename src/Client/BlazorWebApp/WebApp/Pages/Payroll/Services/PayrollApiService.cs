using System.Net.Http.Json;
using WebApp.Pages.Payroll.Model;

namespace WebApp.Pages.Payroll.Services
{
    public class PayrollApiService : IPayrollApiService
    {
        private readonly HttpClient _httpClient;
        private const string Base = "/api/public/payroll";

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
                $"api/public/payroll/law-types?year={year}",
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
    }
}
