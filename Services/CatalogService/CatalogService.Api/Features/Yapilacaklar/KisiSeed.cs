using CatalogService.Api.Features.Yapilacaklar.Services;
using CatalogService.Api.Infrastructure.Context;

namespace CatalogService.Api.Features.Yapilacaklar
{
    /// <summary>
    /// Alıcı kayıtlarından kişi üretimi (Prompt 14 · 4.1). Yalnız kişiye bağlanmamış alıcıları işler; yazma
    /// yolu her alıcıyı bağladığı için normalde ilk açılıştan sonra iş bulmaz. Kural
    /// <see cref="KisiEslestirici"/>'de: aynı firmada aynı e-posta tek kişi, e-postasız ada göre; belirsiz ve
    /// şüpheli eşleşme BİRLEŞTİRİLMEZ, burada loglanır.
    /// </summary>
    public static class KisiSeed
    {
        public static async Task SeedVeLoglaAsync(CatalogContext db, ILogger logger)
        {
            var r = await KisiService.TasiAsync(db);
            if (r.AliciKaydi > 0)
                logger.LogInformation(
                    "Kişiler: {Alici} alıcı kaydı {Kisi} yeni kişiye bağlandı ({Eposta} e-posta ile, {Ad} ad ile birleşti; {Belirsiz} belirsiz).",
                    r.AliciKaydi, r.YeniKisi, r.EpostaIleBirlesen, r.AdIleBirlesen, r.Belirsiz);
            foreach (var s in r.FarkliAdYazimlari)
                logger.LogInformation("Kişiler — aynı e-posta, farklı ad yazımı (tek kişi): {Satir}", s);
            foreach (var s in r.Supheliler)
                logger.LogWarning("Kişiler — şüpheli eşleşme (birleştirilmedi): {Satir}", s);
        }
    }
}
