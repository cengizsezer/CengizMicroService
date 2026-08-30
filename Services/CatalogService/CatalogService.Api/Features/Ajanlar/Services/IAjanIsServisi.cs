using CatalogService.Api.Features.Ajanlar.Domain;
using CatalogService.Api.Features.Ajanlar.Dtos;

namespace CatalogService.Api.Features.Ajanlar.Services
{
    /// <summary>
    /// İşin ömrü: oluşturma, ajana gönderme, ajandan gelen bildirimler, iptal ve
    /// zaman aşımı.
    ///
    /// Hub yalnız taşıma katmanı; kurallar burada. Böylece "aynı ajana tek iş",
    /// "yalnız kendi işini bildirebilir" ve "aynı bildirim iki kez zararsız" gibi
    /// kararlar gerçek bir sokete ihtiyaç duymadan sınanabiliyor.
    /// </summary>
    public interface IAjanIsServisi
    {
        Task<AjanIsiOlusturSonucuDto> OlusturAsync(YeniAjanIsiDto istek, string kullaniciId, CancellationToken ct = default);

        Task<AjanIsDto?> GetirAsync(Guid id, CancellationToken ct = default);

        Task<List<AjanIsDto>> ListeleAsync(int? firmaId, AjanIsDurumu? durum, string? ajanId,
                                           int enFazla = 50, CancellationToken ct = default);

        /// <summary>İptal eder ve ajan bağlıysa haber verir. Bitmiş iş iptal edilemez.</summary>
        Task<AjanIsDto?> IptalAsync(Guid id, CancellationToken ct = default);

        // ---- ajanın bildirdiklerini işleyen taraf --------------------------

        Task<bool> BasladiAsync(string ajanId, Guid isId, CancellationToken ct = default);

        Task<bool> IlerlemeAsync(string ajanId, Guid isId, int yuzde, string? mesaj,
                                 int? tamamlananAdim, CancellationToken ct = default);

        Task<bool> BittiAsync(string ajanId, Guid isId, bool basarili, string? hataMesaji,
                              string? sonucOzetiJson, string? hataEkraniDosyaId = null,
                              CancellationToken ct = default);

        // ---- hub olayları --------------------------------------------------

        /// <summary>Ajan bağlandı: bekleyen işleri ona gönder.</summary>
        Task BekleyenleriGonderAsync(string ajanId, CancellationToken ct = default);

        /// <summary>
        /// Ajanın bağlantısı koptu: çalışan işi başarısız yap. İş yarım kaldı ve
        /// ajan onu bildiremeden gitti; "Calisiyor" bırakmak, sonsuza kadar
        /// meşgul görünen bir ajan demek olurdu.
        /// </summary>
        Task BaglantiKoptuAsync(string ajanId, CancellationToken ct = default);
    }
}
