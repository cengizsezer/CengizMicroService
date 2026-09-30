using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Firmalar
{
    /// <summary>
    /// Mükellefiyet kodlarından vergi türü / defter usulü önerisini <b>mevcut</b> firmalara
    /// da uygular. Sınıflandırma alanları eklenmeden önce kaydedilmiş firmalarda
    /// mükellefiyet zaten dolu; yeniden kaydedilmeyi beklemeden öneri görünsün diye.
    ///
    /// İdempotent: her açılışta çalışır, yalnız farkı yazar. <b>Yalnız Belirsiz alanı
    /// doldurur</b>; dolu (otomatik ya da elle) alanı asla geri almaz ya da değiştirmez
    /// (<see cref="MukellefiyetSiniflandirici.Uygula"/>, 6C).
    /// </summary>
    public static class FirmaSiniflandirmaSeed
    {
        public static async Task SeedAsync(CatalogContext db, ILogger? log = null, CancellationToken ct = default)
        {
            var firmalar = await db.Firmalar.ToListAsync(ct);
            if (firmalar.Count == 0) return;

            var mukellefiyetler = await db.FirmaSicilBilgileri.AsNoTracking()
                .Select(s => new { s.FirmaId, s.MukellefiyetTurleri })
                .ToDictionaryAsync(s => s.FirmaId, s => s.MukellefiyetTurleri, ct);

            var degisen = 0;
            foreach (var firma in firmalar)
            {
                mukellefiyetler.TryGetValue(firma.Id, out var metin);
                MukellefiyetSiniflandirici.AyiklaVeLogla(log, firma.Id, metin);
                if (MukellefiyetSiniflandirici.Uygula(firma, metin, log)) degisen++;
            }

            if (degisen > 0) await db.SaveChangesAsync(ct);
        }
    }
}
