using CatalogService.Api.Features.FinansmanGiderKisitlamasi.Dtos;

namespace CatalogService.Api.Features.FinansmanGiderKisitlamasi.Services
{
    public interface IFinansmanGiderKisitlamasiService
    {
        /// <summary>Yılın oranını okuyup motoru çalıştırır. Oran yoksa
        /// <see cref="FinansmanKisitlamaOraniYokException"/> fırlatır.</summary>
        Task<FinansmanKisitlamaSonucDto> HesaplaAsync(FinansmanKisitlamaHesapRequest request, CancellationToken ct = default);

        Task<List<FinansmanKisitlamaOraniDto>> GetOranlarAsync(CancellationToken ct = default);
        Task<FinansmanKisitlamaOraniDto?> GetOranAsync(int yil, CancellationToken ct = default);

        /// <summary>Yıl bazlı upsert — kayıt varsa güncellenir, yoksa eklenir.</summary>
        Task<FinansmanKisitlamaOraniDto> UpsertOranAsync(int yil, FinansmanKisitlamaOraniSaveDto dto, CancellationToken ct = default);

        Task<bool> DeleteOranAsync(int yil, CancellationToken ct = default);
    }
}
