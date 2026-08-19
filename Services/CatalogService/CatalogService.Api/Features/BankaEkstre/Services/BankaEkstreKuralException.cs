namespace CatalogService.Api.Features.BankaEkstre.Services
{
    /// <summary>
    /// Modülün iş kuralı ihlalleri (eksik satırla dışa aktarım, tanımsız parser vb.).
    /// Controller bunu 400 + <c>{ field, message }</c> gövdesine çevirir; Muhasebe
    /// modülündeki <c>MuhasebeKuralException</c> ile aynı sözleşme.
    /// </summary>
    public class BankaEkstreKuralException : Exception
    {
        public string Field { get; }

        public BankaEkstreKuralException(string field, string message) : base(message)
        {
            Field = field;
        }
    }
}
