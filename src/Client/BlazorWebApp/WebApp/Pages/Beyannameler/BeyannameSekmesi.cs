namespace WebApp.Pages.Beyannameler
{
    /// <summary>
    /// Beyannameler sayfasının bir alt sekmesi: adres parçası, başlık, ikon ve içeriği
    /// basan bileşen.
    ///
    /// <b>Yeni sekme nasıl eklenir?</b> İki adım, sayfa iskeletine dokunmadan:
    /// <list type="number">
    /// <item>İçeriği için bir Razor bileşeni yazın (kendi <c>@page</c> rotası OLMASIN —
    /// rotayı <see cref="BeyannamelerPage"/> yönetiyor).</item>
    /// <item><see cref="Hepsi"/> listesine bir satır ekleyin.</item>
    /// </list>
    /// Kalıp Hesaplamalar sayfasından alındı (KARARLAR §79); şerit ve yönlendirme bu
    /// listeden üretiliyor, sayfa dosyası hiç değişmiyor.
    /// </summary>
    /// <param name="Slug">Adresin son parçası: <c>/beyannameler/{Slug}</c>. Küçük harf, tireli.</param>
    /// <param name="Baslik">Sekme şeridinde ve sayfa başlığında görünen ad.</param>
    /// <param name="Ikon">Radzen/Material ikon adı.</param>
    /// <param name="Bilesen">Sekme seçilince basılacak bileşenin tipi.</param>
    public sealed record BeyannameSekmesi(string Slug, string Baslik, string Ikon, Type Bilesen)
    {
        /// <summary>Sekmenin tam adresi.</summary>
        public string Yol => $"/beyannameler/{Slug}";

        /// <summary>
        /// Kayıtlı sekmeler; şeritteki sıra bu listenin sırasıdır. İlk sıradaki sekme
        /// <see cref="Varsayilan"/> olur ve <c>/beyannameler</c> kökü ona yönlenir.
        /// </summary>
        public static readonly IReadOnlyList<BeyannameSekmesi> Hepsi = new[]
        {
            new BeyannameSekmesi("takip", "Takip", "receipt_long", typeof(WebApp.Pages.DeclarationFollow.DeclarationFollow)),
            new BeyannameSekmesi("ozet", "Özet", "grid_on", typeof(BeyannameOzetTab))
        };

        /// <summary>Kök adresin açtığı sekme.</summary>
        public static BeyannameSekmesi Varsayilan => Hepsi[0];

        /// <summary>
        /// Adres parçasına karşılık gelen sekme; tanınmayan parçada <c>null</c>. Sayfa
        /// bu durumda hata vermez, şeridi çizip "sekme bulunamadı" der — kırık bir yer imi
        /// kullanıcıyı çalışan sekmelerin yanına düşürsün.
        /// </summary>
        public static BeyannameSekmesi? Bul(string? slug)
            => string.IsNullOrWhiteSpace(slug)
                ? null
                : Hepsi.FirstOrDefault(s => string.Equals(s.Slug, slug.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
