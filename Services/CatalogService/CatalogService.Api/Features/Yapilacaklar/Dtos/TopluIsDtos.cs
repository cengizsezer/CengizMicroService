namespace CatalogService.Api.Features.Yapilacaklar.Dtos
{
    /// <summary>Yapıştırılan tablonun eşlenebilecek alanları. Sayısal değerler istemciyle ortak.</summary>
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

    /// <summary>Önizleme satırının durumu: yeşil eklenir, amber eksik bilgiyle eklenir, kırmızı atlanır.</summary>
    public enum TopluSatirDurumu : byte
    {
        Tamam = 1,
        Eksik = 2,
        Okunamadi = 3
    }

    /// <summary>Alan ↔ kolon. <see cref="Kolon"/> boş = "eşleşmedi, boş kalacak".</summary>
    public class TopluEslesmeDto
    {
        public TopluAlan Alan { get; set; }
        public int? Kolon { get; set; }

        /// <summary>Yanıtta: ilk satır başlıksa o kolonun başlığı.</summary>
        public string? DosyadakiBaslik { get; set; }
    }

    /// <summary>
    /// Önizleme isteği. İlk istekte yalnız <see cref="Metin"/> (panodaki metin); sonraki isteklerde
    /// istemci düzelttiği hücre tablosunu ve değiştirdiği kararları geri gönderir — ayrıştırma ve
    /// doğrulama TEK YERDE (sunucuda) kalır.
    /// </summary>
    public class TopluOnizleIstekDto
    {
        public string? Metin { get; set; }
        public List<List<string>>? Hucreler { get; set; }

        /// <summary>Boş = sunucu karar verir (öneri).</summary>
        public bool? IlkSatirBaslik { get; set; }

        /// <summary>Boş = otomatik eşleme.</summary>
        public List<TopluEslesmeDto>? Eslesme { get; set; }
    }

    public class TopluSatirDto
    {
        /// <summary>Hücre tablosundaki satır indeksi (0'dan); istemci düzeltmeyi bu satıra yazar.</summary>
        public int Sira { get; set; }

        public TopluSatirDurumu Durum { get; set; }

        /// <summary>Amber/kırmızı sebepleri: "program listede yok: Garanti — boş kalacak".</summary>
        public List<string> Sorunlar { get; set; } = new();

        /// <summary>Çözülen iş; kırmızıda boş.</summary>
        public FirmaIsiKaydetDto? Is { get; set; }

        /// <summary>Tablo gösterimi için okunur değerler.</summary>
        public string? TekrarMetni { get; set; }
        public string? GunMetni { get; set; }
        public string? ProgramMetni { get; set; }
        public string? AliciMetni { get; set; }

        /// <summary>Firmada aynı başlıklı özel iş varsa (üzerine yazma kararı için).</summary>
        public int? MevcutIsId { get; set; }
    }

    public class TopluOnizlemeDto
    {
        public bool IlkSatirBaslik { get; set; }

        /// <summary>Sunucunun önerisi (kullanıcı değiştirmiş olabilir).</summary>
        public bool IlkSatirBaslikOnerisi { get; set; }

        public List<List<string>> Hucreler { get; set; } = new();
        public int KolonSayisi { get; set; }
        public List<TopluEslesmeDto> Eslesme { get; set; } = new();
        public List<TopluSatirDto> Satirlar { get; set; } = new();

        public int Tamam => Satirlar.Count(s => s.Durum == TopluSatirDurumu.Tamam);
        public int EksikBilgili => Satirlar.Count(s => s.Durum == TopluSatirDurumu.Eksik);
        public int Okunamayan => Satirlar.Count(s => s.Durum == TopluSatirDurumu.Okunamadi);
    }

    /// <summary>Onaylanan satırlar (yeşil + amber, atlananlar hariç).</summary>
    public class TopluEkleDto
    {
        public List<FirmaIsiKaydetDto> Isler { get; set; } = new();

        /// <summary>
        /// "Aynı başlıktaki mevcut işin üzerine yazma" — VARSAYILAN AÇIK: aynı başlıklı iş atlanır
        /// ve raporlanır. Kapalıysa mevcut iş güncellenir.
        /// </summary>
        public bool UzerineYazma { get; set; } = true;

        /// <summary>
        /// Eklenen BÜTÜN işlerin başlangıç ayı "2026-09" (Prompt 17) — satır satır değil, tek değer.
        /// Boş = eklendiği aydan. Aynı başlıkla güncellenen mevcut iş kendi başlangıcını korur.
        /// </summary>
        public string? IlkDonem { get; set; }
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

    /// <summary>Başka firmadan kopyalama: kaynak firma ve seçilen özel işleri.</summary>
    public class IsKopyalaDto
    {
        public int KaynakFirmaId { get; set; }
        public List<int> IsIdleri { get; set; } = new();

        /// <summary>Kopyaların hedef firmadaki başlangıç ayı (Prompt 17); kaynağın başlangıcı taşınmaz. Boş = eklendiği aydan.</summary>
        public string? IlkDonem { get; set; }
    }
}
