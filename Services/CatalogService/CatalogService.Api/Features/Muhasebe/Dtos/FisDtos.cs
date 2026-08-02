using CatalogService.Api.Features.Muhasebe.Domain;

namespace CatalogService.Api.Features.Muhasebe.Dtos
{
    /// <summary>Fiş satırı (okuma). Hesap ve masraf merkezi bilgileri gösterim için düzleştirilmiştir.</summary>
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
        public string ParaBirimi { get; set; } = FisParaBirimi.Yerel;

        /// <summary>Döviz satırında döviz tutarı; TL satırında null.</summary>
        public decimal? Doviz { get; set; }

        /// <summary>Döviz satırında kur; TL satırında null.</summary>
        public decimal? Kur { get; set; }
    }

    /// <summary>Fiş (okuma), satırlarıyla birlikte.</summary>
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

    /// <summary>Fiş listesi satırı; satır detayı taşımaz.</summary>
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
    /// Fiş satırı yazma isteği. <see cref="SiraNo"/> istenmez; listedeki sıraya göre servis üretir.
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
    /// Fiş yazma isteği (ekleme ve güncelleme ortak). Fiş numarası ve dönem yılı
    /// istenmez: numara firma + dönem bazında servis tarafından üretilir (iş kuralı 16),
    /// dönem yılı <see cref="Tarih"/>ten gelir.
    /// </summary>
    public class FisYazDto
    {
        public DateTime Tarih { get; set; }
        public FisTuru FisTuru { get; set; } = FisTuru.Mahsup;
        public string? BelgeNo { get; set; }
        public string? Aciklama { get; set; }
        public FisKaynak Kaynak { get; set; } = FisKaynak.Manuel;

        /// <summary>İşaretliyse fiş kesinleşmiş kaydedilir; aksi hâlde taslak kalır.</summary>
        public bool Kesinlestir { get; set; }

        public List<FisSatirYazDto> Satirlar { get; set; } = new();
    }

    /// <summary>
    /// Ters kayıt isteği. Alanlar boş bırakılırsa kaynak fişin tarihi kullanılır ve
    /// ters kayıt taslak olarak açılır; kullanıcı gözden geçirip kesinleştirir.
    /// </summary>
    public class TersKayitDto
    {
        public DateTime? Tarih { get; set; }
        public string? Aciklama { get; set; }
        public bool Kesinlestir { get; set; }
    }

    /// <summary>Fiş listesi filtresi.</summary>
    public class FisFiltreDto
    {
        public DateTime? Bas { get; set; }
        public DateTime? Bit { get; set; }
        public FisDurum? Durum { get; set; }

        /// <summary>Verilirse yalnızca bu hesabın geçtiği fişler döner.</summary>
        public int? HesapId { get; set; }
    }

    /// <summary>Fiş silme sonucu (iş kuralı 15).</summary>
    public enum FisSilmeSonuc
    {
        Silindi = 0,
        Bulunamadi = 1,
        Kesinlesmis = 2
    }

    /// <summary>Para birimi sabitleri. Yerel para birimi dışındaki satırlar döviz satırıdır.</summary>
    public static class FisParaBirimi
    {
        public const string Yerel = "TRY";
    }
}
