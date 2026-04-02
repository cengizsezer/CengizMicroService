namespace CatalogService.Api.Features.Payroll.Dtos.Responses
{
    public class PayrollMonthResultDto
    {
        public int Month { get; set; }
        public decimal InputAmount { get; set; }

        public decimal GrossSalary { get; set; }
        public decimal SgkEmployeeAmount { get; set; }
        public decimal UnemploymentEmployeeAmount { get; set; }

        public decimal IncomeTaxBase { get; set; }
        public decimal CumulativeIncomeTaxBase { get; set; }

        public decimal CalculatedIncomeTax { get; set; }
        public decimal IncomeTaxExemption { get; set; }
        public decimal PayableIncomeTax { get; set; }

        public decimal CalculatedStampTax { get; set; }
        public decimal StampTaxExemption { get; set; }
        public decimal PayableStampTax { get; set; }

        public decimal BesAmount { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal NetSalary { get; set; }
        public decimal? TotalEmployerCost { get; set; }

        public decimal? EmployerCost { get; set; }
    }
}
