using WebApp.Pages.Payroll.Model;

namespace WebApp.Pages.Payroll.Services
{
    public interface IPayrollApiService
    {
        Task<CalculatePayrollResponse?> CalculateAsync(CalculatePayrollRequest request, CancellationToken cancellationToken = default);
        Task<List<PayrollLawTypeDto>> GetLawTypesAsync(int year, CancellationToken cancellationToken = default);
        Task<byte[]> ExportExcelAsync(CalculatePayrollRequest request, CancellationToken cancellationToken = default);
    }
}
