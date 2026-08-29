using CatalogService.Api.Features.Declarations.Dtos;

namespace WebApp.Application.Services.Interfaces
{
    /// <summary>
    /// Beyanname özeti (firma × tür matrisi), tür tanımları ve beyanname belgeleri.
    /// Takip ekranının kendi istemcisi (<see cref="IDeclarationApiService"/>) değişmedi.
    /// </summary>
    public interface IBeyannameOzetApiService
    {
        Task<List<BeyannameTuruDto>> TurleriGetAsync(CancellationToken ct = default);

        Task<BeyannameOzetDto?> OzetGetAsync(int yil, int ay, CancellationToken ct = default);

        Task<List<BeyannameEkDto>> EkleriGetAsync(int declarationId, CancellationToken ct = default);

        /// <summary>
        /// Belge kaydı. Dosya <b>önce</b> FileApiService'e yüklenmiş olmalı; buraya dönen
        /// <c>FileId</c> gelir. Yanıttaki <c>ArtikFileId</c> dolu ise o dosya artık
        /// sahipsizdir ve FileApiService'ten silinmelidir.
        /// </summary>
        Task<BeyannameEkSonucDto?> EkEkleAsync(int declarationId, BeyannameEkOlusturDto istek,
                                               CancellationToken ct = default);

        /// <summary>Kaydı siler ve artık sahipsiz kalan FileApiService dosyasının kimliğini döner.</summary>
        Task<int?> EkSilAsync(int declarationId, int ekId, CancellationToken ct = default);
    }
}
