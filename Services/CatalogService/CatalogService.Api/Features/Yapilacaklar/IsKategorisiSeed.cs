using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Yapilacaklar
{
    /// <summary>
    /// Firma Panosu Aşama 2: başlangıç kategorileri. TEK SEFERLİK — tablo boşsa yazar; bir kayıt bile varsa
    /// (kullanıcı ya da önceki açılış) dokunmaz, silinen kategori geri gelmez. "Diğer" kayıt değildir, yazılmaz.
    /// Mükellefiyet eşlemesi seed'lenmez: eşleşmeyen yasal iş "Beyanname"dedir.
    /// </summary>
    public static class IsKategorisiSeed
    {
        /// <summary>Beyanname yasal varsayılandır (eşlemesi olmayan yasal iş); ad değil kolon belirler.</summary>
        public static readonly (string Ad, int Sira, bool VarsayilanYasal)[] Kategoriler =
        {
            ("Banka & Cari", 1, false),
            ("Kayıt & Kapanış", 2, false),
            ("Bordro", 3, false),
            ("Beyanname", 4, true),
        };

        /// <returns>Yazılan kategori sayısı (tablo doluysa 0).</returns>
        public static Task<int> SeedAsync(CatalogContext db, CancellationToken ct = default)
        {
            // CatalogContext'te retry stratejisi açık: yazma stratejinin İÇİNDE. Yeniden denemede önceki
            // denemenin izlenen (yarım) varlıkları atılır; "boş mu" kontrolü de her denemede tekrar yapılır.
            var deneme = 0;
            return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                if (++deneme > 1) db.ChangeTracker.Clear();
                if (await db.IsKategorileri.AnyAsync(ct)) return 0;

                db.IsKategorileri.AddRange(Kategoriler.Select(k => new IsKategorisi { Ad = k.Ad, Sira = k.Sira, Aktif = true, VarsayilanYasal = k.VarsayilanYasal }));
                await db.SaveChangesAsync(ct);   // tek SaveChanges = tek transaction
                return Kategoriler.Length;
            });
        }

        public static async Task SeedVeLoglaAsync(CatalogContext db, ILogger logger)
        {
            var yazilan = await SeedAsync(db);
            if (yazilan > 0) logger.LogInformation("İş kategorileri: {Sayi} başlangıç kategorisi yazıldı.", yazilan);
            else logger.LogInformation("İş kategorileri: tablo dolu, seed atlandı.");
        }
    }
}
