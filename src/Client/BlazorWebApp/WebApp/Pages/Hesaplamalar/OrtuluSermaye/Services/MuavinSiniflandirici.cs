using System.Globalization;
using System.Text.RegularExpressions;
using WebApp.Pages.Hesaplamalar.OrtuluSermaye.Model;

namespace WebApp.Pages.Hesaplamalar.OrtuluSermaye.Services
{
    /// <summary>
    /// Gider/gelir muavini satırına hesap kodu ve açıklamadan ilk sınıfı verir. Kullanıcı
    /// ekranda her satırı değiştirebilir; burası yalnız varsayılanı üretir.
    /// <list type="bullet">
    /// <item>656 → kur farkı gideri; 646 → kur farkı geliri.</item>
    /// <item>780 / 661 / 770 → faiz gideri; açıklamada "damga" ya da ayrı kelime "DV" varsa damga vergisi.</item>
    /// <item>191 → faiz KDV.</item>
    /// <item>360 → açıklamaya göre stopaj / faiz KDV / damga vergisi; eşleşmezse sınıfsız.</item>
    /// </list>
    /// </summary>
    public static class MuavinSiniflandirici
    {
        private static readonly CultureInfo Tr = new("tr-TR");

        private static readonly Regex Damga = new(@"damga|\bdv\b", RegexOptions.Compiled);
        private static readonly Regex StopajDeseni = new(@"stopaj|muhtasar|kar pay|kâr pay", RegexOptions.Compiled);
        private static readonly Regex Kdv = new(@"kdv", RegexOptions.Compiled);

        public static MuavinSinifi Sinifla(string? hesapKodu, string? aciklama)
        {
            var kod = (hesapKodu ?? "").Trim();
            var a = (aciklama ?? "").ToLower(Tr);

            if (kod.StartsWith("656")) return MuavinSinifi.KurFarkiGideri;
            if (kod.StartsWith("646")) return MuavinSinifi.KurFarkiGeliri;
            if (kod.StartsWith("780") || kod.StartsWith("661") || kod.StartsWith("770"))
                return Damga.IsMatch(a) ? MuavinSinifi.DamgaVergisi : MuavinSinifi.FaizGideri;
            if (kod.StartsWith("191")) return MuavinSinifi.FaizKdv;
            if (kod.StartsWith("360"))
            {
                if (StopajDeseni.IsMatch(a)) return MuavinSinifi.Stopaj;
                if (Kdv.IsMatch(a)) return MuavinSinifi.FaizKdv;
                if (Damga.IsMatch(a)) return MuavinSinifi.DamgaVergisi;
            }
            return MuavinSinifi.Yok;
        }
    }
}
