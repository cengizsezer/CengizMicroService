using FileApiService.Api.Domain.Abstractions;
using Validot;

namespace FileApiService.Api.Core.Validation
{
    internal static class GeneralPredicates
    {
        internal static readonly Predicate<int> isValidId = m => m > 0 && m < int.MaxValue;
        internal static readonly Predicate<string> isValidFileName =
            name => !string.IsNullOrWhiteSpace(name) &&
                    name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
                    !name.Contains(Path.DirectorySeparatorChar) &&
                    !name.Contains(Path.AltDirectorySeparatorChar);

        internal static readonly Specification<IFileProxy> fileSpecification = f =>
            f.Member(m => m.FileName, m => m.NotEmpty().And().NotWhiteSpace().And().Rule(isValidFileName));
    }
}
