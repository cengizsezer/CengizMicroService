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

        public int StartMonth { get; set; } = 1;
        public decimal PreviousCumulativeTaxBase { get; set; }

        public List<PayrollMonthInputDto> Months { get; set; } = new();
    }
}
