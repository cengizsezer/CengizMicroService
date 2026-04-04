using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WebApp.Pages.TaxPaymentPage.DTO;

namespace WebApp.Pages.TaxPaymentPage.Client
{
    public class TaxPaymentClient : ITaxPaymentClient
    {
        private readonly HttpClient _httpClient;
        private const string Base = "/catalog/taxpayments";

        public TaxPaymentClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<bool> DeleteAllAsync()
            => (await _httpClient.DeleteAsync($"{Base}/alldelete")).IsSuccessStatusCode;

        public async Task<List<TaxPaymentEntityDto>> GetAllAsync()
            => await _httpClient.GetFromJsonAsync<List<TaxPaymentEntityDto>>(Base)
               ?? new List<TaxPaymentEntityDto>();

        public Task<TaxPaymentEntityDto?> GetAsync(int id)
            => _httpClient.GetFromJsonAsync<TaxPaymentEntityDto>($"{Base}/{id}");

        public async Task<bool> CreateAsync(TaxPaymentEntityDto model)
            => (await _httpClient.PostAsJsonAsync(Base, model)).IsSuccessStatusCode;

        public async Task<bool> UpdateAsync(TaxPaymentEntityDto model)
            => (await _httpClient.PutAsJsonAsync($"{Base}/{model.Id}", model)).IsSuccessStatusCode;

        public async Task<bool> DeleteAsync(int id)
            => (await _httpClient.DeleteAsync($"{Base}/{id}")).IsSuccessStatusCode;

        public async Task<bool> ImportAsync(List<TaxPaymentEntityDto> items)
            => (await _httpClient.PostAsJsonAsync($"{Base}/import", items)).IsSuccessStatusCode;

        public Task<byte[]> ExportExcelAsync()
            => _httpClient.GetByteArrayAsync($"{Base}/export");


        public async Task<List<TaxPaymentEntityDto>> ParseExcelAsync(IBrowserFile file)
        {
            using var stream = file.OpenReadStream(100_000_000);
            using var content = new MultipartFormDataContent();

            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

            content.Add(fileContent, "file", file.Name);

            var resp = await _httpClient.PostAsync($"{Base}/parse-excel", content);

            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                throw new Exception(err);
            }

            return await resp.Content.ReadFromJsonAsync<List<TaxPaymentEntityDto>>()
                   ?? new List<TaxPaymentEntityDto>();
        }
    }
}
