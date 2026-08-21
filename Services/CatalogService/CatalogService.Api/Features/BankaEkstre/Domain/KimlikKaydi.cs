namespace CatalogService.Api.Features.BankaEkstre.Domain
{
    /// <summary>
    /// Karşı tarafın kimliği — **global**, firmadan bağımsız.
    ///
    /// Bir unvanın kim olduğu her firmada aynıdır; hangi hesap koduna gittiği firmaya
    /// özeldir (<see cref="HesapEslesmesi"/>). Aday'da öğrenilen bir unvan SMMM'de
    /// karşımıza çıktığında kimlik hazır olur, yalnız yerel kod eşlenir.
    /// </summary>
    public class KimlikKaydi
    {
        public int Id { get; set; }

        /// <summary>
        /// Normalize unvan çekirdeği ("DAGI GIYIM"), IBAN (yalnız rakam) veya VKN.
        /// Unvan çıkarılamayan satırlarda "ISLEM:&lt;normalize işlem tipi&gt;".
        /// </summary>
        public string Anahtar { get; set; } = string.Empty;

        public AnahtarTipi AnahtarTipi { get; set; } = AnahtarTipi.UnvanCekirdek;

        /// <summary>Anahtarın türetildiği son normalize unvan; insan okuru ve arama için.</summary>
        public string? NormalizeUnvan { get; set; }

        /// <summary>Bu kimliğin kaç onayda görüldüğü (tüm firmalar toplamı).</summary>
        public int KullanimSayisi { get; set; } = 1;

        public DateTime SonKullanim { get; set; }
    }
}
