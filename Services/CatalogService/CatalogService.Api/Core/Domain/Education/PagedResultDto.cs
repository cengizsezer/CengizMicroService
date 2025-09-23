namespace CatalogService.Api.Core.Domain.Education
{
    public readonly record struct PagedResultDto<T>(
     int Total, int Page, int PageSize, IReadOnlyList<T> Items
 );
}
