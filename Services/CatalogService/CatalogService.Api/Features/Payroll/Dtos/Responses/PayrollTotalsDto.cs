namespace CatalogService.Api.Features.Payroll.Dtos.Responses
{
    public class PayrollTotalsDto
    {
        public decimal TotalGrossSalary { get; set; }
        public decimal TotalSgkEmployeeAmount { get; set; }
        public decimal TotalUnemploymentEmployeeAmount { get; set; }

        public decimal TotalDisabilityExemptionAmount { get; set; }

        public decimal TotalIncomeTaxBase { get; set; }
        public decimal TotalCalculatedIncomeTax { get; set; }
        public decimal TotalIncomeTaxExemption { get; set; }
        public decimal TotalPayableIncomeTax { get; set; }
        public decimal TotalCalculatedStampTax { get; set; }
        public decimal TotalStampTaxExemption { get; set; }
        public decimal TotalPayableStampTax { get; set; }
        public decimal TotalBesAmount { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal TotalNetSalary { get; set; }

        public decimal? TotalSgkEmployerGross { get; set; }
        public decimal? TotalSgkEmployerIncentive { get; set; }
        public decimal? TotalSgkEmployerNet { get; set; }
        public decimal? TotalUnemploymentEmployerAmount { get; set; }
        public decimal? TotalEmployerCost { get; set; }

        public decimal? EmployerCost { get; set; }
    }
}
