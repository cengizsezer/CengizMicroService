namespace CatalogService.Api.Features.Payroll.Entities
{
    public class PayrollParameter
    {
        public int Id { get; set; }
        public int Year { get; set; }

        // Kesinti oranları
        public decimal SgkEmployeeRate { get; set; }
        public decimal UnemploymentEmployeeRate { get; set; }
        public decimal StampTaxRate { get; set; }
        public decimal BesEmployeeRate { get; set; }

        // Asgari ücret brütü
        public decimal MinimumWageGrossAmount { get; set; }

        // Günlük istisnalar
        public decimal MealExemptionDailyTax { get; set; }
        public decimal MealExemptionDailySgk { get; set; }
        public decimal TransportExemptionDailyTax { get; set; }

        // Diğer opsiyonel alanlar
        public decimal MonthlyFamilyAllowanceExemption { get; set; }
        public decimal MonthlyChildAllowanceExemption { get; set; }
        public decimal MonthlyBoardMemberExemption { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
