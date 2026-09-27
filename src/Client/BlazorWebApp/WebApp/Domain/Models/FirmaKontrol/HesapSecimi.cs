namespace WebApp.Domain.Models.FirmaKontrol
{
    /// <summary>
    /// Hesap seçici diyaloğunun çıktısı: seçilen düğüm ve kullanıcının seçtiği işaret.
    /// İşaret, sonraki adımdaki etiket/formül sisteminin "ekle / çıkar" yönüdür;
    /// bu adımda hiçbir hesaplamada kullanılmaz.
    /// </summary>
    public class HesapSecimi
    {
        public HesapDugumu Dugum { get; init; } = new();

        /// <summary>+1 ekle, −1 çıkar.</summary>
        public int Isaret { get; init; } = 1;

        public string IsaretMetni => Isaret < 0 ? "−" : "+";
    }
}
