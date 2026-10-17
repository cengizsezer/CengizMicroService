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

        // DB entity'sine (PayrollParameter) eklenmedi: tablo/migration değişmesin.

        /// <summary>
        /// Emekli (SGDP) işveren sosyal güvenlik destek primi oranı; üstüne işveren KVSK eklenir,
        /// GSS ve işsizlik primi yok. null: işveren tarafında normal çalışan oranları (2026 öncesi, dondurulmuş).
        /// </summary>
        public decimal? RetiredSgdpEmployerRate { get; init; }

        /// <summary>
        /// 4691/5746 GV ve DV teşvikinin uygulandığı brüt ücret üst sınırı = aylık brüt asgari ücret × katsayı
        /// (7555 s. Kanun). null: sınırsız (2026 öncesi, dondurulmuş).
        /// </summary>
        public decimal? IncomeTaxIncentiveCeilingMultiplier { get; init; }
    }
}
