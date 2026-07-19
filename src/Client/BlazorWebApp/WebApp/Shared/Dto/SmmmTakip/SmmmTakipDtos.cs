namespace WebApp.Shared.Dto.SmmmTakip
{
    // ---- Okuma ----

    public class SmmmKonuAgacDto
    {
        public int Id { get; set; }
        public int? UstKonuId { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public int Sira { get; set; }
        public bool Aktif { get; set; }
        public List<SmmmKonuAgacDto> AltKonular { get; set; } = new();
    }

    public class SmmmKonuDetayDto
    {
        public int Id { get; set; }
        public int? UstKonuId { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? IcerikMd { get; set; }
        public int Sira { get; set; }
        public bool Aktif { get; set; }
        public int? SeciliYil { get; set; }
        public List<SmmmHadDto> Hadler { get; set; } = new();
    }

    public class SmmmHadDto
    {
        public int Id { get; set; }
        public int KonuId { get; set; }
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        public string Birim { get; set; } = "TL";
        public string? Dayanak { get; set; }
        public int Sira { get; set; }
        public decimal? SeciliDeger { get; set; }
        public int? SeciliYil { get; set; }
        public string? SeciliTeblig { get; set; }
        public string? SeciliNot { get; set; }
        public List<SmmmHadDegeriDto> Degerler { get; set; } = new();
    }

    public class SmmmHadDegeriDto
    {
        public int Id { get; set; }
        public int HadId { get; set; }
        public int Yil { get; set; }
        public decimal Deger { get; set; }
        public string? Teblig { get; set; }
        public string? Not { get; set; }
    }

    // ---- Yazma (admin) ----

    public class SmmmKonuSaveDto
    {
        public int? UstKonuId { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public string? Slug { get; set; }
        public string? IcerikMd { get; set; }
        public int Sira { get; set; }
        public bool Aktif { get; set; } = true;
    }

    public class SmmmHadSaveDto
    {
        public int KonuId { get; set; }
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        public string Birim { get; set; } = "TL";
        public string? Dayanak { get; set; }
        public int Sira { get; set; }
    }

    public class SmmmHadDegeriSaveDto
    {
        public int HadId { get; set; }
        public int Yil { get; set; }
        public decimal Deger { get; set; }
        public string? Teblig { get; set; }
        public string? Not { get; set; }
    }
}
