using CatalogService.Api.Features.Banka.Domain;
using CatalogService.Api.Features.Banka.Dtos;
using CatalogService.Api.Infrastructure.Auth;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Banka.Services
{
    public class BankaTakipService : IBankaTakipService
    {
        private readonly CatalogContext _context;
        private readonly IHttpCurrentUser _user;

        public BankaTakipService(CatalogContext context, IHttpCurrentUser user)
        {
            _context = context;
            _user = user;
        }

        public async Task<List<HesapTakipDto>> GetAyAsync(int year, int month, int? firmaId = null)
        {
            if (month < 1) month = 1;
            if (month > 12) month = 12;

            var ayBasi = new DateTime(year, month, 1);
            var aySonu = ayBasi.AddMonths(1); // exclusive

            var hesapQuery = _context.Hesaplar.AsNoTracking().Where(h => h.AktifMi);
            if (firmaId is int fId)
                hesapQuery = hesapQuery.Where(h => h.FirmaId == fId);

            var hesaplar = await (
                from h in hesapQuery
                join f in _context.Firmalar.AsNoTracking()
                    on h.FirmaId equals f.Id
                orderby f.KisaAd, h.Ad
                select new HesapTakipDto
                {
                    HesapId = h.Id,
                    Ad      = h.Ad,
                    Tip     = h.Tip,
                    Siklik  = h.Siklik,
                    FirmaId = h.FirmaId,
                    FirmaAd = f.KisaAd
                }
            ).ToListAsync();

            if (hesaplar.Count == 0)
                return hesaplar;

            var hesapIds = hesaplar.Select(h => h.HesapId).ToList();

            var islenenler = await _context.IslemKayitlari.AsNoTracking()
                .Where(k => k.IslendiMi
                            && k.Tarih >= ayBasi
                            && k.Tarih < aySonu
                            && hesapIds.Contains(k.HesapId))
                .Select(k => new { k.HesapId, k.Tarih })
                .ToListAsync();

            var lookup = islenenler
                .GroupBy(x => x.HesapId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Tarih.Date).ToList());

            // Not sayıları — not penceresiyle AYNI ay filtresi: Sabit + Genel (her ay),
            // Ay/Gun kapsamlılar yalnızca bu ay. (notSayisi == pencerede görünen not adedi.)
            var notSayilari = (await _context.HesapNotlari.AsNoTracking()
                    .Where(n => hesapIds.Contains(n.HesapId) &&
                        (n.Sabit
                         || n.Kapsam == Domain.NotKapsam.Genel
                         || (n.Kapsam == Domain.NotKapsam.Ay && n.Yil == year && n.Ay == month)
                         || (n.Kapsam == Domain.NotKapsam.Gun && n.Tarih != null && n.Tarih >= ayBasi && n.Tarih < aySonu)))
                    .GroupBy(n => n.HesapId)
                    .Select(g => new { HesapId = g.Key, Adet = g.Count() })
                    .ToListAsync())
                .ToDictionary(x => x.HesapId, x => x.Adet);

            var notluGunlerRaw = await _context.HesapNotlari.AsNoTracking()
                .Where(n => n.Kapsam == Domain.NotKapsam.Gun
                            && n.Tarih != null
                            && n.Tarih >= ayBasi
                            && n.Tarih < aySonu
                            && hesapIds.Contains(n.HesapId))
                .Select(n => new { n.HesapId, Tarih = n.Tarih!.Value })
                .ToListAsync();

            var notluGunlerLookup = notluGunlerRaw
                .GroupBy(x => x.HesapId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Tarih.Date).Distinct().ToList());

            foreach (var h in hesaplar)
            {
                if (lookup.TryGetValue(h.HesapId, out var gunler))
                    h.IslenenGunler = gunler;

                if (notSayilari.TryGetValue(h.HesapId, out var adet))
                    h.NotSayisi = adet;

                if (notluGunlerLookup.TryGetValue(h.HesapId, out var notlu))
                    h.NotluGunler = notlu;
            }

            return hesaplar;
        }

        public async Task<IslemKaydiDto?> IsaretleAsync(IsaretleRequestDto dto)
        {
            if (!await _context.Hesaplar.AnyAsync(h => h.Id == dto.HesapId))
                return null;

            var gun = dto.Tarih.Date;

            var kayit = await _context.IslemKayitlari
                .FirstOrDefaultAsync(k => k.HesapId == dto.HesapId && k.Tarih == gun);

            if (kayit is null)
            {
                kayit = new IslemKaydi
                {
                    HesapId = dto.HesapId,
                    Tarih = gun
                };
                _context.IslemKayitlari.Add(kayit);
            }

            kayit.IslendiMi = dto.IslendiMi;

            if (dto.IslendiMi)
            {
                kayit.IsleyenKullanici = _user.UserName;
                kayit.IslemZamani = DateTime.UtcNow;
            }
            else
            {
                kayit.IsleyenKullanici = null;
                kayit.IslemZamani = null;
            }

            await _context.SaveChangesAsync();

            return new IslemKaydiDto
            {
                Id = kayit.Id,
                HesapId = kayit.HesapId,
                Tarih = kayit.Tarih,
                IslendiMi = kayit.IslendiMi,
                IsleyenKullanici = kayit.IsleyenKullanici,
                IslemZamani = kayit.IslemZamani
            };
        }
    }
}
