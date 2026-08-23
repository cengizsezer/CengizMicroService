using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    public interface IBankaTemizlikService
    {
        /// <summary>Seçili firmanın silinecek kayıt sayıları; onay diyaloğu bunu gösterir.</summary>
        Task<BankaTemizlikOzetiDto> OzetAsync(CancellationToken ct = default);

        /// <summary>Seçili firmanın banka otomasyon verisini siler; silinen sayıları döndürür.</summary>
        Task<BankaTemizlikOzetiDto> TemizleAsync(CancellationToken ct = default);

        /// <summary>
        /// Hiçbir firmaya bağlı olmayan (<c>FirmaId</c> pozitif değil) eski kayıtların
        /// sayısı. Tenant düzeninden kalan satırlar; hiçbir firmanın ekranında görünmezler.
        /// </summary>
        Task<BankaTemizlikOzetiDto> SahipsizOzetAsync(CancellationToken ct = default);

        /// <summary>Sahipsiz kayıtları siler.</summary>
        Task<BankaTemizlikOzetiDto> SahipsizTemizleAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// "Bu firmanın banka otomasyon verisini temizle".
    ///
    /// Neden migration ile taşıma değil: modül bir dönem veriyi <b>tenant</b> altına yazdı
    /// (bkz. KARARLAR §68). Tenant ile firma arasında güvenilir bir eşleme yok — token'daki
    /// tenant "500 / PKF Istanbul SMMM" iken kayıtlar aslında PKF Aday'a aitti ve bu bilgi
    /// yalnız kullanıcının kafasında. Otomatik bir taşıma, veriyi doğru sandığı bir yere
    /// koyup hatayı görünmez yapardı. Bunun yerine yanlış yerdeki veri <b>silinip</b>
    /// doğru firmada yeniden yükleniyor; karar kullanıcının.
    ///
    /// Silinenler firma bazlı tabloların tamamı. <b>Silinmeyenler</b> global tablolardır
    /// (açıklama şablonları, unvan desenleri, sabit kurallar, vergi kodları, kimlik
    /// kayıtları): bunlar bankanın yazım kalıbına ait, firmaya değil — bir firmanın
    /// temizliği başka firmaların çalışan kurulumunu bozmamalı (bkz. KARARLAR §70).
    /// </summary>
    public class BankaTemizlikService : IBankaTemizlikService
    {
        private readonly CatalogContext _db;
        private readonly IBankaFirmaKapsami _kapsam;

        public BankaTemizlikService(CatalogContext db, IBankaFirmaKapsami kapsam)
        {
            _db = db;
            _kapsam = kapsam;
        }

        public Task<BankaTemizlikOzetiDto> OzetAsync(CancellationToken ct = default)
            => SayAsync(_kapsam.FirmaId, ct);

        public Task<BankaTemizlikOzetiDto> SahipsizOzetAsync(CancellationToken ct = default)
            => SayAsync(Sahipsiz, ct);

        public Task<BankaTemizlikOzetiDto> TemizleAsync(CancellationToken ct = default)
            => SilAsync(_kapsam.FirmaId, ct);

        public Task<BankaTemizlikOzetiDto> SahipsizTemizleAsync(CancellationToken ct = default)
            => SilAsync(Sahipsiz, ct);

        /// <summary>
        /// Sahipsiz kayıtların hedefi. Gerçek <c>Firma.Id</c>'ler pozitiftir; tenant
        /// düzeninden kalan satırlara migration <b>negatif</b> sahte kapsam yazdı (eski
        /// tekillik korunsun diye). Bu yüzden ölçüt "sıfıra eşit" değil, "pozitif değil".
        /// </summary>
        private const int Sahipsiz = 0;

        private async Task<BankaTemizlikOzetiDto> SayAsync(int firmaId, CancellationToken ct)
        {
            var yuklemeler = _db.EkstreYuklemeler
                .Where(y => firmaId > 0 ? y.FirmaId == firmaId : y.FirmaId <= 0);

            return new BankaTemizlikOzetiDto
            {
                FirmaId = firmaId,
                HesapPlaniKaydi = await _db.EkstreHesapPlani
                    .CountAsync(h => firmaId > 0 ? h.FirmaId == firmaId : h.FirmaId <= 0, ct),
                BankaHesabi = await _db.EkstreBankaHesaplari
                    .CountAsync(h => firmaId > 0 ? h.FirmaId == firmaId : h.FirmaId <= 0, ct),
                EkstreYukleme = await yuklemeler.CountAsync(ct),
                EkstreSatiri = await _db.EkstreSatirlari
                    .CountAsync(s => yuklemeler.Any(y => y.Id == s.EkstreYuklemeId), ct),
                HesapEslesmesi = await _db.EkstreHesapEslesmeleri
                    .CountAsync(e => firmaId > 0 ? e.FirmaId == firmaId : e.FirmaId <= 0, ct),
                KisiYonlendirme = await _db.EkstreKisiYonlendirmeleri
                    .CountAsync(k => firmaId > 0 ? k.FirmaId == firmaId : k.FirmaId <= 0, ct)
            };
        }

        /// <summary>
        /// Silme sırası bağımlılıkları izler: satırlar → yüklemeler → banka hesapları.
        /// <c>EkstreYukleme.BankaHesabiId</c> FK'sı <c>Restrict</c>, yani hesap ancak
        /// yüklemesi kalmayınca silinebilir.
        ///
        /// Hesap sahibi unvanları ayrı bir tabloda değil, <c>BankaHesabi</c> satırlarında
        /// duruyor; hesaplar gidince onlar da gidiyor.
        /// </summary>
        private async Task<BankaTemizlikOzetiDto> SilAsync(int firmaId, CancellationToken ct)
        {
            var ozet = await SayAsync(firmaId, ct);

            var yuklemeIdler = await _db.EkstreYuklemeler
                .Where(y => firmaId > 0 ? y.FirmaId == firmaId : y.FirmaId <= 0)
                .Select(y => y.Id).ToListAsync(ct);

            if (yuklemeIdler.Count > 0)
            {
                // Satırlar EkstreYukleme'ye Cascade bağlı; yine de açıkça siliniyor ki
                // InMemory sağlayıcıda (testler) da davranış aynı olsun.
                var satirlar = await _db.EkstreSatirlari
                    .Where(s => yuklemeIdler.Contains(s.EkstreYuklemeId)).ToListAsync(ct);
                _db.EkstreSatirlari.RemoveRange(satirlar);

                var yuklemeler = await _db.EkstreYuklemeler
                    .Where(y => yuklemeIdler.Contains(y.Id)).ToListAsync(ct);
                _db.EkstreYuklemeler.RemoveRange(yuklemeler);

                await _db.SaveChangesAsync(ct);
            }

            _db.EkstreBankaHesaplari.RemoveRange(await _db.EkstreBankaHesaplari
                .Where(h => firmaId > 0 ? h.FirmaId == firmaId : h.FirmaId <= 0).ToListAsync(ct));
            _db.EkstreHesapEslesmeleri.RemoveRange(await _db.EkstreHesapEslesmeleri
                .Where(e => firmaId > 0 ? e.FirmaId == firmaId : e.FirmaId <= 0).ToListAsync(ct));
            _db.EkstreKisiYonlendirmeleri.RemoveRange(await _db.EkstreKisiYonlendirmeleri
                .Where(k => firmaId > 0 ? k.FirmaId == firmaId : k.FirmaId <= 0).ToListAsync(ct));
            _db.EkstreHesapPlani.RemoveRange(await _db.EkstreHesapPlani
                .Where(h => firmaId > 0 ? h.FirmaId == firmaId : h.FirmaId <= 0).ToListAsync(ct));

            await _db.SaveChangesAsync(ct);
            return ozet;
        }
    }
}
