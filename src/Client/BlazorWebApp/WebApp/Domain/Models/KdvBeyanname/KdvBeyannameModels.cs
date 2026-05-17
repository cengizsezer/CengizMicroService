namespace WebApp.Domain.Models.KdvBeyanname
{
    public enum KdvBeyannameTaramaDurumu
    {
        Beklemede = 0,
        Calisiyor = 1,
        Tamamlandi = 2,
        Hatali = 3
    }

    public class KdvFirmaCardDto
    {
        public int Id { get; set; }
        public string VergiKimlikNo { get; set; } = string.Empty;
        public string Unvan { get; set; } = string.Empty;
        public string KisaAd { get; set; } = string.Empty;
        public bool HasDpHesabi { get; set; }
        public bool BdpAlanlariEksik { get; set; }
        public DateTime? SonTaramaAt { get; set; }
        public KdvBeyannameTaramaDurumu? SonTaramaDurumu { get; set; }
    }

    public class KdvTarama
    {
        public long Id { get; set; }
        public int FirmaId { get; set; }
        public DateTime BaslangicTarihi { get; set; }
        public DateTime BitisTarihi { get; set; }
        public KdvBeyannameTaramaDurumu Durum { get; set; }
        public DateTime BaslangicAt { get; set; }
        public DateTime? BitisAt { get; set; }
        public string? HataMesaji { get; set; }
        public int? BulunanFaturaSayisi { get; set; }
    }

    public class KdvGelenFatura
    {
        public long Id { get; set; }
        public string FaturaNo { get; set; } = string.Empty;
        public string GondericiVkn { get; set; } = string.Empty;
        public string GondericiUnvan { get; set; } = string.Empty;
        public DateTime? FaturaTarihi { get; set; }
        public string ParaBirimi { get; set; } = string.Empty;
        public decimal MatrahTutari { get; set; }
        public decimal KdvTutari { get; set; }
        public decimal ToplamTutar { get; set; }
        public decimal? KdvOrani { get; set; }
        public string? Statu { get; set; }
        public DateTime SonGuncelleme { get; set; }
    }

    public class KdvMizanSatir
    {
        public long Id { get; set; }
        public string HesapKodu { get; set; } = string.Empty;
        public string HesapAdi { get; set; } = string.Empty;
        public decimal BorcToplam { get; set; }
        public decimal AlacakToplam { get; set; }
        public decimal BorcKalan { get; set; }
        public decimal AlacakKalan { get; set; }
    }

    public class KdvYevmiyeSatir
    {
        public long Id { get; set; }
        public DateTime? Tarih { get; set; }
        public string HesapKodu { get; set; } = string.Empty;
        public string HesapAdi { get; set; } = string.Empty;
        public string? BelgeTipi { get; set; }
        public string? FaturaNo { get; set; }
        public decimal Borc { get; set; }
        public decimal Alacak { get; set; }
        public string? Aciklama { get; set; }
        public string? FisNo { get; set; }
    }

    public class KdvKarsilastirmaSonucu
    {
        public string Donem { get; set; } = string.Empty;
        public int GelenFaturaSayisi { get; set; }
        public int YevmiyeFaturaNoSayisi { get; set; }
        public int ReddedilenSayisi { get; set; }
        public List<KdvKarsilastirmaSatiri> Eslesen { get; set; } = new();
        public List<KdvKarsilastirmaSatiri> Islenmemis { get; set; } = new();
        public List<KdvKarsilastirmaSatiri> Fazla { get; set; } = new();
    }

    public class KdvKarsilastirmaSatiri
    {
        public string FaturaNo { get; set; } = string.Empty;
        public string? GondericiVkn { get; set; }
        public string? GondericiUnvan { get; set; }
        public DateTime? FaturaTarihi { get; set; }
        public decimal? ToplamTutar { get; set; }
        public decimal? KdvTutari { get; set; }
        public string? YevmiyeHesapKodu { get; set; }
        public string? YevmiyeAciklamasi { get; set; }
    }

    public class KdvOranKirilim
    {
        public string HesapKodu { get; set; } = string.Empty;
        public string HesapAdi { get; set; } = string.Empty;
        public int Oran { get; set; }
        public decimal Bedel { get; set; }
        public decimal KdvTutari { get; set; }
    }

    public class KdvBdpEksiklik
    {
        public bool VarMi { get; set; }
        public List<string> FirmaAlanlari { get; set; } = new();
        public List<string> DuzenleyenAlanlari { get; set; } = new();
        public List<string> MizanHatalari { get; set; } = new();
        public List<string> MizanUyarilari { get; set; } = new();
    }

    public class KdvSonuc
    {
        public string Donem { get; set; } = string.Empty;
        public int Yil { get; set; }
        public int Ay { get; set; }
        public KdvBdpEksiklik Eksiklikler { get; set; } = new();

        public decimal Hesaplanan391 { get; set; }
        public decimal Indirilecek191 { get; set; }
        public decimal Devreden190 { get; set; }
        public decimal Satislar600 { get; set; }

        public decimal ToplamIndirilecek { get; set; }
        public decimal Fark { get; set; }
        public decimal OdenmesiGerekenKDV { get; set; }
        public decimal SonrakiDonemeDevredenKDV { get; set; }

        public List<KdvOranKirilim> TevkifatUygulanmayanlar { get; set; } = new();
        public List<KdvOranKirilim> IndirilecekKdvODler { get; set; } = new();
    }

    public class KdvDuzenleyen
    {
        public int Id { get; set; }
        public string Kisaltma { get; set; } = string.Empty;
        public string Vkn { get; set; } = string.Empty;
        public string? Soyadi { get; set; }
        public string? Adi { get; set; }
        public string? TicaretSicilNo { get; set; }
        public string? Eposta { get; set; }
        public string? AlanKodu { get; set; }
        public string? TelNo { get; set; }
        public bool Aktif { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    // Create/Update request payload — Id, CreatedAt, UpdatedAt yok.
    public class KdvDuzenleyenUpsert
    {
        public string Kisaltma { get; set; } = string.Empty;
        public string Vkn { get; set; } = string.Empty;
        public string? Soyadi { get; set; }
        public string? Adi { get; set; }
        public string? TicaretSicilNo { get; set; }
        public string? Eposta { get; set; }
        public string? AlanKodu { get; set; }
        public string? TelNo { get; set; }
        public bool Aktif { get; set; } = true;
    }

    public class TaraTetikleRequest
    {
        public DateTime BaslangicTarihi { get; set; }
        public DateTime BitisTarihi { get; set; }
    }

    public class MizanUploadResult
    {
        public int OkunanSatir { get; set; }
        public int KaydedilenSatir { get; set; }
        public int AtlananSatir { get; set; }
        public List<string> Uyarilar { get; set; } = new();
        public List<string> Hatalar { get; set; } = new();
    }

    public class YevmiyeUploadResult
    {
        public int OkunanSatir { get; set; }
        public int KaydedilenSatir { get; set; }
        public int AtlananSatir { get; set; }
        public List<string> Uyarilar { get; set; } = new();
        public List<string> Hatalar { get; set; } = new();
    }
}
