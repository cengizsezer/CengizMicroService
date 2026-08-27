using CatalogService.Api.Features.Muhasebe.Dtos;

namespace CatalogService.Api.Features.Muhasebe.Services
{
    /// <summary>
    /// Hesap planı iş kuralları (1–9). Controller yalnızca doğrulama ve yönlendirme yapar;
    /// tüm kurallar bu katmanda zorlanır.
    /// </summary>
    public interface IHesapPlaniService
    {
        /// <summary>
        /// Geçerli firmaya (tenant) tekdüzen hesap planını yükler. Plan doluysa dokunmaz,
        /// şablon dosyası yoksa bunu bildirir — sessizce geçmez (KARARLAR §84).
        /// </summary>
        Task<(PlanYuklemeSonuc Sonuc, int Adet)> TekDuzenPlaniYukleAsync(CancellationToken ct = default);

        /// <summary>Ağacın tamamı; düz liste + Yol, koda göre sıralı.</summary>
        Task<List<HesapPlaniDto>> GetHepsiAsync(CancellationToken ct = default);

        /// <summary>Kod veya isimde arama.</summary>
        Task<List<HesapPlaniDto>> AraAsync(string? q, CancellationToken ct = default);

        /// <summary>Fiş girişi seçim listesi: yalnızca hareket gören ve aktif hesaplar.</summary>
        Task<List<HesapPlaniDto>> GetHareketGorenlerAsync(CancellationToken ct = default);

        Task<HesapPlaniDto?> GetByIdAsync(int id, CancellationToken ct = default);

        /// <summary>Üst hesabın altındaki ilk boş segment. Üst hesap yoksa null.</summary>
        Task<SonrakiKodDto?> GetSonrakiKodAsync(int ustId, CancellationToken ct = default);

        /// <summary>Grubun altındaki kullanılmamış kebir kodları. Grup yoksa null.</summary>
        Task<List<BosKebirDto>?> GetBosKebirlerAsync(int grupId, CancellationToken ct = default);

        /// <summary>Hesap ekler. Üst hesap belirtilip bulunamazsa null.</summary>
        Task<HesapPlaniDto?> CreateAsync(HesapPlaniCreateDto dto, CancellationToken ct = default);

        Task<HesapPlaniDto?> UpdateAsync(int id, HesapPlaniUpdateDto dto, CancellationToken ct = default);

        /// <summary>Hesabı ve alt ağacını pasife çeker (iş kuralı 8).</summary>
        Task<HesapPlaniDto?> PasifeAlAsync(int id, CancellationToken ct = default);

        /// <summary>Yalnızca hareketsiz, alt hesabı olmayan kullanıcı hesabı silinebilir.</summary>
        Task<HesapSilmeSonuc> DeleteAsync(int id, CancellationToken ct = default);
    }
}
