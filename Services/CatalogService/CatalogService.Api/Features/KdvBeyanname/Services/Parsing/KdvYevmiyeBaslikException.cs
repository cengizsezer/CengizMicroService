namespace CatalogService.Api.Features.KdvBeyanname.Services.Parsing
{
    /// <summary>
    /// Yevmiye Excel'inde bir veya daha fazla zorunlu sütun başlığı bulunamadığında
    /// fırlatılır. Generic mesajın yanında, kullanıcıya "ne verdim / ne bekleniyordu"
    /// karşılaştırmasını gösterebilmek için BULUNAN başlıkları ve EKSİK zorunlu
    /// kolonları taşır. <see cref="InvalidOperationException"/>'tan türer; böylece
    /// bu exception'ı özel olarak yakalamayan mevcut akışlar eskisi gibi çalışır.
    /// </summary>
    public class KdvYevmiyeBaslikException : InvalidOperationException
    {
        /// <summary>Excel başlık satırında fiilen bulunan başlıklar (trim'li).</summary>
        public IReadOnlyList<string> BulunanBasliklar { get; }

        /// <summary>Eşleşmesi gereken ama bulunamayan zorunlu kolonlar.</summary>
        public IReadOnlyList<KdvYevmiyeColumn> EksikZorunluKolonlar { get; }

        public KdvYevmiyeBaslikException(
            string message,
            IReadOnlyList<string> bulunanBasliklar,
            IReadOnlyList<KdvYevmiyeColumn> eksikZorunluKolonlar)
            : base(message)
        {
            BulunanBasliklar = bulunanBasliklar;
            EksikZorunluKolonlar = eksikZorunluKolonlar;
        }
    }
}
