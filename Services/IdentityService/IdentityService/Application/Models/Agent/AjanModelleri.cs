using System;

namespace IdentityService.Application.Models.Agent
{
    /// <summary>Ajanın elindeki ham anahtarla token istemesi.</summary>
    public class AjanTokenIstegi
    {
        public string AjanAnahtari { get; set; } = string.Empty;
    }

    /// <summary>Token isteğinin yanıtı. Ham anahtar geri dönmüyor.</summary>
    public class AjanTokenYaniti
    {
        public string Token { get; set; } = string.Empty;
        public DateTime GecerlilikBitisiUtc { get; set; }
        public int AjanId { get; set; }
        public string AjanAdi { get; set; } = string.Empty;
    }

    /// <summary>Yeni ajan kaydı isteği (yönetim ekranı).</summary>
    public class YeniAjanIstegi
    {
        public string Ad { get; set; } = string.Empty;

        /// <summary>Null ise anahtar süresiz.</summary>
        public DateTime? GecerlilikBitisi { get; set; }
    }

    /// <summary>
    /// Yeni ajanın yanıtı. <see cref="Anahtar"/> <b>yalnız burada</b> görünüyor;
    /// veritabanında hash'i var, listede öneki var, başka hiçbir yerde yok.
    /// </summary>
    public class YeniAjanYaniti
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string Anahtar { get; set; } = string.Empty;
        public string AnahtarOnEki { get; set; } = string.Empty;
    }

    /// <summary>Listedeki bir ajan satırı.</summary>
    public class AjanListeSatiri
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string AnahtarOnEki { get; set; } = string.Empty;
        public int OlusturanKullaniciId { get; set; }
        public string? OlusturanKullaniciAdi { get; set; }
        public DateTime OlusturmaZamani { get; set; }
        public DateTime? SonKullanim { get; set; }
        public DateTime? GecerlilikBitisi { get; set; }
        public bool Aktif { get; set; }
        public DateTime? IptalZamani { get; set; }
        public string? IptalNedeni { get; set; }

        /// <summary>Sunucunun kararı: "Aktif", "İptal", "Süresi doldu".</summary>
        public string Durum { get; set; } = string.Empty;
    }

    /// <summary>İptal isteği. Neden zorunlu — kimin niye kapattığı kaybolmasın.</summary>
    public class AjanIptalIstegi
    {
        public string Neden { get; set; } = string.Empty;
    }
}
