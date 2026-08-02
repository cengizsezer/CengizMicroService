using CatalogService.Api.Features.FirmaKontrol.Dtos;

namespace CatalogService.Api.Features.FirmaKontrol.Services
{
    /// <summary>
    /// Kurumlar vergisi beyanname kalemleri ve firma bazlı beyanname girdileri.
    /// Tüm iş kuralları burada ve <see cref="VergiHesaplamaMotoru"/> içindedir;
    /// controller yalnızca yönlendirme yapar.
    /// </summary>
    public interface IVergiBeyannameService
    {
        // ── Kalem katalogu ──

        Task<List<VergiKalemiDto>> GetKalemlerAsync(bool pasifDahil = false, CancellationToken ct = default);

        Task<VergiKalemiDto?> GetKalemAsync(int id, CancellationToken ct = default);

        /// <exception cref="VergiKuralException">Kod boş/tekrarlı ya da alanlar geçersizse.</exception>
        Task<VergiKalemiDto> KalemEkleAsync(VergiKalemiYazDto dto, CancellationToken ct = default);

        /// <summary>Sistem kaleminde kod ve grup değiştirilemez; diğer alanlar güncellenir.</summary>
        Task<VergiKalemiDto?> KalemGuncelleAsync(int id, VergiKalemiYazDto dto, CancellationToken ct = default);

        /// <summary>Kalemi pasife çeker; geçmiş beyannamelerde görünmeye devam eder.</summary>
        Task<VergiKalemiDto?> KalemPasifeAlAsync(int id, CancellationToken ct = default);

        /// <summary>Yalnızca kullanıcı kalemi ve hiç kullanılmamışsa siler.</summary>
        Task<KalemSilmeSonuc> KalemSilAsync(int id, CancellationToken ct = default);

        Task SiralamayiKaydetAsync(List<VergiKalemSiraDto> sira, CancellationToken ct = default);

        // ── Beyanname ──

        /// <summary>Firmanın dönemine ait kayıtlı beyanname; yoksa null.</summary>
        Task<VergiBeyannameDto?> GetBeyannameAsync(int firmaId, short donemYil, CancellationToken ct = default);

        /// <summary>Kaydetmeden hesaplar; ekranın canlı önizlemesi bunu kullanır.</summary>
        Task<VergiSonucDto> OnizleAsync(VergiBeyannameYazDto dto, CancellationToken ct = default);

        /// <summary>Girdileri (FirmaId, DonemYil) bazında upsert eder ve hesaplanmış sonucu döner.</summary>
        Task<VergiBeyannameDto> KaydetAsync(int firmaId, VergiBeyannameYazDto dto, CancellationToken ct = default);

        /// <summary>
        /// Beyanname formatına yakın .xlsx üretir. Kayıt yoksa null döner.
        /// </summary>
        Task<(byte[] Icerik, string DosyaAdi)?> ExcelAsync(int firmaId, short donemYil, CancellationToken ct = default);
    }

    public enum KalemSilmeSonuc
    {
        Silindi = 0,
        Bulunamadi = 1,
        SistemKalemi = 2,
        Kullanilmis = 3
    }

    /// <summary>Vergi modülü iş kuralı ihlali; mesaj kullanıcıya doğrudan gösterilebilir.</summary>
    public class VergiKuralException : Exception
    {
        public string Field { get; }

        public VergiKuralException(string field, string message) : base(message) => Field = field;
    }
}
