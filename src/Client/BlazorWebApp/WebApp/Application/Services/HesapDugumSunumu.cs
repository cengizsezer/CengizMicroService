using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.Application.Services
{
    /// <summary>
    /// Hesap düğümünün ekranda gösterilecek tutarı — TEK KAYNAK.
    ///
    /// Ham mizan bakiyesi borç-pozitiftir; sunum yönü hesabın TDHP ana grubuna göre
    /// <see cref="MaliTabloIsareti"/> ile uygulanır, elle işaret çevirme yapılmaz.
    /// 600 alacak bakiyeli olduğu hâlde kullanıcıya pozitif görünür.
    ///
    /// Ekranda görünen sayı ile hesapta kullanılan sayı aynıdır; hesap seçici ve etiket
    /// motoru bu tek dönüşümü paylaşır.
    /// </summary>
    public static class HesapDugumSunumu
    {
        /// <summary>TDHP ana grubuna karşılık gelen mali tablo bölümü.</summary>
        public static MaliTabloBolumu Bolum(int anaGrup) => anaGrup switch
        {
            1 or 2 => MaliTabloBolumu.Aktif,
            3 or 4 or 5 => MaliTabloBolumu.Pasif,
            6 or 7 => MaliTabloBolumu.GelirTablosu,
            _ => MaliTabloBolumu.Aktif,   // 8-9 nazım: ham borç-pozitif değer olduğu gibi
        };

        public static decimal Sunum(HesapDugumu dugum) =>
            MaliTabloIsareti.Uygula(dugum.Bakiye, Bolum(dugum.AnaGrup)) ?? 0m;
    }
}
