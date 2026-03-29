using CatalogService.Api.Features.Payroll.Enums;

namespace CatalogService.Api.Features.Payroll.Entities
{
    public class PayrollDisabilityExemption
    {
        public int Id { get; set; }

        public int Year { get; set; }

        public PayrollDisabilityType DisabilityType { get; set; }

        /// <summary>
        /// Aylık engellilik indirimi tutarı
        /// </summary>
        public decimal MonthlyExemptionAmount { get; set; }
    }
}
