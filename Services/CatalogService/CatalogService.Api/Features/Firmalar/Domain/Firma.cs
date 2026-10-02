namespace CatalogService.Api.Features.Firmalar.Domain
{
    public class Firma
    {
        public int Id { get; set; }

        public string VergiKimlikNo { get; set; } = string.Empty;
        public string Unvan { get; set; } = string.Empty;
        public string KisaAd { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefon { get; set; } = string.Empty;
        public string TicaretSicilNo { get; set; } = string.Empty;
        public string VergiDairesi { get; set; } = string.Empty;
        public bool Aktif { get; set; } = true;

        // BDP / KDV Beyannamesi için ek alanlar (opsiyonel)
        // NOT: YetkiliAdi / YetkiliSoyadi DB kolon isimleri değişmedi — sadece UI etiketleri
        // "Adı (Unvanın Devamı)" / "Soyadı (Unvanı)" oldu (BDP'de firma ünvanı 2 parçaya bölünüyor).
        public string? VergiDairesiKodu { get; set; }
        public string? YetkiliAdi { get; set; }
        public string? YetkiliSoyadi { get; set; }
        public string? TelefonAlanKodu { get; set; }

        /// <summary>
        /// Firmanın ORKA'daki kodu (ör. "0001"). ORKA giriş zincirinde firma bu
        /// kodla açılıyor; PkfRobot aktarım işini alırken sunucudan bu değeri
        /// istiyor. Boş olabilir: ORKA'ya aktarım yapılmayan firmalarda gerekmez,
        /// aktarım istendiğinde iş anlaşılır bir mesajla reddedilir.
        /// </summary>
        public string? OrkaFirmaKodu { get; set; }

        // ---- Sınıflandırma (anasayfa künyesi) ----
        // Defter usulü ve vergi türü mükellefiyet KODLARINDAN önerilir
        // (MukellefiyetSiniflandirici); kaynak alanı "Elle" ise öneri üzerine yazmaz.

        public DefterUsulu DefterUsulu { get; set; } = DefterUsulu.Belirsiz;
        public SiniflandirmaKaynagi DefterUsuluKaynagi { get; set; } = SiniflandirmaKaynagi.Yok;

        public VergiTuru VergiTuru { get; set; } = VergiTuru.Belirsiz;
        public SiniflandirmaKaynagi VergiTuruKaynagi { get; set; } = SiniflandirmaKaynagi.Yok;

        /// <summary><see cref="MizanFormatlari.Liste"/>'den bir metin; boş = seçilmedi.</summary>
        public string? MizanFormati { get; set; }

        /// <summary>Boş = seçilmedi (eksik sayılır); "takvim yılı" varsayılmaz.</summary>
        public HesapDonemi? HesapDonemi { get; set; }

        /// <summary>Yalnız <see cref="Domain.HesapDonemi.OzelHesapDonemi"/> ise dolu.</summary>
        public DateTime? OzelDonemBas { get; set; }
        public DateTime? OzelDonemBit { get; set; }

        // ---- Sınıflandırma (Prompt 9) — elle seçilir, otomatik önerisi yok ----

        public FirmaTipi FirmaTipi { get; set; } = FirmaTipi.Belirsiz;
        public SgkTesvikKademesi SgkTesvikKademesi { get; set; } = SgkTesvikKademesi.Belirsiz;
        public MuhtasarDonemi MuhtasarDonemi { get; set; } = MuhtasarDonemi.Belirsiz;

        // ---- Kullanılan sistemler (Prompt 9) ----

        /// <summary>Serbest not: "DijitalPlanet şifresi firma yetkilisinde".</summary>
        public string? SistemNotu { get; set; }

        // ---- Takip ----

        /// <summary>
        /// Firmadan sorumlu kullanıcı: IdentityService'teki kullanıcının Id'si (JWT <c>sub</c>).
        /// Kullanıcı tablosu başka servisin veritabanı modelinde; servisler arası DB FK
        /// kurulmadı, adı <see cref="SorumluKullaniciAdi"/>'nda anlık görüntü olarak duruyor.
        /// </summary>
        public int? SorumluKullaniciId { get; set; }
        public string? SorumluKullaniciAdi { get; set; }

        // Bu firmanın KDV beyannamesini hangi SMMM/YMM düzenliyor — FK to Duzenleyenler.
        // ON DELETE SET NULL: düzenleyen silinirse firma kaydı korunur, FK temizlenir.
        public int? DuzenleyenId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
