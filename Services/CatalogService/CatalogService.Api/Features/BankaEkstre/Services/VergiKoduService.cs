using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    public interface IVergiKoduService
    {
        Task<List<VergiKoduEslemesiDto>> GetHepsiAsync(CancellationToken ct = default);
        Task<VergiKoduEslemesiDto> CreateAsync(VergiKoduEslemesiYazDto dto, CancellationToken ct = default);
        Task<VergiKoduEslemesiDto?> UpdateAsync(int id, VergiKoduEslemesiYazDto dto, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    }

    /// <summary>
    /// Vergi kodu eşleme tablosunun yönetimi. Tablo <b>global</b>: vergi kodları firmadan
    /// firmaya değişmez. Eşleme koda gömülmediği için yeni bir vergi türü çıktığında kod
    /// değişmez, Tanımlar ekranından satır eklenir.
    /// </summary>
    public class VergiKoduService : IVergiKoduService
    {
        private readonly CatalogContext _db;

        public VergiKoduService(CatalogContext db) => _db = db;

        public async Task<List<VergiKoduEslemesiDto>> GetHepsiAsync(CancellationToken ct = default)
        {
            var kayitlar = await _db.EkstreVergiKodlari.AsNoTracking()
                .OrderBy(v => v.Sira).ThenBy(v => v.Id)
                .ToListAsync(ct);

            return kayitlar.Select(Esle).ToList();
        }

        public async Task<VergiKoduEslemesiDto> CreateAsync(VergiKoduEslemesiYazDto dto, CancellationToken ct = default)
        {
            Dogrula(dto);

            var kayit = new VergiKoduEslemesi();
            Uygula(kayit, dto);

            _db.EkstreVergiKodlari.Add(kayit);
            await _db.SaveChangesAsync(ct);

            return Esle(kayit);
        }

        public async Task<VergiKoduEslemesiDto?> UpdateAsync(int id, VergiKoduEslemesiYazDto dto, CancellationToken ct = default)
        {
            Dogrula(dto);

            var kayit = await _db.EkstreVergiKodlari.FirstOrDefaultAsync(v => v.Id == id, ct);
            if (kayit is null) return null;

            Uygula(kayit, dto);
            await _db.SaveChangesAsync(ct);

            return Esle(kayit);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var kayit = await _db.EkstreVergiKodlari.FirstOrDefaultAsync(v => v.Id == id, ct);
            if (kayit is null) return false;

            _db.EkstreVergiKodlari.Remove(kayit);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        /// <summary>
        /// Kod ve anahtar kelimenin ikisi birden boş olamaz: hiçbir şeyi eşleştirmeyen bir
        /// satır sessizce durur ve kullanıcı neden çalışmadığını anlamaz.
        /// </summary>
        private static void Dogrula(VergiKoduEslemesiYazDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.VergiKodu) && string.IsNullOrWhiteSpace(dto.AnahtarKelime))
                throw new BankaEkstreKuralException(nameof(dto.VergiKodu),
                    "Vergi kodu ve anahtar kelimeden en az biri dolu olmalı.");

            if (string.IsNullOrWhiteSpace(dto.HesapKodu))
                throw new BankaEkstreKuralException(nameof(dto.HesapKodu), "Hesap kodu boş olamaz.");
        }

        private static void Uygula(VergiKoduEslemesi kayit, VergiKoduEslemesiYazDto dto)
        {
            kayit.VergiKodu = string.IsNullOrWhiteSpace(dto.VergiKodu) ? null : dto.VergiKodu.Trim();
            kayit.AnahtarKelime = string.IsNullOrWhiteSpace(dto.AnahtarKelime) ? null : Normalizasyon.Kirp(dto.AnahtarKelime, 100);
            // Kod boşluklu saklanır; format değiştirilmez, ORKA tanımaz.
            kayit.HesapKodu = Normalizasyon.HesapKoduNormalize(dto.HesapKodu);
            kayit.HesapAdi = string.IsNullOrWhiteSpace(dto.HesapAdi) ? null : Normalizasyon.Kirp(dto.HesapAdi, 200);
            kayit.Sira = dto.Sira;
            kayit.Aktif = dto.Aktif;
        }

        private static VergiKoduEslemesiDto Esle(VergiKoduEslemesi v) => new()
        {
            Id = v.Id,
            VergiKodu = v.VergiKodu,
            AnahtarKelime = v.AnahtarKelime,
            HesapKodu = v.HesapKodu,
            HesapAdi = v.HesapAdi,
            Sira = v.Sira,
            Aktif = v.Aktif
        };
    }
}
