namespace CatalogService.Api.Features.FinansmanGiderKisitlamasi.Services
{
    /// <summary>
    /// Seçilen yıl için kısıtlama oranı tanımlı değil. Oran mevzuattan geliyor ve
    /// varsayılanı yok: eksikse sessizce %10 varsaymak yerine hesap durduruluyor,
    /// yanlış bir KKEG üretmektense kullanıcı oranı tanımlasın (bkz. KARARLAR §80).
    /// </summary>
    public class FinansmanKisitlamaOraniYokException : Exception
    {
        public int Yil { get; }

        public FinansmanKisitlamaOraniYokException(int yil)
            : base($"{yil} yılı için finansman gider kısıtlaması oranı tanımlı değil. " +
                   "Oranı ekrandaki \"Kısıtlama oranı\" bölümünden tanımlayın.")
        {
            Yil = yil;
        }
    }
}
