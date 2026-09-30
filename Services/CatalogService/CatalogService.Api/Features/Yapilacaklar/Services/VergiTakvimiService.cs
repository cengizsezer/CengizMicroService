using System.Text.RegularExpressions;
using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Yapilacaklar.Services
{
    public interface IVergiTakvimiService
    {
        Task<List<VergiTakvimiDto>> ListeAsync(int? yil, CancellationToken ct = default);
        Task<VergiTakvimiDto> EkleAsync(VergiTakvimiKaydetDto dto, CancellationToken ct = default);
        Task<VergiTakvimiDto> GuncelleAsync(int id, VergiTakvimiKaydetDto dto, CancellationToken ct = default);

        /// <summary>Tamamlama kaydı olan satır silinmez (geçmiş kaybolur); pasife alınır.</summary>
        Task SilAsync(int id, CancellationToken ct = default);

        /// <summary>Eksik varsayılan satırları ekler (açılış seed'inin elle tetiklenen hâli).</summary>
        Task<int> VarsayilanlariYukleAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Yönetim → Vergi Takvimi. Tarihlerin tek düzenleme yeri: yasal süre uzatıldığında
    /// burada düzeltilir, yasal işler bir sonraki açılışta yeni tarihi gösterir. Dönem
    /// başı/sonu dönemden hesaplanır, elle girilmez.
    /// </summary>
    public class VergiTakvimiService : IVergiTakvimiService
    {
        private static readonly Regex KodDeseni = new(@"^0\d{3}$", RegexOptions.Compiled);

        private readonly CatalogContext _db;
        private readonly TimeProvider _saat;

        public VergiTakvimiService(CatalogContext db, TimeProvider saat)
        {
            _db = db;
            _saat = saat;
        }

        public async Task<List<VergiTakvimiDto>> ListeAsync(int? yil, CancellationToken ct = default)
        {
            var sorgu = _db.VergiTakvimi.AsNoTracking();
            if (yil is not null) sorgu = sorgu.Where(t => t.Yil == yil);

            var satirlar = await sorgu.OrderBy(t => t.SonGun).ThenBy(t => t.MukellefiyetKodu).ToListAsync(ct);
            var idler = satirlar.Select(s => s.Id).ToList();

            var sayilar = await _db.IsTamamlamalari.AsNoTracking()
                .Where(t => t.KaynakTip == IsKaynagi.Yasal && idler.Contains(t.KaynakId))
                .GroupBy(t => t.KaynakId)
                .Select(g => new { g.Key, Sayi = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Sayi, ct);

            return satirlar.Select(s => Dto(s, sayilar.GetValueOrDefault(s.Id))).ToList();
        }

        public async Task<VergiTakvimiDto> EkleAsync(VergiTakvimiKaydetDto dto, CancellationToken ct = default)
        {
            var satir = new VergiTakvimi();
            await UygulaAsync(satir, dto, ct);

            _db.VergiTakvimi.Add(satir);
            await _db.SaveChangesAsync(ct);
            return Dto(satir, 0);
        }

        public async Task<VergiTakvimiDto> GuncelleAsync(int id, VergiTakvimiKaydetDto dto, CancellationToken ct = default)
        {
            var satir = await _db.VergiTakvimi.FirstOrDefaultAsync(t => t.Id == id, ct)
                        ?? throw new KeyNotFoundException("Takvim satırı bulunamadı.");

            var sayi = await TamamlamaSayisiAsync(id, ct);

            // Tamamlanmış bir satırın dönemini değiştirmek, "Ağustos yapıldı" kaydını
            // sessizce Eylül'e taşır. Tarih ve ad düzeltilebilir; dönem değişmez.
            if (sayi > 0 && (satir.MukellefiyetKodu != dto.MukellefiyetKodu?.Trim() || satir.Tekrar != dto.Tekrar
                             || satir.Yil != dto.Yil || satir.DonemNo != dto.DonemNo))
                throw new YapilacaklarKuralException(nameof(dto.DonemNo),
                    "Bu satırda yapıldı işareti var; kod ve dönem değiştirilemez. Son gün, ad ve aktiflik düzeltilebilir.");

            await UygulaAsync(satir, dto, ct);
            await _db.SaveChangesAsync(ct);
            return Dto(satir, sayi);
        }

        public async Task SilAsync(int id, CancellationToken ct = default)
        {
            var satir = await _db.VergiTakvimi.FirstOrDefaultAsync(t => t.Id == id, ct)
                        ?? throw new KeyNotFoundException("Takvim satırı bulunamadı.");

            if (await TamamlamaSayisiAsync(id, ct) > 0)
                throw new YapilacaklarKuralException("id",
                    "Bu satırda yapıldı işareti var; silinemez. İş üretmesin istiyorsanız pasife alın.");

            _db.VergiTakvimi.Remove(satir);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<int> VarsayilanlariYukleAsync(CancellationToken ct = default)
        {
            var (eklenen, _) = await VergiTakvimiSeed.SeedAsync(_db, _saat.GetLocalNow().Date, ct);
            return eklenen;
        }

        private Task<int> TamamlamaSayisiAsync(int id, CancellationToken ct)
            => _db.IsTamamlamalari.CountAsync(t => t.KaynakTip == IsKaynagi.Yasal && t.KaynakId == id, ct);

        private async Task UygulaAsync(VergiTakvimi satir, VergiTakvimiKaydetDto dto, CancellationToken ct)
        {
            var kod = dto.MukellefiyetKodu?.Trim() ?? string.Empty;
            if (!KodDeseni.IsMatch(kod))
                throw new YapilacaklarKuralException(nameof(dto.MukellefiyetKodu), "Mükellefiyet kodu 0 ile başlayan dört rakam olmalı (ör. 0015).");

            var ad = dto.Ad?.Trim() ?? string.Empty;
            if (ad.Length == 0)
                throw new YapilacaklarKuralException(nameof(dto.Ad), "Ad boş olamaz.");
            if (ad.Length > 150)
                throw new YapilacaklarKuralException(nameof(dto.Ad), "Ad en çok 150 karakter olabilir.");

            if (dto.Tekrar is not (IsTekrari.Aylik or IsTekrari.UcAylik or IsTekrari.Yillik))
                throw new YapilacaklarKuralException(nameof(dto.Tekrar), "Takvim satırı aylık, üç aylık ya da yıllık olmalı.");

            if (dto.Yil is < 2000 or > 2100)
                throw new YapilacaklarKuralException(nameof(dto.Yil), "Geçersiz yıl.");

            var enFazla = dto.Tekrar switch { IsTekrari.Aylik => 12, IsTekrari.UcAylik => 4, _ => 1 };
            if (dto.DonemNo < 1 || dto.DonemNo > enFazla)
                throw new YapilacaklarKuralException(nameof(dto.DonemNo), $"Dönem 1 ile {enFazla} arasında olmalı.");

            var donem = new IsDonemi(dto.Tekrar, dto.Yil, dto.DonemNo);
            if (dto.SonGun.Date < donem.Bit)
                throw new YapilacaklarKuralException(nameof(dto.SonGun), "Son gün dönem bitmeden olamaz.");

            var cakisan = await _db.VergiTakvimi.AnyAsync(t => t.Id != satir.Id && t.MukellefiyetKodu == kod
                                                                && t.Tekrar == dto.Tekrar && t.Yil == dto.Yil
                                                                && t.DonemNo == dto.DonemNo, ct);
            if (cakisan)
                throw new YapilacaklarKuralException(nameof(dto.DonemNo), "Bu kodun bu dönemi takvimde zaten var.");

            satir.MukellefiyetKodu = kod;
            satir.Ad = ad;
            satir.Tekrar = dto.Tekrar;
            satir.Yil = dto.Yil;
            satir.DonemNo = dto.DonemNo;
            satir.DonemBas = donem.Bas;
            satir.DonemBit = donem.Bit;
            satir.SonGun = dto.SonGun.Date;
            satir.Aktif = dto.Aktif;
        }

        private static VergiTakvimiDto Dto(VergiTakvimi s, int tamamlama) => new()
        {
            Id = s.Id,
            MukellefiyetKodu = s.MukellefiyetKodu,
            Ad = s.Ad,
            Tekrar = s.Tekrar,
            Yil = s.Yil,
            DonemNo = s.DonemNo,
            DonemBas = s.DonemBas,
            DonemBit = s.DonemBit,
            SonGun = s.SonGun,
            Aktif = s.Aktif,
            TamamlamaSayisi = tamamlama
        };
    }
}
