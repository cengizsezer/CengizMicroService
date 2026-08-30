using WebApp.Shared.Dto.Anasayfa;

namespace WebApp.Pages.Anasayfa
{
    /// <summary>
    /// Firma panelindeki arama kutusunun süzgeci.
    ///
    /// Süzme <b>istemcide</b> yapılıyor: sekiz-on firmanın tamamı zaten tek çağrıda
    /// geldi; her harfte sunucuya gitmek listeyi hızlandırmaz, yavaşlatır.
    ///
    /// İki alanda arıyor:
    /// <list type="bullet">
    /// <item><b>Ad/unvan</b> — Türkçe harf duyarsız: "citadel" yazan CİTADEL'i,
    /// "sirket" yazan ŞİRKET'i bulur. Kullanıcı arama kutusunda şapkalı harflerle
    /// uğraşmak zorunda kalmasın.</item>
    /// <item><b>VKN</b> — yalnız rakamlar karşılaştırılır; kullanıcının araya koyduğu
    /// boşluk ya da nokta aramayı bozmaz.</item>
    /// </list>
    /// </summary>
    public static class FirmaPaneliArama
    {
        public static List<FirmaPaneliOzetDto> Suz(IEnumerable<FirmaPaneliOzetDto> firmalar, string? arama)
        {
            var liste = firmalar?.ToList() ?? new List<FirmaPaneliOzetDto>();

            if (string.IsNullOrWhiteSpace(arama)) return liste;

            var metin = Sadelestir(arama);
            var rakamlar = Rakamlar(arama);

            return liste.Where(f =>
                    (metin.Length > 0 && (Sadelestir(f.Ad).Contains(metin) || Sadelestir(f.Unvan).Contains(metin)))
                    || (rakamlar.Length > 0 && Rakamlar(f.VergiKimlikNo).Contains(rakamlar)))
                .ToList();
        }

        /// <summary>
        /// Karşılaştırma biçimi: küçük harf + Türkçe harfleri ASCII karşılığına indirme.
        /// <c>ToLowerInvariant</c> tek başına yetmiyor — "İ" onun için "i̇" (iki kod
        /// noktası) oluyor ve "citadel" araması CİTADEL'i bulamıyordu.
        /// </summary>
        public static string Sadelestir(string? metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return string.Empty;

            var sonuc = new System.Text.StringBuilder(metin.Length);

            foreach (var harf in metin.Trim())
            {
                sonuc.Append(harf switch
                {
                    'ç' or 'Ç' => 'c',
                    'ğ' or 'Ğ' => 'g',
                    'ı' or 'I' or 'i' or 'İ' => 'i',
                    'ö' or 'Ö' => 'o',
                    'ş' or 'Ş' => 's',
                    'ü' or 'Ü' => 'u',
                    _ => char.ToLowerInvariant(harf)
                });
            }

            return sonuc.ToString();
        }

        public static string Rakamlar(string? metin)
            => string.IsNullOrWhiteSpace(metin)
                ? string.Empty
                : new string(metin.Where(char.IsDigit).ToArray());
    }
}
