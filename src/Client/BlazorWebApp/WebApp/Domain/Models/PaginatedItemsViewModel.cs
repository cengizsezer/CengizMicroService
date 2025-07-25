using System.Collections.Generic;
using Newtonsoft.Json;

namespace WebApp.Domain.Models
{
    public class PaginatedItemsViewModel<TEntity> where TEntity : class
    {
        [JsonProperty("pageIndex")]
        public int PageIndex { get; set; }

        [JsonProperty("pageSize")]
        public int PageSize { get; set; }

        [JsonProperty("count")]
        public long Count { get; set; }

        [JsonProperty("data")]
        public IEnumerable<TEntity> Data { get; set; } = new List<TEntity>();

        public PaginatedItemsViewModel(int pageIndex, int pageSize, long count, IEnumerable<TEntity> data)
        {
            PageIndex = pageIndex;
            PageSize = pageSize;
            Count = count;
            Data = data;
        }

        public PaginatedItemsViewModel()
        {
        }
    }
}
