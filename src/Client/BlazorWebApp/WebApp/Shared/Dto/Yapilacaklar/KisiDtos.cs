
namespace WebApp.Shared.Dto.Yapilacaklar
{
    // Sunucudaki KisiDtos'un aynası (Prompt 14 · ADIM 4).

    /// <summary>Olası aynı kişi türü (sunucu: KisiEslestirici.SupheTuru).</summary>
    public enum SupheTuru : byte
    {
        AyniAd = 1,
        BenzerAd = 2
    }

    /// <summary>
    /// Tablonun satırı: Firma · Kişi · E-posta · Hangi iş · Ne zaman · Rol (Kime/Bilgi). Bir kişi birden çok
    /// işe alıcıysa her iş ayrı satır. Alıcısı girilmemiş iş de satırdır (<see cref="Eksik"/>) — gizlenmez,
    /// boş bırakmak da bilgidir. Hiçbir işe alıcı olmayan kişi de satırdır (iş boş).
    /// </summary>
    public class KisiSatiriDto
    {
        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;

        public int? KisiId { get; set; }
        public string? KisiAdi { get; set; }
        public string? Eposta { get; set; }
        public string? KisiRolu { get; set; }
        public string? Notu { get; set; }
        public bool KisiAktif { get; set; } = true;

        public int? FirmaIsiId { get; set; }
        public string? IsBaslik { get; set; }
        public bool Yasal { get; set; }

        /// <summary>"her ay · ayın 25'i".</summary>
        public string? NeZaman { get; set; }

        public AliciTipi? AliciTipi { get; set; }

        /// <summary>İşin alıcısı girilmemiş → amber "eksik".</summary>
        public bool Eksik { get; set; }
    }

    /// <summary>Ayın mail takviminde bir gün · bir iş: "25 · Bordro · J. van Dijk, R. de Vries · 4 firma".</summary>
    public class MailIsiDto
    {
        public string Baslik { get; set; } = string.Empty;

        /// <summary>Tarif grubu anahtarı (aynı iş farklı firmalarda tek satır).</summary>
        public string Anahtar { get; set; } = string.Empty;

        public List<string> Kime { get; set; } = new();
        public List<string> Bilgi { get; set; } = new();
        public List<string> Firmalar { get; set; } = new();
        public int FirmaSayisi => Firmalar.Count;
    }

    public class MailGunuDto
    {
        public DateTime Tarih { get; set; }
        public List<MailIsiDto> Isler { get; set; } = new();
    }

    /// <summary>Bu ay son günü olan ama alıcısı girilmemiş iş.</summary>
    public class AlicisizIsDto
    {
        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;
        public string Baslik { get; set; } = string.Empty;
        public DateTime SonGun { get; set; }
    }

    public class KisiOzetDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string? Eposta { get; set; }
        public int AliciSayisi { get; set; }
    }

    /// <summary>Olası aynı kişi — otomatik birleştirilmez, kullanıcı karar verir.</summary>
    public class SupheliKisiDto
    {
        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;
        public SupheTuru Tur { get; set; }
        public List<KisiOzetDto> Kisiler { get; set; } = new();
    }

    public class KisilerPanosuDto
    {
        /// <summary>Mail takviminin ayı.</summary>
        public int Yil { get; set; }
        public int Ay { get; set; }
        public string Etiket { get; set; } = string.Empty;

        public List<KisiSatiriDto> Satirlar { get; set; } = new();

        /// <summary>TÜRETİLİR (iş gün kuralı + alıcılar); elle doldurulmaz. Güne göre artan.</summary>
        public List<MailGunuDto> Takvim { get; set; } = new();

        public List<AlicisizIsDto> AlicisizIsler { get; set; } = new();
        public List<SupheliKisiDto> Supheliler { get; set; } = new();
    }

    /// <summary>Kişi formu. Şifre / kullanıcı adı / erişim bilgisi alanı YOK ve olmayacak.</summary>
    public class KisiKaydetDto
    {
        public int FirmaId { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string? Eposta { get; set; }
        public string? Rol { get; set; }
        public string? Notu { get; set; }
        public bool Aktif { get; set; } = true;
    }

    public class AliciOnizlemeIstegiDto
    {
        public int FirmaId { get; set; }
        public List<FirmaIsiAlicisiDto> Alicilar { get; set; } = new();
    }

    /// <summary>Alıcı satırının kişi durumu (Prompt 14B) — kaydedince ne olacağı.</summary>
    public class AliciOnizlemeDto
    {
        public bool Kayitli { get; set; }
        public int? KisiId { get; set; }
        public string? KayitliAd { get; set; }
        public string? KayitliEposta { get; set; }
        public bool EpostaKisidenGelir { get; set; }
        public bool AdFarkli { get; set; }
    }

    /// <summary>Alıcı → kişi taşıma raporu (seed).</summary>
    public class KisiTasimaRaporuDto
    {
        public int AliciKaydi { get; set; }
        public int YeniKisi { get; set; }
        public int EpostaIleBirlesen { get; set; }
        public int AdIleBirlesen { get; set; }

        /// <summary>E-postasız ad birden çok kişiye uyuyordu; seçilmedi, ayrı kişi açıldı.</summary>
        public int Belirsiz { get; set; }

        /// <summary>Aynı e-posta, farklı ad yazımı (tek kişi oldu): "a@x.com: J. van Dijk / Jan van Dijk".</summary>
        public List<string> FarkliAdYazimlari { get; set; } = new();

        public List<string> Supheliler { get; set; } = new();
    }
}
