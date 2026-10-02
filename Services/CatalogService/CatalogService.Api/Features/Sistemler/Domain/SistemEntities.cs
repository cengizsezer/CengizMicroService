using System.Globalization;
using System.Text;

namespace CatalogService.Api.Features.Sistemler.Domain
{
    /// <summary>Sistemin hangi iş için kullanıldığı. Sayısal değerler istemciyle ortak.</summary>
    public enum SistemTuru : byte
    {
        Muhasebe = 1,
        EFatura = 2,
        Bordro = 3,
        Banka = 4,
        Beyanname = 5,
        Diger = 6
    }

    /// <summary>
    /// "Hangi iş nerede yapılıyor" listesinin bir kaydı: Luca, ORKA, DijitalPlanet…
    ///
    /// Liste BÜTÜN FİRMALAR İÇİN ORTAK (tenant'tan ve firmadan bağımsız): DijitalPlanet bir
    /// kez tanımlanır, her firma aynı kaydı gösterir — "DijitalPlanet kullanan firmalarım"
    /// ancak böyle sorulabilir. Serbest metin yazılsaydı aynı sistem üç farklı yazımla dağılırdı.
    ///
    /// Silinmez: yanlış açılan kayıt başka bir kayda <b>birleştirilir</b>, kullanılmayan
    /// pasife alınır (pasif kayıt yeni seçimde çıkmaz, mevcut atamalar bozulmaz).
    /// </summary>
    public class Sistem
    {
        public int Id { get; set; }

        public string Ad { get; set; } = string.Empty;

        /// <summary>
        /// Aynı ad iki türde ayrı kayıt olabilir (Luca hem muhasebe hem bordro programı):
        /// her açılır liste yalnız kendi türünü gösterir.
        /// </summary>
        public SistemTuru Tur { get; set; }

        public bool Aktif { get; set; } = true;

        /// <summary>UTC.</summary>
        public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// JWT <c>sub</c>. Seed kayıtlarında <see cref="SistemSeed.SeedKullanicisi"/>, Prompt 8'in
        /// program alanından taşınırken açılan kayıtlarda <see cref="SistemSeed.TasimaKullanicisi"/>.
        /// </summary>
        public string? OlusturanKullaniciId { get; set; }
    }

    /// <summary>
    /// Firmanın kullandığı sistem. Bir firmada aynı türden birden çok sistem olabilir (iki
    /// banka portalı); aynı sistem iki kez atanmaz.
    ///
    /// ORKA firma kodu burada TUTULMAZ: <c>Firma.OrkaFirmaKodu</c> tek kaynaktır (ORKA'ya
    /// aktarım yükü oradan okur). Kart onu da gösterir ve aynı alana yazar.
    /// </summary>
    public class FirmaSistemi
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public int SistemId { get; set; }

        /// <summary>O sistemdeki firma kodu (Luca'da "0417" gibi).</summary>
        public string? FirmaKodu { get; set; }

        public int Sira { get; set; }
    }

    /// <summary>
    /// Benzer ad karşılaştırması. "DijitalPlanet", "Dijital Planet" ve "dijital-planet"
    /// aynı anahtara iner; liste üç hafta sonra üç kayda bölünmesin.
    /// </summary>
    public static class SistemAdi
    {
        private static readonly CultureInfo Tr = new("tr-TR");

        /// <summary>
        /// Türkçe küçültme (İ→i, I→ı — <c>ToLowerInvariant</c> "İ"yi "i̇" yapar, karşılaştırma
        /// bozulur), sonra boşluk, nokta, tire, alt çizgi atılır. Noktalı/noktasız i farkı da
        /// sayılmaz: "DIJITAL" yazan ile "Dijital" yazan aynı sistemi kastediyor.
        /// </summary>
        public static string Normalize(string? ad)
        {
            if (string.IsNullOrWhiteSpace(ad)) return string.Empty;

            var kucuk = ad.Trim().ToLower(Tr).Normalize(NormalizationForm.FormC);
            var sb = new StringBuilder(kucuk.Length);
            foreach (var c in kucuk)
            {
                if (char.IsWhiteSpace(c) || c is '.' or '-' or '_' or '̇') continue;
                sb.Append(c == 'ı' ? 'i' : c);
            }
            return sb.ToString();
        }

        /// <summary>Normalize edilmiş adlar aynı ya da tek harf farkla aynı (uzun adlarda yazım hatası).</summary>
        public static bool Benzer(string? a, string? b)
        {
            var x = Normalize(a);
            var y = Normalize(b);
            if (x.Length == 0 || y.Length == 0) return false;
            if (x == y) return true;
            return Math.Min(x.Length, y.Length) >= 5 && TekHarfFark(x, y);
        }

        /// <summary>Levenshtein uzaklığı ≤ 1 (ekleme, silme ya da değiştirme).</summary>
        private static bool TekHarfFark(string x, string y)
        {
            if (Math.Abs(x.Length - y.Length) > 1) return false;

            int i = 0, j = 0, fark = 0;
            while (i < x.Length && j < y.Length)
            {
                if (x[i] == y[j]) { i++; j++; continue; }
                if (++fark > 1) return false;

                if (x.Length > y.Length) i++;
                else if (y.Length > x.Length) j++;
                else { i++; j++; }
            }
            return fark + (x.Length - i) + (y.Length - j) <= 1;
        }
    }
}
