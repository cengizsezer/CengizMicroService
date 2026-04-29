using CatalogService.Api.Features.Payroll.Entities;

namespace CatalogService.Api.Features.Payroll.Services.Models
{
    public class PayrollCalculationContext
    {
        public PayrollParameter Parameter { get; set; } = default!;
        public List<PayrollTaxBracket> TaxBrackets { get; set; } = new();
        public PayrollDisabilityExemption? DisabilityExemption { get; set; }
        public bool IsManufacturingSector { get; set; }
    }
}
