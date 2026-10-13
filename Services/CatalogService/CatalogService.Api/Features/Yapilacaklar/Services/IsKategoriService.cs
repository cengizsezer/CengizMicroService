using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Yapilacaklar.Services
{
    public interface IIsKategoriService
    {
        Task<List<IsKategorisiDto>> ListeAsync(CancellationToken ct = default);
        Task<IsKategorisiDto> EkleAsync(IsKategorisiKaydetDto dto, CancellationToken ct = default);

        /// <summary>Ad ve aktiflik. Pasife almak silmenin yerine geçer: çözümde atlanır, atanamaz.</summary>
        Task<IsKategorisiDto> GuncelleAsync(int id, IsKategorisiKaydetDto dto, CancellationToken ct = default);

        /// <summary>Yeni sıra: bütün kategori Id'leri istenen sırada.</summary>
        Task<List<IsKategorisiDto>> SiralaAsync(List<int> idler, CancellationToken ct = default);

        /// <summary>Yalnız hiçbir yerde kullanılmayan kategori silinir; yasal varsayılan hiç silinmez.</summary>
        Task SilAsync(int id, CancellationToken ct = default);

        /// <summary>Yasal varsayılanı bu kategoriye taşır: eskisinden kaldırılır, buna verilir. Hedef aktif olmalı.</summary>
        Task<List<IsKategorisiDto>> YasalVarsayilanYapAsync(int id, CancellationToken ct = default);

        Task<List<MukellefiyetEslemeDto>> MukellefiyetlerAsync(CancellationToken ct = default);
        Task<MukellefiyetEslemeDto> MukellefiyetKaydetAsync(string kod, MukellefiyetEslemeKaydetDto dto, CancellationToken ct = default);

        Task<List<KategorisizGrupDto>> KategorisizAsync(CancellationToken ct = default);

        /// <summary>Grubun KATEGORİSİZ işlerine uygulanır; başka gruba ve kategorisi olan işe dokunmaz.</summary>
        Task<TopluKategoriSonucDto> TopluAtaAsync(TopluKategoriAtaDto dto, CancellationToken ct = default);
    }

    /// <summary>
    /// İş kategorileri (Firma Panosu Aşama 2) — global. Çözüm kuralı burada DEĞİL,
    /// <see cref="IsKategoriCozucu"/>'da; bu servis yalnız kayıtları yönetir.
    /// </summary>
    public class IsKategoriService : IIsKategoriService
    {
        public const int AdEnFazla = 100;

        private readonly CatalogContext _db;

        public IsKategoriService(CatalogContext db) => _db = db;

        // ---- Kategoriler ----

        public async Task<List<IsKategorisiDto>> ListeAsync(CancellationToken ct = default)
        {
            var kategoriler = await _db.IsKategorileri.AsNoTracking().ToListAsync(ct);
            var isler = await _db.FirmaIsleri.AsNoTracking()
                .Where(i => i.KategoriId != null && i.YasalMukellefiyetKodu == null)
                .GroupBy(i => i.KategoriId!.Value).Select(g => new { g.Key, Sayi = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Sayi, ct);
            var tarifler = await _db.IsTarifleri.AsNoTracking()
                .Where(t => t.KategoriId != null)
                .GroupBy(t => t.KategoriId!.Value).Select(g => new { g.Key, Sayi = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Sayi, ct);
            var eslemeler = await _db.MukellefiyetKategorileri.AsNoTracking()
                .GroupBy(m => m.KategoriId).Select(g => new { g.Key, Sayi = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Sayi, ct);
            var sablonMaddeleri = await _db.ChecklistSablonMaddeleri.AsNoTracking()
                .Where(m => m.KategoriId != null)
                .GroupBy(m => m.KategoriId!.Value).Select(g => new { g.Key, Sayi = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Sayi, ct);
            return kategoriler
                .OrderBy(k => k.Sira).ThenBy(k => k.Id)
                .Select(k => new IsKategorisiDto
                {
                    Id = k.Id, Ad = k.Ad, Sira = k.Sira, Aktif = k.Aktif,
                    IsSayisi = isler.GetValueOrDefault(k.Id),
                    TarifSayisi = tarifler.GetValueOrDefault(k.Id),
                    EslemeSayisi = eslemeler.GetValueOrDefault(k.Id),
                    SablonMaddeSayisi = sablonMaddeleri.GetValueOrDefault(k.Id),
                    YasalVarsayilan = k.VarsayilanYasal
                })
                .ToList();
        }

        public async Task<IsKategorisiDto> EkleAsync(IsKategorisiKaydetDto dto, CancellationToken ct = default)
        {
            var ad = await AdDogrulaAsync(dto.Ad, haricId: null, ct);
            var sira = (await _db.IsKategorileri.MaxAsync(k => (int?)k.Sira, ct) ?? 0) + 1;
            var k = new IsKategorisi { Ad = ad, Sira = sira, Aktif = dto.Aktif };
            _db.IsKategorileri.Add(k);
            await _db.SaveChangesAsync(ct);
            return (await ListeAsync(ct)).Single(x => x.Id == k.Id);
        }

        public async Task<IsKategorisiDto> GuncelleAsync(int id, IsKategorisiKaydetDto dto, CancellationToken ct = default)
        {
            var k = await _db.IsKategorileri.FirstOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new KeyNotFoundException("Kategori bulunamadı.");
            if (k.VarsayilanYasal && !dto.Aktif)
                throw new YapilacaklarKuralException("Aktif",
                    $"\"{k.Ad}\" yasal işlerin varsayılanı; pasife alınamaz. Önce varsayılanı başka bir kategoriye taşıyın.");

            // Ad serbestçe değişir: yasal varsayılan kolonla bulunur, yasal işler etkilenmez.
            k.Ad = await AdDogrulaAsync(dto.Ad, id, ct);
            k.Aktif = dto.Aktif;
            await _db.SaveChangesAsync(ct);
            return (await ListeAsync(ct)).Single(x => x.Id == id);
        }

        public async Task<List<IsKategorisiDto>> SiralaAsync(List<int> idler, CancellationToken ct = default)
        {
            var kategoriler = await _db.IsKategorileri.ToListAsync(ct);
            if (idler.Count != kategoriler.Count || idler.Distinct().Count() != idler.Count
                || kategoriler.Any(k => !idler.Contains(k.Id)))
                throw new YapilacaklarKuralException("idler", "Sıralama bütün kategorileri bir kez içermeli; liste değişmiş olabilir, yenileyin.");

            for (var i = 0; i < idler.Count; i++) kategoriler.Single(k => k.Id == idler[i]).Sira = i + 1;
            await _db.SaveChangesAsync(ct);
            return await ListeAsync(ct);
        }

        public async Task SilAsync(int id, CancellationToken ct = default)
        {
            var k = await _db.IsKategorileri.FirstOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new KeyNotFoundException("Kategori bulunamadı.");

            if (k.VarsayilanYasal)
                throw new YapilacaklarKuralException("id",
                    $"\"{k.Ad}\" yasal işlerin varsayılanı; silinemez. Önce varsayılanı başka bir kategoriye taşıyın.");

            // Prosedür satırları da sayılır: FK'leri varsa silme veritabanında patlardı.
            var isSayisi = await _db.FirmaIsleri.CountAsync(i => i.KategoriId == id, ct);
            var tarifSayisi = await _db.IsTarifleri.CountAsync(t => t.KategoriId == id, ct);
            var eslemeSayisi = await _db.MukellefiyetKategorileri.CountAsync(m => m.KategoriId == id, ct);
            var sablonSayisi = await _db.ChecklistSablonMaddeleri.CountAsync(m => m.KategoriId == id, ct);
            if (isSayisi + tarifSayisi + eslemeSayisi + sablonSayisi > 0)
            {
                var parca = new[] { (isSayisi, "iş"), (tarifSayisi, "tarif"), (eslemeSayisi, "yasal eşleme"), (sablonSayisi, "şablon maddesi") }
                    .Where(p => p.Item1 > 0).Select(p => $"{p.Item1} {p.Item2}");
                throw new YapilacaklarKuralException("id",
                    $"\"{k.Ad}\" kullanılıyor ({string.Join(", ", parca)}); silinemez. Kullanılmasın istiyorsanız pasife alın.");
            }

            _db.IsKategorileri.Remove(k);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<List<IsKategorisiDto>> YasalVarsayilanYapAsync(int id, CancellationToken ct = default)
        {
            var hedef = await _db.IsKategorileri.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                        ?? throw new KeyNotFoundException("Kategori bulunamadı.");
            if (!hedef.Aktif)
                throw new YapilacaklarKuralException("id", $"\"{hedef.Ad}\" pasif; yasal varsayılan aktif bir kategori olmalı.");
            if (hedef.VarsayilanYasal) return await ListeAsync(ct);

            // Filtreli unique index: önce eskisi kaldırılır, SONRA yenisi verilir — iki ayrı SaveChanges, tek
            // transaction. Retry stratejisi açık; transaction stratejinin içinde, yeniden denemede izlenenler atılır.
            var deneme = 0;
            await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                if (++deneme > 1) _db.ChangeTracker.Clear();
                var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
                await using (tx)
                {
                    foreach (var eski in await _db.IsKategorileri.Where(x => x.VarsayilanYasal).ToListAsync(ct))
                        eski.VarsayilanYasal = false;
                    await _db.SaveChangesAsync(ct);

                    (await _db.IsKategorileri.FirstAsync(x => x.Id == id, ct)).VarsayilanYasal = true;
                    await _db.SaveChangesAsync(ct);

                    if (tx is not null) await tx.CommitAsync(ct);
                }
            });

            return await ListeAsync(ct);
        }

        private async Task<string> AdDogrulaAsync(string? ham, int? haricId, CancellationToken ct)
        {
            var ad = ham?.Trim() ?? string.Empty;
            if (ad.Length == 0) throw new YapilacaklarKuralException("Ad", "Kategori adı boş olamaz.");
            if (ad.Length > AdEnFazla) throw new YapilacaklarKuralException("Ad", $"Kategori adı en çok {AdEnFazla} karakter olabilir.");
            if (Ayni(ad, IsKategoriCozucu.DigerAdi))
                throw new YapilacaklarKuralException("Ad", "\"Diğer\" ayrılmış bölümdür (kategorisi olmayan işler); başka ad seçin.");

            var adlar = await _db.IsKategorileri.AsNoTracking().Where(k => k.Id != (haricId ?? 0)).Select(k => k.Ad).ToListAsync(ct);
            if (adlar.FirstOrDefault(a => Ayni(a, ad)) is { } mevcutAd)
                throw new YapilacaklarKuralException("Ad", $"\"{mevcutAd}\" adlı kategori zaten var.");
            return ad;
        }

        private static bool Ayni(string a, string b)
            => string.Equals(a.Trim().ToUpper(IsTakvimHesabi.Tr), b.Trim().ToUpper(IsTakvimHesabi.Tr), StringComparison.Ordinal);

        /// <summary>
        /// Atanacak kategori: var ve aktif olmalı. <paramref name="mevcut"/> ile aynıysa (kayıt dokunulmadan
        /// yeniden kaydedildi) pasif olsa da geçer — pasife alınan kategori kaydı açınca hata vermesin.
        /// </summary>
        public static async Task AtanabilirAsync(CatalogContext db, int? kategoriId, int? mevcut, CancellationToken ct)
        {
            if (kategoriId is not { } id || id == mevcut) return;
            var k = await db.IsKategorileri.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new YapilacaklarKuralException("KategoriId", "Kategori bulunamadı.");
            if (!k.Aktif) throw new YapilacaklarKuralException("KategoriId", $"\"{k.Ad}\" pasif; pasif kategori atanamaz.");
        }

        // ---- Yasal eşleme ----

        public async Task<List<MukellefiyetEslemeDto>> MukellefiyetlerAsync(CancellationToken ct = default)
        {
            // Takvimdeki her kod (yasal iş yalnız takvimdeki koddan türer). Ad: kodun en yeni satırı.
            var takvim = await _db.VergiTakvimi.AsNoTracking()
                .Select(t => new { t.MukellefiyetKodu, t.Ad, t.Yil, t.DonemNo })
                .ToListAsync(ct);
            var eslemeler = await _db.MukellefiyetKategorileri.AsNoTracking()
                .ToDictionaryAsync(m => m.MukellefiyetKodu, m => m.KategoriId, ct);

            return takvim
                .GroupBy(t => t.MukellefiyetKodu)
                .Select(g => new MukellefiyetEslemeDto
                {
                    MukellefiyetKodu = g.Key,
                    Ad = g.OrderByDescending(t => t.Yil).ThenByDescending(t => t.DonemNo).First().Ad,
                    KategoriId = eslemeler.TryGetValue(g.Key, out var k) ? k : null
                })
                .OrderBy(e => e.MukellefiyetKodu, StringComparer.Ordinal)
                .ToList();
        }

        public async Task<MukellefiyetEslemeDto> MukellefiyetKaydetAsync(string kod, MukellefiyetEslemeKaydetDto dto,
                                                                         CancellationToken ct = default)
        {
            kod = kod?.Trim() ?? string.Empty;
            if (!await _db.VergiTakvimi.AnyAsync(t => t.MukellefiyetKodu == kod, ct))
                throw new KeyNotFoundException("Bu mükellefiyet kodu vergi takviminde yok.");

            var mevcut = await _db.MukellefiyetKategorileri.FirstOrDefaultAsync(m => m.MukellefiyetKodu == kod, ct);
            await AtanabilirAsync(_db, dto.KategoriId, mevcut?.KategoriId, ct);

            if (dto.KategoriId is not { } id)
            {
                if (mevcut is not null) _db.MukellefiyetKategorileri.Remove(mevcut);
            }
            else if (mevcut is null)
            {
                _db.MukellefiyetKategorileri.Add(new MukellefiyetKategorisi { MukellefiyetKodu = kod, KategoriId = id });
            }
            else
            {
                mevcut.KategoriId = id;
            }

            await _db.SaveChangesAsync(ct);
            return (await MukellefiyetlerAsync(ct)).Single(e => e.MukellefiyetKodu == kod);
        }

        // ---- Kategorisiz özel işler ----

        public async Task<List<KategorisizGrupDto>> KategorisizAsync(CancellationToken ct = default)
        {
            var (isler, baglam) = await KategorisizIslerAsync(ct);
            var tr = StringComparer.Create(IsTakvimHesabi.Tr, true);

            return isler
                .GroupBy(i => DonemPanosuKurucu.OzelKolonAnahtari(i.Baslik, i.Tekrar))
                .Select(g => new KategorisizGrupDto
                {
                    Anahtar = g.Key,
                    // Gösterilen yazım: grupta en çok kullanılan (eşitse en eski kayıt).
                    Baslik = g.GroupBy(i => i.Baslik.Trim()).OrderByDescending(x => x.Count()).ThenBy(x => x.Min(i => i.Id)).First().Key,
                    Tekrar = g.First().Tekrar,
                    FirmaSayisi = g.Select(i => i.FirmaId).Distinct().Count(),
                    IsSayisi = g.Count()
                })
                .OrderByDescending(g => g.FirmaSayisi)
                .ThenBy(g => g.Baslik, tr)
                .ThenBy(g => g.Tekrar)
                .ToList();
        }

        public async Task<TopluKategoriSonucDto> TopluAtaAsync(TopluKategoriAtaDto dto, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.Anahtar)) throw new YapilacaklarKuralException("Anahtar", "Grup seçilmedi.");
            await AtanabilirAsync(_db, dto.KategoriId, mevcut: null, ct);

            var (isler, _) = await KategorisizIslerAsync(ct);
            var idler = isler.Where(i => DonemPanosuKurucu.OzelKolonAnahtari(i.Baslik, i.Tekrar) == dto.Anahtar)
                             .Select(i => i.Id).ToList();
            if (idler.Count == 0)
                throw new KeyNotFoundException("Bu grupta kategorisiz iş kalmamış; liste değişmiş olabilir, yenileyin.");

            var kayitlar = await _db.FirmaIsleri.Where(i => idler.Contains(i.Id)).ToListAsync(ct);
            foreach (var i in kayitlar) i.KategoriId = dto.KategoriId;
            await _db.SaveChangesAsync(ct);
            return new TopluKategoriSonucDto { Guncellenen = kayitlar.Count };
        }

        /// <summary>Çözümü "Diğer" olan özel işler (prosedür satırları hariç) — liste ve toplu atama aynı kümeyi kullanır.</summary>
        private async Task<(List<FirmaIsi> Isler, IsKategoriBaglami Baglam)> KategorisizIslerAsync(CancellationToken ct)
        {
            var baglam = await IsKategoriBaglami.YukleAsync(_db, ct);
            var isler = (await _db.FirmaIsleri.AsNoTracking()
                    .Where(i => i.YasalMukellefiyetKodu == null)
                    .Select(i => new FirmaIsi { Id = i.Id, FirmaId = i.FirmaId, Baslik = i.Baslik, Tekrar = i.Tekrar })
                    .ToListAsync(ct))
                .Where(i => baglam.OzelIsler.TryGetValue(i.Id, out var k) && IsKategoriCozucu.OzelKategorisiz(k.Kendi, k.Tarif, baglam))
                .ToList();
            return (isler, baglam);
        }
    }
}
