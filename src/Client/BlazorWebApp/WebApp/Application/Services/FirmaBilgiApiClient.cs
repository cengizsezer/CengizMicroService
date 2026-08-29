using System.Net.Http.Json;
using WebApp.Extensions;
using WebApp.Shared.Dto.FirmaBilgileri;

namespace WebApp.Application.Services
{
    /// <summary>
    /// Firma Bilgileri uç noktaları. Firma kapsamı <b>her istekte</b> <c>?firmaId=</c>
    /// olarak gider; sunucu tarafındaki filtre eksik parametreyi 400 ile reddediyor
    /// (Banka Otomasyon'daki mekanizmanın aynısı).
    /// </summary>
    public interface IFirmaBilgiApiClient
    {
        Task<FirmaSicilDto?> SicilGetAsync(int firmaId, CancellationToken ct = default);
        Task<(FirmaSicilDto? Sonuc, string? Hata)> SicilKaydetAsync(int firmaId, FirmaSicilDto dto, CancellationToken ct = default);

        Task<FirmaOrtaklikDto?> OrtaklarGetAsync(int firmaId, CancellationToken ct = default);
        Task<(FirmaOrtaklikDto? Sonuc, string? Hata)> OrtaklarKaydetAsync(int firmaId, List<FirmaOrtakDto> ortaklar, CancellationToken ct = default);

        Task<List<FirmaImzaYetkilisiDto>> YetkililerGetAsync(int firmaId, CancellationToken ct = default);
        Task<(List<FirmaImzaYetkilisiDto>? Sonuc, string? Hata)> YetkililerKaydetAsync(int firmaId, List<FirmaImzaYetkilisiDto> yetkililer, CancellationToken ct = default);

        Task<List<FirmaBelgesiDto>> BelgelerGetAsync(int firmaId, CancellationToken ct = default);
        Task<(FirmaBelgesiDto? Sonuc, string? Hata)> BelgeEkleAsync(int firmaId, FirmaBelgesiOlusturDto istek, CancellationToken ct = default);
        Task<int?> BelgeSilAsync(int firmaId, int belgeId, CancellationToken ct = default);
    }

    /// <inheritdoc cref="IFirmaBilgiApiClient"/>
    public class FirmaBilgiApiClient : IFirmaBilgiApiClient
    {
        private const string Prefix = "/catalog/firma-bilgileri";

        private readonly HttpClient _http;

        public FirmaBilgiApiClient(HttpClient http) => _http = http;

        private static string Yol(string parca, int firmaId) => $"{Prefix}/{parca}?firmaId={firmaId}";

        public Task<FirmaSicilDto?> SicilGetAsync(int firmaId, CancellationToken ct = default)
            => _http.GetResponseAsync<FirmaSicilDto?>(Yol("sicil", firmaId));

        public Task<(FirmaSicilDto? Sonuc, string? Hata)> SicilKaydetAsync(int firmaId, FirmaSicilDto dto, CancellationToken ct = default)
            => Kaydet<FirmaSicilDto, FirmaSicilDto>(Yol("sicil", firmaId), dto, ct);

        public Task<FirmaOrtaklikDto?> OrtaklarGetAsync(int firmaId, CancellationToken ct = default)
            => _http.GetResponseAsync<FirmaOrtaklikDto?>(Yol("ortaklar", firmaId));

        public Task<(FirmaOrtaklikDto? Sonuc, string? Hata)> OrtaklarKaydetAsync(int firmaId, List<FirmaOrtakDto> ortaklar, CancellationToken ct = default)
            => Kaydet<FirmaOrtaklikDto, List<FirmaOrtakDto>>(Yol("ortaklar", firmaId), ortaklar, ct);

        public async Task<List<FirmaImzaYetkilisiDto>> YetkililerGetAsync(int firmaId, CancellationToken ct = default)
            => await _http.GetResponseAsync<List<FirmaImzaYetkilisiDto>>(Yol("imza-yetkilileri", firmaId)) ?? new();

        public Task<(List<FirmaImzaYetkilisiDto>? Sonuc, string? Hata)> YetkililerKaydetAsync(int firmaId, List<FirmaImzaYetkilisiDto> yetkililer, CancellationToken ct = default)
            => Kaydet<List<FirmaImzaYetkilisiDto>, List<FirmaImzaYetkilisiDto>>(Yol("imza-yetkilileri", firmaId), yetkililer, ct);

        public async Task<List<FirmaBelgesiDto>> BelgelerGetAsync(int firmaId, CancellationToken ct = default)
            => await _http.GetResponseAsync<List<FirmaBelgesiDto>>(Yol("belgeler", firmaId)) ?? new();

        public async Task<(FirmaBelgesiDto? Sonuc, string? Hata)> BelgeEkleAsync(int firmaId, FirmaBelgesiOlusturDto istek, CancellationToken ct = default)
        {
            var resp = await _http.PostAsJsonAsync(Yol("belgeler", firmaId), istek, ct);
            if (resp.IsSuccessStatusCode)
                return (await resp.Content.ReadFromJsonAsync<FirmaBelgesiDto>(cancellationToken: ct), null);

            return (default, await HataOku(resp, ct));
        }

        public async Task<int?> BelgeSilAsync(int firmaId, int belgeId, CancellationToken ct = default)
        {
            var resp = await _http.DeleteAsync($"{Prefix}/belgeler/{belgeId}?firmaId={firmaId}", ct);
            if (!resp.IsSuccessStatusCode) return null;

            var sonuc = await resp.Content.ReadFromJsonAsync<SilmeSonucu>(cancellationToken: ct);
            return sonuc?.FileId;
        }

        /// <summary>
        /// PUT + hata mesajı. Sunucu kural ihlallerini <c>{ field, message }</c> ile
        /// döndürüyor; mesaj kullanıcıya olduğu gibi gösteriliyor — istemci kendi
        /// tahminini yazarsa iki taraf ayrışır.
        /// </summary>
        private async Task<(TSonuc? Sonuc, string? Hata)> Kaydet<TSonuc, TIstek>(string yol, TIstek istek, CancellationToken ct)
        {
            var resp = await _http.PutAsJsonAsync(yol, istek, ct);
            if (resp.IsSuccessStatusCode)
                return (await resp.Content.ReadFromJsonAsync<TSonuc>(cancellationToken: ct), null);

            return (default, await HataOku(resp, ct));
        }

        private static async Task<string> HataOku(HttpResponseMessage resp, CancellationToken ct)
        {
            try
            {
                var hata = await resp.Content.ReadFromJsonAsync<KuralHatasi>(cancellationToken: ct);
                if (!string.IsNullOrWhiteSpace(hata?.Message)) return hata!.Message!;
            }
            catch
            {
                // Gövde beklenen biçimde değil; aşağıdaki genel mesaja düşülür.
            }

            return $"İstek başarısız ({(int)resp.StatusCode}).";
        }

        private sealed class KuralHatasi
        {
            public string? Field { get; set; }
            public string? Message { get; set; }
        }

        private sealed class SilmeSonucu
        {
            public int FileId { get; set; }
        }
    }
}
