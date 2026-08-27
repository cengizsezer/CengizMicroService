namespace WebApp.Pages.Hesaplamalar.Bordro.Model
{
    public class HonorariumPayrollMonthResultDto
    {
        public int Month { get; set; }
        public decimal InputAmount { get; set; }

        public decimal GrossHonorarium { get; set; }
        public decimal IncomeTaxBase { get; set; }
        public decimal CumulativeIncomeTaxBase { get; set; }
        public decimal CalculatedIncomeTax { get; set; }
        public decimal IncomeTaxExemption { get; set; }
        public decimal PayableIncomeTax { get; set; }
        public decimal CalculatedStampTax { get; set; }
        public decimal StampTaxExemption { get; set; }
        public decimal PayableStampTax { get; set; }

        public decimal TotalDeductions { get; set; }
        public decimal TaxBurdenRate { get; set; }
        public decimal NetHonorarium { get; set; }

        public decimal? EmployerCost { get; set; }
    }
}
