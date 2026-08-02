namespace CatalogService.Api.Features.FirmaKontrol.Domain
{
    /// <summary>
    /// Beyanname bölümü. Sıra sonucu doğrudan etkiler: Grup 2 indirimleri geçmiş yıl
    /// zararlarından ÖNCE, Grup 3 indirimleri SONRA uygulanır.
    /// </summary>
    public enum VergiKalemGrubu : byte
    {
        /// <summary>Kanunen kabul edilmeyen giderler; ticari kâra eklenir.</summary>
        Kkeg = 1,

        /// <summary>Zarar olsa dahi indirilecek istisna ve indirimler; matrahı negatife çekebilir.</summary>
        ZararOlsaDahi = 2,

        /// <summary>Kazancın bulunması hâlinde indirilecek indirimler; matrahı sıfırın altına indiremez.</summary>
        KazancVarsa = 3,

        /// <summary>Hesaplanan vergiden düşülen mahsuplar (tevkifat, geçici vergi, yurt dışı vergi).</summary>
        Mahsup = 4
    }

    /// <summary>Kalemin tutarsal tavanı. Oran ve tutarlar koda gömülmez, kalemden okunur.</summary>
    public enum UstSinirTuru : byte
    {
        Yok = 0,

        /// <summary>Kurum kazancının yüzdesi (ör. bağışlarda %5). Değer yüzde olarak tutulur.</summary>
        KurumKazanciYuzdesi = 1,

        /// <summary>Sabit tutar tavanı.</summary>
        SabitTutar = 2
    }

    /// <summary>
    /// Kalemin hangi mükellefiyet türünde kullanılacağı. Bu görevin kapsamı yalnızca
    /// kurumlar vergisidir; alan, gelir vergisi kalemleri ileride eklenebilsin diye var.
    /// </summary>
    public enum MukellefiyetTuru : byte
    {
        GelirVergisi = 1,
        KurumlarVergisi = 2,
        Ikisi = 3
    }
}
