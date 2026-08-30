using System.Net;
using System.Net.Http.Json;
using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services.Yonetim
{
    /// <summary>
    /// Ajan işleri. Hem Aktar ekranı (ORKA'ya aktar düğmesi + durum kartı) hem de
    /// Yönetim &gt; Ajanlar (son işler) buradan besleniyor.
    /// </summary>
    public interface IAjanIsApi
    {
        /// <summary>
        /// İş oluşturur. Ajan meşgulse sunucu 409 döner; sonuçtaki
        /// <c>CakisanIs</c> dolu gelir ve <c>Is</c> boş kalır — çağıran taraf ikisini
        /// ayırabilsin diye istisna atılmıyor.
        /// </summary>
        Task<AjanIsiOlusturSonucuDto> OlusturAsync(YeniAjanIsiRequest istek, CancellationToken ct = default);

        Task<AjanIsDto?> GetirAsync(Guid id, CancellationToken ct = default);

        Task<List<AjanIsDto>> ListeleAsync(int? firmaId = null, string? ajanId = null,
                                           int enFazla = 20, CancellationToken ct = default);

        Task<AjanIsDto?> IptalAsync(Guid id, CancellationToken ct = default);

        /// <summary>Hub'a o an bağlı ajanlar; ekran "ajan bağlı değil" uyarısını buna bakarak veriyor.</summary>
        Task<List<BagliAjanDto>> BaglilarAsync(CancellationToken ct = default);
    }

    public class AjanIsApi : IAjanIsApi
    {
        private const string Base = "/catalog/agent";

        private readonly HttpClient _http;
        public AjanIsApi(HttpClient http) => _http = http;

        public async Task<AjanIsiOlusturSonucuDto> OlusturAsync(YeniAjanIsiRequest istek, CancellationToken ct = default)
        {
            var yanit = await _http.PostAsJsonAsync($"{Base}/is", istek, ct);

            // 409 ve 400 da gövdesinde sonucu taşıyor: mesaj kullanıcıya olduğu gibi
            // gösteriliyor.
            if (yanit.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
            {
                var reddedilen = await yanit.Content.ReadFromJsonAsync<AjanIsiOlusturSonucuDto>(cancellationToken: ct);
                return reddedilen ?? new AjanIsiOlusturSonucuDto { Mesaj = "İş oluşturulamadı." };
            }

            yanit.EnsureSuccessStatusCode();
            return (await yanit.Content.ReadFromJsonAsync<AjanIsiOlusturSonucuDto>(cancellationToken: ct))!;
        }

        public async Task<AjanIsDto?> GetirAsync(Guid id, CancellationToken ct = default)
        {
            var yanit = await _http.GetAsync($"{Base}/is/{id}", ct);
            if (yanit.StatusCode == HttpStatusCode.NotFound) return null;

            yanit.EnsureSuccessStatusCode();
            return await yanit.Content.ReadFromJsonAsync<AjanIsDto>(cancellationToken: ct);
        }

        public async Task<List<AjanIsDto>> ListeleAsync(int? firmaId = null, string? ajanId = null,
                                                        int enFazla = 20, CancellationToken ct = default)
        {
            var sorgu = $"?enFazla={enFazla}";
            if (firmaId is > 0) sorgu += $"&firmaId={firmaId}";
            if (!string.IsNullOrWhiteSpace(ajanId)) sorgu += $"&ajanId={Uri.EscapeDataString(ajanId)}";

            return await _http.GetFromJsonAsync<List<AjanIsDto>>($"{Base}/isler{sorgu}", ct) ?? new();
        }

        public async Task<AjanIsDto?> IptalAsync(Guid id, CancellationToken ct = default)
        {
            var yanit = await _http.PostAsync($"{Base}/is/{id}/iptal", content: null, ct);
            if (yanit.StatusCode == HttpStatusCode.NotFound) return null;

            yanit.EnsureSuccessStatusCode();
            return await yanit.Content.ReadFromJsonAsync<AjanIsDto>(cancellationToken: ct);
        }

        public async Task<List<BagliAjanDto>> BaglilarAsync(CancellationToken ct = default)
            => await _http.GetFromJsonAsync<List<BagliAjanDto>>($"{Base}/baglilar", ct) ?? new();
    }
}
