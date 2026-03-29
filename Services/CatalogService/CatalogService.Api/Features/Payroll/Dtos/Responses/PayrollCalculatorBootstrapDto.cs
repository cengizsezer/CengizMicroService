using CatalogService.Api.Features.Payroll.Dtos.Requests;
using CatalogService.Api.Features.Payroll.Dtos.Shared;

namespace CatalogService.Api.Features.Payroll.Dtos.Responses
{
    public class PayrollCalculatorBootstrapDto
    {
        public int Year { get; set; }
        public PayrollParameterDto Parameters { get; set; } = new();
        public List<PayrollMonthInputDto> DefaultMonths { get; set; } = new();
    }
}
