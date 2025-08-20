using FileApiService.Api.Domain.Commands;
using Validot;

namespace FileApiService.Api.Core.Validation
{
    internal sealed class AddFileCommandSpecificationHolder : ISpecificationHolder<AddFilesCommand>
    {
        public Specification<AddFilesCommand> Specification { get; }
        public AddFileCommandSpecificationHolder()
        {
            Specification<AddFilesCommand> spec = s => s.Member(m => m.Files, m => m.AsCollection(GeneralPredicates.fileSpecification));
            Specification = spec;
        }
    }
}
