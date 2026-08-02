namespace WebApp.Shared.Dto.Muhasebe
{
    /// <summary>Fiş türü (tekdüzen). Sunucudaki <c>FisTuru</c> ile aynı sayısal değerler.</summary>
    public enum FisTuru : byte
    {
        Acilis = 1,
        Tahsil = 2,
        Tediye = 3,
        Mahsup = 4,
        Kapanis = 5
    }

    /// <summary>Fişin hangi kanaldan oluştuğu.</summary>
    public enum FisKaynak : byte
    {
        Manuel = 0,
        WhatsApp = 1,
        Ekstre = 2,
        Otomatik = 3
    }

    /// <summary>Fiş durumu. Kesinleşmiş fiş güncellenemez/silinemez (iş kuralı 15).</summary>
    public enum FisDurum : byte
    {
        Taslak = 0,
        Kesinlesmis = 1
    }

    public class FisSatirDto
    {
        public int Id { get; set; }
        public short SiraNo { get; set; }
        public int HesapId { get; set; }
        public string HesapKod { get; set; } = string.Empty;
        public string HesapAd { get; set; } = string.Empty;
        public int? MasrafMerkeziId { get; set; }
        public string? MasrafMerkeziKod { get; set; }
        public string? MasrafMerkeziAd { get; set; }
        public string? Aciklama { get; set; }
        public decimal Borc { get; set; }
        public decimal Alacak { get; set; }
        public string ParaBirimi { get; set; } = "TRY";
        public decimal? Doviz { get; set; }
        public decimal? Kur { get; set; }
    }

    public class FisDto
    {
        public int Id { get; set; }
        public short DonemYil { get; set; }
        public string FisNo { get; set; } = string.Empty;
        public DateTime Tarih { get; set; }
        public FisTuru FisTuru { get; set; }
        public string? BelgeNo { get; set; }
        public string? Aciklama { get; set; }
        public FisKaynak Kaynak { get; set; }
        public FisDurum Durum { get; set; }
        public int OlusturanId { get; set; }
        public DateTime OlusturmaT { get; set; }
        public DateTime? GuncellemeT { get; set; }
        public decimal ToplamBorc { get; set; }
        public decimal ToplamAlacak { get; set; }
        public List<FisSatirDto> Satirlar { get; set; } = new();
    }

    public class FisOzetDto
    {
        public int Id { get; set; }
        public short DonemYil { get; set; }
        public string FisNo { get; set; } = string.Empty;
        public DateTime Tarih { get; set; }
        public FisTuru FisTuru { get; set; }
        public string? BelgeNo { get; set; }
        public string? Aciklama { get; set; }
        public FisKaynak Kaynak { get; set; }
        public FisDurum Durum { get; set; }
        public int SatirSayisi { get; set; }
        public decimal ToplamBorc { get; set; }
        public decimal ToplamAlacak { get; set; }
    }

    /// <summary>
    /// Fiş satırı yazma isteği. <c>SiraNo</c> gönderilmez, sunucu listedeki sıraya göre üretir.
    /// Döviz satırında <see cref="Doviz"/> ve <see cref="Kur"/> zorunludur (iş kuralı 17);
    /// <see cref="Borc"/>/<see cref="Alacak"/> her zaman TL karşılığıdır.
    /// </summary>
    public class FisSatirYazDto
    {
        public int HesapId { get; set; }
        public int? MasrafMerkeziId { get; set; }
        public string? Aciklama { get; set; }
        public decimal Borc { get; set; }
        public decimal Alacak { get; set; }
        public string? ParaBirimi { get; set; }
        public decimal? Doviz { get; set; }
        public decimal? Kur { get; set; }
    }

    /// <summary>
    /// Fiş yazma isteği. Fiş numarası ve dönem yılı gönderilmez: numara firma + dönem
    /// bazında sunucu tarafından üretilir (iş kuralı 16), dönem yılı tarihten gelir.
    /// </summary>
    public class FisYazDto
    {
        public DateTime Tarih { get; set; }
        public FisTuru FisTuru { get; set; } = FisTuru.Mahsup;
        public string? BelgeNo { get; set; }
        public string? Aciklama { get; set; }
        public FisKaynak Kaynak { get; set; } = FisKaynak.Manuel;
        public bool Kesinlestir { get; set; }
        public List<FisSatirYazDto> Satirlar { get; set; } = new();
    }

    /// <summary>Ters kayıt isteği; alanlar boşsa kaynak fişin tarihi kullanılır ve taslak açılır.</summary>
    public class TersKayitDto
    {
        public DateTime? Tarih { get; set; }
        public string? Aciklama { get; set; }
        public bool Kesinlestir { get; set; }
    }

    /// <summary>Masraf merkezi seçeneği; <c>/catalog/muhasebe/masraf-merkezi</c> ucundan gelir.</summary>
    public class MasrafMerkeziSecenekDto
    {
        public int MasrafMerkeziId { get; set; }
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public bool Aktif { get; set; }

        public string Etiket => string.IsNullOrWhiteSpace(Kod) ? Ad : $"{Kod} — {Ad}";
    }
}
