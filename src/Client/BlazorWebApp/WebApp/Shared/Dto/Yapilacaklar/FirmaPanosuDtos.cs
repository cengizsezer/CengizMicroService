namespace WebApp.Shared.Dto.Yapilacaklar
{
    // Sunucudaki CatalogService.Api.Features.Yapilacaklar.Dtos.FirmaPanosuDtos'un aynası (Firma Panosu · Aşama 1);
    // sayısal enum değerleri sözleşmenin parçası.

    /// <summary>Sayaç kovası — vadeye göre; kural sunucuda (ChecklistKartKurucu.Kova).</summary>
    public enum PanoKovasi : byte
    {
        Gecikti = 1,
        BuAy = 2,
        Sonra = 3,
        Tamam = 4
    }

    public class PanoSayaciDto
    {
        public PanoKovasi Kova { get; set; }
        public string Ad { get; set; } = string.Empty;
        public int Sayi { get; set; }
        public string Aciklama { get; set; } = string.Empty;
    }

    public class ChecklistMaddesiDto
    {
        public IsSatiriDto Satir { get; set; } = new();
        public PanoKovasi Kova { get; set; }
        public int OrtakFirmaSayisi { get; set; } = 1;
    }

    public class ChecklistBolumuDto
    {
        public string Anahtar { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public int Toplam { get; set; }
        public int Tamam { get; set; }
        public int Gecikmis { get; set; }
        public List<ChecklistMaddesiDto> Maddeler { get; set; } = new();
    }

    public class ChecklistKartiDto
    {
        public string Anahtar { get; set; } = string.Empty;
        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;
        public string? SorumluKullaniciAdi { get; set; }
        public string DonemAnahtari { get; set; } = string.Empty;
        public string DonemEtiketi { get; set; } = string.Empty;
        public PanoKovasi Durum { get; set; }
        public int Toplam { get; set; }
        public int Tamam { get; set; }
        public int Gecikmis { get; set; }
        public bool YasalVar { get; set; }
        public string? SiradakiBaslik { get; set; }
        public DateTime? SiradakiSonGun { get; set; }
        public PanoKovasi? SiradakiKova { get; set; }

        /// <summary>Aşama 3: maddelerinin geldiği şablon adları (kart etiketi); boş = etiket yok.</summary>
        public List<string> Sablonlar { get; set; } = new();

        public List<ChecklistBolumuDto> Bolumler { get; set; } = new();

        public IEnumerable<ChecklistMaddesiDto> Maddeler => Bolumler.SelectMany(b => b.Maddeler);
    }

    public class ChecklistDonemiDto
    {
        public string Anahtar { get; set; } = string.Empty;
        public string Etiket { get; set; } = string.Empty;
        public int FirmaSayisi { get; set; }
        public int Toplam { get; set; }
        public int Tamam { get; set; }
        public int Gecikmis { get; set; }
        public List<ChecklistKartiDto> Kartlar { get; set; } = new();
    }

    public class PanoListeSatiriDto
    {
        public PanoKovasi Kova { get; set; }
        public IsGrubuDto Grup { get; set; } = new();
    }

    public class PanoSorumluSecenegiDto
    {
        /// <summary>0 = sorumlusu olmayan.</summary>
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public int Sayi { get; set; }
    }

    public class FirmaPanosuDto
    {
        public DateTime Bugun { get; set; }
        public int? FirmaId { get; set; }
        public List<PanoSayaciDto> Sayaclar { get; set; } = new();
        public List<ChecklistDonemiDto> Donemler { get; set; } = new();
        public List<PanoListeSatiriDto> Liste { get; set; } = new();
        public List<PanoSorumluSecenegiDto> Sorumlular { get; set; } = new();
        public int FirmaSayisi { get; set; }
        public int KartSayisi { get; set; }

        public ChecklistKartiDto? Kart(string anahtar)
            => Donemler.SelectMany(d => d.Kartlar).FirstOrDefault(k => k.Anahtar == anahtar);
    }

    /// <summary>Kova adları ve renk sınıfları (ck- öneki) — sayaç, liste bölümü ve rozet aynı adı kullanır.</summary>
    public static class PanoMetin
    {
        public static readonly PanoKovasi[] Sira = { PanoKovasi.Gecikti, PanoKovasi.BuAy, PanoKovasi.Sonra, PanoKovasi.Tamam };

        public static string Ad(PanoKovasi k) => k switch
        {
            PanoKovasi.Gecikti => "Gecikti",
            PanoKovasi.BuAy => "Bu ay",
            PanoKovasi.Sonra => "Sonra",
            _ => "Tamamlanan"
        };

        /// <summary>Renk değişkenlerini (--ck-c, --ck-cs) kuran sınıf.</summary>
        public static string Renk(PanoKovasi k) => k switch
        {
            PanoKovasi.Gecikti => "ck-c-gecikti",
            PanoKovasi.BuAy => "ck-c-buay",
            PanoKovasi.Sonra => "ck-c-sonra",
            _ => "ck-c-tamam"
        };

        /// <summary>Adres değeri (?durum= değil; yalnız localStorage anahtarı).</summary>
        public static string Kod(PanoKovasi k) => k switch
        {
            PanoKovasi.Gecikti => "gecikti",
            PanoKovasi.BuAy => "buay",
            PanoKovasi.Sonra => "sonra",
            _ => "tamam"
        };

        public static string Rozet(PanoKovasi k, DateTime sonGun, DateTime bugun)
            => k == PanoKovasi.Gecikti ? $"Gecikti · {(bugun.Date - sonGun.Date).Days} gün" : Ad(k);

        // Firmanın baş harf kutusu: renk kayıtlı değil, Id'den türer — aynı firma her yerde aynı renkte.
        private static readonly string[] FirmaRenkleri =
            { "#3f6fd8", "#3e9b5f", "#c2702a", "#8a4fd0", "#c2416b", "#2a8a96", "#6b7a2a", "#5a5fc8" };

        public static string FirmaRengi(int firmaId) => FirmaRenkleri[Math.Abs(firmaId) % FirmaRenkleri.Length];

        private static readonly System.Globalization.CultureInfo Tr = new("tr-TR");

        /// <summary>"LIME" → "LI", "dgr a.ş." → "DG"; harf yoksa "?".</summary>
        public static string BasHarfler(string ad)
        {
            var harfler = new string((ad ?? string.Empty).Where(char.IsLetterOrDigit).Take(2).ToArray());
            return harfler.Length == 0 ? "?" : harfler.ToUpper(Tr);
        }
    }
}
