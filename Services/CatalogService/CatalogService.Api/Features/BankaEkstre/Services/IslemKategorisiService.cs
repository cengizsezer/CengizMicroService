using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    public interface IIslemKategorisiService
    {
        Task<List<IslemKategorisiDto>> GetHepsiAsync(CancellationToken ct = default);

        /// <summary>
        /// Kategoriler görünümü: seçili bankanın kuralları kategorilere dağıtılmış hâlde.
        /// Yeni banka eklenirken eksik kategorileri görmek için kontrol listesi.
        /// </summary>
        Task<KategoriKapsamOzetiDto> KapsamAsync(string? parserTipi, CancellationToken ct = default);

        Task<IslemKategorisiDto> CreateAsync(IslemKategorisiYazDto dto, CancellationToken ct = default);
        Task<IslemKategorisiDto?> UpdateAsync(int id, IslemKategorisiYazDto dto, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    }

    /// <summary>
    /// İşlem kategorilerinin yönetimi ve kategori görünümü.
    ///
    /// Tablo <b>global</b>: kategoriler bankadan ve firmadan bağımsız. Görünüm ise banka
    /// bazlı — kuralların bir kısmı bankaya (ayrıştırıcıya) bağlı: sabit kurallar ve
    /// açıklama şablonları. Vergi kodları (global) ve kişi yönlendirmeleri (firma bazlı)
    /// her bankada geçerli olduğu için görünümde her zaman yer alır.
    ///
    /// <b>Eşleştirmeye dokunmaz.</b> Buradaki hiçbir işlem katman sırasını, eşikleri veya
    /// desenleri değiştirmez; kategori kuralın üzerinde bir etikettir.
    /// </summary>
    public class IslemKategorisiService : IIslemKategorisiService
    {
        private readonly CatalogContext _db;
        private readonly IEkstreParserSecici _parserSecici;
        private readonly IBankaFirmaKapsami _kapsam;

        public IslemKategorisiService(CatalogContext db, IEkstreParserSecici parserSecici, IBankaFirmaKapsami kapsam)
        {
            _db = db;
            _parserSecici = parserSecici;
            _kapsam = kapsam;
        }

        public async Task<List<IslemKategorisiDto>> GetHepsiAsync(CancellationToken ct = default)
        {
            var kayitlar = await _db.EkstreIslemKategorileri.AsNoTracking()
                .OrderBy(k => k.Sira).ThenBy(k => k.Id)
                .ToListAsync(ct);

            return kayitlar.Select(Esle).ToList();
        }

        public async Task<KategoriKapsamOzetiDto> KapsamAsync(string? parserTipi, CancellationToken ct = default)
        {
            var tip = (parserTipi ?? string.Empty).Trim();

            var kategoriler = await _db.EkstreIslemKategorileri.AsNoTracking()
                .OrderBy(k => k.Sira).ThenBy(k => k.Id)
                .ToListAsync(ct);

            // Banka bazlı tablolarda "boş ParserTipi = tüm bankalar" kuralı geçerli;
            // görünüm de aynı süzmeyi kullanıyor ki ekran ile eşleştirici aynı kümeyi görsün.
            var kurallar = await _db.EkstreSabitKurallar.AsNoTracking()
                .Where(k => k.ParserTipi == string.Empty || k.ParserTipi == tip)
                .OrderBy(k => k.Sira).ThenBy(k => k.Id)
                .ToListAsync(ct);

            var sablonlar = await _db.EkstreAciklamaSablonlari.AsNoTracking()
                .Where(s => s.ParserTipi == string.Empty || s.ParserTipi == tip)
                .OrderBy(s => s.Sira).ThenBy(s => s.Id)
                .ToListAsync(ct);

            var vergiler = await _db.EkstreVergiKodlari.AsNoTracking()
                .OrderBy(v => v.Sira).ThenBy(v => v.Id)
                .ToListAsync(ct);

            var kisiler = await _db.EkstreKisiYonlendirmeleri.AsNoTracking()
                .Where(k => k.FirmaId == _kapsam.FirmaId)
                .OrderBy(k => k.Isim)
                .ToListAsync(ct);

            var satirlar = new List<(int? KategoriId, KategoriKuralDto Kural)>();

            satirlar.AddRange(kurallar.Select(k => (k.IslemKategorisiId, new KategoriKuralDto
            {
                Id = k.Id,
                Mekanizma = Mekanizmalar.SabitKural,
                Ad = k.IslemTipiDeseni,
                HesapKodu = k.HesapKodu,
                HesapAdi = k.HesapAdi,
                Aktif = k.Aktif
            })));

            // Şablonun hesap kodu yok: açıklama üretir, karşı hesabı belirlemez.
            satirlar.AddRange(sablonlar.Select(s => (s.IslemKategorisiId, new KategoriKuralDto
            {
                Id = s.Id,
                Mekanizma = Mekanizmalar.Sablon,
                Ad = s.IslemTipiDeseni,
                HesapKodu = null,
                HesapAdi = s.Sablon,
                Aktif = s.Aktif
            })));

            satirlar.AddRange(vergiler.Select(v => (v.IslemKategorisiId, new KategoriKuralDto
            {
                Id = v.Id,
                Mekanizma = Mekanizmalar.VergiKodu,
                Ad = string.Join(" · ", new[] { v.VergiKodu, v.AnahtarKelime }
                                        .Where(x => !string.IsNullOrWhiteSpace(x))),
                HesapKodu = v.HesapKodu,
                HesapAdi = v.HesapAdi,
                Aktif = v.Aktif
            })));

            satirlar.AddRange(kisiler.Select(k => (k.IslemKategorisiId, new KategoriKuralDto
            {
                Id = k.Id,
                Mekanizma = Mekanizmalar.Kisi,
                Ad = k.Isim,
                HesapKodu = k.HesapKodu,
                HesapAdi = k.HesapAdi,
                Aktif = k.Aktif
            })));

            var kategoriliDto = kategoriler.Select(k =>
            {
                var kendi = satirlar.Where(x => x.KategoriId == k.Id).Select(x => x.Kural).ToList();

                return new KategoriKapsamDto
                {
                    Id = k.Id,
                    Ad = k.Ad,
                    VarsayilanAnaGrup = k.VarsayilanAnaGrup,
                    Sira = k.Sira,
                    Aktif = k.Aktif,
                    KuralSayisi = kendi.Count,
                    // Kodlar tekrarsız ve sıralı: "195 · 196" gibi tek satırda yazılıyor.
                    HesapKodlari = kendi.Select(x => x.HesapKodu)
                                        .Where(x => !string.IsNullOrWhiteSpace(x))
                                        .Select(x => x!)
                                        .Distinct(StringComparer.Ordinal)
                                        .OrderBy(x => x, StringComparer.Ordinal)
                                        .ToList(),
                    Kurallar = kendi
                };
            }).ToList();

            return new KategoriKapsamOzetiDto
            {
                ParserTipi = tip.Length == 0 ? null : tip,
                ParserAdi = YapilandirmaDogrulama.ParserAdi(_parserSecici, tip),
                Toplam = kategoriliDto.Count,
                Tanimli = kategoriliDto.Count(k => k.KuralSayisi > 0),
                // Kategorisi boş kalan kurallar hiçbir satırda görünmez; sayısı ekranda
                // uyarı olarak yazılıyor ki liste "her şey tanımlı" izlenimi vermesin.
                KategorisizKural = satirlar.Count(x => x.KategoriId is null),
                Kategoriler = kategoriliDto
            };
        }

        public async Task<IslemKategorisiDto> CreateAsync(IslemKategorisiYazDto dto, CancellationToken ct = default)
        {
            var ad = Dogrula(dto);
            await AdTekilMiAsync(ad, null, ct);

            var kayit = new IslemKategorisi();
            Uygula(kayit, dto, ad);

            _db.EkstreIslemKategorileri.Add(kayit);
            await _db.SaveChangesAsync(ct);

            return Esle(kayit);
        }

        public async Task<IslemKategorisiDto?> UpdateAsync(int id, IslemKategorisiYazDto dto, CancellationToken ct = default)
        {
            var ad = Dogrula(dto);
            await AdTekilMiAsync(ad, id, ct);

            var kayit = await _db.EkstreIslemKategorileri.FirstOrDefaultAsync(k => k.Id == id, ct);
            if (kayit is null) return null;

            Uygula(kayit, dto, ad);
            await _db.SaveChangesAsync(ct);

            return Esle(kayit);
        }

        /// <summary>
        /// Kategoriyi siler. Bağlı kurallar <b>silinmez</b>: yabancı anahtar
        /// <c>SetNull</c> ile tanımlı, kural kategorisiz kalır ve çalışmaya devam eder.
        /// Kategori yalnız etiket olduğu için silinmesi eşleştirmeyi değiştirmez.
        /// </summary>
        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var kayit = await _db.EkstreIslemKategorileri.FirstOrDefaultAsync(k => k.Id == id, ct);
            if (kayit is null) return false;

            // EF SetNull davranışı yalnız yüklenmiş (izlenen) bağlı kayıtlarda çalışır;
            // veritabanı kısıtı da SetNull olduğu için sunucu tarafı zaten temizler. Bellek
            // içi sağlayıcıda (testler) kısıt yok, bu yüzden alan burada da boşaltılıyor.
            foreach (var k in await _db.EkstreSabitKurallar.Where(x => x.IslemKategorisiId == id).ToListAsync(ct))
                k.IslemKategorisiId = null;

            foreach (var s in await _db.EkstreAciklamaSablonlari.Where(x => x.IslemKategorisiId == id).ToListAsync(ct))
                s.IslemKategorisiId = null;

            foreach (var v in await _db.EkstreVergiKodlari.Where(x => x.IslemKategorisiId == id).ToListAsync(ct))
                v.IslemKategorisiId = null;

            foreach (var y in await _db.EkstreKisiYonlendirmeleri.Where(x => x.IslemKategorisiId == id).ToListAsync(ct))
                y.IslemKategorisiId = null;

            _db.EkstreIslemKategorileri.Remove(kayit);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        // ---- Yardımcılar ----

        /// <summary>Accordion'daki küçük mekanizma etiketleri; tek yerde tanımlı.</summary>
        public static class Mekanizmalar
        {
            public const string SabitKural = "sabit kural";
            public const string Sablon = "şablon";
            public const string VergiKodu = "vergi kodu";
            public const string Kisi = "kişi";
        }

        private static string Dogrula(IslemKategorisiYazDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Ad))
                throw new BankaEkstreKuralException(nameof(dto.Ad), "Kategori adı boş olamaz.");

            return Normalizasyon.Kirp(dto.Ad.Trim(), 100);
        }

        /// <summary>
        /// Ad tekilliği. Karşılaştırma <b>bellekte ve harf duyarsız</b>: veritabanı
        /// karşılaştırması sağlayıcının harmanlamasına (collation) bağlı ve "Banka gideri"
        /// ile "banka gideri" iki ayrı satır olarak yazılabilirdi — kategori listesi
        /// kontrol listesi olarak kullanıldığı için tekrar eden satır sayımı bozar.
        /// Tablo yirmi satır civarında; tam okuma maliyeti önemsiz.
        /// </summary>
        private async Task AdTekilMiAsync(string ad, int? haricId, CancellationToken ct)
        {
            var adlar = await _db.EkstreIslemKategorileri.AsNoTracking()
                .Where(k => k.Id != (haricId ?? 0))
                .Select(k => k.Ad)
                .ToListAsync(ct);

            var varMi = adlar.Any(a => string.Equals(a, ad, StringComparison.OrdinalIgnoreCase));

            if (varMi)
                throw new BankaEkstreKuralException(nameof(IslemKategorisiYazDto.Ad),
                    $"'{ad}' kategorisi zaten var. Aynı ad iki kez tanımlanırsa sayımlar bozulur.");
        }

        private static void Uygula(IslemKategorisi kayit, IslemKategorisiYazDto dto, string ad)
        {
            kayit.Ad = ad;
            // Ana grup ORKA kodunun ilk segmenti; "120 D22" girilse de "120" saklanır.
            kayit.VarsayilanAnaGrup = string.IsNullOrWhiteSpace(dto.VarsayilanAnaGrup)
                ? null
                : Normalizasyon.AnaGrup(dto.VarsayilanAnaGrup);
            kayit.Sira = dto.Sira;
            kayit.Aktif = dto.Aktif;
        }

        private static IslemKategorisiDto Esle(IslemKategorisi k) => new()
        {
            Id = k.Id,
            Ad = k.Ad,
            VarsayilanAnaGrup = k.VarsayilanAnaGrup,
            Sira = k.Sira,
            Aktif = k.Aktif
        };
    }
}
