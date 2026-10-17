namespace CatalogService.Api.Features.Payroll.Dtos.Shared
{
    /// <summary>
    /// Kanun açılır kutusunun bir seçeneği + ekranın o seçenek için sorması gereken ek girdiler.
    /// Bayraklar seçilen yıla göre doldurulur (2026 öncesi yıllarda ek girdi yok).
    /// </summary>
    public class PayrollLawTypeDto
    {
        /// <summary>İç anahtar — istekte LawCode olarak geri gelir.</summary>
        public string Code { get; set; } = default!;
        public string Label { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? SgkKodu { get; set; }

        public string? AltSecimBasligi { get; set; }
        public List<PayrollLawTypeAltSecimDto> AltSecimler { get; set; } = new();

        public bool EgitimDurumuSorulur { get; set; }
        public bool EngelZorunlu { get; set; }
        public bool EmekliyeUygulanmaz { get; set; }

        /// <summary>SGK primine dokunmayan, ayrı satırda düşen destek (Turizm, İSG).</summary>
        public bool AyriSatirDestek { get; set; }
        /// <summary>Doluysa onay kutusu + prim günü sorulur (Turizm).</summary>
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
