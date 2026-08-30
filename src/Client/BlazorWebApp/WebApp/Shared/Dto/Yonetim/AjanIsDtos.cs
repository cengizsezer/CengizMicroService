namespace WebApp.Shared.Dto.Yonetim
{
    /// <summary>Sunucudaki <c>AjanIsDurumu</c> ile aynı sıra; int olarak taşınıyor.</summary>
    public enum AjanIsDurumu
    {
        Bekliyor = 0,
        Gonderildi = 1,
        Calisiyor = 2,
        Tamamlandi = 3,
        Basarisiz = 4,
        IptalEdildi = 5,
        ZamanAsimi = 6
    }

    public class YeniAjanIsiRequest
    {
        /// <summary>Boş bırakılırsa sunucu tek adayı kendisi bulur.</summary>
        public string? AjanId { get; set; }

        public int FirmaId { get; set; }
        public string IsTipi { get; set; } = AjanIsTipleri.SahteAktarim;
        public string? Yuk { get; set; }
    }

    public class AjanIsDto
    {
        public Guid Id { get; set; }
        public string AjanId { get; set; } = "";
        public int FirmaId { get; set; }
        public string IsTipi { get; set; } = "";
        public AjanIsDurumu Durum { get; set; }

        /// <summary>Durumun okunur hâli; sunucu üretiyor, ekran kendi sözlüğünü tutmuyor.</summary>
        public string DurumAdi { get; set; } = "";

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
        public bool AjanBagliydi { get; set; }
    }

    public class AjanIsiOlusturSonucuDto
    {
        public AjanIsDto? Is { get; set; }

        /// <summary>Doluysa istek reddedildi: bu iş hâlâ sürüyor.</summary>
        public AjanIsDto? CakisanIs { get; set; }

        public string Mesaj { get; set; } = "";
    }

    public static class AjanIsTipleri
    {
        public const string SahteAktarim = "SahteAktarim";
        public const string OrkayaAktar = "OrkayaAktar";
    }
}
