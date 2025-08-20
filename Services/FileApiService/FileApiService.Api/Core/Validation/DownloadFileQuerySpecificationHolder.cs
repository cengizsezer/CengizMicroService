using FileApiService.Api.Domain.Queries;
using Validot;

namespace FileApiService.Api.Core.Validation
{
    internal sealed class DownloadFileQuerySpecificationHolder : ISpecificationHolder<DownloadFileQuery>
    {
        public Specification<DownloadFileQuery> Specification { get; }
        public DownloadFileQuerySpecificationHolder()
        {
            Specification<DownloadFileQuery> spec = s => s.Member(m => m.Id, m => m.Rule(GeneralPredicates.isValidId));
            Specification = spec;
        }
    }
}
