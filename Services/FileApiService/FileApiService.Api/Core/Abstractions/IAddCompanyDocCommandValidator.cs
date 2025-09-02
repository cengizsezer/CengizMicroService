using FileApiService.Api.Domain.Commands;
using FluentResults;
using Validot;

namespace FileApiService.Api.Core.Abstractions
{
    public interface IAddCompanyDocCommandValidator
    {
        Result<bool> Validate(AddCompanyDocCommand cmd);
    }
    
}

