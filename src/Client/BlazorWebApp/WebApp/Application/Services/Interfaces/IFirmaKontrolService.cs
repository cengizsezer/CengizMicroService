using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.Application.Services.Interfaces
{
    public class MizanUpdateResult
    {
        public int Matched { get; set; }
        public int Unmatched { get; set; }
        public List<string> UnmatchedKodlar { get; set; } = new();
    }

    public interface IFirmaKontrolService
    {
        Task<IReadOnlyList<Firma>> GetFirmsAsync();

        Task<Firma?> GetFirmAsync(int firmaId);

        Task<IReadOnlyList<ControlItem>> GetControlItemsAsync(int firmaId);

        Task UpdateControlItemAsync(int firmaId, ControlItem item);

        Task<HesapPlani> GetMizanAsync(int firmaId);

        Task<MizanUpdateResult> UpdateMizanFromExcelAsync(int firmaId, IEnumerable<MizanExcelRow> rows, Donem donem);

        Task ResetMizanAsync(int firmaId);

        Task<IReadOnlyDictionary<string, decimal?>> GetRawMizanValuesAsync(int firmaId, Donem donem);

        Task<VergiHesaplama> GetVergiBilgisiAsync(int firmaId);
    }
}
