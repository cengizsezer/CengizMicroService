using WebApp.Pages.Hesaplamalar.Bordro.Model;

namespace WebApp.Pages.Hesaplamalar.Bordro.Services
{
    public interface IPayrollApiService
    {
        Task<CalculatePayrollResponse?> CalculateAsync(CalculatePayrollRequest request, CancellationToken cancellationToken = default);
        Task<List<PayrollLawTypeDto>> GetLawTypesAsync(int year, CancellationToken cancellationToken = default);
        Task<byte[]> ExportExcelAsync(CalculatePayrollRequest request, CancellationToken cancellationToken = default);
        Task<byte[]> ExportPdfAsync(CalculatePayrollRequest request, CancellationToken cancellationToken = default);
        Task<DistributionComparisonResultDto?> CompareDistributionAsync(CompareDistributionRequest request, CancellationToken cancellationToken = default);
        Task<byte[]> ExportComparisonExcelAsync(CompareDistributionRequest request, CancellationToken cancellationToken = default);
        Task<byte[]> ExportComparisonPdfAsync(CompareDistributionRequest request, CancellationToken cancellationToken = default);
    }
}
