namespace WebApp.Domain.Models.FirmaKontrol
{
    public class Firma
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string VergiNo { get; set; } = string.Empty;
        public string Sektor { get; set; } = string.Empty;
        public string Donem { get; set; } = string.Empty;
        public string LogoText { get; set; } = string.Empty;
        public HesapPlani Mizan { get; set; } = new();
        public VergiHesaplama VergiBilgisiCariDonem { get; set; } = new();
    }
}
