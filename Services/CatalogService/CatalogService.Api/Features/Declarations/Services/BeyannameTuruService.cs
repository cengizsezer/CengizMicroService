using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Features.Declarations.Entities;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Declarations.Services
{
    public interface IBeyannameTuruService
    {
        Task<List<BeyannameTuruDto>> GetHepsiAsync(bool pasifDahil = false, CancellationToken ct = default);

        Task<BeyannameTuruDto> CreateAsync(BeyannameTuruYazDto dto, CancellationToken ct = default);

        Task<BeyannameTuruDto?> UpdateAsync(int id, BeyannameTuruYazDto dto, CancellationToken ct = default);

        /// <summary>Eksik varsayılan tanımları ekler; eklenen sayısı ve toplam döner.</summary>
        Task<(int Eklenen, int Toplam)> VarsayilanlariYukleAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Beyanname türü tanımlarının tek yönetim noktası. Tablo <b>global</b>: beyanname
    /// türleri ülke çapında aynı, firmadan/tenant'tan bağımsız — bu yüzden burada tenant
    /// filtresi yok (bkz. <see cref="BeyannameTuru"/>).
    ///
    /// Silme bilerek yok: <c>Declaration.DeclarationType</c> serbest metin olarak tanımın
    /// <c>Deger</c> alanına bağlı. Tanım silinirse o türdeki eski kayıtlar Özet matrisinde
    /// kolonsuz kalır. Kullanımdan kaldırmanın yolu <c>Aktif = false</c>: kolon çizilmez,
    /// kayıtlar durur ve tanım geri açılabilir.
    /// </summary>
    public class BeyannameTuruService : IBeyannameTuruService
    {
        private readonly CatalogContext _db;

        public BeyannameTuruService(CatalogContext db) => _db = db;

        public async Task<List<BeyannameTuruDto>> GetHepsiAsync(bool pasifDahil = false,
                                                                 CancellationToken ct = default)
        {
            var sorgu = _db.BeyannameTurleri.AsNoTracking();
            if (!pasifDahil) sorgu = sorgu.Where(t => t.Aktif);

            var turler = await sorgu.OrderBy(t => t.Sira).ThenBy(t => t.Id).ToListAsync(ct);
            return turler.Select(BeyannameOzetKurucu.TuruDto).ToList();
        }

        public async Task<BeyannameTuruDto> CreateAsync(BeyannameTuruYazDto dto, CancellationToken ct = default)
        {
            var (deger, kod, ad) = Dogrula(dto);

            if (await _db.BeyannameTurleri.AnyAsync(t => t.Deger == deger, ct))
                throw new BeyannameKuralException(nameof(dto.Deger),
                    $"\"{deger}\" zaten tanımlı. Aynı değer iki kez eklenemez.");

            var kayit = new BeyannameTuru
            {
                Deger = deger,
                Kod = kod,
                Ad = ad,
                Sira = dto.Sira > 0 ? dto.Sira : await SonrakiSiraAsync(ct),
                Aktif = dto.Aktif
            };

            _db.BeyannameTurleri.Add(kayit);
            await _db.SaveChangesAsync(ct);

            return BeyannameOzetKurucu.TuruDto(kayit);
        }

        public async Task<BeyannameTuruDto?> UpdateAsync(int id, BeyannameTuruYazDto dto,
                                                          CancellationToken ct = default)
        {
            var kayit = await _db.BeyannameTurleri.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (kayit is null) return null;

            var (deger, kod, ad) = Dogrula(dto);

            if (await _db.BeyannameTurleri.AnyAsync(t => t.Id != id && t.Deger == deger, ct))
                throw new BeyannameKuralException(nameof(dto.Deger),
                    $"\"{deger}\" başka bir tanımda kullanılıyor.");

            // Deger değiştirilebilir ama bedeli var: eski metni taşıyan beyanname kayıtları
            // artık bu tanımla eşleşmez ve Özet'te "eşleşmeyen tür" olarak listelenir.
            kayit.Deger = deger;
            kayit.Kod = kod;
            kayit.Ad = ad;
            kayit.Sira = dto.Sira;
            kayit.Aktif = dto.Aktif;

            await _db.SaveChangesAsync(ct);
            return BeyannameOzetKurucu.TuruDto(kayit);
        }

        public Task<(int Eklenen, int Toplam)> VarsayilanlariYukleAsync(CancellationToken ct = default)
            => BeyannameTuruSeed.SeedAsync(_db, ct);

        private async Task<int> SonrakiSiraAsync(CancellationToken ct)
        {
            var enBuyuk = await _db.BeyannameTurleri.MaxAsync(t => (int?)t.Sira, ct) ?? 0;
            return enBuyuk + 10;
        }

        /// <summary>Alan doğrulamaları; sınır değerleri veritabanı kolon uzunluklarıyla aynı.</summary>
        private static (string Deger, string? Kod, string Ad) Dogrula(BeyannameTuruYazDto dto)
        {
            var deger = (dto.Deger ?? string.Empty).Trim();
            var ad = (dto.Ad ?? string.Empty).Trim();
            var kod = string.IsNullOrWhiteSpace(dto.Kod) ? null : dto.Kod.Trim();

            if (deger.Length == 0)
                throw new BeyannameKuralException(nameof(dto.Deger),
                    "Saklanan değer boş olamaz; beyanname kayıtları bu metinle eşleşiyor.");

            if (deger.Length > 100)
                throw new BeyannameKuralException(nameof(dto.Deger), "Saklanan değer en fazla 100 karakter.");

            if (ad.Length == 0)
                throw new BeyannameKuralException(nameof(dto.Ad), "Ad boş olamaz.");

            if (ad.Length > 150)
                throw new BeyannameKuralException(nameof(dto.Ad), "Ad en fazla 150 karakter.");

            if (kod is { Length: > 20 })
                throw new BeyannameKuralException(nameof(dto.Kod), "Vergi kodu en fazla 20 karakter.");

            return (deger, kod, ad);
        }
    }
}
