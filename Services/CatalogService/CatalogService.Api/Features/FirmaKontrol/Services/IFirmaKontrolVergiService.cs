using CatalogService.Api.Features.FirmaKontrol.Dtos;

namespace CatalogService.Api.Features.FirmaKontrol.Services
{
    public interface IFirmaKontrolVergiService
    {
        /// <summary>Firmanın (dönem, yıl) vergi girdileri. Kayıt yoksa null.</summary>
        Task<FirmaKontrolVergiDto?> GetAsync(int firmaId, int donem, int yil, CancellationToken ct = default);

        /// <summary>Vergi girdilerini (FirmaId, Donem, Yil) bazında upsert eder (tek satır).</summary>
        Task UpsertAsync(int firmaId, FirmaKontrolVergiDto dto, CancellationToken ct = default);
    }
}
