using WebApp.Domain.Models.KdvBeyanname;

namespace WebApp.Application.Services.KdvBeyanname
{
    public interface IKdvBeyannameApiService
    {
        Task<List<KdvFirmaCardDto>> GetFirmalarAsync(CancellationToken ct = default);
        Task<bool> TaraTetikleAsync(int firmaId, TaraTetikleRequest req, CancellationToken ct = default);
        Task<List<KdvTarama>> GetTaramalarAsync(int firmaId, int take = 20, CancellationToken ct = default);

        Task<List<KdvGelenFatura>> GetGelenFaturalarAsync(
            int firmaId, DateTime? baslangic = null, DateTime? bitis = null, CancellationToken ct = default);

        Task<List<KdvMizanSatir>> GetMizanAsync(int firmaId, string donem, CancellationToken ct = default);
        Task<List<KdvYevmiyeSatir>> GetYevmiyeAsync(int firmaId, string donem, CancellationToken ct = default);

        Task<MizanUploadResult> UploadMizanAsync(int firmaId, string donem, Stream content, string fileName, CancellationToken ct = default);
        Task<YevmiyeUploadResult> UploadYevmiyeAsync(int firmaId, string donem, Stream content, string fileName, CancellationToken ct = default);

        // Yevmiye Excel'inde beklenen sütun başlıkları (backend tek kaynağı).
        Task<List<BeklenenKolon>> GetYevmiyeBeklenenBasliklarAsync(CancellationToken ct = default);

        Task<KdvKarsilastirmaSonucu> GetKarsilastirmaAsync(int firmaId, string donem, CancellationToken ct = default);
        Task<KdvSonuc> GetSonucAsync(int firmaId, string donem, CancellationToken ct = default);

        // XML indirme — backend byte[] döndürüyor. Wrapper hem byte hem dosya adını verir.
        Task<(byte[] Content, string FileName)?> IndirXmlAsync(int firmaId, string donem, CancellationToken ct = default);

        // Düzenleyen CRUD (SMMM/YMM kayıtları)
        Task<List<KdvDuzenleyen>> ListDuzenleyenlerAsync(bool includeInactive = false, CancellationToken ct = default);
        Task<KdvDuzenleyen?> GetDuzenleyenByIdAsync(int id, CancellationToken ct = default);
        Task<KdvDuzenleyen> CreateDuzenleyenAsync(KdvDuzenleyenUpsert dto, CancellationToken ct = default);
        Task<KdvDuzenleyen?> UpdateDuzenleyenAsync(int id, KdvDuzenleyenUpsert dto, CancellationToken ct = default);
        Task<bool> DeleteDuzenleyenAsync(int id, CancellationToken ct = default);
    }
}
