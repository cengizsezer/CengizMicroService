using CatalogService.Api.Features.Ajanlar.Domain;

namespace CatalogService.Api.Features.Ajanlar.Services
{
    /// <summary>Kaydın sonucu: yeni kayıt ve varsa onun yerini aldığı eski kayıt.</summary>
    /// <param name="Ajan">Depoya konan kayıt.</param>
    /// <param name="Dusurulen">
    /// Aynı <c>MakineId</c> ile duran eski kayıt. Hub bunun bağlantısını kapatır;
    /// böylece yeniden bağlanma sırasında hayalet kayıt kalmaz.
    /// </param>
    public record AjanKaydetmeSonucu(AjanKaydi Ajan, AjanKaydi? Dusurulen);

    /// <summary>
    /// Bağlı ajanların bellekteki listesi. Singleton ve thread-safe.
    /// </summary>
    public interface IAjanDeposu
    {
        /// <summary>Kaydeder; aynı makinenin eski kaydı varsa onu çıkarıp geri verir.</summary>
        AjanKaydetmeSonucu Kaydet(AjanKaydi ajan);

        /// <summary>
        /// Bağlantı kimliğine göre çıkarır. Kayıt başka bir bağlantıya geçmişse
        /// (makine yeniden bağlandı) dokunmaz — düşürülen eski soketin kopuş
        /// bildirimi, yerine geçen yeni kaydı silmemeli.
        /// </summary>
        AjanKaydi? Cikar(string connectionId);

        /// <summary>Kalp atışını işler; kayıt yoksa false döner.</summary>
        bool KalpAtisi(string connectionId);

        /// <summary>
        /// Zaman aşımına uğramamış kayıtlar. Süresi geçenler bu okuma sırasında
        /// depodan da düşer.
        /// </summary>
        IReadOnlyList<AjanKaydi> Baglilar();
    }
}
