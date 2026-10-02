namespace WebApp.Shared.Dto.Yapilacaklar
{
    // Sunucudaki CatalogService.Api.Features.Yapilacaklar.Dtos.TopluIsDtos'un aynası.
    // Ayrıştırma ve doğrulama SUNUCUDA (TopluIsAyristirici); istemci yalnız gösterir ve düzeltmeyi geri yollar.

    public enum TopluAlan : byte
    {
        Baslik = 1,
        Tekrar = 2,
        Gun = 3,
        Program = 4,
        MenuYolu = 5,
        Alici = 6,
        AliciEposta = 7,
        Aciklama = 8
    }

    public enum TopluSatirDurumu : byte
    {
        Tamam = 1,
        Eksik = 2,
        Okunamadi = 3
    }

    public class TopluEslesmeDto
    {
        public TopluAlan Alan { get; set; }
        public int? Kolon { get; set; }
        public string? DosyadakiBaslik { get; set; }
    }

    public class TopluOnizleIstekDto
    {
        public string? Metin { get; set; }
        public List<List<string>>? Hucreler { get; set; }
        public bool? IlkSatirBaslik { get; set; }
        public List<TopluEslesmeDto>? Eslesme { get; set; }
    }

    public class TopluSatirDto
    {
        public int Sira { get; set; }
        public TopluSatirDurumu Durum { get; set; }
        public List<string> Sorunlar { get; set; } = new();
        public FirmaIsiKaydetDto? Is { get; set; }
        public string? TekrarMetni { get; set; }
        public string? GunMetni { get; set; }
        public string? ProgramMetni { get; set; }
        public string? AliciMetni { get; set; }
        public int? MevcutIsId { get; set; }
    }

    public class TopluOnizlemeDto
    {
        public bool IlkSatirBaslik { get; set; }
        public bool IlkSatirBaslikOnerisi { get; set; }
        public List<List<string>> Hucreler { get; set; } = new();
        public int KolonSayisi { get; set; }
        public List<TopluEslesmeDto> Eslesme { get; set; } = new();
        public List<TopluSatirDto> Satirlar { get; set; } = new();
    }

    public class TopluEkleDto
    {
        public List<FirmaIsiKaydetDto> Isler { get; set; } = new();
        public bool UzerineYazma { get; set; } = true;
    }

    public class TopluAtlananDto
    {
        public string Baslik { get; set; } = string.Empty;
        public string Sebep { get; set; } = string.Empty;
    }

    public class TopluSonucDto
    {
        public int Eklenen { get; set; }
        public int Guncellenen { get; set; }
        public List<TopluAtlananDto> Atlananlar { get; set; } = new();
    }

    public class IsKopyalaDto
    {
        public int KaynakFirmaId { get; set; }
        public List<int> IsIdleri { get; set; } = new();
    }

    public static class TopluMetin
    {
        public static string Alan(TopluAlan a) => a switch
        {
            TopluAlan.Baslik => "Başlık",
            TopluAlan.Tekrar => "Tekrar",
            TopluAlan.Gun => "Gün",
            TopluAlan.Program => "Program",
            TopluAlan.MenuYolu => "Menü yolu",
            TopluAlan.Alici => "Alıcı",
            TopluAlan.AliciEposta => "Alıcı e-posta",
            _ => "Açıklama"
        };

        /// <summary>Excel kolon harfi: 0 → A, 26 → AA.</summary>
        public static string KolonHarfi(int i)
        {
            var s = string.Empty;
            for (i++; i > 0; i = (i - 1) / 26) s = (char)('A' + (i - 1) % 26) + s;
            return s;
        }
    }
}
