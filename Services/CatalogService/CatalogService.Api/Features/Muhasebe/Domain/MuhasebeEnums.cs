namespace CatalogService.Api.Features.Muhasebe.Domain
{
    /// <summary>Hesabın ağaçtaki seviyesi/rolü.</summary>
    public enum HesapTuru : byte
    {
        Sinif = 1,
        Grup = 2,
        Kebir = 3,
        Muavin = 4
    }

    /// <summary>
    /// Hesabın bakiye karakteri. Kök sınıftan miras alınır:
    /// 1–2 → Aktif, 3–5 → Pasif, 6 → Gelir, 7 → Gider, 8 → Maliyet, 9 → Nazım.
    /// </summary>
    public enum HesapKarakter : byte
    {
        Aktif = 1,
        Pasif = 2,
        Gelir = 3,
        Gider = 4,
        Maliyet = 5,
        Nazim = 6
    }

    /// <summary>Fiş türü (tekdüzen).</summary>
    public enum FisTuru : byte
    {
        Acilis = 1,
        Tahsil = 2,
        Tediye = 3,
        Mahsup = 4,
        Kapanis = 5
    }

    /// <summary>Fişin hangi kanaldan oluştuğu. WhatsApp ileride kullanılacak.</summary>
    public enum FisKaynak : byte
    {
        Manuel = 0,
        WhatsApp = 1,
        Ekstre = 2,
        Otomatik = 3
    }

    /// <summary>Fiş durumu. Kesinleşmiş fiş güncellenemez/silinemez.</summary>
    public enum FisDurum : byte
    {
        Taslak = 0,
        Kesinlesmis = 1
    }
}
