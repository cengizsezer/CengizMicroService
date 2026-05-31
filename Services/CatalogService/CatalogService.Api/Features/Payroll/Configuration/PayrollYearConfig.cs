using CatalogService.Api.Features.Payroll.Entities;

namespace CatalogService.Api.Features.Payroll.Configuration
{
    /// <summary>
    /// Bir yıla ait payroll parametre paketi: PayrollParameter + tax brackets + engellilik istisnaları.
    /// Mevcut entity tipleri reused ediliyor ki engine / strategy kodu değişmesin.
    /// </summary>
    public sealed class PayrollYearConfig
    {
        public required PayrollParameter Parameter { get; init; }
        public required IReadOnlyList<PayrollTaxBracket> TaxBrackets { get; init; }
        public required IReadOnlyList<PayrollDisabilityExemption> DisabilityExemptions { get; init; }
    }
}
