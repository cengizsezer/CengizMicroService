using System.Net.Http.Json;
using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services.Yonetim
{
    public class PersonelMailApiClient : IPersonelMailApiClient
    {
        private const string Base = "/catalog/personnel-emails";

        private readonly HttpClient _http;
        public PersonelMailApiClient(HttpClient http) => _http = http;

        public async Task<List<PersonelMailDto>> GetAllAsync(CancellationToken ct = default)
            => await _http.GetFromJsonAsync<List<PersonelMailDto>>(Base, ct) ?? new();

        public async Task<PersonelMailDto> UpsertAsync(UpsertPersonelMailRequest req, CancellationToken ct = default)
        {
            var resp = await _http.PutAsJsonAsync(Base, req, ct);
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<PersonelMailDto>(cancellationToken: ct))!;
        }
    }
}
