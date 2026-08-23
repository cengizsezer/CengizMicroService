using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    public interface ISabitKuralService
    {
        Task<List<SabitKuralDto>> GetHepsiAsync(CancellationToken ct = default);
        Task<SabitKuralDto> CreateAsync(SabitKuralYazDto dto, CancellationToken ct = default);
        Task<SabitKuralDto?> UpdateAsync(int id, SabitKuralYazDto dto, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    }

    /// <summary>
    /// Sabit kural tablosunun yönetimi (Katman 4: işlem tipi/açıklama → hesap kodu).
    /// Tablo <b>global</b>: kurallar bankanın yazım kalıbına bağlıdır, firmaya değil.
    ///
    /// Mimari hedef "yeni banka = yeni parser + yeni kural satırları" olduğu için tablo
    /// koda gömülmez; buradan düzenlenir. <see cref="SabitKural.ParserTipi"/> boşsa kural
    /// tüm bankalarda geçerlidir, doluysa yalnız o bankanın ekstresinde çalışır.
    /// </summary>
    public class SabitKuralService : ISabitKuralService
    {
        private readonly CatalogContext _db;
        private readonly IEkstreParserSecici _parserSecici;

        public SabitKuralService(CatalogContext db, IEkstreParserSecici parserSecici)
        {
            _db = db;
            _parserSecici = parserSecici;
        }

        public async Task<List<SabitKuralDto>> GetHepsiAsync(CancellationToken ct = default)
        {
            var kayitlar = await _db.EkstreSabitKurallar.AsNoTracking()
                .OrderBy(k => k.Sira).ThenBy(k => k.Id)
                .ToListAsync(ct);

            return kayitlar.Select(Esle).ToList();
        }

        public async Task<SabitKuralDto> CreateAsync(SabitKuralYazDto dto, CancellationToken ct = default)
        {
            var kayit = new SabitKural();
            await UygulaAsync(kayit, dto, null, ct);

            _db.EkstreSabitKurallar.Add(kayit);
            await _db.SaveChangesAsync(ct);

            return Esle(kayit);
        }

        public async Task<SabitKuralDto?> UpdateAsync(int id, SabitKuralYazDto dto, CancellationToken ct = default)
        {
            var kayit = await _db.EkstreSabitKurallar.FirstOrDefaultAsync(k => k.Id == id, ct);
            if (kayit is null) return null;

            await UygulaAsync(kayit, dto, id, ct);
            await _db.SaveChangesAsync(ct);

            return Esle(kayit);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var kayit = await _db.EkstreSabitKurallar.FirstOrDefaultAsync(k => k.Id == id, ct);
            if (kayit is null) return false;

            _db.EkstreSabitKurallar.Remove(kayit);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        /// <summary>
        /// Doğrular ve yazar. <see cref="SabitKural.Guven"/> arayüzden düzenlenmez: sabit
        /// kural kesin kabul edilir, eşik altına düşürülmez — alanın varsayılanı korunur.
        /// </summary>
        private async Task UygulaAsync(SabitKural kayit, SabitKuralYazDto dto, int? mevcutId, CancellationToken ct)
        {
            var parser = YapilandirmaDogrulama.ParserNormalize(_parserSecici, dto.ParserTipi, nameof(dto.ParserTipi));

            var desen = YapilandirmaDogrulama.DesenKirp(
                dto.IslemTipiDeseni, 200, nameof(dto.IslemTipiDeseni), "Eşleşme ifadesi");

            if (dto.EslesmeTuru == EslesmeTuru.Regex)
                YapilandirmaDogrulama.RegexDogrula(desen, nameof(dto.IslemTipiDeseni));

            await TekilligiDogrulaAsync(parser, dto.Kapsam, desen, mevcutId, ct);

            var plandaki = await YapilandirmaDogrulama.HesapKoduDogrulaAsync(
                _db, dto.HesapKodu, nameof(dto.HesapKodu), ct);

            kayit.ParserTipi = parser;
            kayit.IslemTipiDeseni = desen;
            kayit.Kapsam = dto.Kapsam;
            kayit.EslesmeTuru = dto.EslesmeTuru;
            kayit.Yon = dto.Yon;
            // Kod boşluklu saklanır; format değiştirilmez, ORKA tanımaz.
            kayit.HesapKodu = Normalizasyon.HesapKoduNormalize(dto.HesapKodu);
            // Ad boş bırakıldıysa plandan doldurulur; listede kodun ne olduğu görünsün.
            kayit.HesapAdi = string.IsNullOrWhiteSpace(dto.HesapAdi)
                ? plandaki?.Ad
                : Normalizasyon.Kirp(dto.HesapAdi, 200);
            kayit.UnvanCikarilsin = dto.UnvanCikarilsin;
            kayit.AltHesapGerekli = dto.AltHesapGerekli;
            kayit.Sira = dto.Sira;
            kayit.Aktif = dto.Aktif;
        }

        /// <summary>
        /// Aynı ayrıştırıcı + kapsam + ifade için tek kural. İki kayıt olsaydı hangisinin
        /// uygulandığı sıraya ve Id'ye kalırdı; kullanıcı düzenlediği satırın neden etkisiz
        /// kaldığını göremezdi.
        /// </summary>
        private async Task TekilligiDogrulaAsync(string parser, KuralKapsami kapsam, string desen,
                                                 int? haricId, CancellationToken ct)
        {
            var cakisma = await _db.EkstreSabitKurallar.AsNoTracking()
                .Where(k => k.ParserTipi == parser && k.Kapsam == kapsam && (haricId == null || k.Id != haricId))
                .Select(k => k.IslemTipiDeseni)
                .ToListAsync(ct);

            if (cakisma.Any(d => string.Equals(d, desen, StringComparison.OrdinalIgnoreCase)))
                throw new BankaEkstreKuralException(nameof(SabitKural.IslemTipiDeseni),
                    "Bu ayrıştırıcı ve kapsam için aynı ifadeye sahip bir kural zaten var; mevcut kaydı düzenleyin.");
        }

        private SabitKuralDto Esle(SabitKural k) => new()
        {
            Id = k.Id,
            ParserTipi = k.ParserTipi,
            ParserAdi = YapilandirmaDogrulama.ParserAdi(_parserSecici, k.ParserTipi),
            IslemTipiDeseni = k.IslemTipiDeseni,
            Kapsam = k.Kapsam,
            EslesmeTuru = k.EslesmeTuru,
            Yon = k.Yon,
            HesapKodu = k.HesapKodu,
            HesapAdi = k.HesapAdi,
            UnvanCikarilsin = k.UnvanCikarilsin,
            AltHesapGerekli = k.AltHesapGerekli,
            Sira = k.Sira,
            Aktif = k.Aktif
        };
    }
}
