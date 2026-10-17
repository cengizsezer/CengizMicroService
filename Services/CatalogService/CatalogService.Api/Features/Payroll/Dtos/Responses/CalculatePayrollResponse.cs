using CatalogService.Api.Features.Payroll.Enums;

namespace CatalogService.Api.Features.Payroll.Dtos.Responses
{
    public class CalculatePayrollResponse
    {
        public int Year { get; set; }
        public PayrollCalculationType CalculationType { get; set; }
        public PayrollEmployeeType EmployeeType { get; set; }
        public bool HasMandatoryBes { get; set; }
        public PayrollDisabilityType DisabilityType { get; set; }

        /// <summary>Yol / yemek hesaba katıldı mı — tablo ve çıktılarda yan hak sütunları buna bakar.</summary>
        public bool YolYemekDahil { get; set; }

        public List<PayrollMonthResultDto> Months { get; set; } = new();
        public PayrollTotalsDto? Totals { get; set; }

        public List<HonorariumPayrollMonthResultDto> HonorariumMonths { get; set; } = new();
        public HonorariumPayrollTotalsDto? HonorariumTotals { get; set; }

        /// <summary>Hesabı bozmayan ama kullanıcının bilmesi gereken durumlar (ör. emekliye teşvik uygulanmadı).</summary>
        public List<string> Warnings { get; set; } = new();
    }
}
