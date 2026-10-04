using CatalogService.Api.Features.Payroll.Commands.CalculatePayroll;
using CatalogService.Api.Features.Payroll.Dtos.Requests;

namespace CatalogService.Api.Features.Payroll.Acik
{
    /// <summary>
    /// İstek → komut eşlemesi (açık uçlar için: calculate, export-excel, export-pdf).
    ///
    /// <c>PayrollController</c>'ın kendi özel <c>MapToCommand</c>'ı var ve o controller'a bu
    /// turda dokunulmadı (Prompt 18). İki eşleme birbirinden sapamasın diye
    /// <c>PayrollAcikTests</c> ikisini aynı dolu istekle karşılaştırıyor: isteğe yeni
    /// alan eklenip yalnız birinde eşlenirse test kırılır — açık sayfa sessizce farklı
    /// rakam üretemez.
    /// </summary>
    public static class CalculatePayrollEslemesi
    {
        public static CalculatePayrollCommand KomutaCevir(CalculatePayrollRequest request) =>
            new()
            {
                Year = request.Year,
                CalculationType = request.CalculationType,
                EmployeeType = request.EmployeeType,
                HasMandatoryBes = request.HasMandatoryBes,
                DisabilityType = request.DisabilityType,
                IncludeMinimumWageExemption = request.IncludeMinimumWageExemption,
                IncludeStampTax = request.IncludeStampTax,
                IncludeEmployerCost = request.IncludeEmployerCost,
                StartMonth = request.StartMonth,
                PreviousCumulativeTaxBase = request.PreviousCumulativeTaxBase,
                Months = request.Months,
                LawCode = request.LawCode,
                IsManufacturingSector = request.IsManufacturingSector,
            };
    }
}
