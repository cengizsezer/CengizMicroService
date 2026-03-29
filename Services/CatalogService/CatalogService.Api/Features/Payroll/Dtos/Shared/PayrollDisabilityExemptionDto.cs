using CatalogService.Api.Features.Payroll.Enums;

namespace CatalogService.Api.Features.Payroll.Dtos.Shared
{
    public class PayrollDisabilityExemptionDto
    {
        public PayrollDisabilityType DisabilityType { get; set; }
        public decimal MonthlyExemptionAmount { get; set; }
    }
}
