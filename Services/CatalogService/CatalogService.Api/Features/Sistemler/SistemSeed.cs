using CatalogService.Api.Features.Sistemler.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Sistemler
{
    /// <summary>
    /// Ortak sistem listesinin başlangıç kayıtları. YALNIZ gerçekten kullanılanlar —
    /// başkası uydurulmaz, kullanıcı "+ Yeni ekle" ile ekler.
    ///
    /// Aynı kayıtları <c>KullanilanSistemler</c> migration'ı da ekliyor (Prompt 8'in program
    /// değerleri taşınırken hedef kayıt hazır olsun diye). Bu seed migration'sız kurulan
    /// ortam ve testler için; var olana dokunmaz, adı değiştirilmiş kaydı geri açmaz
    /// (tür + normalize ad ile bakar).
    /// </summary>
    public static class SistemSeed
    {
        public const string SeedKullanicisi = "seed";

        /// <summary>Prompt 8'deki program değerinden, eşleşen kayıt bulunamadığı için açılan sistem.</summary>
        public const string TasimaKullanicisi = "tasima:prompt9";

        public static readonly IReadOnlyList<(string Ad, SistemTuru Tur)> Kayitlar = new[]
        {
            ("Luca", SistemTuru.Muhasebe),
            ("ORKA", SistemTuru.Muhasebe),
            ("Logo", SistemTuru.Muhasebe),
            ("Mikro", SistemTuru.Muhasebe),
            ("DijitalPlanet", SistemTuru.EFatura),
            ("TURMOB", SistemTuru.EFatura),
            ("e-Beyanname GİB", SistemTuru.Beyanname)
        };

        public static async Task<int> SeedAsync(CatalogContext db, CancellationToken ct = default)
        {
            var mevcut = await db.Sistemler.AsNoTracking().Select(s => new { s.Ad, s.Tur }).ToListAsync(ct);
            var anahtarlar = mevcut.Select(s => (s.Tur, SistemAdi.Normalize(s.Ad))).ToHashSet();

            var eklenen = 0;
            foreach (var (ad, tur) in Kayitlar)
            {
                if (anahtarlar.Contains((tur, SistemAdi.Normalize(ad)))) continue;

                db.Sistemler.Add(new Sistem { Ad = ad, Tur = tur, OlusturanKullaniciId = SeedKullanicisi });
                eklenen++;
            }

            if (eklenen > 0) await db.SaveChangesAsync(ct);
            return eklenen;
        }

        /// <summary>
        /// Seed + rapor: program taşımasında eşleşmeyip açılan kayıtlar her açılışta uyarı
        /// olarak loglanır — Yönetim → Sistemler'de birleştirilene ya da adı düzeltilene kadar.
        /// </summary>
        public static async Task SeedVeLoglaAsync(CatalogContext db, ILogger logger, CancellationToken ct = default)
        {
            var eklenen = await SeedAsync(db, ct);
            if (eklenen > 0) logger.LogInformation("Sistemler: {Adet} başlangıç kaydı eklendi.", eklenen);

            var tasinan = await db.Sistemler.AsNoTracking()
                .Where(s => s.OlusturanKullaniciId == TasimaKullanicisi)
                .Select(s => s.Ad + " (" + s.Tur + ")")
                .ToListAsync(ct);

            if (tasinan.Count > 0)
                logger.LogWarning("Sistemler: iş programı taşımasında eşleşmeyen değerden açılan kayıtlar: {Kayitlar}. " +
                                  "Yönetim → Sistemler'de kontrol edin.", string.Join(", ", tasinan));
        }
    }
}
