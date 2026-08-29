using WebApp.Application.Services.Yonetim;
using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services
{
    /// <summary>Açılır listelerde gösterilen firma: kimlik + görünen ad.</summary>
    public sealed record FirmaSecenegi(int Id, string Ad, string? VergiKimlikNo)
    {
        /// <summary>Listede unvan gösterilir; boşsa kısa ad — sunucudaki kuralın aynısı.</summary>
        public static FirmaSecenegi Kur(FirmaDto f)
            => new(f.Id,
                   string.IsNullOrWhiteSpace(f.Unvan) ? f.KisaAd : f.Unvan,
                   string.IsNullOrWhiteSpace(f.VergiKimlikNo) ? null : f.VergiKimlikNo);
    }

    /// <summary>
    /// Firma seçici ve filtrelerin ortak veri kaynağı.
    ///
    /// Firma artık bir oturum bağlamı değil (KARARLAR §99); buna karşılık firma listesi
    /// neredeyse her ekranda gerekiyor — filtrelerde, formlarda, kolon adlarında. Liste
    /// istek başına değil <b>uygulama ömrü</b> boyunca bir kez okunur: firma sayısı iki
    /// haneli ve gün içinde değişmiyor, her bölümün kendi çağrısını yapması aynı listeyi
    /// onlarca kez indirirdi.
    ///
    /// Bilerek "aktif firma" tutmuyor — yalnız listeyi verir. Hangi firmanın seçili olduğu
    /// ekranın kendi durumudur ve ekranla birlikte kaybolur.
    /// </summary>
    public interface IFirmaSecenekleri
    {
        Task<IReadOnlyList<FirmaSecenegi>> HepsiAsync(CancellationToken ct = default);

        /// <summary>Id'nin görünen adı; bilinmiyorsa boş dizi.</summary>
        Task<string> AdAsync(int firmaId, CancellationToken ct = default);
    }

    public sealed class FirmaSecenekleri : IFirmaSecenekleri
    {
        private readonly IFirmaApiClient _firmalar;

        private IReadOnlyList<FirmaSecenegi>? _liste;

        public FirmaSecenekleri(IFirmaApiClient firmalar) => _firmalar = firmalar;

        public async Task<IReadOnlyList<FirmaSecenegi>> HepsiAsync(CancellationToken ct = default)
        {
            if (_liste is not null) return _liste;

            try
            {
                var kayitlar = await _firmalar.GetAllAsync(includeInactive: false, ct);

                _liste = kayitlar
                    .Select(FirmaSecenegi.Kur)
                    .OrderBy(f => f.Ad, StringComparer.CurrentCulture)
                    .ToList();
            }
            catch (Exception)
            {
                // Ağ hatası önbelleğe alınmaz: sonraki deneme yeniden sorabilsin.
                return Array.Empty<FirmaSecenegi>();
            }

            return _liste;
        }

        public async Task<string> AdAsync(int firmaId, CancellationToken ct = default)
        {
            if (firmaId <= 0) return string.Empty;

            var liste = await HepsiAsync(ct);
            return liste.FirstOrDefault(f => f.Id == firmaId)?.Ad ?? string.Empty;
        }
    }
}
