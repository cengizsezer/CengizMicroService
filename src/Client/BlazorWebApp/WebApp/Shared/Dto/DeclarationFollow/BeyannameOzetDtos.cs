namespace CatalogService.Api.Features.Declarations.Entities
{
    /// <summary>
    /// Beyanname kaydına bağlanabilen belge türleri. Sunucudaki
    /// <c>BeyannameEkTuru</c>'nün aynası; sayısal değerler sözleşmenin parçası.
    /// </summary>
    public enum BeyannameEkTuru : byte
    {
        Tahakkuk = 1,
        Beyanname = 2,
        Dekont = 3
    }
}

namespace CatalogService.Api.Features.Declarations.Dtos
{
    using CatalogService.Api.Features.Declarations.Entities;

    /// <summary>Özet matrisinde bir hücrenin durumu; sıra ilerleyiş sırasıdır.</summary>
    public enum BeyannameHucreDurum : byte
    {
        Yok = 0,
        Hazirlandi = 1,
        Onaylandi = 2,
        Odendi = 3
    }

    public class BeyannameTuruDto
    {
        public int Id { get; set; }
        public string Deger { get; set; } = string.Empty;
        public string? Kod { get; set; }
        public string Ad { get; set; } = string.Empty;
        public int Sira { get; set; }
        public bool Aktif { get; set; }
    }

    /// <summary>
    /// Tanımlar ekranından sunucuya giden yazma isteği; sunucudaki
    /// <c>BeyannameTuruYazDto</c>'nun aynası.
    /// </summary>
    public class BeyannameTuruYazDto
    {
        public string Deger { get; set; } = string.Empty;
        public string? Kod { get; set; }
        public string Ad { get; set; } = string.Empty;
        public int Sira { get; set; }
        public bool Aktif { get; set; } = true;
    }

    public class BeyannameEkTuruDto
    {
        public int EkId { get; set; }
        public BeyannameEkTuru Tur { get; set; }
    }

    public class BeyannameOzetHucreDto
    {
        public int TuruId { get; set; }
        public int? DeclarationId { get; set; }
        public BeyannameHucreDurum Durum { get; set; }
        public decimal Tutar { get; set; }
        public int KayitSayisi { get; set; }
        public List<BeyannameEkTuruDto> Ekler { get; set; } = new();

        /// <summary>Hücrenin arkasındaki beyanname kayıtları; detay penceresi bunları gösterir.</summary>
        public List<DeclarationDto> Kayitlar { get; set; } = new();
    }

    public class BeyannameOzetSatirDto
    {
        public int Sira { get; set; }
        public int CustomerCompanyId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;
        public string? VergiKimlikNo { get; set; }
        public List<BeyannameOzetHucreDto> Hucreler { get; set; } = new();
        public int DoluHucreSayisi { get; set; }
        public decimal ToplamTutar { get; set; }
    }

    public class BeyannameOzetKolonToplamDto
    {
        public int TuruId { get; set; }
        public int DoluHucreSayisi { get; set; }
        public decimal ToplamTutar { get; set; }
    }

    public class BeyannameOzetDto
    {
        public int Yil { get; set; }
        public int Ay { get; set; }
        public List<BeyannameTuruDto> Turler { get; set; } = new();
        public List<BeyannameOzetSatirDto> Satirlar { get; set; } = new();
        public List<BeyannameOzetKolonToplamDto> KolonToplamlari { get; set; } = new();
        public int ToplamBeyanname { get; set; }
        public decimal ToplamTutar { get; set; }
        public List<string> EslesmeyenTurler { get; set; } = new();
    }

    public class BeyannameEkDto
    {
        public int Id { get; set; }
        public int DeclarationId { get; set; }
        public BeyannameEkTuru Tur { get; set; }
        public int FileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/pdf";
        public long Length { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? YukleyenKullanici { get; set; }
    }

    public class BeyannameEkOlusturDto
    {
        public BeyannameEkTuru Tur { get; set; }
        public int FileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/pdf";
        public long Length { get; set; }
    }

    /// <summary>
    /// Ek kaydı yanıtı. <see cref="ArtikFileId"/> dolu geldiğinde aynı türden eski bir
    /// belge değiştirilmiştir; istemci o dosyayı FileApiService'ten silmelidir.
    /// </summary>
    public class BeyannameEkSonucDto
    {
        public BeyannameEkDto? Ek { get; set; }
        public int? ArtikFileId { get; set; }
    }
}
