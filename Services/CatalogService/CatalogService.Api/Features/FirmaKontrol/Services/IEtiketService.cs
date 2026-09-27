using CatalogService.Api.Features.FirmaKontrol.Dtos;

namespace CatalogService.Api.Features.FirmaKontrol.Services
{
    /// <summary>
    /// Etiket TANIMLARININ yönetimi (boyut / değer / kural). Hangi hesaba hangi etiketin
    /// düştüğü burada hesaplanmaz ve saklanmaz — kural motoru istemcide, çalışma zamanında
    /// çalışır. Tanımlar dönemden bağımsızdır.
    /// </summary>
    public interface IEtiketService
    {
        /// <summary>
        /// Firma için geçerli boyutlar: kapsamı TumFirmalar olanlar + yalnızca bu firmaya
        /// ait olanlar. Kurallardan da yalnızca bu firmada geçerli olanlar (FirmaId null
        /// ya da bu firma) döner.
        /// </summary>
        Task<List<EtiketBoyutuDto>> GetBoyutlarAsync(int firmaId, CancellationToken ct = default);

        Task<EtiketBoyutuDto> BoyutEkleAsync(int firmaId, EtiketBoyutuYazDto dto, CancellationToken ct = default);
        /// <summary>Ad, sıra ve kapsam hesapları güncellenir; firmada aynı adda başka boyut olamaz.</summary>
        Task BoyutGuncelleAsync(int firmaId, int boyutId, EtiketBoyutuYazDto dto, CancellationToken ct = default);
        Task BoyutSilAsync(int boyutId, CancellationToken ct = default);

        Task<EtiketDegeriDto> DegerEkleAsync(int boyutId, EtiketDegeriYazDto dto, CancellationToken ct = default);
        Task DegerGuncelleAsync(int degerId, EtiketDegeriYazDto dto, CancellationToken ct = default);
        Task DegerSilAsync(int degerId, CancellationToken ct = default);

        Task<EtiketKuraliDto> KuralEkleAsync(int boyutId, EtiketKuraliYazDto dto, CancellationToken ct = default);
        Task KuralGuncelleAsync(long kuralId, EtiketKuraliYazDto dto, CancellationToken ct = default);
        Task KuralSilAsync(long kuralId, CancellationToken ct = default);

        /// <summary>Kuralların sırasını toplu günceller (ilk eşleşen kazandığı için sıra anlamlı).</summary>
        Task KuralSiraGuncelleAsync(int boyutId, List<EtiketKuralSiraDto> siralar, CancellationToken ct = default);
    }
}
