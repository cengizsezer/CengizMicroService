using WebApp.Application.RuleEngine;
using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.Application.Services.Interfaces
{
    public class MizanUpdateResult
    {
        public int Matched { get; set; }

        /// <summary>
        /// Üst yeşil mesajda gösterilen "atlanan" sayısı — SADECE 3 haneli geçerli
        /// kod olup hesap planında bulunamayan satırlar. Hiyerarşik alt kodlar
        /// (parser tarafından kasıtlı atlananlar) bu sayıya dahil DEĞİLDİR;
        /// onlar AtlananSatirlar listesinde HiyerarsikAltKod sebebi ile yer alır.
        /// </summary>
        public int Unmatched { get; set; }

        public List<string> UnmatchedKodlar { get; set; } = new();

        /// <summary>
        /// Detay panelinde gösterilecek tüm atlanan satırlar (parser elidikleri +
        /// service'in plan'da bulamadıkları). UI sebep bazlı gruplandırır.
        /// </summary>
        public List<AtlananSatir> AtlananSatirlar { get; set; } = new();
    }

    public interface IFirmaKontrolService
    {
        Task<IReadOnlyList<Firma>> GetFirmsAsync();

        Task<Firma?> GetFirmAsync(int firmaId);

        Task<IReadOnlyList<ControlItem>> GetControlItemsAsync(int firmaId);

        Task UpdateControlItemAsync(int firmaId, ControlItem item);

        Task<HesapPlani> GetMizanAsync(int firmaId);

        Task<MizanUpdateResult> UpdateMizanFromExcelAsync(int firmaId, MizanParseResult parseResult, Donem donem);

        Task ResetMizanAsync(int firmaId);

        Task<IReadOnlyDictionary<string, decimal?>> GetRawMizanValuesAsync(int firmaId, Donem donem);

        /// <summary>
        /// Mizandan gelen kod -> hesap adı eşleşmesi (PROGROUP formatında B sütunundan).
        /// Mizan tab'ı ve diğer UI'lar HesapPlani'nde ad bulamadığında fallback olarak kullanır.
        /// </summary>
        Task<IReadOnlyDictionary<string, string>> GetRawMizanAdlarAsync(int firmaId);

        Task<VergiHesaplama> GetVergiBilgisiAsync(int firmaId);

        Task<IReadOnlyList<UyariSonucu>> GetUyarilarAsync(int firmaId);
    }
}
