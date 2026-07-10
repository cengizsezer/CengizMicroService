using CatalogService.Api.Features.KdvBeyanname.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.KdvBeyanname.Services
{
    public class KdvBeyannameQueryService : IKdvBeyannameQueryService
    {
        private readonly CatalogContext _db;

        public KdvBeyannameQueryService(CatalogContext db)
        {
            _db = db;
        }

        public async Task<List<KdvFirmaCardDto>> GetFirmaCardsAsync(CancellationToken ct)
        {
            // Sadece aktif firmaları getir.
            var firmalar = await _db.Firmalar
                .AsNoTracking()
                .Where(f => f.Aktif)
                .OrderBy(f => f.KisaAd)
                .Select(f => new
                {
                    f.Id,
                    f.VergiKimlikNo,
                    f.Unvan,
                    f.KisaAd,
                    f.VergiDairesiKodu,
                    f.YetkiliAdi,
                    f.YetkiliSoyadi,
                    f.TicaretSicilNo,
                    f.Email,
                    f.Telefon
                })
                .ToListAsync(ct);

            var firmaIds = firmalar.Select(f => f.Id).ToList();

            // Her firmanın son taramasını tek sorguda topla.
            var sonTaramalar = await _db.KdvBeyannameTaramalar
                .AsNoTracking()
                .Where(t => firmaIds.Contains(t.FirmaId))
                .GroupBy(t => t.FirmaId)
                .Select(g => g.OrderByDescending(x => x.BaslangicAt).First())
                .ToDictionaryAsync(t => t.FirmaId, ct);

            return firmalar.Select(f =>
            {
                sonTaramalar.TryGetValue(f.Id, out var son);
                return new KdvFirmaCardDto
                {
                    Id              = f.Id,
                    VergiKimlikNo   = f.VergiKimlikNo,
                    Unvan           = f.Unvan,
                    KisaAd          = f.KisaAd,
                    // BDP için kritik 4 alandan biri eksikse "eksik" işaretle.
                    BdpAlanlariEksik = string.IsNullOrWhiteSpace(f.VergiDairesiKodu)
                                       || string.IsNullOrWhiteSpace(f.YetkiliAdi)
                                       || string.IsNullOrWhiteSpace(f.YetkiliSoyadi)
                                       || string.IsNullOrWhiteSpace(f.TicaretSicilNo),
                    HasDpHesabi      = false, // bu alan controller'da SovosCompanies kontrolüyle doldurulur
                    SonTaramaAt      = son?.BaslangicAt,
                    SonTaramaDurumu  = son?.Durum
                };
            }).ToList();
        }

        public async Task<List<TaramaDto>> GetTaramalarAsync(int firmaId, int take, CancellationToken ct)
        {
            if (take <= 0) take = 20;
            return await _db.KdvBeyannameTaramalar
                .AsNoTracking()
                .Where(t => t.FirmaId == firmaId)
                .OrderByDescending(t => t.BaslangicAt)
                .Take(take)
                .Select(t => new TaramaDto
                {
                    Id                  = t.Id,
                    FirmaId             = t.FirmaId,
                    BaslangicTarihi     = t.BaslangicTarihi,
                    BitisTarihi         = t.BitisTarihi,
                    Durum               = t.Durum,
                    BaslangicAt         = t.BaslangicAt,
                    BitisAt             = t.BitisAt,
                    HataMesaji          = t.HataMesaji,
                    BulunanFaturaSayisi = t.BulunanFaturaSayisi
                })
                .ToListAsync(ct);
        }

        public async Task<List<MizanSatirDto>> GetMizanAsync(
            int firmaId, string donem, CancellationToken ct)
        {
            return await _db.KdvBeyannameMizan
                .AsNoTracking()
                .Where(m => m.FirmaId == firmaId && m.Donem == donem)
                .OrderBy(m => m.HesapKodu)
                .Select(m => new MizanSatirDto
                {
                    Id           = m.Id,
                    HesapKodu    = m.HesapKodu,
                    HesapAdi     = m.HesapAdi,
                    BorcToplam   = m.BorcToplam,
                    AlacakToplam = m.AlacakToplam,
                    BorcKalan    = m.BorcKalan,
                    AlacakKalan  = m.AlacakKalan
                })
                .ToListAsync(ct);
        }

        public async Task<List<YevmiyeSatirDto>> GetYevmiyeAsync(
            int firmaId, string donem, CancellationToken ct)
        {
            return await _db.KdvBeyannameYevmiye
                .AsNoTracking()
                .Where(y => y.FirmaId == firmaId && y.Donem == donem)
                .OrderBy(y => y.Tarih).ThenBy(y => y.FaturaNo)
                .Select(y => new YevmiyeSatirDto
                {
                    Id        = y.Id,
                    Tarih     = y.Tarih,
                    HesapKodu = y.HesapKodu,
                    HesapAdi  = y.HesapAdi,
                    BelgeTipi = y.BelgeTipi,
                    FaturaNo  = y.FaturaNo,
                    Borc      = y.Borc,
                    Alacak    = y.Alacak,
                    Aciklama  = y.Aciklama,
                    FisNo     = y.FisNo
                })
                .ToListAsync(ct);
        }

        public async Task<List<GelenFaturaDto>> GetGelenFaturalarAsync(
            int firmaId, DateTime? baslangic, DateTime? bitis, CancellationToken ct)
        {
            var query = _db.GelenFaturalar
                .AsNoTracking()
                .Where(g => g.FirmaId == firmaId);

            if (baslangic.HasValue)
                query = query.Where(g => g.FaturaTarihi >= baslangic.Value.Date);
            if (bitis.HasValue)
            {
                var bitisUst = bitis.Value.Date.AddDays(1);
                query = query.Where(g => g.FaturaTarihi < bitisUst);
            }

            return await query
                .OrderByDescending(g => g.FaturaTarihi)
                .ThenBy(g => g.FaturaNo)
                .Select(g => new GelenFaturaDto
                {
                    Id             = g.Id,
                    FaturaNo       = g.FaturaNo,
                    GondericiVkn   = g.GondericiVkn,
                    GondericiUnvan = g.GondericiUnvan,
                    FaturaTarihi   = g.FaturaTarihi,
                    ParaBirimi     = g.ParaBirimi,
                    MatrahTutari   = g.MatrahTutari,
                    KdvTutari      = g.KdvTutari,
                    ToplamTutar    = g.ToplamTutar,
                    KdvOrani       = g.KdvOrani,
                    Statu          = g.Statu,
                    SonGuncelleme  = g.SonGuncelleme
                })
                .ToListAsync(ct);
        }
    }
}
