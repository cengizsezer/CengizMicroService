using System.Text.RegularExpressions;
using CatalogService.Api.Features.BankaEkstre.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    public interface IUnvanCikarici
    {
        /// <summary>Ham açıklamadan karşı tarafın unvanı; hiçbir desen tutmazsa null.</summary>
        string? Cikar(string? hamAciklama, IReadOnlyList<UnvanDeseni> desenler);
    }

    /// <summary>
    /// Desen listesi sırayla denenir, ilk yakalayan kazanır. Desenler veritabanından
    /// (banka bazlı) gelir; kodda desen yoktur.
    /// </summary>
    public class UnvanCikarici : IUnvanCikarici
    {
        private static readonly TimeSpan ZamanAsimi = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Derlenmiş regex önbelleği. Desenler veritabanından geldiği için her satırda
        /// yeniden derlenmesin diye tutulur (bir ekstre 300+ satır olabiliyor).
        /// </summary>
        private static readonly Dictionary<string, Regex?> Onbellek = new(StringComparer.Ordinal);
        private static readonly object Kilit = new();

        /// <summary>Unvan olarak kabul edilmeyecek kadar kısa yakalamalar elenir.</summary>
        private const int EnAzUzunluk = 3;

        public string? Cikar(string? hamAciklama, IReadOnlyList<UnvanDeseni> desenler)
        {
            if (string.IsNullOrWhiteSpace(hamAciklama)) return null;

            foreach (var desen in desenler.Where(d => d.Aktif).OrderBy(d => d.Sira))
            {
                var regex = Derle(desen.Desen);
                if (regex is null) continue;

                Match eslesme;
                try
                {
                    eslesme = regex.Match(hamAciklama);
                }
                catch (RegexMatchTimeoutException)
                {
                    // Patolojik desen tüm ekstreyi durdurmasın; sıradaki desene geç.
                    continue;
                }

                if (!eslesme.Success) continue;
                if (desen.GrupNo >= eslesme.Groups.Count && desen.GrupNo != 0) continue;

                var ham = eslesme.Groups[desen.GrupNo].Value;
                var temiz = Temizle(ham);

                if (temiz.Length >= EnAzUzunluk) return temiz;
            }

            return null;
        }

        /// <summary>
        /// Yakalanan metnin kuyruk gürültüsünü atar: noktalama, çift boşluk,
        /// sonda kalan bağlaç/kesik kelime işaretleri.
        /// </summary>
        private static string Temizle(string? ham)
        {
            if (string.IsNullOrWhiteSpace(ham)) return string.Empty;

            // Nokta kırpılmaz: "A.Ş." gibi kısaltmalar unvanın parçası.
            var temiz = ham.Trim().Trim('-', '/', ',', ':', ';', '(', ')', '"', '\'').Trim();
            temiz = Regex.Replace(temiz, @"\s+", " ", RegexOptions.None, ZamanAsimi);

            return Normalizasyon.Kirp(temiz, 150);
        }

        /// <summary>
        /// Desenler büyük/küçük harf duyarlı derlenir: ölçülen desenler
        /// <c>[A-ZÇĞİÖŞÜ]</c> gibi büyük harf sınıflarına dayanıyor, IgnoreCase
        /// bu sınıfları küçük harfe de açıp kapsamı bozardı.
        /// </summary>
        private static Regex? Derle(string desen)
        {
            if (string.IsNullOrWhiteSpace(desen)) return null;

            lock (Kilit)
            {
                if (Onbellek.TryGetValue(desen, out var mevcut)) return mevcut;

                Regex? regex;
                try
                {
                    regex = new Regex(desen, RegexOptions.CultureInvariant, ZamanAsimi);
                }
                catch (ArgumentException)
                {
                    // Bozuk desen kaydı tüm ayrıştırmayı düşürmesin.
                    regex = null;
                }

                Onbellek[desen] = regex;
                return regex;
            }
        }
    }
}
