namespace CatalogService.Api.Features.Education.DTO
{
    public readonly record struct PagedResultDto<T>(
     int Total, int Page, int PageSize, IReadOnlyList<T> Items
 );
}
