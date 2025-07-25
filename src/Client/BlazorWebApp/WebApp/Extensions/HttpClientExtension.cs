using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WebApp.Extensions
{
    public static class HttpClientExtension
    {
        public static async Task<TResult> PostGetResponseAsync<TResult, TValue>(this HttpClient client, string url, TValue value)
        {
            var json = JsonConvert.SerializeObject(value);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpRes = await client.PostAsync(url, content);
            if (!httpRes.IsSuccessStatusCode) return default;

            var responseJson = await httpRes.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TResult>(responseJson);
        }

        public static async Task PostAsync<TValue>(this HttpClient client, string url, TValue value)
        {
            var json = JsonConvert.SerializeObject(value);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            await client.PostAsync(url, content);
        }

        public static async Task<T> GetResponseAsync<T>(this HttpClient client, string url)
        {
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return default;

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
