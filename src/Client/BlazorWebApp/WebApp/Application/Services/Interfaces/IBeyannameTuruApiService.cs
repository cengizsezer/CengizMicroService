using CatalogService.Api.Features.Declarations.Dtos;

namespace WebApp.Application.Services.Interfaces
{
    /// <summary>
    /// Beyanname türü tanımlarının <b>tek</b> istemcisi. Takip sekmesinin açılır listesi,
    /// Özet matrisinin kolonları ve Tanımlar ekranı aynı kaynaktan okur; Takip'te duran
    /// sabit <c>List&lt;string&gt;</c> kaldırıldı.
    /// </summary>
    public interface IBeyannameTuruApiService
    {
        /// <summary><paramref name="pasifDahil"/> yalnız yönetim ekranında true.</summary>
        Task<List<BeyannameTuruDto>> GetHepsiAsync(bool pasifDahil = false, CancellationToken ct = default);

        /// <summary>Hata metni null ise kayıt başarılı.</summary>
        Task<(BeyannameTuruDto? Kayit, string? Hata)> EkleAsync(BeyannameTuruYazDto dto,
                                                                CancellationToken ct = default);

        Task<(BeyannameTuruDto? Kayit, string? Hata)> GuncelleAsync(int id, BeyannameTuruYazDto dto,
                                                                    CancellationToken ct = default);

        /// <summary>Eksik varsayılan tanımları yükler; eklenen sayısı ve hata metni döner.</summary>
        Task<(int Eklenen, int Toplam, string? Hata)> VarsayilanlariYukleAsync(CancellationToken ct = default);
    }
}
