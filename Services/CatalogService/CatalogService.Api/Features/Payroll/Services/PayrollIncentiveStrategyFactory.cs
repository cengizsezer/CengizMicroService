using CatalogService.Api.Features.Payroll.Services.Interfaces;
using CatalogService.Api.Features.Payroll.Services.Strategies;

namespace CatalogService.Api.Features.Payroll.Services
{
    public static class PayrollIncentiveStrategyFactory
    {
        public static IPayrollIncentiveStrategy Create(string lawCode) => lawCode switch
        {
            "02828" => new Law02828Strategy(),
            "04691" => new Law04691Strategy(),
            "05510" or "15510" or "25510" => new Law05510Strategy(),
            "05746" or "15746"            => new Law05746Strategy(),
            "06111"                       => new Law06111Strategy(),
            "06486" or "46486" or "56486" or "66486" => new Law06486Strategy(),
            "06645"                       => new Law06645Strategy(),
            "14857"                       => new Law14857Strategy(),
            "16322" or "26322"            => new Law16322Strategy(),
            "25225" or "55225"            => new Law25225Strategy(),
            _                             => new NoIncentiveStrategy()
        };
    }
}
