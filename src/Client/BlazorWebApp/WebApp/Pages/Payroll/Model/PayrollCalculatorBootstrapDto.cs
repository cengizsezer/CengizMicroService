
namespace WebApp.Pages.Payroll.Model
{
    public class PayrollCalculatorBootstrapDto
    {
        public int Year { get; set; }
        public PayrollParameterDto Parameters { get; set; } = new();
        public List<PayrollMonthInputDto> DefaultMonths { get; set; } = new();
    }
}
