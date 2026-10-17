namespace WebApp.Pages.Hesaplamalar.Bordro.Model
{
    // Sunucudaki PayrollLawTypeDto'nun aynası: seçenek + o seçenek için sorulacak ek girdiler.
    public class PayrollLawTypeDto
    {
        public string Code { get; set; } = default!;
        public string Label { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? SgkKodu { get; set; }

        public string? AltSecimBasligi { get; set; }
        public List<PayrollLawTypeAltSecimDto> AltSecimler { get; set; } = new();

        public bool EgitimDurumuSorulur { get; set; }
        public bool EngelZorunlu { get; set; }
        public bool EmekliyeUygulanmaz { get; set; }

        public bool AyriSatirDestek { get; set; }
        public string? OnayMetni { get; set; }
        public bool AylikTutarSorulur { get; set; }
        public bool ManuelSorulur { get; set; }
    }

    public class PayrollLawTypeAltSecimDto
    {
        public int Deger { get; set; }
        public string Ad { get; set; } = default!;
    }
}
