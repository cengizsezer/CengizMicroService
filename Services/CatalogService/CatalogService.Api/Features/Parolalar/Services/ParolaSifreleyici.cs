using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace CatalogService.Api.Features.Parolalar.Services
{
    /// <summary>Parolalar şifre alanlarının şifrelenmesi (Data Protection, purpose "Parolalar.v1").</summary>
    public interface IParolaSifreleyici
    {
        string Sifrele(string duzMetin);

        /// <summary>Çözer; anahtar yoksa / veri bozuksa <see cref="ParolaCozulemediException"/>.</summary>
        string Coz(string sifreli);
    }

    /// <summary>Anahtar halkası değişmiş ya da kaybolmuş: şifre çözülemiyor (500, açık mesaj).</summary>
    public sealed class ParolaCozulemediException : Exception
    {
        public ParolaCozulemediException(Exception ic)
            : base("Şifre çözülemedi. Data Protection anahtarları değişmiş ya da kaybolmuş olabilir (catalog_dp_keys volume'ü).", ic) { }
    }

    public sealed class ParolaSifreleyici : IParolaSifreleyici
    {
        /// <summary>Purpose; değişirse eski kayıtlar çözülemez — yeni sürüm "Parolalar.v2" ile ayrıca eklenir.</summary>
        public const string Amac = "Parolalar.v1";

        private readonly IDataProtector _koruyucu;

        public ParolaSifreleyici(IDataProtectionProvider saglayici) => _koruyucu = saglayici.CreateProtector(Amac);

        public string Sifrele(string duzMetin) => _koruyucu.Protect(duzMetin);

        public string Coz(string sifreli)
        {
            try
            {
                return _koruyucu.Unprotect(sifreli);
            }
            catch (CryptographicException ex)
            {
                throw new ParolaCozulemediException(ex);
            }
        }
    }
}
