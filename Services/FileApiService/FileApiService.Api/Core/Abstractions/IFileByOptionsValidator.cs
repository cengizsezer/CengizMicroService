using FileApiService.Api.Domain.Abstractions;
using FluentResults;

namespace FileApiService.Api.Core.Abstractions
{
    internal interface IFileByOptionsValidator
    {
        Result<bool> Validate(IFileProxy file);
        Result<bool> Validate(string extension);
        Result<bool> ValidateConversion(string sourceExtension, string destinationExtension);
    }
}
