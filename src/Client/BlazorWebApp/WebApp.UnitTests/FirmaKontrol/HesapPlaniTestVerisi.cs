using System.Text.Json;
using System.Text.Json.Serialization;
using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.UnitTests.FirmaKontrol
{
    /// <summary>
    /// Testler gerçek hesap planını (WebApp/wwwroot/data/hesap_plani.json) kullanır —
    /// dosya csproj üzerinden test çıktısına kopyalanır. Böylece plandaki satır tipi
    /// değişiklikleri (örn. "C-NET SATIŞLAR" ara toplam satırı) testlerle korunur.
    /// </summary>
    internal static class HesapPlaniTestVerisi
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static HesapPlani Yukle()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "data", "hesap_plani.json");
            var dto = JsonSerializer.Deserialize<HesapPlaniDto>(File.ReadAllText(path), JsonOptions)
                      ?? new HesapPlaniDto();

            return new HesapPlani
            {
                Aktif = dto.Aktif.Select(Map).ToList(),
                Pasif = dto.Pasif.Select(Map).ToList(),
                GelirTablosu = dto.GelirTablosu.Select(Map).ToList()
            };
        }

        /// <summary>
        /// Ham mizan değerlerini Account satırlarına yazar — MockFirmaKontrolService'in
        /// yüklemeden sonra yaptığı eşleştirmenin aynısı (cari dönem).
        /// </summary>
        public static void MizanUygula(HesapPlani plan, IReadOnlyDictionary<string, decimal?> rawCari)
        {
            foreach (var satir in plan.Aktif.Concat(plan.Pasif).Concat(plan.GelirTablosu))
            {
                if (satir.Tip != SatirTipi.Account) continue;
                if (string.IsNullOrWhiteSpace(satir.Kod)) continue;
                if (rawCari.TryGetValue(satir.Kod, out var v)) satir.CariDonem = v;
            }
        }

        private static MizanSatir Map(MizanSatirDto r) => new()
        {
            Kod = r.Kod ?? string.Empty,
            Ad = r.Ad ?? string.Empty,
            Tip = Enum.TryParse<SatirTipi>(r.Tip, ignoreCase: true, out var t) ? t : SatirTipi.Other,
            OncekiDonem = null,
            CariDonem = null
        };

        private sealed class HesapPlaniDto
        {
            [JsonPropertyName("aktif")] public List<MizanSatirDto> Aktif { get; set; } = new();
            [JsonPropertyName("pasif")] public List<MizanSatirDto> Pasif { get; set; } = new();
            [JsonPropertyName("gelirTablosu")] public List<MizanSatirDto> GelirTablosu { get; set; } = new();
        }

        private sealed class MizanSatirDto
        {
            [JsonPropertyName("tip")] public string? Tip { get; set; }
            [JsonPropertyName("kod")] public string? Kod { get; set; }
            [JsonPropertyName("ad")] public string? Ad { get; set; }
        }
    }
}
