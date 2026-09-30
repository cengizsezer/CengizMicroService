using CatalogService.Api.Features.Anasayfa.Domain;
using CatalogService.Api.Infrastructure.Auth;
using CatalogService.Api.Infrastructure.Context;

namespace CatalogService.Api.Features.Anasayfa.Services
{
    /// <summary>
    /// Firma olay kaydı ("Son işlemler") yazıcısı. Yalnız <b>yazma</b> işlemlerinden sonra
    /// çağrılır; görüntüleme kaydı tutulmaz.
    ///
    /// <b>Asıl işlemi asla bozmaz:</b> çağıran kendi kaydını tamamladıktan SONRA çağırır;
    /// kayıt ayrı bir <c>SaveChanges</c> ve ayrı bir try/catch içinde yazılır. Hata loglanır,
    /// eklenen satır context'ten sökülür (sonraki kayıtlara bulaşmasın) ve kullanıcıya
    /// yansımaz.
    /// </summary>
    public interface IFirmaOlayYazici
    {
        Task YazAsync(int firmaId, FirmaOlayTipi tip, string aciklama, CancellationToken ct = default);
    }

    /// <inheritdoc cref="IFirmaOlayYazici"/>
    public class FirmaOlayYazici : IFirmaOlayYazici
    {
        private readonly CatalogContext _db;
        private readonly IHttpCurrentUser _kullanici;
        private readonly ILogger<FirmaOlayYazici> _log;

        public FirmaOlayYazici(CatalogContext db, IHttpCurrentUser kullanici, ILogger<FirmaOlayYazici> log)
        {
            _db = db;
            _kullanici = kullanici;
            _log = log;
        }

        public async Task YazAsync(int firmaId, FirmaOlayTipi tip, string aciklama, CancellationToken ct = default)
        {
            FirmaOlayKaydi? kayit = null;
            try
            {
                kayit = new FirmaOlayKaydi
                {
                    FirmaId = firmaId,
                    OlayTipi = tip,
                    Aciklama = aciklama.Length > 500 ? aciklama[..500] : aciklama,
                    KullaniciId = KullaniciId(),
                    KullaniciAdi = _kullanici.UserName,
                    Zaman = DateTime.UtcNow
                };

                _db.FirmaOlayKayitlari.Add(kayit);
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                try
                {
                    if (kayit is not null) _db.Entry(kayit).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                }
                catch
                {
                    // Sökme de başarısızsa yapılacak başka bir şey yok; asıl işlem zaten bitti.
                }

                _log.LogWarning(ex, "Firma olay kaydı yazılamadı (firma {FirmaId}, {Tip}). Asıl işlem etkilenmedi.",
                                firmaId, tip);
            }
        }

        private string? KullaniciId()
        {
            try
            {
                return _kullanici.IsAuthenticated ? _kullanici.UserId : null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}
