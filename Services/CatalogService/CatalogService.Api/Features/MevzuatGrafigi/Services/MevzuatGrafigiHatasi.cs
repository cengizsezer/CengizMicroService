namespace CatalogService.Api.Features.MevzuatGrafigi.Services
{
    /// <summary>
    /// Servisin kural ihlali (400) ya da gövdede adı geçen kaydın bulunamaması (404) için
    /// fırlattığı hata. Mesaj kullanıcıya olduğu gibi gösterilir; Türkçe yazın.
    /// Rotadaki id bulunamazsa servis hata fırlatmaz, null/false döner.
    /// </summary>
    public sealed class MevzuatGrafigiHatasi : Exception
    {
        public bool Bulunamadi { get; }

        private MevzuatGrafigiHatasi(string mesaj, bool bulunamadi) : base(mesaj) => Bulunamadi = bulunamadi;

        public static MevzuatGrafigiHatasi Kural(string mesaj) => new(mesaj, false);
        public static MevzuatGrafigiHatasi Yok(string mesaj) => new(mesaj, true);
    }
}
