using CatalogService.Api.Features.Payroll.Dtos.Shared;
using CatalogService.Api.Features.Payroll.Services.Interfaces;
using CatalogService.Api.Infrastructure.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Payroll.Commands.CompareDistribution
{
    public class CompareDistributionCommandHandler : IRequestHandler<CompareDistributionCommand, DistributionComparisonResultDto>
    {
        private readonly CatalogContext _context;
        private readonly IDistributionComparisonService _comparisonService;

        public CompareDistributionCommandHandler(
            CatalogContext context,
            IDistributionComparisonService comparisonService)
        {
            _context = context;
            _comparisonService = comparisonService;
        }

        public async Task<DistributionComparisonResultDto> Handle(
            CompareDistributionCommand request,
            CancellationToken cancellationToken)
        {
            var taxBrackets = await _context.PayrollTaxBrackets
                .AsNoTracking()
                .Where(x => x.Year == request.Year)
                .OrderBy(x => x.Order)
                .ToListAsync(cancellationToken);

            if (taxBrackets.Count == 0)
                throw new InvalidOperationException($"'{request.Year}' yılı için vergi dilimi bulunamadı.");

            return _comparisonService.Compare(
                request.Year,
                request.YillikBrut,
                request.YillikVergiMaliyeti,
                request.YillikNetEleGecen,
                request.StopajOrani,
                taxBrackets);
        }
    }
}
