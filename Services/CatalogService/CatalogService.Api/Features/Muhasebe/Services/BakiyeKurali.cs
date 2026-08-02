using CatalogService.Api.Features.Muhasebe.Domain;
using CatalogService.Api.Features.Muhasebe.Dtos;

namespace CatalogService.Api.Features.Muhasebe.Services
{
    /// <summary>
    /// İş kuralı 20: bakiye yönü hesabın karakterine göre belirlenir.
    /// Aktif/Gider/Maliyet hesaplarda <c>Borç − Alacak</c>, Pasif/Gelir hesaplarda
    /// <c>Alacak − Borç</c>.
    /// </summary>
    public static class BakiyeKurali
    {
        /// <summary>Karakterin bakiyeyi borç yönlü mü okuduğu.</summary>
        public static bool BorcYonlu(HesapKarakter karakter) => karakter switch
        {
            HesapKarakter.Aktif => true,
            HesapKarakter.Gider => true,
            HesapKarakter.Maliyet => true,
            HesapKarakter.Pasif => false,
            HesapKarakter.Gelir => false,
            // Nazım hesaplar kural 20'de sayılmıyor; borçlu nazım hesap yaygın
            // olduğu için borç yönlü okunur. Karşı yönlü kullanım gerekirse
            // karakter listesine ayrı bir değer eklenmeli.
            _ => true
        };

        /// <summary>İş kuralı 20'ye göre yönlü bakiye.</summary>
        public static decimal Bakiye(HesapKarakter karakter, decimal borc, decimal alacak)
            => BorcYonlu(karakter) ? borc - alacak : alacak - borc;

        /// <summary>Bakiyenin fiilen hangi tarafta kaldığı; karakterden bağımsızdır.</summary>
        public static BakiyeYonu Yon(decimal borc, decimal alacak)
        {
            var fark = borc - alacak;
            if (fark > 0) return BakiyeYonu.Borc;
            return fark < 0 ? BakiyeYonu.Alacak : BakiyeYonu.Yok;
        }

        /// <summary>Mizanın borç bakiye kolonu.</summary>
        public static decimal BorcBakiye(decimal borc, decimal alacak) => Math.Max(borc - alacak, 0m);

        /// <summary>Mizanın alacak bakiye kolonu.</summary>
        public static decimal AlacakBakiye(decimal borc, decimal alacak) => Math.Max(alacak - borc, 0m);
    }
}
