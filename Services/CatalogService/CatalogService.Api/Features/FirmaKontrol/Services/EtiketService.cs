using CatalogService.Api.Features.Anasayfa.Domain;
using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.FirmaKontrol.Domain;
using CatalogService.Api.Features.FirmaKontrol.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.FirmaKontrol.Services
{
    public class EtiketService : IEtiketService
    {
        private readonly CatalogContext _db;
        private readonly IFirmaOlayYazici? _olay;

        public EtiketService(CatalogContext db, IFirmaOlayYazici? olay = null)
        {
            _db = db;
            _olay = olay;
        }

        /// <summary>
        /// Kural olayı firmanın "Son işlemler"ine yazılır. Kuralın firması yoksa boyutun
        /// firmasına bakılır; ikisi de boşsa (tüm firmalara ait kural) olay yazılmaz.
        /// </summary>
        private async Task KuralOlayiAsync(EtiketKurali kural, int? boyutFirmaId, FirmaOlayTipi tip, CancellationToken ct)
        {
            if (_olay is null || (kural.FirmaId ?? boyutFirmaId) is not { } firmaId) return;

            var aciklama = tip == FirmaOlayTipi.EtiketKuraliEklendi
                ? $"Etiket kuralı eklendi: {kural.Desen}"
                : $"Etiket kuralı silindi: {kural.Desen}";

            await _olay.YazAsync(firmaId, tip, aciklama, ct);
        }

        public async Task<List<EtiketBoyutuDto>> GetBoyutlarAsync(int firmaId, CancellationToken ct = default)
        {
            var boyutlar = await _db.EtiketBoyutlari
                .AsNoTracking()
                .Include(b => b.Degerler)
                .Include(b => b.Kurallar)
                .Where(b => b.Kapsam == EtiketKapsami.TumFirmalar || b.FirmaId == firmaId)
                .OrderBy(b => b.Sira).ThenBy(b => b.Id)
                .ToListAsync(ct);

            return boyutlar.Select(b => new EtiketBoyutuDto
            {
                Id = b.Id,
                Ad = b.Ad,
                Kapsam = b.Kapsam,
                FirmaId = b.FirmaId,
                Sira = b.Sira,
                KapsamHesaplari = b.KapsamHesaplari,

                Degerler = b.Degerler
                    .OrderBy(d => d.Sira).ThenBy(d => d.Id)
                    .Select(d => new EtiketDegeriDto
                    {
                        Id = d.Id,
                        BoyutId = d.BoyutId,
                        Ad = d.Ad,
                        Renk = d.Renk,
                        Sira = d.Sira,
                        FirmaId = d.FirmaId
                    }).ToList(),

                // Yalnızca bu firmada geçerli kurallar: tüm firmalar (null) + bu firma.
                Kurallar = b.Kurallar
                    .Where(k => k.FirmaId == null || k.FirmaId == firmaId)
                    .OrderBy(k => k.Sira).ThenBy(k => k.Id)
                    .Select(k => new EtiketKuraliDto
                    {
                        Id = k.Id,
                        BoyutId = k.BoyutId,
                        Sira = k.Sira,
                        EslesmeTipi = k.EslesmeTipi,
                        Desen = k.Desen,
                        DegerId = k.DegerId,
                        FirmaId = k.FirmaId
                    }).ToList()
            }).ToList();
        }

        public async Task<EtiketBoyutuDto> BoyutEkleAsync(int firmaId, EtiketBoyutuYazDto dto, CancellationToken ct = default)
        {
            var boyut = new EtiketBoyutu
            {
                Ad = (dto.Ad ?? string.Empty).Trim(),
                Kapsam = dto.Kapsam,
                FirmaId = dto.Kapsam == EtiketKapsami.BuFirma ? firmaId : null,
                Sira = dto.Sira,
                KapsamHesaplari = (dto.KapsamHesaplari ?? string.Empty).Trim()
            };

            if (boyut.Ad.Length == 0)
                throw new ArgumentException("Boyut adı boş olamaz.");

            if (boyut.KapsamHesaplari.Length == 0)
                throw new ArgumentException("Kapsam hesapları boş olamaz (örn. \"600,601,610\" ya da \"6*\").");

            await AyniIsimKontrolAsync(firmaId, boyut.Ad, haricBoyutId: null, ct);

            _db.EtiketBoyutlari.Add(boyut);
            await _db.SaveChangesAsync(ct);

            return new EtiketBoyutuDto
            {
                Id = boyut.Id,
                Ad = boyut.Ad,
                Kapsam = boyut.Kapsam,
                FirmaId = boyut.FirmaId,
                Sira = boyut.Sira,
                KapsamHesaplari = boyut.KapsamHesaplari
            };
        }

        public async Task BoyutGuncelleAsync(int firmaId, int boyutId, EtiketBoyutuYazDto dto, CancellationToken ct = default)
        {
            var boyut = await BulAsync(_db.EtiketBoyutlari, boyutId, "Etiket boyutu", ct);

            // Kapsam türü (TumFirmalar / BuFirma) sonradan değiştirilmez; yalnızca ad,
            // sıra ve kapsam hesapları güncellenir.
            boyut.Ad = (dto.Ad ?? string.Empty).Trim();
            boyut.Sira = dto.Sira;
            boyut.KapsamHesaplari = (dto.KapsamHesaplari ?? string.Empty).Trim();

            if (boyut.Ad.Length == 0)
                throw new ArgumentException("Boyut adı boş olamaz.");

            if (boyut.KapsamHesaplari.Length == 0)
                throw new ArgumentException("Kapsam hesapları boş olamaz (örn. \"600,601,610\" ya da \"6*\").");

            await AyniIsimKontrolAsync(firmaId, boyut.Ad, haricBoyutId: boyutId, ct);

            await _db.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Firmanın gördüğü boyutlar (tüm firmalar + bu firmaya özel) arasında aynı adda
        /// ikinci bir boyut olmasın. Karşılaştırma büyük/küçük harf ve Türkçe karakter
        /// duyarsızdır ("Grup Şirketi" = "GRUP ŞİRKETİ").
        /// </summary>
        private async Task AyniIsimKontrolAsync(int firmaId, string ad, int? haricBoyutId, CancellationToken ct)
        {
            var adlar = await _db.EtiketBoyutlari
                .AsNoTracking()
                .Where(b => b.Kapsam == EtiketKapsami.TumFirmalar || b.FirmaId == firmaId)
                .Where(b => haricBoyutId == null || b.Id != haricBoyutId)
                .Select(b => b.Ad)
                .ToListAsync(ct);

            var aranan = AdAnahtari(ad);
            if (adlar.Any(a => AdAnahtari(a) == aranan))
                throw new ArgumentException($"\"{ad}\" adında bir boyut zaten var. Farklı bir ad seçin.");
        }

        private static readonly System.Globalization.CultureInfo Tr = new("tr-TR");

        private static string AdAnahtari(string? ad) =>
            string.Join(' ', (ad ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLower(Tr);

        public async Task BoyutSilAsync(int boyutId, CancellationToken ct = default)
        {
            var boyut = await _db.EtiketBoyutlari.FirstOrDefaultAsync(b => b.Id == boyutId, ct);
            if (boyut is null) return;

            // Kurallar değere Restrict ile bağlı; önce onları düşür, sonra boyut cascade eder.
            var kurallar = await _db.EtiketKurallari.Where(k => k.BoyutId == boyutId).ToListAsync(ct);
            if (kurallar.Count > 0) _db.EtiketKurallari.RemoveRange(kurallar);

            _db.EtiketBoyutlari.Remove(boyut);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<EtiketDegeriDto> DegerEkleAsync(int boyutId, EtiketDegeriYazDto dto, CancellationToken ct = default)
        {
            _ = await BulAsync(_db.EtiketBoyutlari, boyutId, "Etiket boyutu", ct);

            var deger = new EtiketDegeri
            {
                BoyutId = boyutId,
                Ad = (dto.Ad ?? string.Empty).Trim(),
                Renk = dto.Renk,
                Sira = dto.Sira,
                FirmaId = dto.FirmaId
            };

            if (deger.Ad.Length == 0)
                throw new ArgumentException("Etiket adı boş olamaz.");

            _db.EtiketDegerleri.Add(deger);
            await _db.SaveChangesAsync(ct);

            return new EtiketDegeriDto
            {
                Id = deger.Id,
                BoyutId = deger.BoyutId,
                Ad = deger.Ad,
                Renk = deger.Renk,
                Sira = deger.Sira,
                FirmaId = deger.FirmaId
            };
        }

        public async Task DegerGuncelleAsync(int degerId, EtiketDegeriYazDto dto, CancellationToken ct = default)
        {
            var deger = await BulAsync(_db.EtiketDegerleri, degerId, "Etiket değeri", ct);

            deger.Ad = (dto.Ad ?? string.Empty).Trim();
            deger.Renk = dto.Renk;
            deger.Sira = dto.Sira;
            deger.FirmaId = dto.FirmaId;

            if (deger.Ad.Length == 0)
                throw new ArgumentException("Etiket adı boş olamaz.");

            await _db.SaveChangesAsync(ct);
        }

        public async Task DegerSilAsync(int degerId, CancellationToken ct = default)
        {
            var deger = await _db.EtiketDegerleri.FirstOrDefaultAsync(d => d.Id == degerId, ct);
            if (deger is null) return;

            // Bu değeri kullanan kurallar anlamsız kalır; birlikte silinir (FK Restrict).
            var kurallar = await _db.EtiketKurallari.Where(k => k.DegerId == degerId).ToListAsync(ct);
            if (kurallar.Count > 0) _db.EtiketKurallari.RemoveRange(kurallar);

            _db.EtiketDegerleri.Remove(deger);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<EtiketKuraliDto> KuralEkleAsync(int boyutId, EtiketKuraliYazDto dto, CancellationToken ct = default)
        {
            var boyut = await BulAsync(_db.EtiketBoyutlari, boyutId, "Etiket boyutu", ct);
            _ = await BulAsync(_db.EtiketDegerleri, dto.DegerId, "Etiket değeri", ct);

            var kural = new EtiketKurali
            {
                BoyutId = boyutId,
                Sira = dto.Sira,
                EslesmeTipi = dto.EslesmeTipi,
                Desen = (dto.Desen ?? string.Empty).Trim(),
                DegerId = dto.DegerId,
                FirmaId = dto.FirmaId
            };

            if (kural.Desen.Length == 0)
                throw new ArgumentException("Kural deseni boş olamaz.");

            _db.EtiketKurallari.Add(kural);
            await _db.SaveChangesAsync(ct);

            await KuralOlayiAsync(kural, boyut.FirmaId, FirmaOlayTipi.EtiketKuraliEklendi, ct);

            return new EtiketKuraliDto
            {
                Id = kural.Id,
                BoyutId = kural.BoyutId,
                Sira = kural.Sira,
                EslesmeTipi = kural.EslesmeTipi,
                Desen = kural.Desen,
                DegerId = kural.DegerId,
                FirmaId = kural.FirmaId
            };
        }

        public async Task KuralGuncelleAsync(long kuralId, EtiketKuraliYazDto dto, CancellationToken ct = default)
        {
            var kural = await _db.EtiketKurallari.FirstOrDefaultAsync(k => k.Id == kuralId, ct)
                        ?? throw new KeyNotFoundException($"Etiket kuralı bulunamadı: Id={kuralId}");

            _ = await BulAsync(_db.EtiketDegerleri, dto.DegerId, "Etiket değeri", ct);

            kural.Sira = dto.Sira;
            kural.EslesmeTipi = dto.EslesmeTipi;
            kural.Desen = (dto.Desen ?? string.Empty).Trim();
            kural.DegerId = dto.DegerId;
            kural.FirmaId = dto.FirmaId;

            await _db.SaveChangesAsync(ct);
        }

        public async Task KuralSilAsync(long kuralId, CancellationToken ct = default)
        {
            var kural = await _db.EtiketKurallari.FirstOrDefaultAsync(k => k.Id == kuralId, ct);
            if (kural is null) return;

            var boyutFirmaId = await _db.EtiketBoyutlari.Where(b => b.Id == kural.BoyutId)
                .Select(b => b.FirmaId).FirstOrDefaultAsync(ct);

            _db.EtiketKurallari.Remove(kural);
            await _db.SaveChangesAsync(ct);

            await KuralOlayiAsync(kural, boyutFirmaId, FirmaOlayTipi.EtiketKuraliSilindi, ct);
        }

        public async Task KuralSiraGuncelleAsync(int boyutId, List<EtiketKuralSiraDto> siralar, CancellationToken ct = default)
        {
            if (siralar is null || siralar.Count == 0) return;

            var idler = siralar.Select(s => s.KuralId).ToHashSet();

            var kurallar = await _db.EtiketKurallari
                .Where(k => k.BoyutId == boyutId && idler.Contains(k.Id))
                .ToListAsync(ct);

            foreach (var k in kurallar)
            {
                var yeni = siralar.First(s => s.KuralId == k.Id);
                k.Sira = yeni.Sira;
            }

            await _db.SaveChangesAsync(ct);
        }

        private static async Task<T> BulAsync<T>(DbSet<T> set, object id, string ad, CancellationToken ct)
            where T : class
        {
            var kayit = await set.FindAsync(new[] { id }, ct);
            return kayit ?? throw new KeyNotFoundException($"{ad} bulunamadı: Id={id}");
        }
    }
}
