using CatalogService.Api.Features.Payroll.Enums;

namespace CatalogService.Api.Features.Payroll.Dtos.Requests
{
    public class CalculatePayrollRequest
    {
        public int Year { get; set; }
        public PayrollCalculationType CalculationType { get; set; }
        public PayrollEmployeeType EmployeeType { get; set; }

        public bool HasMandatoryBes { get; set; }
        public PayrollDisabilityType DisabilityType { get; set; }

        public bool IncludeMinimumWageExemption { get; set; } = true;
        public bool IncludeStampTax { get; set; } = true;
        public bool IncludeEmployerCost { get; set; } = false;

        public int StartMonth { get; set; } = 1;
        public decimal PreviousCumulativeTaxBase { get; set; }

        public List<PayrollMonthInputDto> Months { get; set; } = new();
        public string LawCode { get; set; } = "00000";
        public bool IsManufacturingSector { get; set; } = false;
    }
}
