namespace CatalogService.Api.Features.Payroll.Dtos.Responses
{
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

        /// <summary>Hazine'nin karşıladığı sigortalı SGK hissesi (Yatırım 6. bölge); kesintiden düşer, net artar.</summary>
        public decimal EmployeeSgkIncentive { get; set; }

        public decimal TotalDeductions { get; set; }

        /// <summary>Nakit net: ücret − kesintiler + nakit ödenen yol/yemek. Kartla ödenen buna girmez.</summary>
        public decimal NetSalary { get; set; }

        // Yol / yemek (2026+; kapalıyken hepsi 0)
        public decimal MealAmount { get; set; }
        public decimal TransportAmount { get; set; }
        /// <summary>İstisna aşan kısım — SGK matrahına eklenen.</summary>
        public decimal SgkTaxableBenefits { get; set; }
        /// <summary>İstisna aşan kısım — GV ve DV matrahına eklenen.</summary>
        public decimal IncomeTaxableBenefits { get; set; }
        /// <summary>Nakit ödenen yol/yemek — NetSalary'nin içinde.</summary>
        public decimal CashBenefits { get; set; }
        /// <summary>Kart/çek/bilet ile verilen yol/yemek — nete girmez.</summary>
        public decimal CardPaidBenefits { get; set; }

        public decimal? SgkEmployerGross { get; set; }
        public decimal? SgkEmployerIncentive { get; set; }
        public decimal? SgkEmployerNet { get; set; }
        public decimal? UnemploymentEmployerAmount { get; set; }
        public decimal? TotalEmployerCost { get; set; }
        public string? IncentiveSource { get; set; }

        /// <summary>SGK primine dokunmayan, işveren maliyetinden ayrı satırda düşen destek (Turizm, İSG).</summary>
        public decimal? EmployerSupportAmount { get; set; }
        public string? EmployerSupportSource { get; set; }

        public decimal? EmployerCost { get; set; }
    }
}
