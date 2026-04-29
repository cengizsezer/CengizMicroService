namespace CatalogService.Api.Features.Payroll.Entities
{
    public class PayrollParameter
    {
        public int Id { get; set; }
        public int Year { get; set; }

        // Normal çalışan oranları
        public decimal SgkEmployeeRate { get; set; }
        public decimal UnemploymentEmployeeRate { get; set; }

        // Emekli çalışan oranları
        public decimal RetiredSgkEmployeeRate { get; set; }
        public decimal RetiredUnemploymentEmployeeRate { get; set; }

        public decimal StampTaxRate { get; set; }
        public decimal BesEmployeeRate { get; set; }

        public decimal MinimumWageIncomeTaxExemptionMonthly { get; set; }
        public decimal MinimumWageStampTaxExemptionMonthly { get; set; }

        public decimal MinimumWageGrossAmount { get; set; }

        public decimal MealExemptionDailyTax { get; set; }
        public decimal MealExemptionDailySgk { get; set; }
        public decimal TransportExemptionDailyTax { get; set; }

        public decimal MonthlyFamilyAllowanceExemption { get; set; }
        public decimal MonthlyChildAllowanceExemption { get; set; }
        public decimal MonthlyBoardMemberExemption { get; set; }

        // İşveren SGK oranları
        public decimal SgkEmployerMYORate { get; set; }        // Malullük/Yaşlılık/Ölüm: 0.11
        public decimal SgkEmployerGSSRate { get; set; }        // Genel Sağlık Sigortası: 0.075
        public decimal SgkEmployerKVSKRate { get; set; }       // Kısa Vadeli Sigorta Kolları: 0.02
        public decimal UnemploymentEmployerRate { get; set; }  // İşsizlik işveren: 0.02
        public decimal SgkCeilingMultiplier { get; set; }      // Prime esas kazanç tavan katsayısı: 7.5

        // 05510 - 5510/81-ı Beş Puan İşveren SGK Teşviki
        public decimal Incentive05510TreasuryRate { get; set; }     // Hazine desteği oranı: 0.05
        public decimal SgkEmployerMYO05510Rate { get; set; }        // 5 puan indirim sonrası MYO oranı: 0.06

        public bool IsActive { get; set; } = true;
    }
}
