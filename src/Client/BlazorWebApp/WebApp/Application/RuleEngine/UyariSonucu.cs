namespace WebApp.Application.RuleEngine
{
    public record UyariSonucu(
        UyariSeviyesi Seviye,
        string HesapKodu,
        string Baslik,
        string Aciklama,
        string CozumOnerisi,
        string KuralKodu);
}
