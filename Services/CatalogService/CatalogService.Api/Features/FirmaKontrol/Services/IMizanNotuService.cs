using CatalogService.Api.Features.FirmaKontrol.Dtos;

namespace CatalogService.Api.Features.FirmaKontrol.Services
{
    public interface IMizanNotuService
    {
        /// <summary>
        /// Mizan ekranının ihtiyacı olan notlar: kalıcı notlar (DonemYili = null) +
        /// verilen yılın dönem notları. <paramref name="yil"/> null ise firmanın
        /// tüm notları döner.
        /// </summary>
        Task<List<MizanNotuDto>> GetNotlarAsync(int firmaId, int? yil, CancellationToken ct = default);

        /// <summary>
        /// Notu yazar. (FirmaId, HesapKodu, DonemYili) tekil olduğundan mevcut kayıt
        /// varsa güncellenir, yoksa oluşturulur.
        /// </summary>
        Task<MizanNotuDto> UpsertAsync(int firmaId, MizanNotuUpsertDto dto, CancellationToken ct = default);

        /// <summary>
        /// Mevcut notu Id üzerinden günceller. Tip (kalıcı ↔ dönem notu) burada
        /// değişebilir; hedef tipte zaten not varsa <see cref="InvalidOperationException"/> atar.
        /// </summary>
        Task<MizanNotuDto> GuncelleAsync(int firmaId, long id, MizanNotuGuncelleDto dto, CancellationToken ct = default);

        /// <summary>
        /// Notun metnine dokunmadan snapshot alanlarını güncel mizan bakiyesiyle
        /// tazeler ("Güncel say"). Hesap mizanda yoksa snapshot korunur ve
        /// <see cref="InvalidOperationException"/> atılır.
        /// </summary>
        Task<MizanNotuDto> SnapshotYenileAsync(int firmaId, long id, CancellationToken ct = default);

        Task<bool> SilAsync(int firmaId, long id, CancellationToken ct = default);

        /// <summary>
        /// Dönem devrinde kullanıcıya sunulacak adaylar: kaynak yılın, hedef yılda
        /// karşılığı henüz olmayan dönem notları. Kalıcı notlar listeye girmez.
        /// </summary>
        Task<List<MizanNotuDto>> DevirAdaylariAsync(int firmaId, int kaynakYil, int hedefYil, CancellationToken ct = default);

        /// <summary>
        /// Seçilen dönem notlarını hedef yıla kopyalar; metnin sonuna "(2025'ten devir)"
        /// etiketi eklenir. Hedefte aynı hesap için not varsa o kayıt atlanır.
        /// Oluşturulan yeni notlar döner.
        /// </summary>
        Task<List<MizanNotuDto>> DevretAsync(int firmaId, MizanNotuDevirRequest req, CancellationToken ct = default);
    }
}
