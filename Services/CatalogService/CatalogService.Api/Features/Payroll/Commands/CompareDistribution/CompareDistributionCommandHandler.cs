using CatalogService.Api.Features.Payroll.Dtos.Shared;
using CatalogService.Api.Features.Payroll.Services.Interfaces;
using MediatR;

namespace CatalogService.Api.Features.Payroll.Commands.CompareDistribution
{
    public class CompareDistributionCommandHandler : IRequestHandler<CompareDistributionCommand, DistributionComparisonResultDto>
    {
        private readonly IDistributionComparisonService _comparisonService;

        public CompareDistributionCommandHandler(IDistributionComparisonService comparisonService)
        {
            _comparisonService = comparisonService;
        }

        public Task<DistributionComparisonResultDto> Handle(
            CompareDistributionCommand request,
            CancellationToken cancellationToken)
        {
            // Karşılaştırma artık yıl bazlı kod-içi tarifeyle hesaplanıyor
            // (DistributionComparisonService). DB'deki ücret vergi dilimleri
            // bu hesapta kullanılmıyordu; ilgili sorgu ve "dilim bulunamadı"
            // exception'ı kaldırıldı — aksi halde 2026 dışı yıllar gereksiz patlıyordu.
            var result = _comparisonService.Compare(
                request.Year,
                request.YillikBrut,
                request.YillikVergiMaliyeti,
                request.YillikNetEleGecen,
                request.StopajOrani);

            return Task.FromResult(result);
        }
    }
}
