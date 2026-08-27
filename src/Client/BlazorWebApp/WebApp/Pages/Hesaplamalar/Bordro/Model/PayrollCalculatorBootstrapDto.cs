
namespace WebApp.Pages.Hesaplamalar.Bordro.Model
{
    public class PayrollCalculatorBootstrapDto
    {
        public int Year { get; set; }
        public PayrollParameterDto Parameters { get; set; } = new();
        public List<PayrollMonthInputDto> DefaultMonths { get; set; } = new();
    }
}
