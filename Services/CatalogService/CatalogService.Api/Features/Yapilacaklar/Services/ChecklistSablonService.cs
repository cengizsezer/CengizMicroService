using CatalogService.Api.Features.Anasayfa.Domain;
using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Infrastructure.Auth;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Yapilacaklar.Services
{
    public interface IChecklistSablonService
    {
        Task<List<ChecklistSablonuDto>> ListeAsync(CancellationToken ct = default);
        Task<ChecklistSablonuDto> GetirAsync(int id, CancellationToken ct = default);
        Task<ChecklistSablonuDto> EkleAsync(ChecklistSablonuKaydetDto dto, CancellationToken ct = default);

        /// <summary>Şablonu değiştirir; ondan daha önce oluşmuş işlere DOKUNMAZ.</summary>
        Task<ChecklistSablonuDto> GuncelleAsync(int id, ChecklistSablonuKaydetDto dto, CancellationToken ct = default);

        /// <summary>Kullanılmamış şablon silinir; kullanılmış (FirmaIsi.SablonId) şablon pasife alınır.</summary>
        Task<ChecklistSablonSilSonucDto> SilAsync(int id, CancellationToken ct = default);

        Task<FirmadanSablonOnizlemeDto> FirmadanOnizleAsync(int firmaId, CancellationToken ct = default);
        Task<ChecklistSablonuDto> FirmadanOlusturAsync(FirmadanSablonDto dto, CancellationToken ct = default);

        /// <summary>Yazmadan: firma başına eklenecek ve atlanacak maddeler.</summary>
        Task<SablondanSonucDto> SablondanOnizleAsync(SablondanIstekDto istek, CancellationToken ct = default);

        /// <summary>Maddeleri FirmaIsi olarak yazar — tek transaction; önizlemeyle aynı plan.</summary>
        Task<SablondanSonucDto> SablondanOlusturAsync(SablondanIstekDto istek, CancellationToken ct = default);
    }

    /// <summary>
    /// Checklist şablonları (Firma Panosu Aşama 3) — global "iş tanımı paketi". Şablon uygulanınca maddeleri
    /// seçilen firmalara ÖZEL İŞ (<see cref="FirmaIsi"/>, <c>SablonId</c> dolu) olarak yazılır; checklist örneği
    /// saklanmaz. Plan tek yerde: <see cref="ChecklistSablonPlanlayici.Planla"/> (önizleme = yazma).
    ///
    /// İş, KopyalaAsync ile aynı kurallarla kurulur (<see cref="YapilacaklarService.Dogrula"/> +
    /// <see cref="YapilacaklarService.Uygula"/>): sorumlu boş (firmanınki), tamamlama/ek/alıcı yok, başlangıç ayı
    /// isteğin <c>IlkDonem</c>'i.
    /// </summary>
    public class ChecklistSablonService : IChecklistSablonService
    {
        public const int AdEnFazla = 100;
        public const int AciklamaEnFazla = 500;
        public const int EnFazlaMadde = 200;
        public const int EnFazlaFirma = 300;

        private readonly CatalogContext _db;
        private readonly TimeProvider _saat;
        private readonly IHttpCurrentUser? _kullanici;
        private readonly IFirmaOlayYazici? _olay;

        public ChecklistSablonService(CatalogContext db, TimeProvider saat, IHttpCurrentUser? kullanici = null,
                                      IFirmaOlayYazici? olay = null)
        {
            _db = db;
            _saat = saat;
            _kullanici = kullanici;
            _olay = olay;
        }

        // ---- Şablon CRUD ----

        public async Task<List<ChecklistSablonuDto>> ListeAsync(CancellationToken ct = default)
        {
            var sablonlar = await _db.ChecklistSablonlari.AsNoTracking().Include(s => s.Maddeler).ToListAsync(ct);
            var kullanim = await _db.FirmaIsleri.AsNoTracking()
                .Where(i => i.SablonId != null)
                .GroupBy(i => i.SablonId!.Value).Select(g => new { g.Key, Sayi = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Sayi, ct);
            var (kategoriler, tarifler) = await BaglamAsync(ct);
            var tr = StringComparer.Create(IsTakvimHesabi.Tr, true);

            return sablonlar
                .OrderBy(s => !s.Aktif).ThenBy(s => s.Ad, tr)
                .Select(s => Dto(s, kullanim.GetValueOrDefault(s.Id), kategoriler, tarifler))
                .ToList();
        }

        public async Task<ChecklistSablonuDto> GetirAsync(int id, CancellationToken ct = default)
            => (await ListeAsync(ct)).FirstOrDefault(s => s.Id == id) ?? throw new KeyNotFoundException("Şablon bulunamadı.");

        public async Task<ChecklistSablonuDto> EkleAsync(ChecklistSablonuKaydetDto dto, CancellationToken ct = default)
        {
            var s = new ChecklistSablonu();
            await UygulaAsync(s, dto, mevcutKategoriler: new HashSet<int>(), ct);
            _db.ChecklistSablonlari.Add(s);
            await _db.SaveChangesAsync(ct);
            return await GetirAsync(s.Id, ct);
        }

        public async Task<ChecklistSablonuDto> GuncelleAsync(int id, ChecklistSablonuKaydetDto dto, CancellationToken ct = default)
        {
            var s = await _db.ChecklistSablonlari.Include(x => x.Maddeler).FirstOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new KeyNotFoundException("Şablon bulunamadı.");

            // Maddeler baştan yazılır. Oluşmuş FirmaIsi'ler maddeye değil şablona bağlı (SablonId) — dokunulmaz.
            var mevcutKategoriler = s.Maddeler.Where(m => m.KategoriId is not null).Select(m => m.KategoriId!.Value).ToHashSet();
            _db.ChecklistSablonMaddeleri.RemoveRange(s.Maddeler.ToList());
            s.Maddeler.Clear();
            await UygulaAsync(s, dto, mevcutKategoriler, ct);
            await _db.SaveChangesAsync(ct);
            return await GetirAsync(id, ct);
        }

        public async Task<ChecklistSablonSilSonucDto> SilAsync(int id, CancellationToken ct = default)
        {
            var s = await _db.ChecklistSablonlari.FirstOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new KeyNotFoundException("Şablon bulunamadı.");

            var kullanim = await _db.FirmaIsleri.CountAsync(i => i.SablonId == id, ct);
            if (kullanim > 0)
            {
                s.Aktif = false;
                await _db.SaveChangesAsync(ct);
                return new ChecklistSablonSilSonucDto
                {
                    PasifeAlindi = true,
                    Mesaj = $"\"{s.Ad}\" {kullanim} işte kullanıldı; silinmedi, pasife alındı. Oluşan işler yerinde duruyor."
                };
            }

            _db.ChecklistSablonlari.Remove(s);   // maddeler cascade
            await _db.SaveChangesAsync(ct);
            return new ChecklistSablonSilSonucDto { Silindi = true, Mesaj = $"\"{s.Ad}\" silindi." };
        }

        /// <param name="mevcutKategoriler">Şablonun kayıttaki kategorileri: pasife alınmış olsalar da yeniden kaydedilebilir.</param>
        private async Task UygulaAsync(ChecklistSablonu s, ChecklistSablonuKaydetDto dto, IReadOnlySet<int> mevcutKategoriler,
                                       CancellationToken ct)
        {
            var ad = dto.Ad?.Trim() ?? string.Empty;
            if (ad.Length == 0) throw new YapilacaklarKuralException("Ad", "Şablon adı boş olamaz.");
            if (ad.Length > AdEnFazla) throw new YapilacaklarKuralException("Ad", $"Şablon adı en çok {AdEnFazla} karakter olabilir.");
            var adlar = await _db.ChecklistSablonlari.AsNoTracking().Where(x => x.Id != s.Id).Select(x => x.Ad).ToListAsync(ct);
            if (adlar.FirstOrDefault(a => Anahtar(a) == Anahtar(ad)) is { } mevcutAd)
                throw new YapilacaklarKuralException("Ad", $"\"{mevcutAd}\" adlı şablon zaten var.");

            var aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? null : dto.Aciklama.Trim();
            if (aciklama is { Length: > AciklamaEnFazla })
                throw new YapilacaklarKuralException("Aciklama", $"Açıklama en çok {AciklamaEnFazla} karakter olabilir.");

            var maddeler = dto.Maddeler ?? new();
            if (maddeler.Count == 0) throw new YapilacaklarKuralException("Maddeler", "Şablonda en az bir madde olmalı.");
            if (maddeler.Count > EnFazlaMadde)
                throw new YapilacaklarKuralException("Maddeler", $"Şablonda en çok {EnFazlaMadde} madde olabilir.");

            var kategoriler = await _db.IsKategorileri.AsNoTracking().ToDictionaryAsync(k => k.Id, ct);
            var tarifIdleri = (await _db.IsTarifleri.AsNoTracking().Select(t => t.Id).ToListAsync(ct)).ToHashSet();
            var anahtarlar = new HashSet<string>(StringComparer.Ordinal);

            s.Ad = ad;
            s.Aciklama = aciklama;
            s.Aktif = dto.Aktif;

            for (var i = 0; i < maddeler.Count; i++)
            {
                var m = maddeler[i];
                var sira = i + 1;
                var baslik = m.Baslik?.Trim() ?? string.Empty;
                if (baslik.Length == 0) throw new YapilacaklarKuralException("Maddeler", $"{sira}. maddenin başlığı boş.");
                if (baslik.Length > 200) throw new YapilacaklarKuralException("Maddeler", $"{sira}. maddenin başlığı en çok 200 karakter olabilir.");
                if (!Enum.IsDefined(m.Tekrar) || m.Tekrar == IsTekrari.TekSefer)
                    throw new YapilacaklarKuralException("Maddeler", $"\"{baslik}\": şablonda tekrar her ay, üç ayda bir ya da her yıl olmalı (tek seferlik madde olmaz).");
                if (!Enum.IsDefined(m.GunKurali))
                    throw new YapilacaklarKuralException("Maddeler", $"\"{baslik}\": geçersiz gün kuralı.");
                if (m.GunKurali == IsGunKurali.AyinGunu && m.AyinGunu is not (>= 1 and <= 31))
                    throw new YapilacaklarKuralException("Maddeler", $"\"{baslik}\": ayın günü 1 ile 31 arasında olmalı.");

                // Aynı başlık + tekrar iki kez: uygulanınca ikincisi zaten atlanırdı (aynı kolon).
                if (!anahtarlar.Add(DonemPanosuKurucu.OzelKolonAnahtari(baslik, m.Tekrar)))
                    throw new YapilacaklarKuralException("Maddeler", $"\"{baslik}\" ({IsTakvimHesabi.TekrarAdi(m.Tekrar)}) şablonda iki kez var.");

                if (m.KategoriId is { } kid)
                {
                    if (!kategoriler.TryGetValue(kid, out var k))
                        throw new YapilacaklarKuralException("Maddeler", $"\"{baslik}\": kategori bulunamadı.");
                    if (!k.Aktif && !mevcutKategoriler.Contains(kid))
                        throw new YapilacaklarKuralException("Maddeler", $"\"{baslik}\": \"{k.Ad}\" pasif; pasif kategori atanamaz.");
                }
                if (m.IsTarifiId is { } tid && !tarifIdleri.Contains(tid))
                    throw new YapilacaklarKuralException("Maddeler", $"\"{baslik}\": iş tarifi bulunamadı.");

                s.Maddeler.Add(new ChecklistSablonMaddesi
                {
                    Sira = sira,
                    Baslik = baslik,
                    Tekrar = m.Tekrar,
                    GunKurali = m.GunKurali,
                    AyinGunu = m.GunKurali == IsGunKurali.AyinGunu ? m.AyinGunu : null,
                    KategoriId = m.KategoriId,
                    IsTarifiId = m.IsTarifiId
                });
            }
        }

        // ---- Firmadan şablon ----

        public async Task<FirmadanSablonOnizlemeDto> FirmadanOnizleAsync(int firmaId, CancellationToken ct = default)
        {
            var firma = await _db.Firmalar.AsNoTracking().FirstOrDefaultAsync(f => f.Id == firmaId, ct)
                        ?? throw new YapilacaklarKuralException("FirmaId", "Firma bulunamadı.");

            // Prosedür satırları (yasal) hariç — yalnız firmaya özel işler.
            var isler = await _db.FirmaIsleri.AsNoTracking()
                .Where(i => i.FirmaId == firmaId && i.YasalMukellefiyetKodu == null)
                .OrderBy(i => i.Id)
                .ToListAsync(ct);
            var (kategoriler, tarifler) = await BaglamAsync(ct);

            var onizleme = new FirmadanSablonOnizlemeDto { FirmaId = firmaId, FirmaAdi = FirmaPaneliKurucu.Ad(firma) };
            var anahtarlar = new HashSet<string>(StringComparer.Ordinal);
            foreach (var i in isler)
            {
                string? sebep = null;
                if (!i.Aktif) sebep = "pasif iş";
                else if (i.Tekrar == IsTekrari.TekSefer) sebep = "tek seferlik iş (şablonda tarih olmaz)";
                else if (!anahtarlar.Add(DonemPanosuKurucu.OzelKolonAnahtari(i.Baslik, i.Tekrar))) sebep = "aynı başlık ve tekrarla ikinci iş";

                if (sebep is not null)
                {
                    onizleme.Disarida.Add(new SablonAtlananDto { Baslik = i.Baslik, Tekrar = i.Tekrar, Sebep = sebep });
                    continue;
                }

                // Kopyalanan: başlık, tekrar, gün kuralı, kategori (pasifse taşınmaz), iş tarifi.
                // Kopyalanmayan: sorumlu, alıcı, ek, tamamlama, başlangıç ayı — firmaya özgü.
                var madde = new ChecklistSablonMaddesi
                {
                    Sira = onizleme.Maddeler.Count + 1,
                    Baslik = i.Baslik.Trim(),
                    Tekrar = i.Tekrar,
                    GunKurali = i.GunKurali,
                    AyinGunu = i.GunKurali == IsGunKurali.AyinGunu ? i.AyinGunu ?? 1 : null,
                    KategoriId = i.KategoriId is { } k && kategoriler.TryGetValue(k, out var kat) && kat.Aktif ? k : null,
                    IsTarifiId = i.IsTarifiId
                };
                onizleme.Maddeler.Add(MaddeDto(madde, kategoriler, tarifler));
            }

            return onizleme;
        }

        public async Task<ChecklistSablonuDto> FirmadanOlusturAsync(FirmadanSablonDto dto, CancellationToken ct = default)
        {
            var onizleme = await FirmadanOnizleAsync(dto.FirmaId, ct);
            if (onizleme.Maddeler.Count == 0)
                throw new YapilacaklarKuralException("FirmaId", $"{onizleme.FirmaAdi} firmasında şablona alınacak özel iş yok.");

            return await EkleAsync(new ChecklistSablonuKaydetDto
            {
                Ad = dto.Ad,
                Aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? $"{onizleme.FirmaAdi} firmasının işlerinden" : dto.Aciklama,
                Aktif = true,
                Maddeler = onizleme.Maddeler
            }, ct);
        }

        // ---- Şablondan iş ----

        public async Task<SablondanSonucDto> SablondanOnizleAsync(SablondanIstekDto istek, CancellationToken ct = default)
            => (await PlanlaAsync(istek, ct)).Sonuc;

        public async Task<SablondanSonucDto> SablondanOlusturAsync(SablondanIstekDto istek, CancellationToken ct = default)
        {
            ChecklistSablonPlanlayici.Plan? plan = null;
            var deneme = 0;

            // Retry stratejisi açık: transaction stratejinin içinde; yeniden denemede izlenenler atılır, plan yeniden kurulur.
            await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                if (++deneme > 1) _db.ChangeTracker.Clear();
                var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
                await using (tx)
                {
                    plan = await PlanlaAsync(istek, ct);
                    var simdi = _saat.GetUtcNow().UtcDateTime;
                    var olusturan = KullaniciId();

                    foreach (var f in plan.Firmalar)
                    {
                        foreach (var e in f.Eklenecekler)
                        {
                            var kaydet = new FirmaIsiKaydetDto
                            {
                                Baslik = e.Madde.Baslik,
                                Tekrar = e.Madde.Tekrar,
                                GunKurali = e.Madde.GunKurali,
                                AyinGunu = e.Madde.AyinGunu,
                                IlkDonem = plan.Sonuc.IlkDonem,
                                Aktif = true,
                                KategoriId = e.KategoriId
                            };
                            YapilacaklarService.Dogrula(kaydet);

                            var isi = new FirmaIsi { FirmaId = f.FirmaId, OlusturanKullaniciId = olusturan, OlusturmaZamani = simdi };
                            YapilacaklarService.Uygula(isi, kaydet);
                            isi.IsTarifiId = e.Madde.IsTarifiId;
                            isi.SablonId = plan.Sonuc.SablonId;
                            _db.FirmaIsleri.Add(isi);
                        }
                    }

                    await _db.SaveChangesAsync(ct);
                    if (tx is not null) await tx.CommitAsync(ct);
                }
            });

            plan!.Sonuc.Yazildi = true;

            // Olay kaydı asıl yazmadan SONRA, ayrı (FirmaOlayYazici asıl işlemi bozmaz).
            foreach (var f in plan.Sonuc.Firmalar.Where(f => f.Eklenenler.Count > 0))
                await OlayAsync(f.FirmaId, $"Şablondan {f.Eklenenler.Count} iş eklendi: {plan.Sonuc.SablonAdi}", ct);

            return plan.Sonuc;
        }

        private async Task<ChecklistSablonPlanlayici.Plan> PlanlaAsync(SablondanIstekDto istek, CancellationToken ct)
        {
            var sablon = await _db.ChecklistSablonlari.AsNoTracking().Include(s => s.Maddeler)
                             .FirstOrDefaultAsync(s => s.Id == istek.SablonId, ct)
                         ?? throw new YapilacaklarKuralException("SablonId", "Şablon bulunamadı.");
            if (!sablon.Aktif) throw new YapilacaklarKuralException("SablonId", $"\"{sablon.Ad}\" pasif; pasif şablondan iş oluşturulmaz.");
            if (sablon.Maddeler.Count == 0) throw new YapilacaklarKuralException("SablonId", $"\"{sablon.Ad}\" şablonunda madde yok.");

            var firmaIdleri = (istek.FirmaIdleri ?? new()).Distinct().ToList();
            if (firmaIdleri.Count == 0) throw new YapilacaklarKuralException("FirmaIdleri", "En az bir firma seçin.");
            if (firmaIdleri.Count > EnFazlaFirma)
                throw new YapilacaklarKuralException("FirmaIdleri", $"Bir seferde en çok {EnFazlaFirma} firma seçilebilir.");

            YapilacaklarService.IlkDonemDogrula(istek.IlkDonem);
            var ilkDonem = string.IsNullOrWhiteSpace(istek.IlkDonem) ? null : istek.IlkDonem.Trim();

            var firmalar = await _db.Firmalar.AsNoTracking().Where(f => firmaIdleri.Contains(f.Id)).ToListAsync(ct);
            if (firmalar.Count != firmaIdleri.Count) throw new YapilacaklarKuralException("FirmaIdleri", "Seçilen firmalardan biri bulunamadı.");

            // Firmanın özel işleri (pasifler dahil, prosedür satırları hariç) — AyniAdKontrol'ün kümesi.
            var mevcut = (await _db.FirmaIsleri.AsNoTracking()
                    .Where(i => firmaIdleri.Contains(i.FirmaId) && i.YasalMukellefiyetKodu == null)
                    .Select(i => new { i.FirmaId, i.Baslik, i.Tekrar })
                    .ToListAsync(ct))
                .GroupBy(i => i.FirmaId)
                .ToDictionary(g => g.Key, g => (IReadOnlySet<string>)g.Select(i => DonemPanosuKurucu.OzelKolonAnahtari(i.Baslik, i.Tekrar))
                                                                     .ToHashSet(StringComparer.Ordinal));

            var tr = StringComparer.Create(IsTakvimHesabi.Tr, true);
            var girdiler = firmalar
                .Select(f => new ChecklistSablonPlanlayici.FirmaGirdisi(f.Id, FirmaPaneliKurucu.Ad(f),
                    mevcut.GetValueOrDefault(f.Id) ?? new HashSet<string>()))
                .OrderBy(f => f.FirmaAdi, tr).ThenBy(f => f.FirmaId)
                .ToList();

            var (kategoriler, tarifler) = await BaglamAsync(ct);
            return ChecklistSablonPlanlayici.Planla(sablon, sablon.Maddeler, girdiler, kategoriler,
                                                    tarifler.ToDictionary(t => t.Key, t => t.Value.KategoriId), ilkDonem);
        }

        // ---- yardımcılar ----

        private async Task<(Dictionary<int, IsKategorisi> Kategoriler, Dictionary<int, IsTarifi> Tarifler)> BaglamAsync(CancellationToken ct)
            => (await _db.IsKategorileri.AsNoTracking().ToDictionaryAsync(k => k.Id, ct),
                await _db.IsTarifleri.AsNoTracking().Select(t => new IsTarifi { Id = t.Id, Ad = t.Ad, KategoriId = t.KategoriId })
                         .ToDictionaryAsync(t => t.Id, ct));

        private static ChecklistSablonuDto Dto(ChecklistSablonu s, int kullanim, IReadOnlyDictionary<int, IsKategorisi> kategoriler,
                                               IReadOnlyDictionary<int, IsTarifi> tarifler)
        {
            var maddeler = s.Maddeler.OrderBy(m => m.Sira).ThenBy(m => m.Id).ToList();
            return new ChecklistSablonuDto
            {
                Id = s.Id,
                Ad = s.Ad,
                Aciklama = s.Aciklama,
                Aktif = s.Aktif,
                KullanimSayisi = kullanim,
                KategoriSayisi = ChecklistSablonPlanlayici.KategoriSayilari(maddeler, kategoriler,
                    tarifler.ToDictionary(t => t.Key, t => t.Value.KategoriId)).Count,
                Maddeler = maddeler.Select(m => MaddeDto(m, kategoriler, tarifler)).ToList()
            };
        }

        private static ChecklistSablonMaddesiDto MaddeDto(ChecklistSablonMaddesi m, IReadOnlyDictionary<int, IsKategorisi> kategoriler,
                                                          IReadOnlyDictionary<int, IsTarifi> tarifler)
        {
            kategoriler.TryGetValue(m.KategoriId ?? 0, out var k);
            return new ChecklistSablonMaddesiDto
            {
                Id = m.Id,
                Baslik = m.Baslik,
                Tekrar = m.Tekrar,
                GunKurali = m.GunKurali,
                AyinGunu = m.AyinGunu,
                KategoriId = m.KategoriId,
                IsTarifiId = m.IsTarifiId,
                KategoriAdi = k?.Ad,
                KategoriPasif = k is { Aktif: false },
                IsTarifiAdi = m.IsTarifiId is { } t && tarifler.TryGetValue(t, out var tarif) ? tarif.Ad : null,
                KuralMetni = IsTakvimHesabi.KuralMetni(m.Tekrar, m.GunKurali, m.AyinGunu, null)
            };
        }

        private static string Anahtar(string ad) => ad.Trim().ToUpper(IsTakvimHesabi.Tr);

        private Task OlayAsync(int firmaId, string aciklama, CancellationToken ct)
            => _olay?.YazAsync(firmaId, FirmaOlayTipi.IsEklendi, aciklama, ct) ?? Task.CompletedTask;

        private string? KullaniciId()
        {
            try
            {
                return _kullanici is { IsAuthenticated: true } ? _kullanici.UserId : null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}
