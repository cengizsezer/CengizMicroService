namespace WebApp.Pages.Hesaplamalar.FinansmanGiderKisitlamasi.Model
{
    /// <summary>Ekrandaki dört giriş alanı; sunucuya gönderilen gövde.</summary>
    public class FinansmanKisitlamaHesapRequest
    {
        public int Yil { get; set; }
        public decimal? Ozsermaye { get; set; }
        public decimal YabanciKaynakToplami { get; set; }
        public decimal FinansmanGideri { get; set; }
        public decimal OrtuluSermayeVeFinansmanGeliri { get; set; }
    }

    /// <summary>Sunucudan dönen dokuz satır.</summary>
    public class FinansmanKisitlamaSonucDto
    {
        public int Yil { get; set; }
        public decimal KisitlamaOrani { get; set; }
        public decimal Ozsermaye { get; set; }
        public decimal YabanciKaynakToplami { get; set; }
        public decimal AsanYabanciKaynak { get; set; }
        public decimal AsanKisimOrani { get; set; }
        public decimal FinansmanGideri { get; set; }
        public decimal OrtuluSermayeVeFinansmanGeliri { get; set; }
        public decimal DikkateAlinacakFinansmanGideri { get; set; }
        public decimal AsanKismaIsabetEdenGider { get; set; }
        public decimal Kkeg { get; set; }
        public bool KisitlamaVar { get; set; }
        public string? Aciklama { get; set; }
    }

    public class FinansmanKisitlamaOraniDto
    {
        public int Id { get; set; }
        public int Yil { get; set; }
        public decimal Oran { get; set; }
        public string? Dayanak { get; set; }
        public string? Not { get; set; }
        public DateTime? GuncellenmeTarihi { get; set; }
    }

    public class FinansmanKisitlamaOraniSaveDto
    {
        public decimal Oran { get; set; }
        public string? Dayanak { get; set; }
        public string? Not { get; set; }
    }

    /// <summary>Ekranın giriş durumu; sunucuya gönderilmeden önce burada tutulur.</summary>
    public class FinansmanKisitlamaPageModel
    {
        public int Yil { get; set; } = DateTime.Now.Year;
        public decimal? Ozsermaye { get; set; }
        public decimal YabanciKaynakToplami { get; set; }
        public decimal FinansmanGideri { get; set; }
        public decimal OrtuluSermayeVeFinansmanGeliri { get; set; }
    }
}
