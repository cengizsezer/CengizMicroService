namespace WebApp.Pages.Yapilacaklar
{
    /// <summary>
    /// İş Takip Panosu'nun adresi — TEK YER (Prompt 15). Uygulamadaki bütün iç bağlantılar bunu kullanır;
    /// adres bir daha değişirse bağlantı kırılmaz. Eski adres <see cref="EskiAdres"/> yönlendirilir.
    /// </summary>
    public static class IsTakipPanosuAdres
    {
        public const string Yol = "/is-takip-panosu";

        /// <summary>Prompt 10–14'teki adres; kayıtlı bağlantılar için yönlendirme duruyor.</summary>
        public const string EskiAdres = "/donem-panosu";

        /// <summary>Nasıl yapılır sekmesinde bir işin tarifi (isteğe bağlı firma).</summary>
        public static string Tarif(string anahtar, int? firmaId = null)
            => $"{Yol}?sekme=nasil&is={Uri.EscapeDataString(anahtar)}" + (firmaId is { } f ? $"&firma={f}" : "");

        /// <summary>Kişiler sekmesinde tek kişi.</summary>
        public static string Kisi(int kisiId) => $"{Yol}?sekme=kisiler&kisi={kisiId}";
    }
}
