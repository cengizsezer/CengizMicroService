namespace CatalogService.Api.Features.Payroll.Dtos.Shared
{
    public class PayrollParameterDto
    {
        public int Year { get; set; }

        public decimal SgkEmployeeRate { get; set; }
        public decimal UnemploymentEmployeeRate { get; set; }

        public decimal RetiredSgkEmployeeRate { get; set; }
        public decimal RetiredUnemploymentEmployeeRate { get; set; }

        public decimal StampTaxRate { get; set; }
        public decimal BesEmployeeRate { get; set; }

        public decimal MinimumWageGrossAmount { get; set; }

        public decimal MealExemptionDailyTax { get; set; }
        public decimal MealExemptionDailySgk { get; set; }
        public decimal TransportExemptionDailyTax { get; set; }

        public decimal MonthlyFamilyAllowanceExemption { get; set; }
        public decimal MonthlyChildAllowanceExemption { get; set; }
        public decimal MonthlyBoardMemberExemption { get; set; }

        public bool IsActive { get; set; }
    }
}
