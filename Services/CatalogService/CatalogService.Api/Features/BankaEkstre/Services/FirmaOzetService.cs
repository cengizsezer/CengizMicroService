using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    public interface IFirmaOzetService
    {
        /// <summary>
        /// Verilen firmaların banka otomasyonu kurulum sayaçları. İstenen her firma için
        /// bir satır döner; hiç kaydı olmayan firma sıfırlarla listelenir ki seçim
        /// ekranında "kurulum gerekli" olarak işaretlenebilsin.
        /// </summary>
        Task<List<FirmaBankaOzetiDto>> OzetlerAsync(IEnumerable<int> firmaIdler, CancellationToken ct = default);
    }

    /// <summary>
    /// Banka Otomasyon firma seçim ekranının sayaçları.
    ///
    /// <b>Burada artık baypas yok.</b> Eskiden bu servis <c>IgnoreQueryFilters()</c>
    /// çağırıyordu: modülün kapsamı token'daki tek <c>tn</c> claim'iydi ve ekran firmaya
    /// <i>girilmeden önce</i> açıldığı için global filtre tüm satırları eliyordu
    /// (bkz. KARARLAR §64, artık geçersiz).
    ///
    /// Kapsam <c>FirmaId</c> olunca sorunun kendisi kalmadı: çok firmalı sayım, tek
    /// firmalı sorgu kadar sıradan bir <c>WHERE FirmaId IN (…)</c>. Baypas edilecek gizli
    /// bir filtre olmadığı için bu servisin ayrıcalığı da yok.
    /// </summary>
    public class FirmaOzetService : IFirmaOzetService
    {
        private readonly CatalogContext _db;

        public FirmaOzetService(CatalogContext db) => _db = db;

        public async Task<List<FirmaBankaOzetiDto>> OzetlerAsync(IEnumerable<int> firmaIdler,
                                                                 CancellationToken ct = default)
        {
            var istenen = (firmaIdler ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (istenen.Count == 0) return new List<FirmaBankaOzetiDto>();

            var planlar = await _db.EkstreHesapPlani.AsNoTracking()
                .Where(h => h.Aktif && istenen.Contains(h.FirmaId))
                .GroupBy(h => h.FirmaId)
                .Select(g => new { FirmaId = g.Key, Sayi = g.Count() })
                .ToDictionaryAsync(x => x.FirmaId, x => x.Sayi, ct);

            var hesaplar = await _db.EkstreBankaHesaplari.AsNoTracking()
                .Where(h => h.Aktif && istenen.Contains(h.FirmaId))
                .GroupBy(h => h.FirmaId)
                .Select(g => new { FirmaId = g.Key, Sayi = g.Count() })
                .ToDictionaryAsync(x => x.FirmaId, x => x.Sayi, ct);

            // Satırın kendi FirmaId'si yok; kapsamını bağlı olduğu yüklemeden alıyor
            // (bkz. EkstreSatiri). Sayım da aynı yoldan, yükleme üzerinden yapılıyor.
            var yuklemeler = _db.EkstreYuklemeler.AsNoTracking()
                .Where(y => istenen.Contains(y.FirmaId));

            var bekleyen = await _db.EkstreSatirlari.AsNoTracking()
                .Where(s => s.Durum == SatirDurum.OnayBekliyor || s.Durum == SatirDurum.Cozulemedi)
                .Join(yuklemeler, s => s.EkstreYuklemeId, y => y.Id, (s, y) => y.FirmaId)
                .GroupBy(f => f)
                .Select(g => new { FirmaId = g.Key, Sayi = g.Count() })
                .ToDictionaryAsync(x => x.FirmaId, x => x.Sayi, ct);

            return istenen.Select(id => new FirmaBankaOzetiDto
            {
                FirmaId = id,
                HesapPlaniSayisi = planlar.TryGetValue(id, out var p) ? p : 0,
                BankaHesabiSayisi = hesaplar.TryGetValue(id, out var h) ? h : 0,
                OnayBekleyen = bekleyen.TryGetValue(id, out var b) ? b : 0
            }).ToList();
        }
    }
}
