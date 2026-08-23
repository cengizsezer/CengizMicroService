using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    public interface IFirmaOzetService
    {
        /// <summary>
        /// Verilen firmaların (tenant) banka otomasyonu kurulum sayaçları. İstenen her
        /// tenant için bir satır döner; hiç kaydı olmayan firma sıfırlarla listelenir ki
        /// seçim ekranında "kurulum gerekli" olarak işaretlenebilsin.
        /// </summary>
        Task<List<FirmaBankaOzetiDto>> OzetlerAsync(IEnumerable<string> tenantlar, CancellationToken ct = default);
    }

    /// <summary>
    /// Banka Otomasyon firma seçim ekranının sayaçları.
    ///
    /// TENANT FİLTRESİ BAYPAS EDİLİR. Modülün diğer tüm sorguları global query filter ile
    /// yalnız token'daki firmayı görür; bu ekran ise <b>girilmeden önce</b> tüm firmaları
    /// listeler, dolayısıyla tek token'la birden fazla tenant okumak zorundadır. Bu yüzden
    /// tek iş burada toplandı: modülün geri kalanı izolasyonunu aynen korur, baypas tek
    /// dosyada ve yalnız <b>sayı</b> üretiminde kalır (kayıt içeriği hiç dönmez).
    ///
    /// Yetki: hangi firmaların sorulacağını istemci belirler ve istemci bu listeyi
    /// login yanıtındaki kendi firmalarından kurar. CatalogService token'da yalnız tek
    /// <c>tn</c> claim'i gördüğü için kullanıcının diğer firmalarını doğrulayamaz;
    /// sızabilecek en fazla şey bilinen bir firma numarasının kayıt <b>adedi</b>dir.
    /// </summary>
    public class FirmaOzetService : IFirmaOzetService
    {
        private readonly CatalogContext _db;

        public FirmaOzetService(CatalogContext db) => _db = db;

        public async Task<List<FirmaBankaOzetiDto>> OzetlerAsync(IEnumerable<string> tenantlar,
                                                                 CancellationToken ct = default)
        {
            var istenen = (tenantlar ?? Enumerable.Empty<string>())
                .Select(t => t?.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (istenen.Count == 0) return new List<FirmaBankaOzetiDto>();

            var planlar = await _db.EkstreHesapPlani.IgnoreQueryFilters().AsNoTracking()
                .Where(h => h.Aktif && istenen.Contains(h.TenantNo))
                .GroupBy(h => h.TenantNo)
                .Select(g => new { Tenant = g.Key, Sayi = g.Count() })
                .ToDictionaryAsync(x => x.Tenant, x => x.Sayi, ct);

            var hesaplar = await _db.EkstreBankaHesaplari.IgnoreQueryFilters().AsNoTracking()
                .Where(h => h.Aktif && istenen.Contains(h.TenantNo))
                .GroupBy(h => h.TenantNo)
                .Select(g => new { Tenant = g.Key, Sayi = g.Count() })
                .ToDictionaryAsync(x => x.Tenant, x => x.Sayi, ct);

            // Satırın kendi TenantNo'su yok; izolasyonu bağlı olduğu yüklemeden alır
            // (bkz. EkstreSatiri). Sayım da aynı yoldan, yükleme üzerinden yapılır.
            var yuklemeler = _db.EkstreYuklemeler.IgnoreQueryFilters().AsNoTracking()
                .Where(y => istenen.Contains(y.TenantNo));

            var bekleyen = await _db.EkstreSatirlari.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.Durum == SatirDurum.OnayBekliyor || s.Durum == SatirDurum.Cozulemedi)
                .Join(yuklemeler, s => s.EkstreYuklemeId, y => y.Id, (s, y) => y.TenantNo)
                .GroupBy(t => t)
                .Select(g => new { Tenant = g.Key, Sayi = g.Count() })
                .ToDictionaryAsync(x => x.Tenant, x => x.Sayi, ct);

            return istenen.Select(t => new FirmaBankaOzetiDto
            {
                TenantNo = t,
                HesapPlaniSayisi = planlar.TryGetValue(t, out var p) ? p : 0,
                BankaHesabiSayisi = hesaplar.TryGetValue(t, out var h) ? h : 0,
                OnayBekleyen = bekleyen.TryGetValue(t, out var b) ? b : 0
            }).ToList();
        }
    }
}
