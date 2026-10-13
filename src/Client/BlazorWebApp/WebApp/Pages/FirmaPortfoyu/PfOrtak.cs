using System.Globalization;
using Radzen;

namespace WebApp.Pages.FirmaPortfoyu
{
    /// <summary>Portföy seçicilerinin firma seçeneği (yalnız aktif firmalar — sunucu süzer).</summary>
    public sealed record PfFirmaSecenegi(int Id, string Ad);

    /// <summary>Türkçe ay adları.</summary>
    public static class PfAy
    {
        private static readonly string[] Uzunlar =
            { "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };

        private static readonly string[] Kisalar =
            { "Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara" };

        public static string Uzun(int ay) => ay is >= 1 and <= 12 ? Uzunlar[ay - 1] : ay.ToString();
        public static string Kisa(int ay) => ay is >= 1 and <= 12 ? Kisalar[ay - 1] : ay.ToString();
    }

    /// <summary>Değişim yönünün rengi: iyi (yeşil), kötü (kırmızı), yatay / karşılaştırma yok (gri).</summary>
    public enum PfYon
    {
        Yatay,
        Iyi,
        Kotu
    }

    /// <summary>
    /// Portföy ekranının sayı biçimleri ve değişim kuralı (taslaktaki tl / delta). Değişim rengi:
    /// <paramref name="artisIyi"/> true ise artış yeşil (gelir, kâr, hazır değer), false ise artış kırmızı
    /// (gider, alacak, borç). Karşılaştırma yoksa ya da sıfırsa "—".
    /// </summary>
    public static class PfBicim
    {
        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

        /// <summary>1.234.567 → "1,23 Mn ₺"; 12.345 → "12 bin ₺"; null → "—".</summary>
        public static string Tl(decimal? v)
        {
            if (v is not { } d) return "—";
            var a = Math.Abs(d);
            var s = d < 0 ? "−" : "";
            if (a >= 1_000_000m) return s + (a / 1_000_000m).ToString("N2", Tr) + " Mn ₺";
            if (a >= 1_000m) return s + Math.Round(a / 1_000m).ToString("N0", Tr) + " bin ₺";
            return s + Math.Round(a).ToString("N0", Tr) + " ₺";
        }

        /// <summary>Tam tutar (ipucu için): "1.234.567,89 ₺".</summary>
        public static string TamTl(decimal? v) => v is { } d ? d.ToString("N2", Tr) + " ₺" : "—";

        public static string Yuzde(decimal? oran) => oran is { } o ? "%" + (o * 100m).ToString("N1", Tr) : "—";

        /// <summary>(metin, yön): "▲12,3%" / "▼4,0%" / "≈0,2%" ya da "—".</summary>
        public static (string Metin, PfYon Yon) Degisim(decimal? bu, decimal? onceki, bool artisIyi)
        {
            if (bu is not { } b || onceki is not { } o || o == 0m) return ("—", PfYon.Yatay);
            var x = (b - o) / Math.Abs(o);
            var isaret = Math.Abs(x) < 0.005m ? "≈" : x > 0 ? "▲" : "▼";
            var metin = isaret + Math.Abs(x * 100m).ToString("N1", Tr) + "%";
            if (Math.Abs(x) < 0.005m) return (metin, PfYon.Yatay);
            return (metin, (x > 0) == artisIyi ? PfYon.Iyi : PfYon.Kotu);
        }

        /// <summary>"2026-09" → "Eylül 2026".</summary>
        public static string DonemAdi(string? donem)
        {
            if (string.IsNullOrEmpty(donem)) return "—";
            var p = donem.Split('-');
            return p.Length == 2 && int.TryParse(p[0], out var y) && int.TryParse(p[1], out var a) ? $"{PfAy.Uzun(a)} {y}" : donem;
        }

        public static string Donem(int yil, int ay) => $"{yil:D4}-{ay:D2}";

        /// <summary>Firma rozeti: iki harf ve Id'den sabit renk.</summary>
        public static (string Harfler, string Renk) Rozet(int firmaId, string ad)
        {
            string[] renkler = { "#2f8f8a", "#3e7bd0", "#3e9b5f", "#c2702a", "#8a4fd0", "#c0475a", "#5a6acf", "#a0782a" };
            var temiz = new string((ad ?? "").Where(char.IsLetterOrDigit).ToArray());
            var harfler = temiz.Length >= 2 ? temiz[..2] : temiz.PadRight(2, '·');
            return (harfler.ToUpper(Tr), renkler[Math.Abs(firmaId) % renkler.Length]);
        }
    }

    /// <summary>
    /// Portföy pencerelerinin Radzen seçenekleri: başlık çubuğu yok (pencere kendi .pf-dlg-ust'unu çizer), iç boşluk
    /// yok, köşeler yuvarlak (Firma Panosu'nun CkPencere'siyle aynı).
    /// </summary>
    public static class PfPencere
    {
        public static DialogOptions Secenekler(string genislik) => new()
        {
            ShowTitle = false,
            Width = genislik,
            Style = "--rz-dialog-content-padding:0; padding:0; border-radius:14px; overflow:hidden",
            CloseDialogOnOverlayClick = false,
            CloseDialogOnEsc = true
        };
    }
}
