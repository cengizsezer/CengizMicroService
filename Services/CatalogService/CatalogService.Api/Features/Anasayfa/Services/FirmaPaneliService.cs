using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.FirmaBilgileri.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Anasayfa.Services
{
    public interface IFirmaPaneliService
    {
        /// <param name="kullaniciId">İsteği yapan kullanıcı (JWT <c>sub</c>); notun silinebilirliği için.</param>
        Task<FirmaPaneliDto> PanelAsync(int? seciliFirmaId, CancellationToken ct = default, string? kullaniciId = null);
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
        private readonly TimeProvider _saat;

        /// <param name="saat">Enjekte edilen saat (Prompt 7B: <c>DateTime.Today</c> yerine); testler sabit tarih verir.</param>
        public FirmaPaneliService(CatalogContext db, TimeProvider? saat = null)
        {
            _db = db;
            _saat = saat ?? TimeProvider.System;
        }

        public async Task<FirmaPaneliDto> PanelAsync(int? seciliFirmaId, CancellationToken ct = default,
                                                     string? kullaniciId = null)
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

            var mizanlar = await MizanYuklemeleriAsync(idler, ct);
            var sistemler = await Sistemler.Services.SistemService.FirmaAtamalariAsync(_db, seciliId, ct);

            // Gerekli alan "muhasebe programı" (Prompt 12): firmanın Muhasebe türünde atanmış sistemi var mı.
            var muhasebeli = (await (from fs in _db.FirmaSistemleri.AsNoTracking()
                                     join s in _db.Sistemler.AsNoTracking() on fs.SistemId equals s.Id
                                     where idler.Contains(fs.FirmaId) && s.Tur == Sistemler.Domain.SistemTuru.Muhasebe
                                     select fs.FirmaId).Distinct().ToListAsync(ct)).ToHashSet();

            var panel = FirmaPaneliKurucu.Kur(
                _saat.GetLocalNow().Date,
                firmalar,
                siciller.ToDictionary(s => s.FirmaId),
                ortaklar.ToLookup(o => o.FirmaId),
                yetkililer.ToLookup(y => y.FirmaId),
                belgeler,
                seciliId,
                mizanlar.ToLookup(m => m.FirmaId),
                sistemler,
                muhasebeli);

            if (panel.Secili is not null)
                await TakipDoldurAsync(panel.Secili.Takip, seciliId, kullaniciId, ct);

            return panel;
        }

        /// <summary>Takip kartının son notu ve son beş olayı — yalnız seçili firma için.</summary>
        private async Task TakipDoldurAsync(FirmaPaneliTakipDto takip, int firmaId, string? kullaniciId,
                                            CancellationToken ct)
        {
            var notlar = _db.FirmaNotlari.AsNoTracking().Where(n => n.FirmaId == firmaId);

            takip.NotSayisi = await notlar.CountAsync(ct);
            takip.SonNot = await notlar
                .OrderByDescending(n => n.OlusturmaZamani).ThenByDescending(n => n.Id)
                .Select(n => new FirmaNotuDto
                {
                    Id = n.Id,
                    Metin = n.Metin,
                    OlusturanKullaniciAdi = n.OlusturanKullaniciAdi,
                    OlusturmaZamani = DateTime.SpecifyKind(n.OlusturmaZamani, DateTimeKind.Utc),
                    Silinebilir = kullaniciId != null && n.OlusturanKullaniciId == kullaniciId
                })
                .FirstOrDefaultAsync(ct);

            takip.SonOlaylar = await OlaylarAsync(_db, firmaId, SonOlaySayisi, ct);
        }

        /// <summary>Takip kartında gösterilen olay sayısı; tamamı "Tüm hareketler"de.</summary>
        public const int SonOlaySayisi = 5;

        /// <summary>Firmanın olayları, yeni → eski. <paramref name="adet"/> boşsa tamamı.</summary>
        public static async Task<List<FirmaOlayDto>> OlaylarAsync(CatalogContext db, int firmaId, int? adet,
                                                                 CancellationToken ct)
        {
            var sorgu = db.FirmaOlayKayitlari.AsNoTracking()
                .Where(o => o.FirmaId == firmaId)
                .OrderByDescending(o => o.Zaman).ThenByDescending(o => o.Id)
                .AsQueryable();

            if (adet is { } n) sorgu = sorgu.Take(n);

            return await sorgu
                .Select(o => new FirmaOlayDto
                {
                    OlayTipi = o.OlayTipi,
                    Aciklama = o.Aciklama,
                    KullaniciAdi = o.KullaniciAdi,
                    Zaman = DateTime.SpecifyKind(o.Zaman, DateTimeKind.Utc)
                })
                .ToListAsync(ct);
        }

        /// <summary>
        /// Bütün firmaların mizan yüklemeleri, iki sorguda. Ağaç kaydından yalnız özet
        /// kolonları okunuyor — düğüm JSON'u (binlerce düğüm) anasayfaya taşınmıyor.
        /// Ağacı olmayan (özellikten önceki) yüklemeler ham satır tablosundan geliyor;
        /// onların özet alanları boş.
        /// </summary>
        private async Task<List<MizanYuklemesi>> MizanYuklemeleriAsync(List<int> idler, CancellationToken ct)
        {
            var agaclar = await _db.FirmaKontrolMizanAgaclari.AsNoTracking()
                .Where(a => idler.Contains(a.FirmaId))
                .Select(a => new MizanYuklemesi(a.FirmaId, a.Donem, a.Yil, a.UploadedAt,
                                                a.DugumSayisi, a.SeviyeSayisi, a.BorcToplam, a.AlacakToplam))
                .ToListAsync(ct);

            var hamlar = await _db.FirmaKontrolMizanSatirlari.AsNoTracking()
                .Where(s => idler.Contains(s.FirmaId))
                .GroupBy(s => new { s.FirmaId, s.Donem, s.Yil })
                .Select(g => new { g.Key.FirmaId, g.Key.Donem, g.Key.Yil, Zaman = g.Max(s => s.UploadedAt) })
                .ToListAsync(ct);

            var agacAnahtarlari = agaclar.Select(a => (a.FirmaId, a.Donem, a.AnalizYili)).ToHashSet();

            agaclar.AddRange(hamlar
                .Where(h => !agacAnahtarlari.Contains((h.FirmaId, (int)h.Donem, h.Yil)))
                .Select(h => new MizanYuklemesi(h.FirmaId, (int)h.Donem, h.Yil, h.Zaman,
                                                null, null, null, null)));

            return agaclar;
        }
    }
}
