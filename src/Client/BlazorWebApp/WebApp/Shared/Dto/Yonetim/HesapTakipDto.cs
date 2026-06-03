namespace WebApp.Shared.Dto.Yonetim
{
    public class HesapTakipDto
    {
        public int HesapId { get; set; }
        public string Ad { get; set; } = string.Empty;
        public HesapTip Tip { get; set; }
        public Siklik Siklik { get; set; }
        public int FirmaId { get; set; }
        public string FirmaAd { get; set; } = string.Empty;

        // Bu ay içinde işlendi olarak işaretli günler.
        public List<DateTime> IslenenGunler { get; set; } = new();

        // Bu hesabın toplam not sayısı (tüm kapsamlar).
        public int NotSayisi { get; set; }

        // Bu ay içinde gün-notu (Kapsam=Gun) olan tarihler.
        public List<DateTime> NotluGunler { get; set; } = new();
    }
}
