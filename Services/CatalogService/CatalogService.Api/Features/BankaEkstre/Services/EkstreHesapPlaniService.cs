using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Infrastructure.Context;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    public interface IEkstreHesapPlaniService
    {
        Task<List<HesapPlaniKaydiDto>> AraAsync(string? q, string? anaGrup, int enFazla, CancellationToken ct = default);
        Task<HesapPlaniKaydiDto?> KodaGoreAsync(string kod, CancellationToken ct = default);
        Task<HesapPlaniIceAktarimSonucDto> IceAktarAsync(Stream excel, CancellationToken ct = default);
        Task<int> SayAsync(CancellationToken ct = default);
        Task<HesapPlaniOzetDto> OzetAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// ORKA hesap planının içe aktarımı ve araması. Kod boşluklu saklanır ve boşluklu
    /// yazılır ("120 D22"); format hiçbir noktada değiştirilmez.
    /// </summary>
    public class EkstreHesapPlaniService : IEkstreHesapPlaniService
    {
        private readonly CatalogContext _db;
        private readonly IBankaFirmaKapsami _kapsam;

        public EkstreHesapPlaniService(CatalogContext db, IBankaFirmaKapsami kapsam)
        {
            _db = db;
            _kapsam = kapsam;
        }

        /// <summary>Seçili firmanın hesap planı. Kapsam her sorguda görünür yazılır.</summary>
        private IQueryable<HesapPlaniKaydi> Plan => _db.EkstreHesapPlani.Where(h => h.FirmaId == _kapsam.FirmaId);

        private static readonly string[] KodBasliklari = { "Hesap Kodu", "Hesap Kod", "Kod", "HesapKodu" };
        private static readonly string[] AdBasliklari = { "Hesap Adı", "Hesap Adi", "Ad", "Unvan", "Ünvan", "HesapAdi" };

        public async Task<List<HesapPlaniKaydiDto>> AraAsync(string? q, string? anaGrup, int enFazla, CancellationToken ct = default)
        {
            var sorgu = Plan.AsNoTracking().Where(h => h.Aktif);

            if (!string.IsNullOrWhiteSpace(anaGrup))
            {
                var grup = anaGrup.Trim();
                sorgu = sorgu.Where(h => h.AnaGrup == grup);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var arama = q.Trim();
                var normal = Normalizasyon.UnvanNormalize(arama);
                sorgu = sorgu.Where(h => h.Kod.Contains(arama) || h.Ad.Contains(arama) || h.NormalizeAd.Contains(normal));
            }

            var kayitlar = await sorgu
                .OrderBy(h => h.Kod)
                .Take(enFazla <= 0 ? 50 : Math.Min(enFazla, 500))
                .ToListAsync(ct);

            return kayitlar.Select(Esle).ToList();
        }

        public async Task<HesapPlaniKaydiDto?> KodaGoreAsync(string kod, CancellationToken ct = default)
        {
            var normal = Normalizasyon.HesapKoduNormalize(kod);
            var kayit = await Plan.AsNoTracking().FirstOrDefaultAsync(h => h.Kod == normal, ct);
            return kayit is null ? null : Esle(kayit);
        }

        public Task<int> SayAsync(CancellationToken ct = default)
            => Plan.CountAsync(h => h.Aktif, ct);

        /// <summary>
        /// Tanımlar ekranının hesap planı özeti: kaç kayıt var, en son ne zaman içe aktarıldı.
        /// Hesap planı yılda birkaç kez değişir; günlük ekranda yükleme formu yok, bunun
        /// yerine bayatlamışsa hatırlatma gösteriliyor.
        /// </summary>
        public async Task<HesapPlaniOzetDto> OzetAsync(CancellationToken ct = default)
        {
            var sayi = await Plan.CountAsync(h => h.Aktif, ct);

            var son = await Plan
                .OrderByDescending(h => h.SonGuncelleme)
                .Select(h => (DateTime?)h.SonGuncelleme)
                .FirstOrDefaultAsync(ct);

            // Varsayılan DateTime, hiç içe aktarım yapılmamış eski kayıtlarda görülür.
            if (son == default(DateTime)) son = null;

            return new HesapPlaniOzetDto
            {
                Sayi = sayi,
                SonIceAktarim = son,
                GunFarki = son is null ? null : (int)(DateTime.Now.Date - son.Value.Date).TotalDays
            };
        }

        /// <summary>
        /// xlsx içe aktarımı. Beklenen kolonlar: <c>Hesap Kodu</c>, <c>Hesap Adı</c>.
        /// Kayıt **silinmez**: yeni kodlar eklenir, değişen adlar güncellenir, ORKA
        /// dosyasında olmayanlar pasife çekilir (geçmiş satırların kod bağı kopmasın).
        /// </summary>
        public async Task<HesapPlaniIceAktarimSonucDto> IceAktarAsync(Stream excel, CancellationToken ct = default)
        {
            var sonuc = new HesapPlaniIceAktarimSonucDto();

            using var kitap = new XLWorkbook(excel);
            var sayfa = kitap.Worksheets.FirstOrDefault()
                        ?? throw new InvalidDataException("Excel dosyasında sayfa bulunamadı.");

            var (kolonKod, kolonAd, baslikSatiri) = BasliklariBul(sayfa);

            var mevcutlar = await Plan.ToDictionaryAsync(h => h.Kod, ct);
            var dosyadaGorulen = new HashSet<string>(StringComparer.Ordinal);
            var simdi = DateTime.Now;

            var sonSatir = sayfa.LastRowUsed()?.RowNumber() ?? 0;

            for (var satirNo = baslikSatiri + 1; satirNo <= sonSatir; satirNo++)
            {
                var satir = sayfa.Row(satirNo);
                if (satir.IsEmpty()) continue;

                var hamKod = satir.Cell(kolonKod).GetString();
                var hamAd = satir.Cell(kolonAd).GetString();

                if (string.IsNullOrWhiteSpace(hamKod) && string.IsNullOrWhiteSpace(hamAd)) continue;

                sonuc.Okunan++;

                var kod = Normalizasyon.HesapKoduNormalize(hamKod);
                var ad = hamAd.Trim();

                if (kod.Length == 0 || ad.Length == 0)
                {
                    sonuc.Atlanan++;
                    if (sonuc.Uyarilar.Count < 20)
                        sonuc.Uyarilar.Add($"Satır {satirNo}: kod veya ad boş, atlandı.");
                    continue;
                }

                if (!dosyadaGorulen.Add(kod))
                {
                    sonuc.Atlanan++;
                    if (sonuc.Uyarilar.Count < 20)
                        sonuc.Uyarilar.Add($"Satır {satirNo}: '{kod}' dosyada tekrar ediyor, ilk kayıt korundu.");
                    continue;
                }

                if (mevcutlar.TryGetValue(kod, out var mevcut))
                {
                    mevcut.Ad = Normalizasyon.Kirp(ad, 200);
                    mevcut.NormalizeAd = Normalizasyon.Kirp(Normalizasyon.UnvanNormalize(ad), 200);
                    mevcut.AnaGrup = Normalizasyon.AnaGrup(kod);
                    mevcut.BaslangicHarfi = Normalizasyon.BaslangicHarfi(kod);
                    mevcut.Aktif = true;
                    mevcut.SonGuncelleme = simdi;
                    sonuc.Guncellenen++;
                }
                else
                {
                    _db.EkstreHesapPlani.Add(new HesapPlaniKaydi
                    {
                        FirmaId = _kapsam.FirmaId,
                        Kod = kod,
                        Ad = Normalizasyon.Kirp(ad, 200),
                        NormalizeAd = Normalizasyon.Kirp(Normalizasyon.UnvanNormalize(ad), 200),
                        AnaGrup = Normalizasyon.AnaGrup(kod),
                        BaslangicHarfi = Normalizasyon.BaslangicHarfi(kod),
                        Aktif = true,
                        SonGuncelleme = simdi
                    });
                    sonuc.Eklenen++;
                }
            }

            // ORKA'da artık olmayan kodlar silinmez, pasife çekilir: geçmiş ekstre
            // satırlarındaki kodun karşılığı kaybolmasın.
            if (sonuc.Okunan > 0)
            {
                foreach (var (kod, kayit) in mevcutlar)
                {
                    if (dosyadaGorulen.Contains(kod) || !kayit.Aktif) continue;

                    kayit.Aktif = false;
                    sonuc.Pasiflenen++;
                }
            }

            await _db.SaveChangesAsync(ct);
            return sonuc;
        }

        /// <summary>Başlık satırını ilk 20 satırda isimle arar; bulunamazsa açık hata verir.</summary>
        private static (int Kod, int Ad, int BaslikSatiri) BasliklariBul(IXLWorksheet sayfa)
        {
            var sonTaranan = Math.Min(sayfa.LastRowUsed()?.RowNumber() ?? 0, 20);

            for (var satirNo = 1; satirNo <= sonTaranan; satirNo++)
            {
                var harita = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var hucre in sayfa.Row(satirNo).CellsUsed())
                {
                    var metin = hucre.GetString().Trim();
                    if (metin.Length > 0) harita.TryAdd(metin, hucre.Address.ColumnNumber);
                }

                var kod = Ara(harita, KodBasliklari);
                var ad = Ara(harita, AdBasliklari);
                if (kod is not null && ad is not null) return (kod.Value, ad.Value, satirNo);
            }

            throw new InvalidDataException(
                "Başlık satırı bulunamadı. Dosyada 'Hesap Kodu' ve 'Hesap Adı' kolonları olmalı.");
        }

        private static int? Ara(Dictionary<string, int> harita, string[] adaylar)
        {
            foreach (var ad in adaylar)
                if (harita.TryGetValue(ad, out var kolon)) return kolon;
            return null;
        }

        private static HesapPlaniKaydiDto Esle(HesapPlaniKaydi h) => new()
        {
            Id = h.Id,
            Kod = h.Kod,
            Ad = h.Ad,
            AnaGrup = h.AnaGrup,
            Aktif = h.Aktif
        };
    }
}
