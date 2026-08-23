using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    public interface IKisiYonlendirmeService
    {
        Task<List<KisiYonlendirmeDto>> GetHepsiAsync(CancellationToken ct = default);
        Task<KisiYonlendirmeDto> CreateAsync(KisiYonlendirmeYazDto dto, CancellationToken ct = default);
        Task<KisiYonlendirmeDto?> UpdateAsync(int id, KisiYonlendirmeYazDto dto, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    }

    /// <summary>
    /// Kişi yönlendirmelerinin yönetimi. Tablo <b>firma bazlı</b>: kimin ortak, kimin
    /// personel olduğu firmaya özeldir (vergi kodlarının aksine).
    ///
    /// İki denetim var:
    /// <list type="bullet">
    /// <item><b>Hesap kodu planda olmalı</b> — ortak denetim
    /// <see cref="YapilandirmaDogrulama.HesapKoduDogrulaAsync"/>. Yönlendirme kaydı bir daha
    /// sorulmadan uygulanacak; yanlış yazılmış bir kod her ay sessizce yanlış hesaba yazardı.
    /// Plan hiç yüklenmemişse denetim atlanır (kurulum sırası bozulmasın).</item>
    /// <item><b>Aynı isim + aynı yön tek kayıt.</b> İki kayıt olsaydı hangisinin
    /// uygulandığı kayıt sırasına kalırdı.</item>
    /// </list>
    /// </summary>
    public class KisiYonlendirmeService : IKisiYonlendirmeService
    {
        private readonly CatalogContext _db;
        private readonly IBankaFirmaKapsami _kapsam;

        public KisiYonlendirmeService(CatalogContext db, IBankaFirmaKapsami kapsam)
        {
            _db = db;
            _kapsam = kapsam;
        }

        /// <summary>Seçili firmanın yönlendirmeleri; kapsam her sorguda görünür yazılır.</summary>
        private IQueryable<KisiYonlendirme> Kayitlar
            => _db.EkstreKisiYonlendirmeleri.Where(k => k.FirmaId == _kapsam.FirmaId);

        public async Task<List<KisiYonlendirmeDto>> GetHepsiAsync(CancellationToken ct = default)
        {
            var kayitlar = await Kayitlar.AsNoTracking()
                .OrderBy(k => k.IsimCekirdegi).ThenBy(k => k.Yon)
                .ToListAsync(ct);

            return kayitlar.Select(Esle).ToList();
        }

        public async Task<KisiYonlendirmeDto> CreateAsync(KisiYonlendirmeYazDto dto, CancellationToken ct = default)
        {
            var cekirdek = Dogrula(dto);
            await YapilandirmaDogrulama.HesapKoduDogrulaAsync(_db, _kapsam.FirmaId, dto.HesapKodu, nameof(dto.HesapKodu), ct);
            await TekilligiDogrulaAsync(cekirdek, dto.Yon, null, ct);

            var kayit = new KisiYonlendirme { FirmaId = _kapsam.FirmaId };
            await UygulaAsync(kayit, dto, cekirdek, ct);

            _db.EkstreKisiYonlendirmeleri.Add(kayit);
            await _db.SaveChangesAsync(ct);

            return Esle(kayit);
        }

        public async Task<KisiYonlendirmeDto?> UpdateAsync(int id, KisiYonlendirmeYazDto dto, CancellationToken ct = default)
        {
            var cekirdek = Dogrula(dto);
            await YapilandirmaDogrulama.HesapKoduDogrulaAsync(_db, _kapsam.FirmaId, dto.HesapKodu, nameof(dto.HesapKodu), ct);
            await TekilligiDogrulaAsync(cekirdek, dto.Yon, id, ct);

            var kayit = await Kayitlar.FirstOrDefaultAsync(k => k.Id == id, ct);
            if (kayit is null) return null;

            await UygulaAsync(kayit, dto, cekirdek, ct);
            await _db.SaveChangesAsync(ct);

            return Esle(kayit);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var kayit = await Kayitlar.FirstOrDefaultAsync(k => k.Id == id, ct);
            if (kayit is null) return false;

            _db.EkstreKisiYonlendirmeleri.Remove(kayit);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        /// <summary>
        /// İsim ve kod zorunlu. İsim çekirdeği boşsa (yalnız noktalama/tek harf yazılmışsa)
        /// kayıt hiçbir satırı tutmaz ve kullanıcı neden çalışmadığını anlamaz.
        /// </summary>
        private static string Dogrula(KisiYonlendirmeYazDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Isim))
                throw new BankaEkstreKuralException(nameof(dto.Isim), "İsim boş olamaz.");

            if (string.IsNullOrWhiteSpace(dto.HesapKodu))
                throw new BankaEkstreKuralException(nameof(dto.HesapKodu), "Hesap kodu boş olamaz.");

            var cekirdek = Normalizasyon.Cekirdek(dto.Isim);
            if (cekirdek.Length == 0)
                throw new BankaEkstreKuralException(nameof(dto.Isim),
                    "İsimden eşleştirilebilir bir çekirdek çıkmadı; ad ve soyadı yazın.");

            return Normalizasyon.Kirp(cekirdek, 200);
        }

        private async Task TekilligiDogrulaAsync(string cekirdek, YonlendirmeYonu yon, int? haricId, CancellationToken ct)
        {
            var cakisma = await Kayitlar
                .AnyAsync(k => k.IsimCekirdegi == cekirdek && k.Yon == yon && (haricId == null || k.Id != haricId), ct);

            if (cakisma)
                throw new BankaEkstreKuralException(nameof(cekirdek),
                    "Bu isim ve yön için zaten bir yönlendirme var; mevcut kaydı düzenleyin.");
        }

        private async Task UygulaAsync(KisiYonlendirme kayit, KisiYonlendirmeYazDto dto, string cekirdek, CancellationToken ct)
        {
            var kod = Normalizasyon.HesapKoduNormalize(dto.HesapKodu);

            kayit.Isim = Normalizasyon.Kirp(dto.Isim, 200);
            kayit.IsimCekirdegi = cekirdek;
            kayit.Yon = dto.Yon;
            kayit.HesapKodu = kod;
            kayit.HesapAdi = await _db.EkstreHesapPlani.AsNoTracking()
                .Where(h => h.FirmaId == _kapsam.FirmaId && h.Kod == kod).Select(h => h.Ad).FirstOrDefaultAsync(ct);
            kayit.Aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? null : Normalizasyon.Kirp(dto.Aciklama, 300);
            kayit.Aktif = dto.Aktif;
        }

        private static KisiYonlendirmeDto Esle(KisiYonlendirme k) => new()
        {
            Id = k.Id,
            Isim = k.Isim,
            IsimCekirdegi = k.IsimCekirdegi,
            Yon = k.Yon,
            HesapKodu = k.HesapKodu,
            HesapAdi = k.HesapAdi,
            Aciklama = k.Aciklama,
            Aktif = k.Aktif
        };
    }
}
