namespace WebApp.Application.RuleEngine
{
    public class MizanEsikler
    {
        public decimal KasaLimiti { get; set; } = 50_000m;

        public decimal AdatFaizOrani { get; set; } = 0.42m;

        public decimal NakitOdemeLimiti { get; set; } = 30_000m;

        public static MizanEsikler Default() => new();
    }
}
