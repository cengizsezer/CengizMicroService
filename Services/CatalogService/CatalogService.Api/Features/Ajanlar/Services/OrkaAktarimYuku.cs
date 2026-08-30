using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CatalogService.Api.Features.Ajanlar.Services
{
    /// <summary>
    /// <c>OrkayaAktar</c> işinin yükü. Ajanın ORKA'yı sürerken ihtiyaç duyduğu her
    /// şey burada; <b>dosyalar yüke gömülmüyor</b>, ajan onları ayrıca indiriyor.
    /// </summary>
    public class OrkayaAktarYuku
    {
        public int EkstreYuklemeId { get; set; }
        public int FirmaId { get; set; }

        /// <summary>Ekstresi işlenen banka hesabının ORKA kodu — kaydın diğer bacağı.</summary>
        public string BankaHesabiOrkaKodu { get; set; } = string.Empty;

        /// <summary>ORKA giriş zincirinde firmanın açıldığı kod (ör. "0001").</summary>
        public string FirmaKodu { get; set; } = string.Empty;

        /// <summary>
        /// ORKA'ya gidecek satır sayısı. Ajan indirdiği iki dosyayı bununla
        /// karşılaştırıyor: sayı tutmuyorsa kodlar yanlış satırlara giderdi.
        /// </summary>
        public int SatirSayisi { get; set; }
    }

    public interface IOrkaAktarimYuku
    {
        /// <summary>
        /// Yükü sunucuda hazırlar. Eksik bir şey varsa <c>Hata</c> dolu döner ve iş
        /// hiç oluşturulmaz — ajanı yola çıkarıp orada durdurmaktansa burada durmak.
        /// </summary>
        Task<(string? Yuk, string? Hata)> HazirlaAsync(int ekstreYuklemeId, CancellationToken ct = default);
    }

    public class OrkaAktarimYuku : IOrkaAktarimYuku
    {
        private readonly CatalogContext _db;
        private readonly IEkstreService _ekstreler;
        private readonly IBankaFirmaKapsami _kapsam;

        public OrkaAktarimYuku(CatalogContext db, IEkstreService ekstreler, IBankaFirmaKapsami kapsam)
        {
            _db = db;
            _ekstreler = ekstreler;
            _kapsam = kapsam;
        }

        public async Task<(string? Yuk, string? Hata)> HazirlaAsync(int ekstreYuklemeId, CancellationToken ct = default)
        {
            if (ekstreYuklemeId <= 0)
                return (null, "Aktarılacak ekstre seçilmedi.");

            var yukleme = await _db.EkstreYuklemeler.AsNoTracking()
                .Include(y => y.BankaHesabi)
                .FirstOrDefaultAsync(y => y.Id == ekstreYuklemeId, ct);

            if (yukleme is null)
                return (null, "Ekstre yüklemesi bulunamadı.");

            var hesapKodu = yukleme.BankaHesabi?.OrkaHesapKodu;
            if (string.IsNullOrWhiteSpace(hesapKodu))
                return (null, "Banka hesabının ORKA kodu tanımlı değil; " +
                              "Banka Otomasyon > Tanımlar ekranından girin.");

            var firmaKodu = await _db.Firmalar.AsNoTracking()
                .Where(f => f.Id == yukleme.FirmaId)
                .Select(f => f.OrkaFirmaKodu)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(firmaKodu))
                return (null, "Firmanın ORKA firma kodu tanımlı değil; " +
                              "Yönetim > Firmalarım ekranından girin.");

            // Satır sayısı dışa aktarımın kendi mantığından geliyor: "diğer bankada"
            // işaretli satırlar ORKA'ya gitmiyor ve iki dosyada da yoklar.
            _kapsam.Ayarla(yukleme.FirmaId);

            BankaEkstre.Dtos.DisaAktarimSonucDto? aktarim;
            try
            {
                aktarim = await _ekstreler.DisaAktarAsync(ekstreYuklemeId, ct);
            }
            catch (BankaEkstreKuralException ex)
            {
                // Çözülemeyen ya da onay bekleyen satır var: robotu göndermenin anlamı yok.
                return (null, ex.Message);
            }

            if (aktarim is null) return (null, "Ekstre yüklemesi bulunamadı.");
            if (aktarim.SatirSayisi == 0)
                return (null, "ORKA'ya gidecek satır yok; bütün satırlar başka bankada işlenmiş.");

            var yuk = new OrkayaAktarYuku
            {
                EkstreYuklemeId = ekstreYuklemeId,
                FirmaId = yukleme.FirmaId,
                BankaHesabiOrkaKodu = hesapKodu!,
                FirmaKodu = firmaKodu!,
                SatirSayisi = aktarim.SatirSayisi
            };

            return (JsonSerializer.Serialize(yuk), null);
        }
    }
}
