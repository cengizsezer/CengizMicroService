using System.Text;
using WebApp.Domain.Models.FirmaKontrol;
using WebApp.Shared.Dto.FirmaKontrol;

namespace WebApp.Application.Services
{
    /// <summary>
    /// Etiket kural motoru ve ÇİFT SAYIM KORUMASI — PROMPT 3A madde 2 ve 3'ün tek uygulaması.
    ///
    /// Motor tanımdan (boyut/değer/kural) türetilmiş sonucu ÜRETİR, saklamaz; hiçbir yerde
    /// kalıcı hâle getirilmez. Bilanço, GelirTablosuCalculator, Vergi, Dikey Yüzdeler ve
    /// Finansal Oranlar bu sınıfı ÇAĞIRMAZ — etiketler hiçbir resmi hesaba girmez.
    /// Tek tüketicisi ekrandır: Etiketler sekmesi ve (3B) <see cref="GrupGorunumu"/> üzerinden
    /// yönetsel görünüm / grup matrisi.
    /// </summary>
    public static class EtiketMotoru
    {
        /// <summary>Denklik toleransı (kuruş).</summary>
        public const decimal Tolerans = 0.01m;

        /// <summary>Bir düğüme düşen etiket: eşleşen kural + değeri.</summary>
        public sealed record DugumEtiketi(string Kod, long KuralId, int DegerId);

        /// <summary>
        /// Bir ana hesap ağacının etiket dağılımı.
        /// <c>DegerToplamlari</c> + <c>Atanmamis</c> = ana hesabın bakiyesi (HER ZAMAN).
        /// </summary>
        public sealed class Dagilim
        {
            public Dictionary<int, decimal> DegerToplamlari { get; } = new();
            public decimal Atanmamis { get; set; }

            /// <summary>Kod → düşen etiket. Yalnızca kural eşleşen düğümler yer alır.</summary>
            public Dictionary<string, DugumEtiketi> DugumEtiketleri { get; } =
                new(StringComparer.OrdinalIgnoreCase);

            public Dictionary<long, decimal> KuralTutarlari { get; } = new();
            public Dictionary<long, int> KuralHesapSayilari { get; } = new();

            /// <summary>Etiketsiz kalan, bakiyesi olan düğümler (ekranda amber listelenir).</summary>
            public List<HesapDugumu> EtiketsizDugumler { get; } = new();

            public decimal EtiketliToplam => DegerToplamlari.Values.Sum();
            public decimal GenelToplam => EtiketliToplam + Atanmamis;
        }

        // ── Madde 2: kural motoru ──

        /// <summary>
        /// Düğüme uyan İLK kuralı döner. Kurallar Sira'ya (eşitse Id'ye) göre gezilir,
        /// ilk eşleşen kazanır ve devam edilmez. Hiçbiri uymazsa null.
        /// </summary>
        public static EtiketKuraliDto? Eslesen(HesapDugumu dugum, IEnumerable<EtiketKuraliDto> kurallar)
        {
            foreach (var kural in kurallar.OrderBy(k => k.Sira).ThenBy(k => k.Id))
            {
                if (Uyuyor(dugum, kural)) return kural;
            }
            return null;
        }

        public static bool Uyuyor(HesapDugumu dugum, EtiketKuraliDto kural) => kural.EslesmeTipi switch
        {
            EtiketEslesmeTipi.KodDeseni => KodDeseniUyuyor(dugum.Kod, kural.Desen),
            EtiketEslesmeTipi.AdIcerir => AdIceriyor(dugum.Ad, kural.Desen),
            EtiketEslesmeTipi.TekSecim => TamKodUyuyor(dugum.Kod, kural.Desen),
            _ => false
        };

        /// <summary>
        /// Sondaki <c>*</c> jokerdir: "600 1 21*" hem "600 1 21"i hem de ALTINDAKİ tüm
        /// kodları eşler. Joker yoksa tam eşleşme aranır.
        ///
        /// Alt kod kontrolü boşluk sınırına bakar: "600 1 2" öneki "600 1 21"i YANLIŞLIKLA
        /// eşlemesin diye, önekin ardından ya dizge biter ya da bir boşluk gelir.
        /// </summary>
        public static bool KodDeseniUyuyor(string? kod, string? desen)
        {
            var k = (kod ?? string.Empty).Trim();
            var d = (desen ?? string.Empty).Trim();
            if (k.Length == 0 || d.Length == 0) return false;

            if (!d.EndsWith('*')) return TamKodUyuyor(k, d);

            var onek = d[..^1].TrimEnd();
            if (onek.Length == 0) return true;

            if (!k.StartsWith(onek, StringComparison.OrdinalIgnoreCase)) return false;

            return k.Length == onek.Length || k[onek.Length] == ' ';
        }

