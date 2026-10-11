using CatalogService.Api.Features.MevzuatGrafigi.Dtos;

namespace CatalogService.Api.Features.MevzuatGrafigi.Services
{
    /// <summary>
    /// Mevzuat grafiği. Kural ihlalinde <see cref="MevzuatGrafigiHatasi"/> fırlatır;
    /// rotadaki id bulunamazsa null/false döner.
    /// </summary>
    public interface IMevzuatGrafigiService
    {
        Task<MevzuatGrafigiDto> GetAsync(CancellationToken ct = default);

        Task<MevzuatKartDto> CreateKartAsync(MevzuatKartSaveDto dto, CancellationToken ct = default);
        Task<MevzuatKartDto?> UpdateKartAsync(int id, MevzuatKartSaveDto dto, CancellationToken ct = default);
        Task<bool> DeleteKartAsync(int id, CancellationToken ct = default);

        Task<MevzuatBaglantiDto> CreateBaglantiAsync(MevzuatBaglantiSaveDto dto, CancellationToken ct = default);
        Task<bool> DeleteBaglantiAsync(int id, CancellationToken ct = default);

        Task<MevzuatDislamaDto> CreateDislamaAsync(MevzuatDislamaSaveDto dto, CancellationToken ct = default);
        Task<bool> DeleteDislamaAsync(int id, CancellationToken ct = default);

        Task<MevzuatTuvalDto> CreateTuvalAsync(MevzuatTuvalSaveDto dto, CancellationToken ct = default);
        Task<MevzuatTuvalDto?> UpdateTuvalAsync(int id, MevzuatTuvalSaveDto dto, CancellationToken ct = default);
        Task<MevzuatYerlesimDto?> TuvaleKartEkleAsync(int tuvalId, MevzuatYerlesimDto dto, CancellationToken ct = default);
        Task<bool> TuvaldenKartKaldirAsync(int tuvalId, int kartId, CancellationToken ct = default);
        Task<List<MevzuatYerlesimDto>?> YerlesimGuncelleAsync(int tuvalId, List<MevzuatYerlesimDto> liste, CancellationToken ct = default);

        Task<MevzuatSartTakipDto> SartTakipKaydetAsync(MevzuatSartTakipDto dto, CancellationToken ct = default);
    }
}
