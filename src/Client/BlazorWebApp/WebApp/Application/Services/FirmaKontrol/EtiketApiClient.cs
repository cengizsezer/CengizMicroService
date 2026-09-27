using System.Net.Http.Json;
using WebApp.Application.Services.Yonetim; // ApiErrorParser
using WebApp.Shared.Dto.FirmaKontrol;

namespace WebApp.Application.Services.FirmaKontrol
{
    /// <summary>
    /// Etiket TANIMLARININ (boyut / değer / kural) CRUD'u. Hangi hesaba hangi etiketin
    /// düştüğü sunucuda tutulmaz; onu <see cref="WebApp.Application.Services.EtiketMotoru"/>
    /// çalışma zamanında hesaplar.
    /// </summary>
    public interface IEtiketApiClient
    {
        Task<List<EtiketBoyutuDto>> GetBoyutlarAsync(int firmaId, CancellationToken ct = default);

        Task<EtiketBoyutuDto> BoyutEkleAsync(int firmaId, EtiketBoyutuYazDto dto, CancellationToken ct = default);
        Task BoyutGuncelleAsync(int firmaId, int boyutId, EtiketBoyutuYazDto dto, CancellationToken ct = default);
        Task BoyutSilAsync(int firmaId, int boyutId, CancellationToken ct = default);

        Task<EtiketDegeriDto> DegerEkleAsync(int firmaId, int boyutId, EtiketDegeriYazDto dto, CancellationToken ct = default);
        Task DegerGuncelleAsync(int firmaId, int degerId, EtiketDegeriYazDto dto, CancellationToken ct = default);
        Task DegerSilAsync(int firmaId, int degerId, CancellationToken ct = default);

        Task<EtiketKuraliDto> KuralEkleAsync(int firmaId, int boyutId, EtiketKuraliYazDto dto, CancellationToken ct = default);
        Task KuralGuncelleAsync(int firmaId, long kuralId, EtiketKuraliYazDto dto, CancellationToken ct = default);
        Task KuralSilAsync(int firmaId, long kuralId, CancellationToken ct = default);
        Task KuralSiraGuncelleAsync(int firmaId, int boyutId, List<EtiketKuralSiraDto> siralar, CancellationToken ct = default);
    }

    public class EtiketApiClient : IEtiketApiClient
    {
        // Gateway yolu — FirmaKontrolApiClient ile AYNI desen. Ocelot "/catalog/{everything}"
        // isteğini "/api/catalog/{everything}" adresine taşıdığı için istemci "api/" öneki
        // YAZMAZ; yazarsa adres "/api/catalog/api/catalog/..." olup 404 döner.
        private const string Base = "/catalog/firma-kontrol";

        private readonly HttpClient _http;

        public EtiketApiClient(HttpClient http) => _http = http;

        public async Task<List<EtiketBoyutuDto>> GetBoyutlarAsync(int firmaId, CancellationToken ct = default)
        {
            var response = await _http.GetAsync($"{Base}/{firmaId}/etiket/boyutlar", ct);
            await HataKontrol(response, ct);

            return await response.Content.ReadFromJsonAsync<List<EtiketBoyutuDto>>(cancellationToken: ct)
                   ?? new List<EtiketBoyutuDto>();
        }

        public Task<EtiketBoyutuDto> BoyutEkleAsync(int firmaId, EtiketBoyutuYazDto dto, CancellationToken ct = default)
            => PostAsync<EtiketBoyutuYazDto, EtiketBoyutuDto>($"{Base}/{firmaId}/etiket/boyutlar", dto, ct);

        public Task BoyutGuncelleAsync(int firmaId, int boyutId, EtiketBoyutuYazDto dto, CancellationToken ct = default)
            => PutAsync($"{Base}/{firmaId}/etiket/boyutlar/{boyutId}", dto, ct);

        public Task BoyutSilAsync(int firmaId, int boyutId, CancellationToken ct = default)
            => DeleteAsync($"{Base}/{firmaId}/etiket/boyutlar/{boyutId}", ct);

        public Task<EtiketDegeriDto> DegerEkleAsync(int firmaId, int boyutId, EtiketDegeriYazDto dto, CancellationToken ct = default)
            => PostAsync<EtiketDegeriYazDto, EtiketDegeriDto>($"{Base}/{firmaId}/etiket/boyutlar/{boyutId}/degerler", dto, ct);

        public Task DegerGuncelleAsync(int firmaId, int degerId, EtiketDegeriYazDto dto, CancellationToken ct = default)
            => PutAsync($"{Base}/{firmaId}/etiket/degerler/{degerId}", dto, ct);

        public Task DegerSilAsync(int firmaId, int degerId, CancellationToken ct = default)
            => DeleteAsync($"{Base}/{firmaId}/etiket/degerler/{degerId}", ct);

        public Task<EtiketKuraliDto> KuralEkleAsync(int firmaId, int boyutId, EtiketKuraliYazDto dto, CancellationToken ct = default)
            => PostAsync<EtiketKuraliYazDto, EtiketKuraliDto>($"{Base}/{firmaId}/etiket/boyutlar/{boyutId}/kurallar", dto, ct);

        public Task KuralGuncelleAsync(int firmaId, long kuralId, EtiketKuraliYazDto dto, CancellationToken ct = default)
            => PutAsync($"{Base}/{firmaId}/etiket/kurallar/{kuralId}", dto, ct);

        public Task KuralSilAsync(int firmaId, long kuralId, CancellationToken ct = default)
            => DeleteAsync($"{Base}/{firmaId}/etiket/kurallar/{kuralId}", ct);

        public Task KuralSiraGuncelleAsync(int firmaId, int boyutId, List<EtiketKuralSiraDto> siralar, CancellationToken ct = default)
            => PutAsync($"{Base}/{firmaId}/etiket/boyutlar/{boyutId}/kural-sira", siralar, ct);

        // ── Ortak ──

        private async Task<TSonuc> PostAsync<TIstek, TSonuc>(string url, TIstek dto, CancellationToken ct)
        {
            var response = await _http.PostAsJsonAsync(url, dto, ct);
            await HataKontrol(response, ct);

            return await response.Content.ReadFromJsonAsync<TSonuc>(cancellationToken: ct)
                   ?? throw new InvalidOperationException("Sunucudan boş yanıt geldi.");
        }

        private async Task PutAsync<TIstek>(string url, TIstek dto, CancellationToken ct)
        {
            var response = await _http.PutAsJsonAsync(url, dto, ct);
            await HataKontrol(response, ct);
        }

        private async Task DeleteAsync(string url, CancellationToken ct)
        {
            var response = await _http.DeleteAsync(url, ct);
            await HataKontrol(response, ct);
        }

        private static async Task HataKontrol(HttpResponseMessage response, CancellationToken ct)
        {
            if (!response.IsSuccessStatusCode)
                await ApiErrorParser.ThrowAsync(response, ct);
        }
    }
}
