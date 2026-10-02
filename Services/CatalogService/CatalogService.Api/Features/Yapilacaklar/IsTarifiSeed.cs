using CatalogService.Api.Features.Yapilacaklar.Services;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Yapilacaklar
{
    /// <summary>
    /// Mevcut firma işlerinden ilk iş tariflerinin üretimi (Prompt 14 · 3.3). TEK SEFERLİK: tarif
    /// tablosu boşsa çalışır; bir tarif varsa (kullanıcı ya da önceki açılış) dokunmaz.
    /// Kurallar <see cref="IsTarifiKurucu.Planla"/>'da: metni farklı kayıt bağlanmaz, burada
    /// başlık başlık loglanır — birleştirmeye kullanıcı ekrandan karar verir.
    /// </summary>
    public static class IsTarifiSeed
    {
        public static async Task SeedVeLoglaAsync(CatalogContext db, ILogger logger)
        {
            if (await db.IsTarifleri.AnyAsync()) return;

            var rapor = await new IsTarifiService(db, TimeProvider.System).UretAsync(null);

            logger.LogInformation(
                "İş tarifleri üretildi: {Grup} grup, {Tarif} tarif, {Baglanan} kayıt bağlandı, {Baglanmayan} kayıt farklı metin nedeniyle bağlanmadı.",
                rapor.GrupSayisi, rapor.UretilenTarif, rapor.BaglananKayit, rapor.Baglanmayanlar.Sum(b => b.KayitSayisi));

            foreach (var b in rapor.Baglanmayanlar)
                logger.LogWarning(
                    "İş tarifi — \"{Baslik}\": {Kayit} kayıt bağlanmadı ({Metin} farklı metin) · firmalar: {Firmalar}",
                    b.Baslik, b.KayitSayisi, b.FarkliMetinSayisi, string.Join(", ", b.Firmalar));
        }
    }
}
