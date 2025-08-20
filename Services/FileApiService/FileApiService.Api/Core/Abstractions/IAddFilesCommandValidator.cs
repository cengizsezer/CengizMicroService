using FileApiService.Api.Domain.Commands;
using FluentResults;

namespace FileApiService.Api.Core.Abstractions
{
    public interface IAddFilesCommandValidator { Result<bool> Validate(AddFilesCommand cmd); }
}
