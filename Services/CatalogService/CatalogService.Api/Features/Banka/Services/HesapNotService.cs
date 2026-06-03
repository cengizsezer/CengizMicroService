using CatalogService.Api.Features.Banka.Domain;
using CatalogService.Api.Features.Banka.Dtos;
using CatalogService.Api.Infrastructure.Auth;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Banka.Services
{
    public class HesapNotService : IHesapNotService
    {
        private readonly CatalogContext _context;
        private readonly IHttpCurrentUser _user;

        public HesapNotService(CatalogContext context, IHttpCurrentUser user)
        {
            _context = context;
            _user = user;
        }

        public async Task<List<NotDto>> GetByHesapAsync(int hesapId, int yil, int ay)
        {
            if (ay < 1) ay = 1;
            if (ay > 12) ay = 12;

            var ayBasi = new DateTime(yil, ay, 1);
            var aySonu = ayBasi.AddMonths(1); // exclusive

            var notlar = await _context.HesapNotlari.AsNoTracking()
                .Where(n => n.HesapId == hesapId &&
                    // Her ay görünenler:
                    (n.Sabit
                     || n.Kapsam == NotKapsam.Genel
                     // Yalnızca bakılan aya/güne ait olanlar:
                     || (n.Kapsam == NotKapsam.Ay && n.Yil == yil && n.Ay == ay)
                     || (n.Kapsam == NotKapsam.Gun && n.Tarih != null && n.Tarih >= ayBasi && n.Tarih < aySonu)))
                // Sabit (pinli) notlar önce, sonra en yeni.
                .OrderByDescending(n => n.Sabit)
                .ThenByDescending(n => n.OlusturmaZamani)
                .ToListAsync();

            return notlar.Select(ToDto).ToList();
        }

        public async Task<NotDto?> CreateAsync(NotCreateDto dto)
        {
            if (!await _context.Hesaplar.AnyAsync(h => h.Id == dto.HesapId))
                return null;

            // Kapsam'a göre yalnızca ilgili alanları doldur.
            var not = new Not
            {
                HesapId = dto.HesapId,
                Kapsam = dto.Kapsam,
                Metin = dto.Metin.Trim(),
                Sabit = dto.Sabit,
                OlusturanKullanici = _user.UserName,
                OlusturmaZamani = DateTime.UtcNow
            };

            switch (dto.Kapsam)
            {
                case NotKapsam.Gun:
                    not.Tarih = dto.Tarih?.Date;
                    break;
                case NotKapsam.Ay:
                    not.Yil = dto.Yil;
                    not.Ay = dto.Ay;
                    break;
            }

            _context.HesapNotlari.Add(not);
            await _context.SaveChangesAsync();

            return ToDto(not);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var not = await _context.HesapNotlari.FirstOrDefaultAsync(n => n.Id == id);
            if (not is null) return false;

            _context.HesapNotlari.Remove(not);
            await _context.SaveChangesAsync();
            return true;
        }

        private static NotDto ToDto(Not n) => new()
        {
            Id = n.Id,
            HesapId = n.HesapId,
            Kapsam = n.Kapsam,
            Tarih = n.Tarih,
            Yil = n.Yil,
            Ay = n.Ay,
            Metin = n.Metin,
            Sabit = n.Sabit,
            OlusturanKullanici = n.OlusturanKullanici,
            OlusturmaZamani = n.OlusturmaZamani
        };
    }
}
