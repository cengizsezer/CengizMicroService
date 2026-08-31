using Microsoft.AspNetCore.Authorization;

namespace IdentityService.Application.Services.Agent
{
    /// <summary>
    /// Ajan yönetimi ekranının izinleri.
    ///
    /// Ajan yönetimi bir role değil, <c>perm</c> claim'ine bağlı — repodaki
    /// <c>Vehicle.View</c> / <c>BeyannameTakip.View</c> kalıbının aynısı. İzinleri
    /// token'a <c>GenerateJwtToken</c> basıyor, rol → izin eşlemesi
    /// <c>IdentityContextSeed</c>'de duruyor.
    ///
    /// Aynı anahtarlar CatalogService tarafında da yazılı
    /// (<c>Features/Ajanlar/AjanKimligi.cs</c>): iki servis arasında paylaşılan bir
    /// kütüphane yok, sözleşme iki dosyada duruyor ve birlikte değişmeli.
    /// </summary>
    public static class AjanYetkileri
    {
        public const string IzinClaim = "perm";

        /// <summary>Ajan listesini ve durumunu görme.</summary>
        public const string Goruntule = "AjanYonetimi.View";

        /// <summary>
        /// Anahtar üretme, ajan iptal etme, bağlantı düşürme. Görüntülemeden ayrı:
        /// üretilen anahtar ofisteki makineye uzun ömürlü kimlik veriyor, listeyi
        /// görmekle aynı yetki sayılmamalı.
        /// </summary>
        public const string Duzenle = "AjanYonetimi.Edit";

        public const string GoruntulePolitikasi = "AjanYonetimiGoruntule";
        public const string DuzenlePolitikasi = "AjanYonetimiDuzenle";

        public static void Ekle(AuthorizationOptions o)
        {
            o.AddPolicy(GoruntulePolitikasi, p => p
                .RequireAuthenticatedUser()
                .RequireClaim(IzinClaim, Goruntule));

            o.AddPolicy(DuzenlePolitikasi, p => p
                .RequireAuthenticatedUser()
                .RequireClaim(IzinClaim, Duzenle));
        }
    }
}
