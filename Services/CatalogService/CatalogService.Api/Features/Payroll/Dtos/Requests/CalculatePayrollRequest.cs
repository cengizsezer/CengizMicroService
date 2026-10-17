using CatalogService.Api.Features.Payroll.Enums;

namespace CatalogService.Api.Features.Payroll.Dtos.Requests
{
    public class CalculatePayrollRequest
    {
        public int Year { get; set; }
        public PayrollCalculationType CalculationType { get; set; }
        public PayrollEmployeeType EmployeeType { get; set; }

        public bool HasMandatoryBes { get; set; }
        public PayrollDisabilityType DisabilityType { get; set; }

        public bool IncludeMinimumWageExemption { get; set; } = true;
        public bool IncludeStampTax { get; set; } = true;
        public bool IncludeEmployerCost { get; set; } = false;

        public int StartMonth { get; set; } = 1;
        public decimal PreviousCumulativeTaxBase { get; set; }

        public List<PayrollMonthInputDto> Months { get; set; } = new();
        public string LawCode { get; set; } = "00000";
        public bool IsManufacturingSector { get; set; } = false;
        public PayrollArGeEgitimDurumu ArGeEgitimDurumu { get; set; } = PayrollArGeEgitimDurumu.Diger80;

        // Teşvik ek girdileri (2026+ kurallar; bkz. PayrollTesvikKurallari)
        public int? TesvikAltSecimi { get; set; }
        public bool TurizmKonaklamaOnayi { get; set; }
        public int TurizmPrimGunu { get; set; } = 30;
        public decimal IsgDestegiTutari { get; set; }
        public PayrollManuelTesvikTuru? ManuelTesvikTuru { get; set; }
        public decimal ManuelTesvikDegeri { get; set; }

        /// <summary>Yol / yemek yan hakkı (2026+); null: kapalı.</summary>
        public PayrollYolYemekDto? YolYemek { get; set; }
    }
}
