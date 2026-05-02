using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebApp.Application.Services.Interfaces;
using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.Application.Services
{
    public class HesapPlaniLoader : IHesapPlaniLoader
    {
        private readonly HttpClient _http;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public HesapPlaniLoader(HttpClient http)
        {
            _http = http;
        }

        public async Task<HesapPlani> LoadAsync()
        {
            var dto = await _http.GetFromJsonAsync<HesapPlaniDto>("data/hesap_plani.json", JsonOptions)
                      ?? new HesapPlaniDto();

            return new HesapPlani
            {
                Aktif = dto.Aktif.Select(MapRow).ToList(),
                Pasif = dto.Pasif.Select(MapRow).ToList(),
                GelirTablosu = dto.GelirTablosu.Select(MapRow).ToList()
            };
        }

        private static MizanSatir MapRow(MizanSatirDto r) => new()
        {
            Kod = r.Kod ?? string.Empty,
            Ad = r.Ad ?? string.Empty,
            Tip = ParseTip(r.Tip),
            OncekiDonem = null,
            CariDonem = null
        };

        private static SatirTipi ParseTip(string? raw) =>
            Enum.TryParse<SatirTipi>(raw, ignoreCase: true, out var t) ? t : SatirTipi.Other;

        private sealed class HesapPlaniDto
        {
            [JsonPropertyName("aktif")]
            public List<MizanSatirDto> Aktif { get; set; } = new();

            [JsonPropertyName("pasif")]
            public List<MizanSatirDto> Pasif { get; set; } = new();

            [JsonPropertyName("gelirTablosu")]
            public List<MizanSatirDto> GelirTablosu { get; set; } = new();
        }

        private sealed class MizanSatirDto
        {
            [JsonPropertyName("tip")]
            public string? Tip { get; set; }

            [JsonPropertyName("kod")]
            public string? Kod { get; set; }

            [JsonPropertyName("ad")]
            public string? Ad { get; set; }
        }
    }
}
