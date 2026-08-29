using System.Net.Http.Json;
using CatalogService.Api.Features.Declarations.Dtos;
using WebApp.Application.Services.Interfaces;
using WebApp.Extensions;

namespace WebApp.Application.Services
{
    /// <inheritdoc cref="IBeyannameOzetApiService"/>
    public class BeyannameOzetApiService : IBeyannameOzetApiService
    {
        private const string Prefix = "/catalog/beyanname";

        private readonly HttpClient _http;

        public BeyannameOzetApiService(HttpClient http) => _http = http;

        public async Task<BeyannameOzetDto?> OzetGetAsync(int yil, int ay, CancellationToken ct = default)
            => await _http.GetResponseAsync<BeyannameOzetDto>($"{Prefix}/ozet?yil={yil}&ay={ay}");

        public async Task<List<BeyannameEkDto>> EkleriGetAsync(int declarationId, CancellationToken ct = default)
            => await _http.GetResponseAsync<List<BeyannameEkDto>>($"{Prefix}/{declarationId}/ekler") ?? new();

        public async Task<BeyannameEkSonucDto?> EkEkleAsync(int declarationId, BeyannameEkOlusturDto istek,
                                                            CancellationToken ct = default)
        {
            var resp = await _http.PostAsJsonAsync($"{Prefix}/{declarationId}/ekler", istek, ct);
            if (!resp.IsSuccessStatusCode) return null;

            return await resp.Content.ReadFromJsonAsync<BeyannameEkSonucDto>(cancellationToken: ct);
        }

        public async Task<int?> EkSilAsync(int declarationId, int ekId, CancellationToken ct = default)
        {
            var resp = await _http.DeleteAsync($"{Prefix}/{declarationId}/ekler/{ekId}", ct);
            if (!resp.IsSuccessStatusCode) return null;

            var sonuc = await resp.Content.ReadFromJsonAsync<EkSilmeSonucu>(cancellationToken: ct);
            return sonuc?.FileId;
        }

        private sealed class EkSilmeSonucu
        {
            public int FileId { get; set; }
        }
    }
}
