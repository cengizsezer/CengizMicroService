using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.FirmaBilgileri.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Anasayfa.Services
{
    public interface IFirmaPaneliService
    {
        Task<FirmaPaneliDto> PanelAsync(int? seciliFirmaId, CancellationToken ct = default);
    }

    /// <summary>
    /// Anasayfadaki firma bilgi panelinin verisi.
    ///
    /// <b>Firma başına sorgu yok.</b> Sol listedeki uyarı göstergesi bütün firmaların
    /// ortak ve yetkili kayıtlarını gerektiriyor; bunlar tek seferde <c>IN (...)</c> ile
    /// okunup bellekte firmaya göre gruplanıyor. On firma için beş sorgu yerine kırk
    /// sorgu atmak, ekranın açılışını tek bir isteğe sığdırma amacını da bozardı.
    ///
    /// Yeni tablo açılmadı: veriler Firma Bilgileri modülünün kendi tablolarından
    /// (KARARLAR §126). Bu servisin işi okumak ve <see cref="FirmaPaneliKurucu"/>'ya
    /// vermek; kurallar orada, saf fonksiyonda.
    ///
    /// <b>Kapsam:</b> liste doğası gereği tüm firmaları gösteriyor (anasayfanın kendi
    /// kararı, §69); seçili firma isteğin <c>?firmaId=</c> parametresinden geliyor ve
    /// ayrıntı yalnız o firmanın kayıtlarından kuruluyor.
    /// </summary>
    public class FirmaPaneliService : IFirmaPaneliService
    {
        private readonly CatalogContext _db;

        public FirmaPaneliService(CatalogContext db) => _db = db;

        public async Task<FirmaPaneliDto> PanelAsync(int? seciliFirmaId, CancellationToken ct = default)
        {
            var firmalar = await _db.Firmalar.AsNoTracking()
                .Where(f => f.Aktif)
                .ToListAsync(ct);

            if (firmalar.Count == 0) return new FirmaPaneliDto();

            var idler = firmalar.Select(f => f.Id).ToList();

            var siciller = await _db.FirmaSicilBilgileri.AsNoTracking()
                .Where(s => idler.Contains(s.FirmaId))
                .ToListAsync(ct);

            var ortaklar = await _db.FirmaOrtaklari.AsNoTracking()
                .Where(o => idler.Contains(o.FirmaId))
                .ToListAsync(ct);

            var yetkililer = await _db.FirmaImzaYetkilileri.AsNoTracking()
                .Where(y => idler.Contains(y.FirmaId))
                .ToListAsync(ct);

            // Seçili firma listede yoksa (silinmiş ya da pasife alınmış) kurucu ilk
            // firmaya düşüyor; belgeleri de o firmanınki olmalı.
            var seciliId = firmalar.Any(f => f.Id == seciliFirmaId)
                ? seciliFirmaId!.Value
                : firmalar
                    .OrderBy(FirmaPaneliKurucu.Ad, StringComparer.CurrentCultureIgnoreCase)
                    .First().Id;

            var belgeler = await _db.FirmaBelgeleri.AsNoTracking()
                .Where(b => b.FirmaId == seciliId)
                .OrderBy(b => b.Tur).ThenByDescending(b => b.CreatedAt)
                .Select(b => new FirmaBelgesiDto
                {
                    Id = b.Id,
                    Tur = b.Tur,
                    FileId = b.FileId,
                    FileName = b.FileName,
                    ContentType = b.ContentType,
                    Length = b.Length,
                    Aciklama = b.Aciklama,
                    CreatedAt = b.CreatedAt,
                    YukleyenKullanici = b.YukleyenKullanici
                })
                .ToListAsync(ct);

            return FirmaPaneliKurucu.Kur(
                DateTime.Today,
                firmalar,
                siciller.ToDictionary(s => s.FirmaId),
                ortaklar.ToLookup(o => o.FirmaId),
                yetkililer.ToLookup(y => y.FirmaId),
                belgeler,
                seciliId);
        }
    }
}
