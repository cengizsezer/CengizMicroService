namespace CatalogService.Api.Features.Declarations.Services
{
    /// <summary>
    /// Beyanname modülünün iş kuralı ihlalleri (PDF olmayan dosya, ödenmemiş kayda dekont
    /// eklenmesi vb.). Controller bunu 400 + <c>{ field, message }</c> gövdesine çevirir;
    /// <c>BankaEkstreKuralException</c> ve <c>MuhasebeKuralException</c> ile aynı sözleşme.
    /// </summary>
    public class BeyannameKuralException : Exception
    {
        public string Field { get; }

        public BeyannameKuralException(string field, string message) : base(message)
        {
            Field = field;
        }
    }
}
