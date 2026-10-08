namespace WebApp.Pages.Hesaplamalar.OrtuluSermaye.Model
{
    /// <summary>
    /// Hesabın sonucu. Tutarlar yuvarlanmaz; yuvarlama yalnız ekranda (N2) yapılır.
    /// Sınıf tutarlarının hepsi örtülü kısma isabet edendir (toplam × <see cref="OrtuluOran"/>);
    /// oransız toplamlar <see cref="Toplamlar"/> içinde.
    /// </summary>
    public class OrtuluSermayeSonuc
    {
        public decimal EnYuksekBorc { get; set; }

        /// <summary>Zirvenin günü; borç hiç pozitif olmadıysa null.</summary>
        public DateTime? EnYuksekBorcTarihi { get; set; }

        public decimal DikkateAlinanBorc { get; set; }
        public decimal Sinir { get; set; }
        public decimal AsanKisim { get; set; }
        public decimal OrtuluOran { get; set; }

        public bool OrtuluSermayeVar => AsanKisim > 0;

        public decimal KurFarkiGideri { get; set; }

        /// <summary>Gidere mahsup EDİLMEZ; beyannamede ayrıca indirilir (GT 12.3).</summary>
        public decimal KurFarkiGeliri { get; set; }

        public decimal FaizGideri { get; set; }
        public decimal FaizStopaji { get; set; }
        public decimal FaizKdv { get; set; }
        public decimal DamgaVergisi { get; set; }

        /// <summary>Kur farkı gideri + faiz + faiz KDV + DV (örtülü kısımlar). Stopaj ve kur farkı geliri girmez.</summary>
        public decimal KkegToplam { get; set; }

        public SinifToplamlari Toplamlar { get; set; } = new();
    }

    /// <summary>
    /// Gider/gelir muavininin sınıf bazında oransız toplamları. Giderlerde Borç − Alacak,
    /// kur farkı geliri ve stopajda Alacak − Borç. <see cref="Stopaj"/> yalnız bilgi amaçlı;
    /// beyanname tutarına girmez (faiz stopajı orandan hesaplanır).
    /// </summary>
    public class SinifToplamlari
    {
        public decimal KurFarkiGideri { get; set; }
        public decimal KurFarkiGeliri { get; set; }
        public decimal FaizGideri { get; set; }
        public decimal FaizKdv { get; set; }
        public decimal Stopaj { get; set; }
        public decimal DamgaVergisi { get; set; }
    }
}
