using FluentResults;

namespace FileApiService.Api.Core.Extensions
{
    public static class FluentResultExtensions
    {
        public static IEnumerable<string> ToErrorMessages(this IEnumerable<IError> errors) =>
            errors?.Select(e => e.Message) ?? Enumerable.Empty<string>();

        public static string JoinToMessage(this IEnumerable<IError> errors) =>
            string.Join(',', errors?.Select(e => e.Message) ?? Enumerable.Empty<string>());
    }
}
