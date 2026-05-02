namespace WebApp.Domain.Models.FirmaKontrol
{
    public class HesapPlani
    {
        public List<MizanSatir> Aktif { get; set; } = new();
        public List<MizanSatir> Pasif { get; set; } = new();
        public List<MizanSatir> GelirTablosu { get; set; } = new();

        public HesapPlani Clone()
        {
            static List<MizanSatir> CloneList(List<MizanSatir> source) =>
                source.Select(s => new MizanSatir
                {
                    Kod = s.Kod,
                    Ad = s.Ad,
                    Tip = s.Tip,
                    OncekiDonem = s.OncekiDonem,
                    CariDonem = s.CariDonem
                }).ToList();

            return new HesapPlani
            {
                Aktif = CloneList(Aktif),
                Pasif = CloneList(Pasif),
                GelirTablosu = CloneList(GelirTablosu)
            };
        }
    }
}
