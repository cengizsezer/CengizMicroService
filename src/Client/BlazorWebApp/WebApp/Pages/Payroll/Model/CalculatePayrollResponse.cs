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

        public List<HonorariumPayrollMonthResultDto> HonorariumMonths { get; set; } = new();
        public HonorariumPayrollTotalsDto? HonorariumTotals { get; set; }
    }

    public class PayrollMonthResultDto
    {
        public int Month { get; set; }
        public decimal InputAmount { get; set; }
        public decimal GrossSalary { get; set; }
        public decimal SgkEmployeeAmount { get; set; }
        public decimal UnemploymentEmployeeAmount { get; set; }
        public decimal DisabilityExemptionAmount { get; set; }
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

        public decimal? SgkEmployerGross { get; set; }
        public decimal? SgkEmployerIncentive { get; set; }
        public decimal? SgkEmployerNet { get; set; }
        public decimal? UnemploymentEmployerAmount { get; set; }
        public decimal? TotalEmployerCost { get; set; }
        public string? IncentiveSource { get; set; }
    }

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
    }
}
