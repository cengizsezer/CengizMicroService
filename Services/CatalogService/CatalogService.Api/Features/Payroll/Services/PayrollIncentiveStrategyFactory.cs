using CatalogService.Api.Features.Payroll.Services.Interfaces;
using CatalogService.Api.Features.Payroll.Services.Strategies;

namespace CatalogService.Api.Features.Payroll.Services
{
    /// <summary>
    /// YALNIZ 2026 öncesi yıllar (dondurulmuş hesap, yanlış oranlar dahil). 2026+ bütün
    /// teşvikler <see cref="TesvikKuraliStratejisi"/> ile kural tablosundan hesaplanır.
    /// Anahtarlar <c>PayrollTesvikKurallari</c>'ndaki IlkYil'i boş olanlarla aynı.
    /// </summary>
    public static class PayrollIncentiveStrategyFactory
    {
        public static IPayrollIncentiveStrategy Create(string lawCode) => lawCode switch
        {
            "02828"   => new Law02828Strategy(),
            "04691"   => new Law04691Strategy(),
            "05510"   => new Law05510Strategy(),
            "05746"   => new Law05746Strategy(),
            "06111"   => new Law06111Strategy(),
            "14857"   => new Law14857Strategy(),
            // Eski 16322 ve 26322 aynı hesabı paylaşıyordu; bölge alt seçimi 2026'dan itibaren.
            "YATIRIM" => new Law16322Strategy(),
            _         => new NoIncentiveStrategy()
        };
    }
}
