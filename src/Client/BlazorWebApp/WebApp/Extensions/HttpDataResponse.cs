using Newtonsoft.Json;

namespace WebApp.Extensions
{
    public sealed class HttpDataResponse<T>
    {
        [JsonProperty("data")]
        public T? Data { get; set; }

        [JsonProperty("errors")]
        public List<string>? Errors { get; set; }

        [JsonProperty("succeeded")]
        public bool? Succeeded { get; set; }
    }
}
