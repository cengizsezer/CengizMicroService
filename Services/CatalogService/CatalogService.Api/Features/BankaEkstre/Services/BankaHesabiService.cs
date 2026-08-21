using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.Api.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    public interface IBankaHesabiService
    {
        Task<List<BankaHesabiDto>> GetHepsiAsync(bool pasifDahil, CancellationToken ct = default);
        Task<BankaHesabiDto?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<BankaHesabiDto> CreateAsync(BankaHesabiYazDto dto, CancellationToken ct = default);
        Task<BankaHesabiDto?> UpdateAsync(int id, BankaHesabiYazDto dto, CancellationToken ct = default);
        Task<bool?> DeleteAsync(int id, CancellationToken ct = default);
        List<ParserSecenekDto> GetParserSecenekleri();
    }

    /// <summary>
    /// Banka hesapları CRUD. Hesap aynı zamanda banka kayıt defteridir (Katman 3),
    /// bu yüzden ekstresi olan hesap silinmez, pasife çekilir.
    /// </summary>
    public class BankaHesabiService : IBankaHesabiService
    {
        private readonly CatalogContext _db;
        private readonly IEkstreParserSecici _parserSecici;

        public BankaHesabiService(CatalogContext db, IEkstreParserSecici parserSecici)
        {
            _db = db;
            _parserSecici = parserSecici;
        }

        public async Task<List<BankaHesabiDto>> GetHepsiAsync(bool pasifDahil, CancellationToken ct = default)
        {
            var sorgu = _db.EkstreBankaHesaplari.AsNoTracking();
            if (!pasifDahil) sorgu = sorgu.Where(h => h.Aktif);

            var kayitlar = await sorgu
                .OrderBy(h => h.BankaAdi).ThenBy(h => h.OrkaHesapKodu)
                .ToListAsync(ct);

            return kayitlar.Select(Esle).ToList();
        }

        public async Task<BankaHesabiDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var hesap = await _db.EkstreBankaHesaplari.AsNoTracking()
                .FirstOrDefaultAsync(h => h.Id == id, ct);

            return hesap is null ? null : Esle(hesap);
        }

        public async Task<BankaHesabiDto> CreateAsync(BankaHesabiYazDto dto, CancellationToken ct = default)
        {
            Dogrula(dto);
            await OrkaKoduTekilMi(dto.OrkaHesapKodu, null, ct);

            var hesap = new BankaHesabi();
            Uygula(hesap, dto);

            _db.EkstreBankaHesaplari.Add(hesap);
            await _db.SaveChangesAsync(ct);

            return Esle(hesap);
        }

        public async Task<BankaHesabiDto?> UpdateAsync(int id, BankaHesabiYazDto dto, CancellationToken ct = default)
        {
            Dogrula(dto);

            var hesap = await _db.EkstreBankaHesaplari.FirstOrDefaultAsync(h => h.Id == id, ct);
            if (hesap is null) return null;

            await OrkaKoduTekilMi(dto.OrkaHesapKodu, id, ct);
            Uygula(hesap, dto);

            await _db.SaveChangesAsync(ct);
            return Esle(hesap);
        }

        /// <summary>Ekstresi olan hesap silinmez (geçmiş kayıtların bağı kopar); null = bulunamadı.</summary>
        public async Task<bool?> DeleteAsync(int id, CancellationToken ct = default)
        {
            var hesap = await _db.EkstreBankaHesaplari.FirstOrDefaultAsync(h => h.Id == id, ct);
            if (hesap is null) return null;

            if (await _db.EkstreYuklemeler.AnyAsync(y => y.BankaHesabiId == id, ct))
                return false;

            _db.EkstreBankaHesaplari.Remove(hesap);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public List<ParserSecenekDto> GetParserSecenekleri()
            => _parserSecici.Hepsi
                .Select(p => new ParserSecenekDto { Tip = p.ParserTipi, Ad = p.Ad })
                .ToList();

        // ---- Yardımcılar ----

        private void Dogrula(BankaHesabiYazDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.BankaAdi))
                throw new BankaEkstreKuralException(nameof(dto.BankaAdi), "Banka adı zorunludur.");

            if (string.IsNullOrWhiteSpace(dto.OrkaHesapKodu))
                throw new BankaEkstreKuralException(nameof(dto.OrkaHesapKodu), "ORKA hesap kodu zorunludur.");

            if (_parserSecici.Sec(dto.ParserTipi) is null)
                throw new BankaEkstreKuralException(nameof(dto.ParserTipi),
                    $"Tanımsız ayrıştırıcı: '{dto.ParserTipi}'. Seçilebilir tipler: " +
                    string.Join(", ", _parserSecici.Hepsi.Select(p => p.ParserTipi)) + ".");
        }

        private async Task OrkaKoduTekilMi(string kod, int? haricId, CancellationToken ct)
        {
            var normal = Normalizasyon.HesapKoduNormalize(kod);
            var cakisiyor = await _db.EkstreBankaHesaplari
                .AnyAsync(h => h.OrkaHesapKodu == normal && (haricId == null || h.Id != haricId), ct);

            if (cakisiyor)
                throw new DuplicateRecordException(nameof(BankaHesabi.OrkaHesapKodu),
                    $"'{normal}' kodlu bir banka hesabı zaten var.");
        }

        private static void Uygula(BankaHesabi hesap, BankaHesabiYazDto dto)
        {
            hesap.BankaAdi = dto.BankaAdi.Trim();
            hesap.HesapTipi = dto.HesapTipi;
            hesap.ParaBirimi = string.IsNullOrWhiteSpace(dto.ParaBirimi) ? "TRY" : dto.ParaBirimi.Trim().ToUpperInvariant();
            hesap.Iban = string.IsNullOrWhiteSpace(dto.Iban) ? null : dto.Iban.Trim().Replace(" ", string.Empty).ToUpperInvariant();
            // Kod boşluklu saklanır; format değiştirilmez, ORKA tanımaz.
            hesap.OrkaHesapKodu = Normalizasyon.HesapKoduNormalize(dto.OrkaHesapKodu);
            hesap.ParserTipi = dto.ParserTipi.Trim();
            hesap.Aktif = dto.Aktif;
            // Katmanlar varsayılan kapalı; kullanıcı bilerek açar (bkz. BankaHesabi yorumları).
            hesap.IbanKatmaniAktif = dto.IbanKatmaniAktif;
            hesap.VknKatmaniAktif = dto.VknKatmaniAktif;
        }

        private static BankaHesabiDto Esle(BankaHesabi h) => new()
        {
            Id = h.Id,
            BankaAdi = h.BankaAdi,
            HesapTipi = h.HesapTipi,
            ParaBirimi = h.ParaBirimi,
            Iban = h.Iban,
            OrkaHesapKodu = h.OrkaHesapKodu,
            ParserTipi = h.ParserTipi,
            Aktif = h.Aktif,
            IbanKatmaniAktif = h.IbanKatmaniAktif,
            VknKatmaniAktif = h.VknKatmaniAktif
        };
    }
}
