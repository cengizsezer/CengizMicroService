using WebApp.Shared.Dto.FirmaKontrol;

namespace WebApp.Application.Services.FirmaKontrol
{
    /// <summary>
    /// CatalogService "firma-kontrol" endpoint'lerine erişim (kontrol maddesi durumları
    /// + özel maddeler). Auth+tenant header pipeline'lı HttpClient üzerinden gider.
    /// </summary>
    public interface IFirmaKontrolApiClient
    {
        Task<List<FirmaKontrolMaddeDto>> GetMaddelerAsync(int firmaId, CancellationToken ct = default);
        Task UpsertMaddeAsync(int firmaId, FirmaKontrolMaddeUpsertDto dto, CancellationToken ct = default);
        Task<FirmaKontrolMaddeDto> AddOzelAsync(int firmaId, OzelMaddeCreateDto dto, CancellationToken ct = default);
        Task<FirmaKontrolMaddeDto> UpdateOzelAsync(int firmaId, long id, OzelMaddeUpdateDto dto, CancellationToken ct = default);
        Task DeleteOzelAsync(int firmaId, long id, CancellationToken ct = default);

        // ── Ham mizan ───────────────────────────────────────────────────────
        Task<List<FirmaKontrolMizanSatirDto>> GetMizanAsync(int firmaId, int yil, CancellationToken ct = default);
        Task SaveMizanAsync(int firmaId, MizanKaydetRequest req, CancellationToken ct = default);
        Task DeleteMizanAsync(int firmaId, int yil, CancellationToken ct = default);

        // ── Mizan hesap notları ─────────────────────────────────────────────

        /// <summary>Kalıcı notlar + verilen yılın dönem notları. yil null ise tümü.</summary>
        Task<List<MizanNotuDto>> GetMizanNotlariAsync(int firmaId, int? yil, CancellationToken ct = default);

        Task<MizanNotuDto> UpsertMizanNotuAsync(int firmaId, MizanNotuUpsertDto dto, CancellationToken ct = default);

        /// <summary>Mevcut notu Id ile günceller; tip (kalıcı ↔ dönem) burada değişebilir.</summary>
        Task<MizanNotuDto> GuncelleMizanNotuAsync(int firmaId, long id, MizanNotuGuncelleDto dto, CancellationToken ct = default);

        /// <summary>"Güncel say": not metnine dokunmadan snapshot'ı güncel bakiyeyle tazeler.</summary>
        Task<MizanNotuDto> SnapshotYenileAsync(int firmaId, long id, CancellationToken ct = default);

        Task DeleteMizanNotuAsync(int firmaId, long id, CancellationToken ct = default);

        /// <summary>Kaynak yılın, hedef yılda karşılığı olmayan dönem notları.</summary>
        Task<List<MizanNotuDto>> GetDevirAdaylariAsync(int firmaId, int kaynakYil, int hedefYil, CancellationToken ct = default);

        /// <summary>Seçilen notları hedef yıla kopyalar; oluşan yeni notlar döner.</summary>
        Task<List<MizanNotuDto>> DevretMizanNotlariAsync(int firmaId, MizanNotuDevirRequest req, CancellationToken ct = default);

        // ── Vergi paneli girdileri ──────────────────────────────────────────
        Task<FirmaKontrolVergiDto?> GetVergiAsync(int firmaId, int donem, int yil, CancellationToken ct = default);
        Task SaveVergiAsync(int firmaId, FirmaKontrolVergiDto dto, CancellationToken ct = default);
    }
}
