using FileApiService.Api.Domain.Abstractions;

namespace FileApiService.Api.Domain.Commands
{
    public sealed class AddCompanyDocCommand
    {
        public required IFileProxy File { get; init; }
        public required string CompanyId { get; init; }
        public required string Year { get; init; }
        public required string DocCategory { get; init; } // ENVANTERDEFTERI, VERGILEVHASI, ...

        public string? Description { get; init; }
        public int? SequenceNo { get; init; }
    }
}
