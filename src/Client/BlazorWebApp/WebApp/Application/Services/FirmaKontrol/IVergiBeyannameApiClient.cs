using WebApp.Shared.Dto.FirmaKontrol;

namespace WebApp.Application.Services.FirmaKontrol
{
    /// <summary>Kurumlar vergisi beyannamesi istemcisi.</summary>
    public interface IVergiBeyannameApiClient
    {
        Task<List<VergiKalemiDto>> GetKalemlerAsync(bool pasifDahil = false, CancellationToken ct = default);
        Task<VergiKalemiDto> KalemEkleAsync(VergiKalemiYazDto dto, CancellationToken ct = default);
        Task<VergiKalemiDto> KalemGuncelleAsync(int id, VergiKalemiYazDto dto, CancellationToken ct = default);
        Task<VergiKalemiDto> KalemPasifeAlAsync(int id, CancellationToken ct = default);
        Task KalemSilAsync(int id, CancellationToken ct = default);
        Task SiralamayiKaydetAsync(List<VergiKalemSiraDto> sira, CancellationToken ct = default);

        /// <summary>Kayıt yoksa null döner (sunucu 204).</summary>
        Task<VergiBeyannameDto?> GetBeyannameAsync(int firmaId, short donemYil, CancellationToken ct = default);

        /// <summary>Kaydetmeden hesaplar; ekranın canlı önizlemesi bunu kullanır.</summary>
        Task<VergiSonucDto> OnizleAsync(VergiBeyannameYazDto dto, CancellationToken ct = default);

        Task<VergiBeyannameDto> KaydetAsync(int firmaId, VergiBeyannameYazDto dto, CancellationToken ct = default);

        /// <summary>Sunucuda üretilen .xlsx; kayıt yoksa null döner.</summary>
        Task<(byte[] Icerik, string DosyaAdi)?> ExcelAsync(int firmaId, short donemYil, CancellationToken ct = default);
    }
}
