namespace CatalogService.Api.Features.MevzuatGrafigi.Domain
{
    /// <summary>
    /// Kartların yerleştirildiği bir görünüm (ör. "İhracat &amp; İthalat"). Tuval kart sahibi
    /// değildir; yalnız hangi kartın nerede durduğunu tutar.
    /// </summary>
    public class MevzuatTuval
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;

        /// <summary>Sekme şeridindeki sıra.</summary>
        public int Sira { get; set; }

        public ICollection<MevzuatTuvalKarti> Kartlar { get; set; } = new List<MevzuatTuvalKarti>();
    }

    /// <summary>
    /// Bir kartın bir tuvaldeki yerleşimi. (TuvalId, KartId) birincil anahtardır.
    /// Tuvalden kaldırmak yalnız bu satırı siler; kart kütüphanede ve diğer tuvallerde kalır.
    /// </summary>
    public class MevzuatTuvalKarti
    {
        public int TuvalId { get; set; }
        public int KartId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }
}
