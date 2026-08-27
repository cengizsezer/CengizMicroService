using WebApp.Pages.Hesaplamalar.Bordro;

namespace WebApp.Pages.Hesaplamalar
{
    /// <summary>
    /// Hesaplamalar sayfasının bir alt sekmesi: adres parçası, başlık, ikon ve içeriği
    /// basan bileşen.
    ///
    /// <b>Yeni sekme nasıl eklenir?</b> İki adım, sayfa iskeletine dokunmadan:
    /// <list type="number">
    /// <item>İçeriği için bir Razor bileşeni yazın (kendi <c>@page</c> rotası OLMASIN —
    /// rotayı <see cref="HesaplamalarPage"/> yönetiyor).</item>
    /// <item><see cref="Hepsi"/> listesine bir satır ekleyin.</item>
    /// </list>
    /// Şerit ve yönlendirme bu listeden üretiliyor; <c>HesaplamalarPage.razor</c> hiç
    /// değişmiyor (bkz. KARARLAR §79).
    /// </summary>
    /// <param name="Slug">Adresin son parçası: <c>/hesaplamalar/{Slug}</c>. Küçük harf, tireli.</param>
    /// <param name="Baslik">Sekme şeridinde ve sayfa başlığında görünen ad.</param>
    /// <param name="Ikon">Radzen/Material ikon adı.</param>
    /// <param name="Bilesen">Sekme seçilince basılacak bileşenin tipi.</param>
    public sealed record HesaplamaSekmesi(string Slug, string Baslik, string Ikon, Type Bilesen)
    {
        /// <summary>Sekmenin tam adresi.</summary>
        public string Yol => $"/hesaplamalar/{Slug}";

        /// <summary>
        /// Kayıtlı sekmeler; şeritteki sıra bu listenin sırasıdır. İlk sıradaki sekme
        /// <see cref="Varsayilan"/> olur ve <c>/hesaplamalar</c> kökü ona yönlenir.
        /// </summary>
        public static readonly IReadOnlyList<HesaplamaSekmesi> Hepsi = new[]
        {
            new HesaplamaSekmesi("bordro", "Bordro Hesaplaması", "payments", typeof(BordroHesaplamasi)),
            new HesaplamaSekmesi("finansman-gider-kisitlamasi", "Finansman Gider Kısıtlaması", "percent",
                typeof(FinansmanGiderKisitlamasi.FinansmanKisitlamaHesabi))
        };

        /// <summary>Kök adresin açtığı sekme.</summary>
        public static HesaplamaSekmesi Varsayilan => Hepsi[0];

        /// <summary>
        /// Adres parçasına karşılık gelen sekme; tanınmayan parçada <c>null</c>. Sayfa
        /// bu durumda hata vermez, şeridi çizip "sekme bulunamadı" der — kırık bir yer imi
        /// kullanıcıyı çalışan sekmelerin yanına düşürsün.
        /// </summary>
        public static HesaplamaSekmesi? Bul(string? slug)
            => string.IsNullOrWhiteSpace(slug)
                ? null
                : Hepsi.FirstOrDefault(s => string.Equals(s.Slug, slug.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
