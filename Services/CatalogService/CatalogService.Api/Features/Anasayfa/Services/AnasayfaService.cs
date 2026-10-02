using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Anasayfa.Services
{
    public interface IAnasayfaService
    {
        Task<AnasayfaOzetDto> OzetAsync(int yil, int ay, CancellationToken ct = default);
    }

    /// <summary>
    /// Anasayfa kartlarının verisi.
    ///
    /// <b>Yeni hesaplama yazılmadı</b>: banka sayaçları Banka Otomasyon'un kendi
    /// <see cref="IFirmaOzetService"/>'inden geliyor (firma seçim ekranıyla aynı sayı),
    /// beyanname verisi de <c>catalog.Declarations</c>'ın kendisinden. Bu servisin işi
    /// üç kaynağı tek çağrıda toplamak; kuralları <see cref="AnasayfaOzetKurucu"/>
    /// (saf fonksiyon) uyguluyor.
    ///
    /// Anasayfa <b>tek firmaya bağlı değil</b>: kullanıcı sekiz firmayı birlikte
    /// yönetiyor ve açılışta hepsinin durumunu görmek istiyor. Bu yüzden firma kapsam
    /// filtresi uygulanmaz — Banka Otomasyon'un firma seçim ekranındaki karar (§64'ün
    /// yerine geçen §69) ile aynı gerekçe.
    /// </summary>
    public class AnasayfaService : IAnasayfaService
    {
        /// <summary>Yaklaşan ödemelerin arandığı pencere. Gecikmiş olanlar da listelenir.</summary>
        public const int OdemePenceresiGun = 15;

        /// <summary>Gecikmiş ödemelerin ne kadar geriye kadar gösterileceği.</summary>
        public const int GecmisPenceresiGun = 30;

        private readonly CatalogContext _db;
        private readonly IFirmaOzetService _firmaOzet;
        private readonly TimeProvider _saat;

        public AnasayfaService(CatalogContext db, IFirmaOzetService firmaOzet, TimeProvider? saat = null)
        {
            _db = db;
            _firmaOzet = firmaOzet;
            _saat = saat ?? TimeProvider.System;
        }

        public async Task<AnasayfaOzetDto> OzetAsync(int yil, int ay, CancellationToken ct = default)
        {
            // Prompt 7B: saat enjekte edilir (DateTime.Today değil); davranış aynı.
            var bugun = _saat.GetLocalNow().Date;
            if (yil < 2000 || yil > 2100) yil = bugun.Year;
            if (ay is < 1 or > 12) ay = bugun.Month;

            var ayinBeyannameleri = await _db.Declarations.AsNoTracking()
                .Where(d => d.Year == yil && d.Month == ay)
                .ToListAsync(ct);

            // Yaklaşan ödemeler ay sınırını aşabiliyor (ağustos beyannamesinin vadesi
            // eylülde); bu yüzden ay değil TARİH aralığı sorgulanıyor.
            var bas = bugun.AddDays(-GecmisPenceresiGun);
            var bit = bugun.AddDays(OdemePenceresiGun);

            var yaklasanlar = await _db.Declarations.AsNoTracking()
                .Where(d => d.DueDate >= bas && d.DueDate <= bit)
                .ToListAsync(ct);

            var firmalar = await _db.Firmalar.AsNoTracking()
                .Where(f => f.Aktif)
                .Select(f => new { f.Id, f.KisaAd, f.Unvan })
                .ToListAsync(ct);

            var firmaAdlari = firmalar.ToDictionary(
                f => f.Id,
                f => string.IsNullOrWhiteSpace(f.KisaAd) ? f.Unvan : f.KisaAd);

            var bankaOzetleri = await _firmaOzet.OzetlerAsync(firmalar.Select(f => f.Id), ct);

            return AnasayfaOzetKurucu.Kur(yil, ay, bugun, OdemePenceresiGun,
                                          ayinBeyannameleri, yaklasanlar, firmaAdlari, bankaOzetleri);
        }
    }
}
