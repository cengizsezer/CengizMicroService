namespace CatalogService.Api.Features.Muhasebe.Services
{
    /// <summary>
    /// Muhasebe iş kurallarından biri ihlal edildiğinde fırlatılır.
    /// Mesaj kullanıcıya doğrudan gösterilebilecek şekilde Türkçe ve eyleme dönüktür.
    /// </summary>
    public class MuhasebeKuralException : Exception
    {
        /// <summary>Hatanın ilişkili olduğu form alanı (ör. "segment").</summary>
        public string Field { get; }

        public MuhasebeKuralException(string field, string message) : base(message)
        {
            Field = field;
        }
    }
}
