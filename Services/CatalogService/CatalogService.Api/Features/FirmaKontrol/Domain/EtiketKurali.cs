
namespace CatalogService.Api.Features.FirmaKontrol.Domain
{
    /// <summary>
    /// Bir hesap düğümüne hangi etiket değerinin düşeceğini belirleyen kural.
    /// TANIMDIR; uygulandığı sonuç saklanmaz, çalışma zamanında hesaplanır.
    /// Dönemden bağımsızdır — eşleşme hesap koduna/adına bakar.
    /// </summary>
    public class EtiketKurali
    {
        public long Id { get; set; }

        public int BoyutId { get; set; }
        public EtiketBoyutu? Boyut { get; set; }

        public int Sira { get; set; }

        public EtiketEslesmeTipi EslesmeTipi { get; set; }

        /// <summary>Eşleşme deseni: kod deseni ("600 1 21*"), aranan ad parçası ya da tek kod.</summary>
        public string Desen { get; set; } = string.Empty;

        public int DegerId { get; set; }
        public EtiketDegeri? Deger { get; set; }

        /// <summary>
        /// null = tüm firmalar; dolu ise yalnızca o firmada geçerli.
        ///
        /// Bilerek FK/navigation YOK: kural zaten boyuta (EtiketBoyutu) cascade ile bağlı ve
        /// boyut da Firma'ya cascade ile bağlı. Buraya ikinci bir Firma FK'sı eklenince
        /// Firmalar → EtiketKurallari arasında İKİ cascade yolu oluşuyor ve SQL Server
        /// tabloyu yaratmayı reddediyor (hata 1785: multiple cascade paths). Bu alan
        /// yalnızca süzme anahtarıdır; üzerinde indeks var.
        /// </summary>
        public int? FirmaId { get; set; }
    }
}
