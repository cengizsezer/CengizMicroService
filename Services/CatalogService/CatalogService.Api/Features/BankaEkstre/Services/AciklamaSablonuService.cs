using System.Text.RegularExpressions;
using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    public interface IAciklamaSablonuService
    {
        Task<List<AciklamaSablonuDto>> GetHepsiAsync(CancellationToken ct = default);
        List<YerTutucuDto> YerTutucular();
        Task<AciklamaSablonuDto> CreateAsync(AciklamaSablonuYazDto dto, CancellationToken ct = default);
        Task<AciklamaSablonuDto?> UpdateAsync(int id, AciklamaSablonuYazDto dto, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    }

    /// <summary>
    /// Muhasebe açıklaması şablonlarının yönetimi. Tablo banka bazlıdır: boş
    /// <see cref="AciklamaSablonu.ParserTipi"/> tüm bankalarda geçerli, dolusu yalnız
    /// o bankanın ekstresinde.
    ///
    /// Şablon metni yalnız tanınan yer tutucuları içerebilir; tanınmayan bir yer tutucu
    /// ("{MUSTERI}") üretilen açıklamada süslü parantezleriyle birlikte ORKA'ya giderdi.
    /// </summary>
    public class AciklamaSablonuService : IAciklamaSablonuService
    {
        private readonly CatalogContext _db;
        private readonly IEkstreParserSecici _parserSecici;

        public AciklamaSablonuService(CatalogContext db, IEkstreParserSecici parserSecici)
        {
            _db = db;
            _parserSecici = parserSecici;
        }

        /// <summary>Metindeki <c>{...}</c> yer tutucularını bulur; denetim bunun üzerinden yapılır.</summary>
        private static readonly Regex YerTutucuDeseni =
            new(@"\{[^{}]*\}", RegexOptions.Compiled | RegexOptions.CultureInvariant,
                YapilandirmaDogrulama.RegexZamanAsimi);

        public async Task<List<AciklamaSablonuDto>> GetHepsiAsync(CancellationToken ct = default)
        {
            var kayitlar = await _db.EkstreAciklamaSablonlari.AsNoTracking()
                .OrderBy(s => s.Sira).ThenBy(s => s.Id)
                .ToListAsync(ct);

            return kayitlar.Select(Esle).ToList();
        }

        /// <summary>
        /// Ekranda listelenen yer tutucular. Tek kaynak <see cref="AciklamaUretici"/>:
        /// orada doldurulan bir yer tutucu burada da görünür, listeye elle eklenmez.
        /// </summary>
        public List<YerTutucuDto> YerTutucular()
            => AciklamaUretici.YerTutucular
                .Select(y => new YerTutucuDto { Ad = y.Ad, Aciklama = y.Aciklama })
                .ToList();

        public async Task<AciklamaSablonuDto> CreateAsync(AciklamaSablonuYazDto dto, CancellationToken ct = default)
        {
            var kayit = new AciklamaSablonu();
            await UygulaAsync(kayit, dto, null, ct);

            _db.EkstreAciklamaSablonlari.Add(kayit);
            await _db.SaveChangesAsync(ct);

            return Esle(kayit);
        }

        public async Task<AciklamaSablonuDto?> UpdateAsync(int id, AciklamaSablonuYazDto dto, CancellationToken ct = default)
        {
            var kayit = await _db.EkstreAciklamaSablonlari.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (kayit is null) return null;

            await UygulaAsync(kayit, dto, id, ct);
            await _db.SaveChangesAsync(ct);

            return Esle(kayit);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var kayit = await _db.EkstreAciklamaSablonlari.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (kayit is null) return false;

            _db.EkstreAciklamaSablonlari.Remove(kayit);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        private async Task UygulaAsync(AciklamaSablonu kayit, AciklamaSablonuYazDto dto, int? mevcutId, CancellationToken ct)
        {
            var parser = YapilandirmaDogrulama.ParserNormalize(_parserSecici, dto.ParserTipi, nameof(dto.ParserTipi));

            var desen = YapilandirmaDogrulama.DesenKirp(
                dto.IslemTipiDeseni, 200, nameof(dto.IslemTipiDeseni), "Eşleşen işlem tipi");

            if (dto.EslesmeTuru == EslesmeTuru.Regex)
                YapilandirmaDogrulama.RegexDogrula(desen, nameof(dto.IslemTipiDeseni));

            var sablon = YapilandirmaDogrulama.DesenKirp(dto.Sablon, 100, nameof(dto.Sablon), "Şablon metni");
            YerTutuculariDogrula(sablon);

            await TekilligiDogrulaAsync(parser, desen, mevcutId, ct);

            kayit.ParserTipi = parser;
            kayit.IslemTipiDeseni = desen;
            kayit.EslesmeTuru = dto.EslesmeTuru;
            kayit.Sablon = sablon;
            kayit.BankalarArasi = dto.BankalarArasi;
            kayit.Sira = dto.Sira;
            kayit.Aktif = dto.Aktif;
        }

        /// <summary>
        /// Şablondaki her <c>{...}</c> tanınan bir yer tutucu olmalı. Tanınmayanı
        /// <see cref="AciklamaUretici"/> doldurmaz ve metin ORKA'ya "{MUSTERI}" diye giderdi.
        /// </summary>
        private static void YerTutuculariDogrula(string sablon)
        {
            var bilinen = AciklamaUretici.YerTutucular.Select(y => y.Ad).ToHashSet(StringComparer.Ordinal);

            var tanimsiz = YerTutucuDeseni.Matches(sablon)
                .Select(m => m.Value)
                .Where(y => !bilinen.Contains(y))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (tanimsiz.Count > 0)
                throw new BankaEkstreKuralException(nameof(AciklamaSablonu.Sablon),
                    $"Tanınmayan yer tutucu: {string.Join(", ", tanimsiz)}. " +
                    $"Kullanılabilenler: {string.Join(" ", bilinen)}.");
        }

        /// <summary>
        /// Aynı ayrıştırıcı + işlem tipi için tek şablon; ilk uyan kazandığı için ikinci
        /// kayıt hiç kullanılmaz ve kullanıcı düzenlediği satırın etkisiz kaldığını görmez.
        /// </summary>
        private async Task TekilligiDogrulaAsync(string parser, string desen, int? haricId, CancellationToken ct)
        {
            var mevcut = await _db.EkstreAciklamaSablonlari.AsNoTracking()
                .Where(s => s.ParserTipi == parser && (haricId == null || s.Id != haricId))
                .Select(s => s.IslemTipiDeseni)
                .ToListAsync(ct);

            if (mevcut.Any(d => string.Equals(d, desen, StringComparison.OrdinalIgnoreCase)))
                throw new BankaEkstreKuralException(nameof(AciklamaSablonu.IslemTipiDeseni),
                    "Bu ayrıştırıcı için aynı işlem tipine sahip bir şablon zaten var; mevcut kaydı düzenleyin.");
        }

        private AciklamaSablonuDto Esle(AciklamaSablonu s) => new()
        {
            Id = s.Id,
            ParserTipi = s.ParserTipi,
            ParserAdi = YapilandirmaDogrulama.ParserAdi(_parserSecici, s.ParserTipi),
            IslemTipiDeseni = s.IslemTipiDeseni,
            EslesmeTuru = s.EslesmeTuru,
            Sablon = s.Sablon,
            BankalarArasi = s.BankalarArasi,
            Sira = s.Sira,
            Aktif = s.Aktif
        };
    }
}
