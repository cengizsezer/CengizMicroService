using CatalogService.Api.Infrastructure.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Kapsam
{
    /// <summary>
    /// Firma kapsamının sorgulara uygulanması.
    ///
    /// Tek bir yerde durmasının sebebi, "tüm firmalar" hâlinin her serviste ayrı ayrı
    /// yazılmasının er geç birinde unutulacak olması: unutulan yerde kapsam
    /// <c>FirmaId == 0</c>'a süzülür ve ekran <b>boş</b> görünür. Burada toplanınca
    /// davranış tek satırda okunuyor.
    ///
    /// Bu yalnız <b>okuma</b> tarafıdır. Yazarken kapsam doğrudan
    /// <see cref="IBankaFirmaKapsami.FirmaId"/>'den alınır ve sıfır olamaz — filtre
    /// yazma isteğinde kapsamsızlığa zaten izin vermez.
    /// </summary>
    public static class FirmaKapsamiSorgu
    {
        /// <summary>
        /// Kapsam tek firmaysa <c>FirmaId</c> eşitliği; "tüm firmalar" ise süzme yok.
        /// </summary>
        public static IQueryable<T> FirmayaGore<T>(this IQueryable<T> sorgu, IBankaFirmaKapsami kapsam)
            where T : FirmaKapsamliEntity
            => kapsam.TumFirmalar ? sorgu : sorgu.Where(x => x.FirmaId == kapsam.FirmaId);
    }
}
