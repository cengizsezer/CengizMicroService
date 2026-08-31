using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace CatalogService.Api.Features.Ajanlar
{
    /// <summary>
    /// Ajan token'ını kullanıcı token'ından ayıran claim'ler ve bu ayrıma dayanan
    /// yetki politikaları.
    ///
    /// Token'ı IdentityService basıyor (<c>AjanClaimleri</c>); iki servis arasında
    /// paylaşılan bir kütüphane yok, sözleşme bu iki dosyada duruyor. Adlar
    /// değişirse ikisi birlikte değişmeli.
    ///
    /// <b>Karar neden <c>ajan_id</c>'ye dayanıyor:</b> <c>typ</c> okunabilir bir
    /// işaret ama JwtBearer gelen kısa claim adlarının bir kısmını uzun URI'lere
    /// çeviriyor. <c>ajan_id</c> o eşleme tablosunda yok — bize ait bir ad, olduğu
    /// gibi geliyor ve bir kullanıcı token'ında hiç bulunmuyor.
    /// </summary>
    public static class AjanKimligi
    {
        public const string TipClaim = "typ";
        public const string AjanTipi = "agent";
        public const string AjanIdClaim = "ajan_id";
        public const string AjanAdiClaim = "ajan_adi";

        /// <summary>Token bir ajana mı ait?</summary>
        public static bool AjanMi(ClaimsPrincipal? kullanici) =>
            !string.IsNullOrWhiteSpace(AjanId(kullanici));

        /// <summary>Ajan kimliği; token bir ajana ait değilse boş.</summary>
        public static string AjanId(ClaimsPrincipal? kullanici) =>
            kullanici?.FindFirst(AjanIdClaim)?.Value?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Hub'ın ve durum ucunun yetki politikaları.
    ///
    /// İkisi birbirinin tersi ve bu bilerek böyle: hub'a yalnız ajan girebilir —
    /// kullanıcı token'ı taşıyan bir istemcinin ajan gibi kaydolup iş emri
    /// beklemesini engeller. Durum ucuna ise yalnız insan girebilir; orası
    /// ajanların listelendiği ekranın kaynağı, ajanın kendisinin okuyacağı bir yer
    /// değil.
    /// </summary>
    public static class AjanPolitikalari
    {
        public const string YalnizAjan = "AjanTokeni";
        public const string YalnizInsan = "KullaniciTokeni";

        /// <summary>
        /// Ajan yönetimi izni; ekranı açan kullanıcının token'ında aranan
        /// <c>perm</c> claim'i. Anahtarlar IdentityService'te
        /// <c>Application/Services/Agent/AjanYetkileri.cs</c>'de de yazılı —
        /// iki servis arasında paylaşılan kütüphane yok, birlikte değişmeli
        /// (bkz. KARARLAR §131).
        /// </summary>
        public const string IzinClaim = "perm";
        public const string AjanYonetimiGoruntule = "AjanYonetimi.View";
        public const string AjanYonetimiDuzenle = "AjanYonetimi.Edit";

        /// <summary>
        /// İnsan + ajan yönetimi düzenleme izni. Bağlantı düşürme buna bağlı:
        /// ajanı iptal eden kullanıcı soketi de kapatabilmeli, ama "ajanlar bağlı
        /// mı" diye bakan herkes kapatabilmemeli.
        /// </summary>
        public const string YonetimiDuzenle = "AjanYonetimiDuzenle";

        public static void Ekle(AuthorizationOptions o)
        {
            o.AddPolicy(YalnizAjan, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => AjanKimligi.AjanMi(ctx.User)));

            o.AddPolicy(YalnizInsan, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => !AjanKimligi.AjanMi(ctx.User)));

            o.AddPolicy(YonetimiDuzenle, p => p
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => !AjanKimligi.AjanMi(ctx.User))
                .RequireClaim(IzinClaim, AjanYonetimiDuzenle));

            // Politika yazılmamış her [Authorize] varsayılan olarak İNSAN ister.
            //
            // Ajan token'ı kullanıcı token'ıyla aynı imzayı taşıyor; varsayılan
            // "kimliği doğrulanmış olsun" kalsaydı ofisteki makinede duran bir
            // anahtar, servisin bütün uçlarını açardı. Ajanın girebileceği yerler
            // tek tek YalnizAjan ile işaretleniyor — kapı varsayılan olarak kapalı,
            // açılan yerler görünür.
            o.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => !AjanKimligi.AjanMi(ctx.User))
                .Build();
        }
    }
}
