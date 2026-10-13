using System.Globalization;
using System.Text;

namespace WebApp.Application.Services
{
    /// <summary>
    /// Mizan Excel'inin başlık satırından bulunan sütun yerleri (0 tabanlı). ExcelMizanParser
    /// sütunları SIRAYLA değil bu eşlemeyle okur; ORKA'nın döviz sütunlu ve dövizsiz
    /// biçimlerinde aynı hesap aynı değere varır.
    /// </summary>
    public sealed class MizanSutunlari
    {
        public int Kod { get; init; }

        /// <summary>Hesap adı; yalnız <see cref="Basit"/> biçimde yoktur.</summary>
        public int? Ad { get; init; }

        /// <summary>TL borç bakiye ("TL Borç Bakiye", yoksa "Borç Bakiye"). Basit biçimde yok.</summary>
        public int? BorcBakiye { get; init; }

        public int? AlacakBakiye { get; init; }

        /// <summary>Borç/alacak toplam (hareket) sütunları; döviz sütunlu biçimde yoktur.</summary>
        public int? BorcToplam { get; init; }

        public int? AlacakToplam { get; init; }

        /// <summary>
        /// Basit biçim: başlık satırında yalnız "Hesap Kodu" ve "Bakiye" var; bakiye o sütundan
        /// okunur. İlk sürümün iki sütunlu deneme biçiminden kalma — gerçek bir ORKA çıktısı
        /// değil, kaldırılmadı.
        /// </summary>
        public int? BasitBakiye { get; init; }

        public bool Basit => BasitBakiye is not null;
    }

    /// <summary>
    /// Mizan başlıklarını adla bulur. Karşılaştırma büyük/küçük harf, Türkçe karakter
    /// (İ/ı/ş/ğ/ü/ö/ç), fazla boşluk ve noktalama farkına duyarsızdır; aday adlar TAM eşleşir
    /// ("Döviz Borç Bakiye" hiçbir zaman "Borç Bakiye" sayılmaz).
    ///
    /// Yaklaşım sunucudaki KDV mizan okuyucusunun HeaderMap'iyle aynı (aday listesi, ilk
    /// eşleşen kazanır); kod paylaşılmaz, istemci kendi kopyasını tutar.
    /// </summary>
    public static class MizanBasliklari
    {
        /// <summary>Başlık satırı bu kadar satır içinde aranır.</summary>
        public const int BaslikArama = 10;

        private static readonly string[] KodAdlari = { "Hesap Kodu" };
        private static readonly string[] AdAdlari = { "Hesap Adı" };

        // TL önce; TL'siz ad yalnız TL sütunu yoksa. Döviz sütunlarının adı bunların
        // hiçbiriyle tam eşleşmez.
        private static readonly string[] BorcBakiyeAdlari =
        {
            "TL Borç Bakiye", "TL Borç Bakiyesi", "Borç Bakiye TL", "Borç Bakiyesi TL",
            "Borç Bakiye", "Borç Bakiyesi"
        };

        private static readonly string[] AlacakBakiyeAdlari =
        {
            "TL Alacak Bakiye", "TL Alacak Bakiyesi", "Alacak Bakiye TL", "Alacak Bakiyesi TL",
            "Alacak Bakiye", "Alacak Bakiyesi"
        };

        private static readonly string[] BorcToplamAdlari =
        {
            "TL Borç Toplam", "TL Borç Toplamı", "Borç Toplam", "Borç Toplamı", "TL Borç", "Borç"
        };

        private static readonly string[] AlacakToplamAdlari =
        {
            "TL Alacak Toplam", "TL Alacak Toplamı", "Alacak Toplam", "Alacak Toplamı", "TL Alacak", "Alacak"
        };

        private static readonly string[] BasitBakiyeAdlari = { "Bakiye", "TL Bakiye" };

        private static readonly string KodAnahtari = Normalize(KodAdlari[0]);

