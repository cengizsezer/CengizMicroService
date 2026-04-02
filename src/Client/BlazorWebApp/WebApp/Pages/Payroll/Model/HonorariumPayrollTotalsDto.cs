namespace WebApp.Pages.Payroll.Model
{
    public class HonorariumPayrollTotalsDto
    {
        public decimal TotalGrossHonorarium { get; set; }
        public decimal TotalIncomeTaxBase { get; set; }
        public decimal TotalCalculatedIncomeTax { get; set; }
        public decimal TotalIncomeTaxExemption { get; set; }
        public decimal TotalPayableIncomeTax { get; set; }
        public decimal TotalCalculatedStampTax { get; set; }
        public decimal TotalStampTaxExemption { get; set; }
        public decimal TotalPayableStampTax { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal AverageTaxBurdenRate { get; set; }
        public decimal TotalNetHonorarium { get; set; }

        public decimal? TotalEmployerCost { get; set; }
    }
}
