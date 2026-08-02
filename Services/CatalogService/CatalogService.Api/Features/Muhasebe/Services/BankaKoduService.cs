using System.Text.Json;
using CatalogService.Api.Features.Muhasebe.Dtos;

namespace CatalogService.Api.Features.Muhasebe.Services
{
    /// <inheritdoc cref="IBankaKoduService"/>
    public class BankaKoduService : IBankaKoduService
    {
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        private readonly IWebHostEnvironment _env;
        private readonly ILogger<BankaKoduService> _logger;
        private readonly SemaphoreSlim _kilit = new(1, 1);

        private IReadOnlyList<BankaKoduDto>? _liste;

        public BankaKoduService(IWebHostEnvironment env, ILogger<BankaKoduService> logger)
        {
            _env = env;
            _logger = logger;
        }

        public async Task<IReadOnlyList<BankaKoduDto>> GetHepsiAsync(CancellationToken ct = default)
        {
            if (_liste is not null) return _liste;

            await _kilit.WaitAsync(ct);
            try
            {
                // Kilit alınana kadar başka istek doldurmuş olabilir.
                if (_liste is not null) return _liste;

                _liste = await OkuAsync(ct);
                return _liste;
            }
            finally
            {
                _kilit.Release();
            }
        }

        private async Task<IReadOnlyList<BankaKoduDto>> OkuAsync(CancellationToken ct)
        {
            var path = Path.Combine(_env.ContentRootPath, "Infrastructure", "Setup", "SeedFiles", "tcmb-banka-kodlari.json");

            if (!File.Exists(path))
            {
                _logger.LogWarning("TCMB banka kodu dosyası bulunamadı: {Path}", path);
                return Array.Empty<BankaKoduDto>();
            }

            try
            {
                var raw = await File.ReadAllTextAsync(path, ct);
                var liste = JsonSerializer.Deserialize<List<BankaKoduDto>>(raw, JsonOpts) ?? new();

                return liste
                    .Where(b => !string.IsNullOrWhiteSpace(b.Kod))
                    .OrderBy(b => b.Kod, StringComparer.Ordinal)
                    .ToList();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "TCMB banka kodu dosyası okunamadı: {Path}", path);
                return Array.Empty<BankaKoduDto>();
            }
        }
    }
}
