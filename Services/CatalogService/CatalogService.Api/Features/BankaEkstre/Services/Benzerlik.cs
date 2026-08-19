namespace CatalogService.Api.Features.BankaEkstre.Services
{
    /// <summary>
    /// Unvan benzerliği (Katman 5). Levenshtein tabanlı oran; biri diğerinin ilk 14
    /// karakteriyle başlıyorsa skor 0.95'e yükseltilir (ORKA muavin adları kısaltılmış
    /// yazıldığı için tam eşitlik beklenemiyor).
    /// </summary>
    public static class Benzerlik
    {
        public const int OnekUzunlugu = 14;
        public const decimal OnekSkoru = 0.95m;

        /// <summary>0..1 arası benzerlik oranı. İki normalize unvan bekler.</summary>
        public static decimal Oran(string? a, string? b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0m;
            if (string.Equals(a, b, StringComparison.Ordinal)) return 1m;

            var mesafe = Levenshtein(a, b);
            var enUzun = Math.Max(a.Length, b.Length);
            var oran = 1m - (decimal)mesafe / enUzun;

            if (OnekEsitMi(a, b) && oran < OnekSkoru)
                oran = OnekSkoru;

            return oran < 0m ? 0m : oran;
        }

        /// <summary>Biri diğerinin ilk 14 karakteriyle başlıyor mu?</summary>
        public static bool OnekEsitMi(string a, string b)
        {
            var kisa = Math.Min(a.Length, b.Length);
            if (kisa < OnekUzunlugu) return false;

            return a.AsSpan(0, OnekUzunlugu).SequenceEqual(b.AsSpan(0, OnekUzunlugu));
        }

        /// <summary>İki satır kullanan klasik Levenshtein; unvanlar kısa olduğu için yeterli.</summary>
        public static int Levenshtein(string a, string b)
        {
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;

            var onceki = new int[b.Length + 1];
            var simdiki = new int[b.Length + 1];

            for (var j = 0; j <= b.Length; j++) onceki[j] = j;

            for (var i = 1; i <= a.Length; i++)
            {
                simdiki[0] = i;
                for (var j = 1; j <= b.Length; j++)
                {
                    var bedel = a[i - 1] == b[j - 1] ? 0 : 1;
                    simdiki[j] = Math.Min(
                        Math.Min(simdiki[j - 1] + 1, onceki[j] + 1),
                        onceki[j - 1] + bedel);
                }
                (onceki, simdiki) = (simdiki, onceki);
            }

            return onceki[b.Length];
        }
    }
}
