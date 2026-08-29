using System.Text;
using CatalogService.Api.Features.Declarations.Entities;

namespace CatalogService.Api.Features.Declarations.Services
{
    /// <summary>
    /// <see cref="Declaration.DeclarationType"/> metnini tanım tablosundaki bir
    /// <see cref="BeyannameTuru"/> ile eşleştirir.
    ///
    /// <b>Neden düz eşitlik yetmiyor?</b> Tür bugüne kadar ekrandaki sabit listeden
    /// seçilen serbest metindi; kayıtlarda "0015 KDV-1", "0015 KDV1", "KDV-1" gibi
    /// yazımlar bir arada olabiliyor. Kolon bulunamayan kayıt matriste hiç görünmez ve
    /// kullanıcı eksiği fark etmez; bu yüzden eşleştirme üç adımlı:
    /// <list type="number">
    /// <item>Saklanan değerin tamamı (normalize edilmiş) — asıl yol.</item>
    /// <item>Baştaki <b>vergi kodu</b> (4 hane): "0015 …" → <c>0015</c>.</item>
    /// <item>Okunur ad.</item>
    /// </list>
    /// Hiçbiri tutmazsa null döner ve çağıran metni "eşleşmeyen" olarak <b>raporlar</b>.
    ///
    /// Karşılaştırma Türkçe sadeleştirmeden geçer: invariant kültür 'ı' → 'I' ve
    /// 'i' → 'İ' dönüşümünü yapmadığı için "KDV1" ile "kdv1" bile
    /// <c>OrdinalIgnoreCase</c> altında ayrışabiliyor (aynı tuzak Banka Otomasyon'da
    /// başlık aramasını bozmuştu).
    /// </summary>
    public static class BeyannameTuruEsleyici
    {
        public static BeyannameTuru? Esle(IReadOnlyList<BeyannameTuru> turler, string? declarationType)
        {
            if (turler.Count == 0) return null;

            var hedef = Normalize(declarationType);
            if (hedef.Length == 0) return null;

            foreach (var tur in turler)
                if (Normalize(tur.Deger) == hedef) return tur;

            var kod = KodOku(hedef);
            if (kod is not null)
                foreach (var tur in turler)
                    if (!string.IsNullOrWhiteSpace(tur.Kod) && Normalize(tur.Kod) == kod) return tur;

            foreach (var tur in turler)
                if (Normalize(tur.Ad) == hedef) return tur;

            return null;
        }

        /// <summary>Metnin başındaki dört haneli vergi kodu; yoksa null.</summary>
        private static string? KodOku(string normalizeMetin)
        {
            var i = 0;
            while (i < normalizeMetin.Length && char.IsDigit(normalizeMetin[i])) i++;

            return i == 4 ? normalizeMetin[..4] : null;
        }

        /// <summary>
        /// Türkçe sadeleştirilmiş, büyük harfli, alfanümerik dışı karakterleri tek boşluğa
        /// indirgenmiş hâl. "0015 KDV-1" ve "0015  kdv 1" aynı metne iner.
        /// </summary>
        internal static string Normalize(string? metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return string.Empty;

            var sb = new StringBuilder(metin.Length);
            var oncekiBosluk = true;

            foreach (var ch in metin)
            {
                var buyuk = ch switch
                {
                    'İ' or 'ı' or 'i' or 'I' => 'I',
                    'Ş' or 'ş' => 'S',
                    'Ğ' or 'ğ' => 'G',
                    'Ü' or 'ü' => 'U',
                    'Ö' or 'ö' => 'O',
                    'Ç' or 'ç' => 'C',
                    _ => char.ToUpperInvariant(ch)
                };

                if (char.IsLetterOrDigit(buyuk))
                {
                    sb.Append(buyuk);
                    oncekiBosluk = false;
                }
                else if (!oncekiBosluk)
                {
                    sb.Append(' ');
                    oncekiBosluk = true;
                }
            }

            return sb.ToString().Trim();
        }
    }
}
