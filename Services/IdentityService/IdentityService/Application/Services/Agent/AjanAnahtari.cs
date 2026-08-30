using System;
using System.Security.Cryptography;

namespace IdentityService.Application.Services.Agent
{
    /// <summary>
    /// Ajan anahtarının biçimi ve üretimi.
    ///
    /// Önek (<c>pkfr_</c>) bilerek var: anahtar bir yapılandırma dosyasına ya da
    /// bir sohbete yapıştırıldığında ne olduğu okunsun, sızdığında aranabilsin.
    /// </summary>
    public static class AjanAnahtari
    {
        public const string OnEk = "pkfr_";

        /// <summary>Listede ve aday aramasında kullanılan önek uzunluğu.</summary>
        public const int OnEkUzunlugu = 8;

        /// <summary>Rastgele kısmın bayt uzunluğu (256 bit).</summary>
        private const int BaytSayisi = 32;

        /// <summary>
        /// Kriptografik olarak güvenli yeni anahtar. Base64'ün URL'e uygun hâli
        /// kullanılıyor: anahtar yapılandırma dosyasına, komut satırına ve gerekirse
        /// bir adrese kaçış gerektirmeden girsin.
        /// </summary>
        public static string Uret()
        {
            var bayt = RandomNumberGenerator.GetBytes(BaytSayisi);
            var govde = Convert.ToBase64String(bayt)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');

            return OnEk + govde;
        }

        /// <summary>Anahtarın listede gösterilen ve aday aramada kullanılan öneki.</summary>
        public static string OnEkiCikar(string anahtar)
        {
            if (string.IsNullOrEmpty(anahtar)) return string.Empty;
            return anahtar.Length <= OnEkUzunlugu ? anahtar : anahtar[..OnEkUzunlugu];
        }
    }
}
