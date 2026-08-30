namespace WebApp.Shared.Dto.Yonetim
{
    /// <summary>
    /// IdentityService'teki bir ajan kaydı. Ham anahtar burada yok — o yalnız
    /// oluşturma yanıtında, bir kez görünüyor.
    /// </summary>
    public class AjanDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = "";
        public string AnahtarOnEki { get; set; } = "";
        public int OlusturanKullaniciId { get; set; }
        public string? OlusturanKullaniciAdi { get; set; }
        public DateTime OlusturmaZamani { get; set; }
        public DateTime? SonKullanim { get; set; }
        public DateTime? GecerlilikBitisi { get; set; }
        public bool Aktif { get; set; }
        public DateTime? IptalZamani { get; set; }
        public string? IptalNedeni { get; set; }

        /// <summary>Sunucunun kararı: "Aktif", "İptal", "Süresi doldu".</summary>
        public string Durum { get; set; } = "";
    }

    public class YeniAjanRequest
    {
        public string Ad { get; set; } = "";
        public DateTime? GecerlilikBitisi { get; set; }
    }

    /// <summary><see cref="Anahtar"/> bir daha hiçbir yerden okunamaz.</summary>
    public class YeniAjanResponse
    {
        public int Id { get; set; }
        public string Ad { get; set; } = "";
        public string Anahtar { get; set; } = "";
        public string AnahtarOnEki { get; set; } = "";
    }

    public class AjanIptalRequest
    {
        public string Neden { get; set; } = "";
    }

    /// <summary>
    /// CatalogService'in hub'ında o an bağlı duran ajan. Ajan listesiyle
    /// <see cref="AjanId"/> üzerinden eşleşiyor.
    /// </summary>
    public class BagliAjanDto
    {
        public string MakineId { get; set; } = "";
        public string MakineAdi { get; set; } = "";
        public string AjanSurumu { get; set; } = "";
        public string? IsletimSistemi { get; set; }
        public string AjanId { get; set; } = "";
        public DateTimeOffset BaglantiZamani { get; set; }
        public DateTimeOffset SonKalpAtisi { get; set; }
        public bool? OrkaCalisiyorMu { get; set; }
    }
}