        /// <summary>
        /// "  TL Borç  Bakiyesi: " → "tl borc bakiyesi". Türkçe harfler ASCII karşılığına iner,
        /// harf/rakam dışı her şey tek boşluk olur.
        /// </summary>
        public static string Normalize(string? metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return string.Empty;

            var sb = new StringBuilder(metin.Length);
            var bosluk = false;
            foreach (var c in metin.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;

                var h = c switch
                {
                    'İ' or 'I' or 'ı' => 'i',
                    'Ş' or 'ş' => 's',
                    'Ğ' or 'ğ' => 'g',
                    'Ü' or 'ü' => 'u',
                    'Ö' or 'ö' => 'o',
                    'Ç' or 'ç' => 'c',
                    _ => char.ToLowerInvariant(c)
                };

                if (char.IsLetterOrDigit(h))
                {
                    if (bosluk && sb.Length > 0) sb.Append(' ');
                    sb.Append(h);
                    bosluk = false;
                }
                else
                {
                    bosluk = true;
                }
            }
            return sb.ToString();
        }

        /// <summary>Satır başlık satırı mı: bir hücresinde "Hesap Kodu" geçiyor.</summary>
        public static bool BaslikSatiriMi(IReadOnlyList<string?> hucreler) =>
            hucreler.Any(h => Normalize(h).Contains(KodAnahtari, StringComparison.Ordinal));

        /// <summary>
        /// Başlık satırından sütunları bulur. Zorunlu başlıklardan (Hesap Kodu, Hesap Adı,
        /// borç bakiye, alacak bakiye) biri yoksa null döner ve <paramref name="hata"/> eksik
        /// olanı ve bulunan başlıkları söyler — sabit sıraya düşülmez.
        /// </summary>
        public static MizanSutunlari? Esle(IReadOnlyList<string?> hucreler, out string? hata)
        {
            hata = null;

            // İlk görülen kazanır (sunucudaki HeaderMap gibi).
            var harita = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < hucreler.Count; i++)
            {
                var n = Normalize(hucreler[i]);
                if (n.Length > 0) harita.TryAdd(n, i);
            }

            int? Bul(string[] adlar)
            {
                foreach (var ad in adlar)
                    if (harita.TryGetValue(Normalize(ad), out var i)) return i;
                return null;
            }

            // "Hesap Kodu" satırı bulduran hücre; tam ad yoksa içeren ilk hücre.
            var kod = Bul(KodAdlari) ?? Enumerable.Range(0, hucreler.Count)
                .Cast<int?>()
                .FirstOrDefault(i => Normalize(hucreler[i!.Value]).Contains(KodAnahtari, StringComparison.Ordinal));

            var ad = Bul(AdAdlari);
            var borc = Bul(BorcBakiyeAdlari);
            var alacak = Bul(AlacakBakiyeAdlari);

            // Basit biçim: yalnız iki başlık, "Hesap Kodu" + "Bakiye".
            var dolu = harita.Count;
            var basit = Bul(BasitBakiyeAdlari);
            if (kod is not null && basit is not null && dolu == 2 && ad is null && borc is null && alacak is null)
                return new MizanSutunlari { Kod = kod.Value, BasitBakiye = basit };

            var eksik = new List<string>();
            if (kod is null) eksik.Add("Hesap Kodu");
            if (ad is null) eksik.Add("Hesap Adı");
            if (borc is null) eksik.Add("Borç bakiye (TL Borç Bakiye / Borç Bakiye)");
            if (alacak is null) eksik.Add("Alacak bakiye (TL Alacak Bakiye / Alacak Bakiye)");

            if (eksik.Count > 0)
            {
                var bulunan = Enumerable.Range(0, hucreler.Count)
                    .Where(i => !string.IsNullOrWhiteSpace(hucreler[i]))
                    .Select(i => $"{SutunHarfi(i)}: {hucreler[i]!.Trim()}")
                    .ToList();
                hata = $"Mizan başlığı eksik: {string.Join(", ", eksik)}. " +
                       $"Bulunan başlıklar: {(bulunan.Count == 0 ? "(yok)" : string.Join(" · ", bulunan))}.";
                return null;
            }

            return new MizanSutunlari
            {
                Kod = kod!.Value,
                Ad = ad,
                BorcBakiye = borc,
                AlacakBakiye = alacak,
                BorcToplam = Bul(BorcToplamAdlari),
                AlacakToplam = Bul(AlacakToplamAdlari)
            };
        }

        /// <summary>0 → "A", 25 → "Z", 26 → "AA".</summary>
        public static string SutunHarfi(int i)
        {
            var s = string.Empty;
            for (i++; i > 0; i = (i - 1) / 26)
                s = (char)('A' + (i - 1) % 26) + s;
            return s;
        }
    }
}
