using CatalogService.Api.Features.Ajanlar.Domain;

namespace CatalogService.Api.Features.Ajanlar.Dtos
{
    /// <summary>Yeni iş isteği (yönetim/aktar ekranı).</summary>
    public class YeniAjanIsiDto
    {
        /// <summary>
        /// Hedef ajan. <b>Boş bırakılabilir:</b> ofiste tek ayrılmış banka
        /// bilgisayarı var, ekranın kullanıcıya "hangi ajan" diye sorması gereksiz
        /// bir soru olurdu. Boşsa sunucu tek adayı kendisi buluyor; birden çok
        /// aday varsa istek reddediliyor ve alan zorunlu hâle geliyor.
        /// </summary>
        public string? AjanId { get; set; }

        public int FirmaId { get; set; }
        public string IsTipi { get; set; } = AjanIsTipleri.SahteAktarim;

        /// <summary>İş parametreleri; tipe göre değişir. Boş bırakılabilir.</summary>
        public string? Yuk { get; set; }
    }

    /// <summary>Listede ve durum yoklamasında dönen iş.</summary>
    public class AjanIsDto
    {
        public Guid Id { get; set; }
        public string AjanId { get; set; } = string.Empty;
        public int FirmaId { get; set; }
        public string IsTipi { get; set; } = string.Empty;
        public AjanIsDurumu Durum { get; set; }

        /// <summary>Durumun okunur hâli; ekranlar kendi sözlüğünü tutmasın.</summary>
        public string DurumAdi { get; set; } = string.Empty;

        public int IlerlemeYuzde { get; set; }
        public string? IlerlemeMesaji { get; set; }
        public int ToplamAdim { get; set; }
        public int TamamlananAdim { get; set; }

        public DateTime OlusturmaZamani { get; set; }
        public DateTime? BaslamaZamani { get; set; }
        public DateTime? BitisZamani { get; set; }

        public string? HataMesaji { get; set; }
        public string? SonucOzeti { get; set; }
        public string? HataEkraniDosyaId { get; set; }

        public bool Bitti { get; set; }

        /// <summary>İş oluşturulduğunda ajan bağlı mıydı? Ekran buna göre uyarı gösteriyor.</summary>
        public bool AjanBagliydi { get; set; }
    }

    /// <summary>
    /// İş oluşturmanın sonucu. Çakışma ayrı bir alan: "aynı ajana tek iş" kuralı
    /// kırıldığında ekranın hangi işin çalıştığını söyleyebilmesi gerekiyor.
    /// </summary>
    public class AjanIsiOlusturSonucuDto
    {
        public AjanIsDto? Is { get; set; }

        /// <summary>Doluysa istek reddedildi: bu iş hâlâ çalışıyor.</summary>
        public AjanIsDto? CakisanIs { get; set; }

        public string Mesaj { get; set; } = string.Empty;
    }

    /// <summary>Ajana hub üzerinden gönderilen paket.</summary>
    public class AjanIsPaketiDto
    {
        public Guid IsId { get; set; }
        public string IsTipi { get; set; } = string.Empty;
        public int FirmaId { get; set; }
        public string Yuk { get; set; } = "{}";
    }

}
