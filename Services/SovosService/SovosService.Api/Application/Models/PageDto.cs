namespace SovosService.Api.Application.Models;

public class PageDto<T>
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int Count { get; set; }
    public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();
}
