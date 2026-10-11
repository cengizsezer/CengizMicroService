using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WebApp.Application.Services.Interfaces;
using WebApp.Shared.Dto.MevzuatGrafigi;

namespace WebApp.Application.Services
{
    public class MevzuatGrafigiApi : IMevzuatGrafigiApi
    {
        private readonly HttpClient _http;
        private const string Prefix = "/catalog/mevzuat-grafigi";

        public MevzuatGrafigiApi(HttpClient http) => _http = http;

        public Task<MevzuatGrafigiSonuc<MevzuatGrafigiDto>> GetAsync()
            => Oku<MevzuatGrafigiDto>(() => _http.GetAsync(Prefix));

        // ---- Kart ----

        public Task<MevzuatGrafigiSonuc<MevzuatKartDto>> KartEkleAsync(MevzuatKartSaveDto dto)
            => Oku<MevzuatKartDto>(() => _http.PostAsJsonAsync($"{Prefix}/kartlar", dto));

        public Task<MevzuatGrafigiSonuc<MevzuatKartDto>> KartGuncelleAsync(int id, MevzuatKartSaveDto dto)
            => Oku<MevzuatKartDto>(() => _http.PutAsJsonAsync($"{Prefix}/kartlar/{id}", dto));

        public Task<MevzuatGrafigiSonuc> KartSilAsync(int id)
            => Yaz(() => _http.DeleteAsync($"{Prefix}/kartlar/{id}"));

        // ---- Bağlantı ----

        public Task<MevzuatGrafigiSonuc<MevzuatBaglantiDto>> BaglantiEkleAsync(MevzuatBaglantiSaveDto dto)
            => Oku<MevzuatBaglantiDto>(() => _http.PostAsJsonAsync($"{Prefix}/baglantilar", dto));

        public Task<MevzuatGrafigiSonuc> BaglantiSilAsync(int id)
            => Yaz(() => _http.DeleteAsync($"{Prefix}/baglantilar/{id}"));

        // ---- Dışlama ----

        public Task<MevzuatGrafigiSonuc<MevzuatDislamaDto>> DislamaEkleAsync(MevzuatDislamaSaveDto dto)
            => Oku<MevzuatDislamaDto>(() => _http.PostAsJsonAsync($"{Prefix}/dislamalar", dto));

        public Task<MevzuatGrafigiSonuc> DislamaSilAsync(int id)
            => Yaz(() => _http.DeleteAsync($"{Prefix}/dislamalar/{id}"));

        // ---- Tuval ----

        public Task<MevzuatGrafigiSonuc<MevzuatTuvalDto>> TuvalEkleAsync(MevzuatTuvalSaveDto dto)
            => Oku<MevzuatTuvalDto>(() => _http.PostAsJsonAsync($"{Prefix}/tuvaller", dto));

        public Task<MevzuatGrafigiSonuc<MevzuatTuvalDto>> TuvalGuncelleAsync(int id, MevzuatTuvalSaveDto dto)
            => Oku<MevzuatTuvalDto>(() => _http.PutAsJsonAsync($"{Prefix}/tuvaller/{id}", dto));

        public Task<MevzuatGrafigiSonuc<MevzuatYerlesimDto>> TuvaleKartEkleAsync(int tuvalId, MevzuatYerlesimDto dto)
            => Oku<MevzuatYerlesimDto>(() => _http.PostAsJsonAsync($"{Prefix}/tuvaller/{tuvalId}/kartlar", dto));

        public Task<MevzuatGrafigiSonuc> TuvaldenKartKaldirAsync(int tuvalId, int kartId)
            => Yaz(() => _http.DeleteAsync($"{Prefix}/tuvaller/{tuvalId}/kartlar/{kartId}"));

        public Task<MevzuatGrafigiSonuc<List<MevzuatYerlesimDto>>> YerlesimGuncelleAsync(int tuvalId, List<MevzuatYerlesimDto> liste)
            => Oku<List<MevzuatYerlesimDto>>(() => _http.PutAsJsonAsync($"{Prefix}/tuvaller/{tuvalId}/yerlesim", liste));

        // ---- Şart takip ----

        public Task<MevzuatGrafigiSonuc<MevzuatSartTakipDto>> SartTakipKaydetAsync(MevzuatSartTakipDto dto)
            => Oku<MevzuatSartTakipDto>(() => _http.PutAsJsonAsync($"{Prefix}/sart-takip", dto));

        // ---- Yardımcılar ----

        private static async Task<MevzuatGrafigiSonuc<T>> Oku<T>(Func<Task<HttpResponseMessage>> istek)
        {
            try
            {
                using var resp = await istek();
                if (!resp.IsSuccessStatusCode)
                    return MevzuatGrafigiSonuc<T>.Hata(await HataMesaji(resp));

                var veri = await resp.Content.ReadFromJsonAsync<T>();
                return veri is null
                    ? MevzuatGrafigiSonuc<T>.Hata("Sunucu boş cevap döndü.")
                    : MevzuatGrafigiSonuc<T>.Tamam(veri);
            }
            catch (Exception)
            {
                return MevzuatGrafigiSonuc<T>.Hata("Sunucuya ulaşılamadı.");
            }
        }

        private static async Task<MevzuatGrafigiSonuc> Yaz(Func<Task<HttpResponseMessage>> istek)
        {
            try
            {
                using var resp = await istek();
                return resp.IsSuccessStatusCode
                    ? MevzuatGrafigiSonuc.Tamam()
                    : MevzuatGrafigiSonuc.Hata(await HataMesaji(resp));
            }
            catch (Exception)
            {
                return MevzuatGrafigiSonuc.Hata("Sunucuya ulaşılamadı.");
            }
        }

        /// <summary>Gövdedeki { message }; yoksa duruma göre genel bir Türkçe metin.</summary>
        private static async Task<string> HataMesaji(HttpResponseMessage resp)
        {
            try
            {
                var govde = await resp.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(govde))
                {
                    using var belge = JsonDocument.Parse(govde);
                    if (belge.RootElement.ValueKind == JsonValueKind.Object
                        && belge.RootElement.TryGetProperty("message", out var m)
                        && m.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(m.GetString()))
                        return m.GetString()!;
                }
            }
            catch (JsonException)
            {
                // Gövde JSON değil: genel mesaja düş.
            }

            return resp.StatusCode switch
            {
                HttpStatusCode.NotFound => "Kayıt bulunamadı; sayfayı yenileyin.",
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Bu işlem için yetkiniz yok ya da oturum süresi doldu.",
                _ => $"İşlem başarısız (HTTP {(int)resp.StatusCode})."
            };
        }
    }
}