        public static bool TamKodUyuyor(string? kod, string? desen) =>
            string.Equals((kod ?? string.Empty).Trim(), (desen ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);

        /// <summary>Hesap adında arama; büyük/küçük harf ve Türkçe karakter duyarsız.</summary>
        public static bool AdIceriyor(string? ad, string? aranan)
        {
            var a = Sadelestir(aranan);
            if (a.Length == 0) return false;

            return Sadelestir(ad).Contains(a, StringComparison.Ordinal);
        }

        /// <summary>i/I/ı/İ → i, ş → s, ğ → g, ü → u, ö → o, ç → c; gerisi küçük harf.</summary>
        public static string Sadelestir(string? metin)
        {
            if (string.IsNullOrEmpty(metin)) return string.Empty;

            var sb = new StringBuilder(metin.Length);
            foreach (var ch in metin)
            {
                sb.Append(ch switch
                {
                    'ı' or 'I' or 'İ' or 'i' => 'i',
                    'ş' or 'Ş' => 's',
                    'ğ' or 'Ğ' => 'g',
                    'ü' or 'Ü' => 'u',
                    'ö' or 'Ö' => 'o',
                    'ç' or 'Ç' => 'c',
                    _ => char.ToLowerInvariant(ch)
                });
            }
            return sb.ToString();
        }

        // ── Kapsam ──

        /// <summary>
        /// Boyutun kapsam tanımını ("600,601,610" ya da "6*") ana hesap kodu desenlerine ayırır.
        /// </summary>
        public static string[] KapsamDesenleri(string? kapsamHesaplari) =>
            (kapsamHesaplari ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        /// <summary>
        /// Düğüm boyutun kapsamında mı. Eşleşme düğümün ANA HESAP koduna bakar: "6*" → 600,
        /// 601, 610; "600" → yalnızca 600 ağacı. Kapsam tanımsızsa hiçbir düğüm kapsamda
        /// sayılmaz — payda yanlışlıkla bütün gelir tablosunun neti olmasın diye.
        /// </summary>
        public static bool KapsamdaMi(HesapDugumu dugum, string[] desenler)
        {
            if (desenler.Length == 0) return false;

            var ana = dugum.AnaHesapKodu ?? string.Empty;

            foreach (var d in desenler)
            {
                if (d.EndsWith('*'))
                {
                    var onek = d[..^1];
                    if (onek.Length == 0 || ana.StartsWith(onek, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                else if (string.Equals(ana, d, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Ağacı boyutun kapsamına indirger.</summary>
        public static IEnumerable<HesapDugumu> Kapsa(IEnumerable<HesapDugumu> dugumler, string? kapsamHesaplari)
        {
            var desenler = KapsamDesenleri(kapsamHesaplari);
            return dugumler.Where(d => KapsamdaMi(d, desenler));
        }

        // ── Madde 3: çift sayım koruması ──

        /// <summary>
        /// Ağacı ana hesaplardan başlayarak gezer ve etiket dağılımını çıkarır.
        ///
        /// Kural (madde 3):
        ///  • Düğüm etiketliyse TÜM bakiyesi o etikete yazılır ve ALTINA İNİLMEZ.
        ///  • Düğüm etiketsiz ve çocuğu varsa çocuklarına inilir; ayrıca
        ///    (düğümün bakiyesi − çocukların toplamı) farkı varsa "Atanmamış"a yazılır
        ///    (doğrudan üst hesaba yazılmış tutar).
        ///  • Düğüm etiketsiz ve çocuğu yoksa bakiyesi "Atanmamış"a yazılır.
        ///
        /// Sonuç: her etiketin toplamı + Atanmamış = ana hesabın bakiyesi, HER ZAMAN.
        ///
        /// Tutarlar <see cref="HesapDugumSunumu.Sunum"/> ile sunum yönüne çevrilmiş
        /// hâlleriyle toplanır; işaret ana hesap grubuna göre sabit olduğundan denklik
        /// bozulmaz (600 gibi gelir hesapları kullanıcıya pozitif görünür).
        /// </summary>
        public static Dagilim Dagit(
            IEnumerable<HesapDugumu> dugumler,
            IEnumerable<EtiketKuraliDto> kurallar)
        {
            var liste = dugumler?.ToList() ?? new List<HesapDugumu>();
            var kuralListesi = (kurallar ?? Enumerable.Empty<EtiketKuraliDto>())
                .OrderBy(k => k.Sira).ThenBy(k => k.Id).ToList();

            var cocuklar = liste
                .Where(d => d.UstKod is not null)
                .GroupBy(d => d.UstKod!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var dagilim = new Dagilim();

            foreach (var kok in liste.Where(d => d.Seviye == 1))
                Gez(kok, cocuklar, kuralListesi, dagilim);

            return dagilim;
        }

        private static void Gez(
            HesapDugumu dugum,
            Dictionary<string, List<HesapDugumu>> cocuklar,
            List<EtiketKuraliDto> kurallar,
            Dagilim dagilim)
        {
            var tutar = HesapDugumSunumu.Sunum(dugum);
            var kural = Eslesen(dugum, kurallar);

            if (kural is not null)
            {
                // Etiketli: tüm bakiye bu etikete yazılır, ALTINA İNİLMEZ (çift sayım olmasın).
                Ekle(dagilim.DegerToplamlari, kural.DegerId, tutar);
                Ekle(dagilim.KuralTutarlari, kural.Id, tutar);

                dagilim.KuralHesapSayilari[kural.Id] =
                    dagilim.KuralHesapSayilari.GetValueOrDefault(kural.Id) + 1;

                dagilim.DugumEtiketleri[dugum.Kod] = new DugumEtiketi(dugum.Kod, kural.Id, kural.DegerId);
                return;
            }

            var altlar = cocuklar.TryGetValue(dugum.Kod, out var liste) ? liste : new List<HesapDugumu>();

            if (altlar.Count == 0)
            {
                dagilim.Atanmamis += tutar;
                if (tutar != 0m) dagilim.EtiketsizDugumler.Add(dugum);
                return;
            }

            foreach (var c in altlar)
                Gez(c, cocuklar, kurallar, dagilim);

            // Doğrudan üst hesaba yazılmış, çocuklarda görünmeyen tutar.
            var fark = tutar - altlar.Sum(HesapDugumSunumu.Sunum);
            if (fark != 0m)
            {
                dagilim.Atanmamis += fark;
                dagilim.EtiketsizDugumler.Add(dugum);
            }
        }

        private static void Ekle<TAnahtar>(Dictionary<TAnahtar, decimal> sozluk, TAnahtar anahtar, decimal tutar)
            where TAnahtar : notnull
            => sozluk[anahtar] = sozluk.GetValueOrDefault(anahtar) + tutar;
    }
}
