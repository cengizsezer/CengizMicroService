using WebApp.Pages.Payroll.Model;

namespace WebApp.Pages.Payroll.Services
{
    public interface IPayrollApiService
    {
        Task<CalculatePayrollResponse?> CalculateAsync(CalculatePayrollRequest request, CancellationToken cancellationToken = default);
    }
}
