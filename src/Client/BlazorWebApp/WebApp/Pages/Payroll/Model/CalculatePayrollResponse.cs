namespace WebApp.Pages.Payroll.Model
{
    public class CalculatePayrollResponse
    {
        public int Year { get; set; }
        public int CalculationType { get; set; }
        public int EmployeeType { get; set; }
        public bool HasMandatoryBes { get; set; }
        public int DisabilityType { get; set; }

        public List<PayrollMonthResultDto> Months { get; set; } = new();
        public PayrollTotalsDto Totals { get; set; } = new();
    }

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
    }

    public class PayrollTotalsDto
    {
        public decimal TotalGrossSalary { get; set; }
        public decimal TotalSgkEmployeeAmount { get; set; }
        public decimal TotalUnemploymentEmployeeAmount { get; set; }
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
    }
}
