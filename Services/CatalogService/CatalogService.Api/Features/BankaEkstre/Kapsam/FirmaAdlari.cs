using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Kapsam
{
    /// <summary>
    /// Firma Id → görünen ad. Listeler artık tüm firmaları gösterdiği için her satırda
    /// firma adı yazıyor; ad tabloda değil <c>catalog.Firmalar</c>'da duruyor.
    ///
    /// Servis <b>Scoped</b>: sözlük istek başına bir kez okunur. Satır satır sorgu
    /// (N+1) yerine tek okuma; firma sayısı iki haneli olduğu için tamamını almak
    /// birleştirme yazmaktan hem ucuz hem okunur.
    /// </summary>
    public interface IFirmaAdlari
    {
        /// <summary>Firmanın görünen adı; tanınmayan Id'de boş dizi.</summary>
        Task<string> AdAsync(int firmaId, CancellationToken ct = default);

        /// <summary>Tüm firmalar; ad çözümü için bir kez yüklenir.</summary>
        Task<IReadOnlyDictionary<int, string>> HepsiAsync(CancellationToken ct = default);

        /// <summary>Liste satırlarının firma adlarını tek okumayla doldurur.</summary>
        Task DoldurAsync(IEnumerable<IFirmaliSatir> satirlar, CancellationToken ct = default);
    }

    public sealed class FirmaAdlari : IFirmaAdlari
    {
        private readonly CatalogContext _db;

        private Dictionary<int, string>? _sozluk;

        public FirmaAdlari(CatalogContext db) => _db = db;

        public async Task<IReadOnlyDictionary<int, string>> HepsiAsync(CancellationToken ct = default)
        {
            if (_sozluk is not null) return _sozluk;

            var kayitlar = await _db.Firmalar.AsNoTracking()
                .Select(f => new { f.Id, f.Unvan, f.KisaAd })
                .ToListAsync(ct);

            // Listede unvan gösterilir; boşsa kısa ad. Firma seçim ekranı da böyle yazıyordu.
            _sozluk = kayitlar.ToDictionary(
                f => f.Id,
                f => string.IsNullOrWhiteSpace(f.Unvan) ? f.KisaAd : f.Unvan);

            return _sozluk;
        }

        public async Task<string> AdAsync(int firmaId, CancellationToken ct = default)
        {
            var sozluk = await HepsiAsync(ct);
            return sozluk.TryGetValue(firmaId, out var ad) ? ad : string.Empty;
        }

        public async Task DoldurAsync(IEnumerable<IFirmaliSatir> satirlar, CancellationToken ct = default)
        {
            var liste = satirlar as ICollection<IFirmaliSatir> ?? satirlar.ToList();
            if (liste.Count == 0) return;

            var sozluk = await HepsiAsync(ct);

            foreach (var satir in liste)
                satir.FirmaAdi = sozluk.TryGetValue(satir.FirmaId, out var ad) ? ad : string.Empty;
        }
    }
}
