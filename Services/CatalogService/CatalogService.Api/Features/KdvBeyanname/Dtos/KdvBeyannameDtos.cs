using System.ComponentModel.DataAnnotations;
using CatalogService.Api.Features.KdvBeyanname.Domain;

namespace CatalogService.Api.Features.KdvBeyanname.Dtos
{
    // ── Firma kart listesi ─────────────────────────────────────────────────

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

    // ── Tarama tetikleme ───────────────────────────────────────────────────

    public class TaramaTetikleRequest
    {
        [Required] public DateTime BaslangicTarihi { get; set; }
        [Required] public DateTime BitisTarihi { get; set; }
    }

    public class TaramaDto
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

    // ── Tab 1: Gelen Faturalar ─────────────────────────────────────────────

    public class GelenFaturaDto
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

    // ── Düzenleyen Bilgileri (CRUD — SMMM/YMM kayıtları) ───────────────────

    // Hem GET list/single hem POST/PUT response için kullanılır.
    public class DuzenleyenDto
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

    public class DuzenleyenCreateDto
    {
        [Required, StringLength(50)]
        public string Kisaltma { get; set; } = string.Empty;

        [Required, StringLength(20, MinimumLength = 10)]
        public string Vkn { get; set; } = string.Empty;

        [StringLength(200)] public string? Soyadi { get; set; }
        [StringLength(200)] public string? Adi { get; set; }
        [StringLength(50)]  public string? TicaretSicilNo { get; set; }
        [StringLength(200), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
        public string? Eposta { get; set; }
        [StringLength(5)]   public string? AlanKodu { get; set; }
        [StringLength(20)]  public string? TelNo { get; set; }
        public bool Aktif { get; set; } = true;
    }

    public class DuzenleyenUpdateDto : DuzenleyenCreateDto { }
}
