using System.Net;
using System.Net.Http.Json;
using WebApp.Pages.Hesaplamalar.Bordro.Model;

namespace WebApp.Pages.Hesaplamalar.Bordro.Services
{
    /// <summary>
    /// Açık (girişsiz) bordro sayfasının sunucu kapısı. Uygulama içi ekranla aynı işlevler
    /// (bootstrap/parameters hariç); gateway'de kimliksiz
    /// <c>/catalog/payroll/acik/{everything}</c> rotasından geçer (KARARLAR §155).
    ///
    /// <c>GatewayBare</c> istemcisini kullanır: token, tenant başlığı ve 401'de token
    /// yenileme EKLENMEZ. Girişli biri açık sayfayı açsa bile oturum bilgisi bu uçlara
    /// gitmez — açık sayfa uygulamanın geri kalanıyla hiçbir şey paylaşmaz.
    /// </summary>
    public class PayrollAcikApiService : IPayrollHesapApi
    {
        private readonly HttpClient _httpClient;

        private const string Base = "/catalog/payroll/acik";

        public PayrollAcikApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<CalculatePayrollResponse?> CalculateAsync(
            CalculatePayrollRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/calculate", request, cancellationToken);
            await HataVarsaFirlatAsync(response, cancellationToken);
            return await response.Content.ReadFromJsonAsync<CalculatePayrollResponse>(cancellationToken: cancellationToken);
        }

        public async Task<List<PayrollLawTypeDto>> GetLawTypesAsync(int year, CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync($"{Base}/law-types?year={year}", cancellationToken);
            await HataVarsaFirlatAsync(response, cancellationToken);
            return await response.Content.ReadFromJsonAsync<List<PayrollLawTypeDto>>(cancellationToken: cancellationToken)
                   ?? new List<PayrollLawTypeDto>();
        }

        public Task<byte[]> ExportExcelAsync(CalculatePayrollRequest request, CancellationToken cancellationToken = default)
            => DosyaAlAsync($"{Base}/export-excel", request, cancellationToken);

        public Task<byte[]> ExportPdfAsync(CalculatePayrollRequest request, CancellationToken cancellationToken = default)
            => DosyaAlAsync($"{Base}/export-pdf", request, cancellationToken);

        public async Task<DistributionComparisonResultDto?> CompareDistributionAsync(
            CompareDistributionRequest request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/compare-distribution", request, cancellationToken);
            await HataVarsaFirlatAsync(response, cancellationToken);
            return await response.Content.ReadFromJsonAsync<DistributionComparisonResultDto>(cancellationToken: cancellationToken);
        }

        public Task<byte[]> ExportComparisonExcelAsync(CompareDistributionRequest request, CancellationToken cancellationToken = default)
            => DosyaAlAsync($"{Base}/compare-distribution/export-excel", request, cancellationToken);

        public Task<byte[]> ExportComparisonPdfAsync(CompareDistributionRequest request, CancellationToken cancellationToken = default)
            => DosyaAlAsync($"{Base}/compare-distribution/export-pdf", request, cancellationToken);

        private async Task<byte[]> DosyaAlAsync<T>(string yol, T request, CancellationToken cancellationToken)
        {
            var response = await _httpClient.PostAsJsonAsync(yol, request, cancellationToken);
            await HataVarsaFirlatAsync(response, cancellationToken);
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        /// <summary>
        /// Hız sınırına takılan istek (429) sessizce düşmesin: sunucunun gönderdiği
        /// açıklama (hesap ya da dosya sınırına göre ayrı) olduğu gibi kullanıcıya gider.
        /// Diğer hatalar eskisi gibi.
        /// </summary>
        private static async Task HataVarsaFirlatAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var mesaj = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    string.IsNullOrWhiteSpace(mesaj)
                        ? "Çok fazla istek gönderildi. Lütfen biraz bekleyip tekrar deneyin."
                        : mesaj,
                    null,
                    HttpStatusCode.TooManyRequests);
            }

            response.EnsureSuccessStatusCode();
        }
    }
}
