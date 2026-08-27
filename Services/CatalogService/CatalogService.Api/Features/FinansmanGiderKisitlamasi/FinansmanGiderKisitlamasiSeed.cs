using CatalogService.Api.Features.FinansmanGiderKisitlamasi.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.FinansmanGiderKisitlamasi
{
    /// <summary>
    /// Kısıtlama oranının bilinen yılları. Satır bazında idempotenttir: yılı zaten
    /// kayıtlı olan orana DOKUNULMAZ — kullanıcı ekrandan değiştirdiyse seed geri almasın.
    /// Oran ortak referans olduğundan tenant'tan bağımsız tek sefer çalışır.
    /// </summary>
    public static class FinansmanGiderKisitlamasiSeed
    {
        private const string Dayanak = "3490 sayılı Cumhurbaşkanı Kararı (RG 4.2.2021)";

        public static async Task SeedAsync(CatalogContext db, CancellationToken ct = default)
        {
            // Kısıtlama 1/1/2021'den itibaren uygulanıyor ve oran o tarihten beri %10.
            foreach (var yil in new[] { 2021, 2022, 2023, 2024, 2025, 2026 })
            {
                if (await db.FinansmanKisitlamaOranlari.AnyAsync(x => x.Yil == yil, ct))
                    continue;

                db.FinansmanKisitlamaOranlari.Add(new FinansmanKisitlamaOrani
                {
                    Yil = yil,
                    Oran = 10m,
                    Dayanak = Dayanak
                });
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
