namespace CatalogService.Api.Features.Ajanlar.Dtos
{
    /// <summary>Ajanın <c>Kaydol</c> çağrısıyla gönderdiği tanıtım bilgisi.</summary>
    public class AjanKaydiIstegi
    {
        /// <summary>Makineye özgü ve kararlı olmalı; ajan bunu yerelde saklar.</summary>
        public string MakineId { get; set; } = string.Empty;

        public string MakineAdi { get; set; } = string.Empty;
        public string AjanSurumu { get; set; } = string.Empty;
        public string? IsletimSistemi { get; set; }

        /// <summary>ORKA açık mı; ajan bakamıyorsa null bırakır.</summary>
        public bool? OrkaCalisiyorMu { get; set; }
    }

    /// <summary>
    /// Kaydın sonucu. Reddedilse bile sunucu ve asgari sürüm dönüyor: ajan
    /// kullanıcıya "şu sürümdesin, şu sürüm gerekiyor" diyebilsin.
    /// </summary>
    public class KayitSonucu
    {
        public bool Kabul { get; set; }
        public string Mesaj { get; set; } = string.Empty;
        public string SunucuSurumu { get; set; } = string.Empty;
        public string AsgariAjanSurumu { get; set; } = string.Empty;
    }

    /// <summary>Durum ucunun döndürdüğü satır.</summary>
    public class BagliAjanDto
    {
        public string MakineId { get; set; } = string.Empty;
        public string MakineAdi { get; set; } = string.Empty;
        public string AjanSurumu { get; set; } = string.Empty;
        public string? IsletimSistemi { get; set; }

        /// <summary>
        /// Bağlantıyı kuran ajan kimliği. Yönetim ekranı bu alanla, IdentityService'ten
        /// gelen ajan listesini hub'daki bağlı listesiyle eşleştiriyor.
        /// </summary>
        public string AjanId { get; set; } = string.Empty;

        public DateTimeOffset BaglantiZamani { get; set; }
        public DateTimeOffset SonKalpAtisi { get; set; }
        public bool? OrkaCalisiyorMu { get; set; }
    }
}
