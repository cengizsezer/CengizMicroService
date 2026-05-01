using CatalogService.Api.Features.Payroll.Dtos.Shared;
using MediatR;

namespace CatalogService.Api.Features.Payroll.Commands.CompareDistribution
{
    public class CompareDistributionCommand : IRequest<DistributionComparisonResultDto>
    {
        public int Year { get; set; }
        public decimal YillikBrut { get; set; }
        public decimal YillikVergiMaliyeti { get; set; }
        public decimal YillikNetEleGecen { get; set; }

        /// <summary>
        /// Stopaj oranı (örn 0.15 = %15). Geçerli değerler: 0.05, 0.10, 0.15.
        /// </summary>
        public decimal StopajOrani { get; set; } = 0.15m;
    }
}
