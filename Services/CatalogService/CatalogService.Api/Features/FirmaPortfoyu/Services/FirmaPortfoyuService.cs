using System.Text.RegularExpressions;
using CatalogService.Api.Features.FirmaPortfoyu.Domain;
using CatalogService.Api.Features.FirmaPortfoyu.Dtos;
using CatalogService.Api.Infrastructure.Auth;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.FirmaPortfoyu.Services
{
    public interface IFirmaPortfoyuService
    {
        /// <summary>Kaydetmeden değerlendirir (yükleme penceresinin önizlemesi). Kayıtla aynı doğrulama ve hesap.</summary>
        Task<AylikMizanOnizlemeDto> OnizleAsync(AylikMizanKaydetDto dto, CancellationToken ct = default);

        /// <summary>Kaydeder. Aynı firma + ay varsa ve <c>UzerineYaz</c> false ise <see cref="AyniAyVarException"/>.</summary>
        Task<AylikMizanOnizlemeDto> KaydetAsync(AylikMizanKaydetDto dto, CancellationToken ct = default);

        Task SilAsync(int firmaId, int yil, int ay, CancellationToken ct = default);

        /// <summary>Aktif firmalar × 12 ay: yüklendi mi, dengeli mi, uyarılar.</summary>
        Task<MizanDurumuDto> DurumAsync(int yil, CancellationToken ct = default);

        /// <summary>
        /// Seçili dönemde her aktif firmanın göstergeleri ve 12 aylık serisi. <paramref name="donem"/> boşsa
        /// mizanı olan en son ay (yoksa geçen ay). <paramref name="karsilastir"/>: "ay" ya da "yil".
        /// </summary>
        Task<PortfoyDto> PortfoyAsync(string? donem, string? karsilastir, CancellationToken ct = default);
    }

    /// <summary>Kural ihlali → 400 <c>{ field, message }</c>.</summary>
    public sealed class FirmaPortfoyuKuralException : Exception
    {
        public FirmaPortfoyuKuralException(string field, string message) : base(message) => Field = field;
        public string Field { get; }
    }

    /// <summary>Aynı firma + ayın mizanı var ve üzerine yazma onayı yok → 409 (gövde: önizleme + mevcut).</summary>
    public sealed class AyniAyVarException : Exception
    {
        public AyniAyVarException(AylikMizanOnizlemeDto onizleme)
            : base("Bu firmanın bu ayına ait mizan zaten var; değiştirmek için onaylayın.") => Onizleme = onizleme;

        public AylikMizanOnizlemeDto Onizleme { get; }
    }

    /// <summary>
    /// Firma Portföyü — aylık mizan yükleme (Aşama 1). Dosya sunucuya gelmez: istemci ExcelMizanParser ile okur,
    /// ayrıştırılmış ana hesap satırlarını gönderir; sunucu her şeyi KENDİSİ yeniden doğrular ve hesaplar
    /// (PortfoyHesaplayici). Yalnız Firmalarım'daki AKTİF firmalar.
    /// </summary>
    public class FirmaPortfoyuService : IFirmaPortfoyuService
    {
        public const int AzamiSatir = 2000;

        private static readonly Regex AnaHesapKodu = new(@"^\d{3}$", RegexOptions.Compiled);

        private readonly CatalogContext _db;
        private readonly TimeProvider _saat;
        private readonly IHttpCurrentUser? _kullanici;

        public FirmaPortfoyuService(CatalogContext db, TimeProvider saat, IHttpCurrentUser? kullanici = null)
        {
            _db = db;
            _saat = saat;
            _kullanici = kullanici;
        }

        public async Task<AylikMizanOnizlemeDto> OnizleAsync(AylikMizanKaydetDto dto, CancellationToken ct = default)
        {
            var (firmaAdi, satirlar) = await DogrulaAsync(dto, ct);
            return (await DegerlendirAsync(dto, firmaAdi, satirlar, ct)).Sonuc;
        }

        public async Task<AylikMizanOnizlemeDto> KaydetAsync(AylikMizanKaydetDto dto, CancellationToken ct = default)
        {
            var (firmaAdi, satirlar) = await DogrulaAsync(dto, ct);
            var (sonuc, degerlendirme) = await DegerlendirAsync(dto, firmaAdi, satirlar, ct);

            if (sonuc.Mevcut is not null && !dto.UzerineYaz)
                throw new AyniAyVarException(sonuc);

            var simdi = _saat.GetLocalNow().DateTime;
            var kullaniciId = int.TryParse(_kullanici?.UserId, out var k) ? k : (int?)null;
            var kullaniciAdi = Kirp(_kullanici?.UserName, 100);

            // Eskisini sil + yenisini yaz tek transaction; SQL Server'da execution strategy içinde (yeniden denemede
            // izlenen nesneler temizlenir). InMemory'de transaction yok.
            var deneme = 0;
            await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                if (++deneme > 1) _db.ChangeTracker.Clear();
                var tx = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
                await using (tx)
                {
                    var eski = await _db.AylikMizanlar.Include(m => m.Satirlar)
                        .FirstOrDefaultAsync(m => m.FirmaId == dto.FirmaId && m.Yil == dto.Yil && m.Ay == dto.Ay, ct);
                    if (eski is not null)
                    {
                        if (!dto.UzerineYaz) throw new AyniAyVarException(sonuc);
                        _db.AylikMizanSatirlari.RemoveRange(eski.Satirlar);
                        _db.AylikMizanlar.Remove(eski);
                        await _db.SaveChangesAsync(ct);
                    }

                    _db.AylikMizanlar.Add(new AylikMizan
                    {
                        FirmaId = dto.FirmaId,
                        Yil = dto.Yil,
                        Ay = dto.Ay,
                        DosyaAdi = DosyaAdi(dto.DosyaAdi),
                        YuklemeTarihi = simdi,
                        YukleyenKullaniciId = kullaniciId,
                        YukleyenKullanici = kullaniciAdi,
                        BorcBakiyeToplami = degerlendirme.BorcToplami,
                        AlacakBakiyeToplami = degerlendirme.AlacakToplami,
                        DengeFarki = degerlendirme.DengeFarki,
                        Dengeli = degerlendirme.Dengeli,
                        KapanisSonrasiMi = degerlendirme.KapanisSonrasi,
                        GiderYontemi = degerlendirme.GiderYontemi,
                        SatirSayisi = satirlar.Count,
                        Satirlar = satirlar.Select(s => new AylikMizanSatiri
                        {
                            HesapKodu = s.HesapKodu,
                            HesapAdi = s.HesapAdi,
                            BorcBakiye = s.BorcBakiye,
                            AlacakBakiye = s.AlacakBakiye,
                            Bakiye = s.Bakiye
                        }).ToList()
                    });
                    await _db.SaveChangesAsync(ct);

                    if (tx is not null) await tx.CommitAsync(ct);
                }
            });

            sonuc.Kaydedildi = true;
            return sonuc;
        }

        public async Task SilAsync(int firmaId, int yil, int ay, CancellationToken ct = default)
        {
            var mizan = await _db.AylikMizanlar.Include(m => m.Satirlar)
                .FirstOrDefaultAsync(m => m.FirmaId == firmaId && m.Yil == yil && m.Ay == ay, ct)
                ?? throw new KeyNotFoundException("Bu firmanın bu ayına ait mizan yok.");

            _db.AylikMizanSatirlari.RemoveRange(mizan.Satirlar);
            _db.AylikMizanlar.Remove(mizan);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<MizanDurumuDto> DurumAsync(int yil, CancellationToken ct = default)
        {
            YilDogrula(yil);

            var firmalar = await _db.Firmalar.AsNoTracking()
                .Where(f => f.Aktif)
                .Select(f => new { f.Id, f.KisaAd, f.Unvan, f.SorumluKullaniciAdi })
                .ToListAsync(ct);

            var firmaIdleri = firmalar.Select(f => f.Id).ToList();

            var mizanlar = await _db.AylikMizanlar.AsNoTracking()
                .Where(m => m.Yil == yil && firmaIdleri.Contains(m.FirmaId))
                .Select(m => new
                {
                    m.Id, m.FirmaId, m.Ay, m.Dengeli, m.DengeFarki, m.KapanisSonrasiMi, m.DosyaAdi,
                    m.YuklemeTarihi, m.YukleyenKullanici, m.SatirSayisi
                })
                .ToListAsync(ct);

            // Uyarılar için yalnız 5xx/6xx/7xx (gelir tablosu, kapanış, gider yöntemi); denge kayıttan.
            var mizanIdleri = mizanlar.Select(m => m.Id).ToList();
            var satirlar = await _db.AylikMizanSatirlari.AsNoTracking()
                .Where(s => mizanIdleri.Contains(s.AylikMizanId)
                            && (s.HesapKodu.StartsWith("5") || s.HesapKodu.StartsWith("6") || s.HesapKodu.StartsWith("7")))
                .Select(s => new { s.AylikMizanId, s.HesapKodu, s.Bakiye })
                .ToListAsync(ct);
            var bakiyeler = satirlar.GroupBy(s => s.AylikMizanId).ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<string, decimal>)g.GroupBy(s => s.HesapKodu).ToDictionary(x => x.Key, x => x.Sum(s => s.Bakiye)));
            IReadOnlyDictionary<string, decimal> Bakiye(int id)
                => bakiyeler.TryGetValue(id, out var b) ? b : new Dictionary<string, decimal>();

            var tr = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), true);

            return new MizanDurumuDto
            {
                Yil = yil,
                Firmalar = firmalar
                    .Select(f =>
                    {
                        var aylar = mizanlar.Where(m => m.FirmaId == f.Id).ToDictionary(m => m.Ay);
                        var hucreler = Enumerable.Range(1, 12).Select(ay =>
                        {
                            if (!aylar.TryGetValue(ay, out var m)) return new MizanDurumHucresiDto { Ay = ay };
                            var onceki = ay > 1 && aylar.TryGetValue(ay - 1, out var o) ? Bakiye(o.Id) : null;
                            return new MizanDurumHucresiDto
                            {
                                Ay = ay,
                                Yuklu = true,
                                Dengeli = m.Dengeli,
                                KapanisSonrasi = m.KapanisSonrasiMi,
                                DosyaAdi = m.DosyaAdi,
                                YuklemeTarihi = m.YuklemeTarihi,
                                YukleyenKullanici = m.YukleyenKullanici,
                                SatirSayisi = m.SatirSayisi,
                                DengeFarki = m.DengeFarki,
                                Uyarilar = PortfoyHesaplayici.Uyarilar(ay, Bakiye(m.Id), onceki, m.DengeFarki)
                            };
                        }).ToList();

                        return new MizanDurumFirmasiDto
                        {
                            FirmaId = f.Id,
                            KisaAd = string.IsNullOrWhiteSpace(f.KisaAd) ? f.Unvan : f.KisaAd,
                            Unvan = f.Unvan,
                            Sorumlu = f.SorumluKullaniciAdi,
                            Aylar = hucreler,
                            YukluAySayisi = hucreler.Count(h => h.Yuklu)
                        };
                    })
                    .OrderBy(f => f.KisaAd, tr)
                    .ToList()
            };
        }

        public async Task<PortfoyDto> PortfoyAsync(string? donem, string? karsilastir, CancellationToken ct = default)
        {
            var yilKars = karsilastir?.Trim().ToLowerInvariant() switch
            {
                null or "" or "ay" => false,
                "yil" => true,
                _ => throw new FirmaPortfoyuKuralException("karsilastir", "Karşılaştırma 'ay' ya da 'yil' olmalı.")
            };

            var firmalar = await _db.Firmalar.AsNoTracking()
                .Where(f => f.Aktif)
                .Select(f => new { f.Id, f.KisaAd, f.Unvan, f.SorumluKullaniciAdi })
                .ToListAsync(ct);
            var firmaIdleri = firmalar.Select(f => f.Id).ToList();

            var bugun = _saat.GetLocalNow();
            var (yil, ay) = await DonemCozAsync(donem, firmaIdleri, bugun, ct);
            var secili = Endeks(yil, ay);

            // Pencere: 12 aylık seri + ilk ayın önceki ayı; geçen yılın aynı ayı ve öncesi de bu aralıkta.
            var bas = secili - 13;
            var mizanlar = await _db.AylikMizanlar.AsNoTracking()
                .Where(m => firmaIdleri.Contains(m.FirmaId)
                            && m.Yil * 12 + m.Ay - 1 >= bas && m.Yil * 12 + m.Ay - 1 <= secili)
                .Select(m => new { m.Id, m.FirmaId, m.Yil, m.Ay, m.Dengeli, m.KapanisSonrasiMi, m.DengeFarki })
                .ToListAsync(ct);

            var mizanIdleri = mizanlar.Select(m => m.Id).ToList();
            var satirlar = await _db.AylikMizanSatirlari.AsNoTracking()
                .Where(s => mizanIdleri.Contains(s.AylikMizanId))
                .Select(s => new { s.AylikMizanId, s.HesapKodu, s.Bakiye })
                .ToListAsync(ct);
            var bakiyeler = satirlar.GroupBy(s => s.AylikMizanId).ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<string, decimal>)g.GroupBy(s => s.HesapKodu).ToDictionary(x => x.Key, x => x.Sum(s => s.Bakiye)));

            var ayMizanlari = mizanlar.ToDictionary(
                m => (m.FirmaId, m.Yil, m.Ay),
                m => new PortfoyHesaplayici.AyMizani(m.Yil, m.Ay,
                    bakiyeler.TryGetValue(m.Id, out var b) ? b : new Dictionary<string, decimal>(),
                    m.Dengeli, m.KapanisSonrasiMi, m.DengeFarki));

            // Son mizan (seçili aya kadar) pencerenin dışında da olabilir.
            var sonMizanlar = await _db.AylikMizanlar.AsNoTracking()
                .Where(m => firmaIdleri.Contains(m.FirmaId) && m.Yil * 12 + m.Ay - 1 <= secili)
                .GroupBy(m => m.FirmaId)
                .Select(g => new { FirmaId = g.Key, Son = g.Max(m => m.Yil * 12 + m.Ay - 1) })
                .ToDictionaryAsync(x => x.FirmaId, x => x.Son, ct);

            var (kYil, kAy) = yilKars ? (yil - 1, ay) : PortfoyHesaplayici.OncekiAy(yil, ay);
            var tr = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), true);

            var sonuc = new PortfoyDto
            {
                Donem = DonemMetni(yil, ay),
                Yil = yil,
                Ay = ay,
                Karsilastirma = yilKars ? "yil" : "ay",
                KarsilastirmaDonemi = DonemMetni(kYil, kAy),
                AktifFirmaSayisi = firmalar.Count
            };

            foreach (var f in firmalar)
            {
                PortfoyHesaplayici.AyMizani? Getir(int y, int a)
                    => ayMizanlari.TryGetValue((f.Id, y, a), out var m) ? m : null;

                var bu = Getir(yil, ay);
                var son = sonMizanlar.TryGetValue(f.Id, out var s) ? s : (int?)null;

                var durum = bu is null ? PortfoyMizanDurumu.Eksik
                    : bu.Dengeli ? PortfoyMizanDurumu.Tamam : PortfoyMizanDurumu.Dengesiz;

                sonuc.Firmalar.Add(new PortfoyFirmasiDto
                {
                    FirmaId = f.Id,
                    KisaAd = string.IsNullOrWhiteSpace(f.KisaAd) ? f.Unvan : f.KisaAd,
                    Unvan = f.Unvan,
                    Sorumlu = f.SorumluKullaniciAdi,
                    Durum = durum,
                    EksikAySayisi = bu is null && son is { } sn ? secili - sn : null,
                    SonMizanDonemi = son is { } e ? DonemMetni(e / 12, e % 12 + 1) : null,
                    Uyarilar = bu is null
                        ? new List<string>()
                        : PortfoyHesaplayici.Uyarilar(ay, bu.Bakiyeler, ay > 1 ? Getir(yil, ay - 1)?.Bakiyeler : null, bu.DengeFarki),
                    Gostergeler = Gostergeler(Getir, yil, ay, kYil, kAy, yilKars),
                    Seri = Enumerable.Range(0, 12).Select(i =>
                    {
                        var k = secili - 11 + i;
                        var (y, a) = (k / 12, k % 12 + 1);
                        var m = Getir(y, a);
                        var g = PortfoyHesaplayici.Aylik(Getir, y, a);
                        return new PortfoyAyDto
                        {
                            Yil = y,
                            Ay = a,
                            Yuklu = m is not null,
                            Dengeli = m?.Dengeli ?? false,
                            NetSatis = g.NetSatis,
                            Smm = g.Smm,
                            FaaliyetGiderleri = g.FaaliyetGiderleri,
                            DonemKari = g.DonemKari,
                            HazirDegerler = m is null ? null : PortfoyHesaplayici.HazirDegerler(m.Bakiyeler),
                            Not = g.Not
                        };
                    }).ToList()
                });
            }

            sonuc.MizaniYuklenenFirmaSayisi = sonuc.Firmalar.Count(f => f.Durum != PortfoyMizanDurumu.Eksik);
            sonuc.Firmalar = sonuc.Firmalar.OrderBy(f => f.KisaAd, tr).ToList();
            return sonuc;
        }

        /// <summary>Seçili firmanın 9 göstergesi; her biri kendi karşılaştırma dönemiyle.</summary>
        private static PortfoyGostergeleriDto Gostergeler(Func<int, int, PortfoyHesaplayici.AyMizani?> getir,
                                                          int yil, int ay, int kYil, int kAy, bool yilKars)
        {
            var bu = PortfoyHesaplayici.Aylik(getir, yil, ay);
            var kar = PortfoyHesaplayici.Aylik(getir, kYil, kAy);
            var buMizan = getir(yil, ay);
            var kMizan = getir(kYil, kAy);

            GostergeDto G(decimal? deger, decimal? kars, string? not) => new() { Deger = deger, Karsilastirma = kars, Not = deger is null ? not : null };

            GostergeDto Bilanco(Func<IReadOnlyDictionary<string, decimal>, decimal> f)
                => G(buMizan is null ? null : f(buMizan.Bakiyeler), kMizan is null ? null : f(kMizan.Bakiyeler),
                     PortfoyHesaplayici.NotYuklenmedi);

            return new PortfoyGostergeleriDto
            {
                NetSatis = G(bu.NetSatis, kar.NetSatis, bu.Not),
                BrutKar = G(bu.BrutKar, kar.BrutKar, bu.Not),
                FaaliyetGiderleri = G(bu.FaaliyetGiderleri, kar.FaaliyetGiderleri, bu.Not),
                FaaliyetKari = G(bu.FaaliyetKari, kar.FaaliyetKari, bu.Not),
                DonemKari = G(bu.DonemKari, kar.DonemKari, bu.Not),
                // Yıl başından kümülatif: "önceki ay" karşılaştırması anlamsız (hep büyür); yalnız geçen yılın aynı dönemi.
                DonemKariYilBasindan = G(PortfoyHesaplayici.DonemKariYilBasindan(buMizan),
                    yilKars ? PortfoyHesaplayici.DonemKariYilBasindan(kMizan) : null,
                    buMizan is null ? PortfoyHesaplayici.NotYuklenmedi
                        : buMizan.KapanisSonrasi ? PortfoyHesaplayici.NotKapanisSonrasi : PortfoyHesaplayici.NotGelirTablosuYok),
                HazirDegerler = Bilanco(PortfoyHesaplayici.HazirDegerler),
                TicariAlacaklar = Bilanco(PortfoyHesaplayici.TicariAlacaklar),
                TicariBorclar = Bilanco(PortfoyHesaplayici.TicariBorclar),
                DevredenKdv = Bilanco(PortfoyHesaplayici.DevredenKdv)
            };
        }

        /// <summary>"2026-09" → (2026, 9); boşsa mizanı olan en son ay (bugünü geçmeyen), o da yoksa geçen ay.</summary>
        private async Task<(int Yil, int Ay)> DonemCozAsync(string? donem, List<int> firmaIdleri, DateTimeOffset bugun, CancellationToken ct)
        {
            var bugunEndeks = Endeks(bugun.Year, bugun.Month);

            if (string.IsNullOrWhiteSpace(donem))
            {
                var son = await _db.AylikMizanlar.AsNoTracking()
                    .Where(m => firmaIdleri.Contains(m.FirmaId) && m.Yil * 12 + m.Ay - 1 <= bugunEndeks)
                    .Select(m => (int?)(m.Yil * 12 + m.Ay - 1))
                    .MaxAsync(ct);
                var e = son ?? bugunEndeks - 1;
                return (e / 12, e % 12 + 1);
            }

            var p = donem.Trim().Split('-');
            if (p.Length != 2 || !int.TryParse(p[0], out var y) || !int.TryParse(p[1], out var a) || a is < 1 or > 12)
                throw new FirmaPortfoyuKuralException("donem", "Dönem 'yyyy-aa' biçiminde olmalı (ör. 2026-09).");
            YilDogrula(y);
            if (Endeks(y, a) > bugunEndeks)
                throw new FirmaPortfoyuKuralException("donem", "Gelecek bir ay seçilemez.");
            return (y, a);
        }

        /// <summary>Ay endeksi: yıl × 12 + ay − 1 (ardışık aylar ardışık sayılar).</summary>
        private static int Endeks(int yil, int ay) => yil * 12 + ay - 1;

        private static string DonemMetni(int yil, int ay) => $"{yil:D4}-{ay:D2}";

        // ---- yardımcılar ----

        private async Task<(AylikMizanOnizlemeDto Sonuc, PortfoyHesaplayici.Degerlendirme Degerlendirme)> DegerlendirAsync(
            AylikMizanKaydetDto dto, string firmaAdi, List<AylikMizanSatiri> satirlar, CancellationToken ct)
        {
            var d = PortfoyHesaplayici.Degerlendir(dto.Ay, satirlar, await OncekiAyAsync(dto.FirmaId, dto.Yil, dto.Ay, ct));

            var mevcut = await _db.AylikMizanlar.AsNoTracking()
                .Where(m => m.FirmaId == dto.FirmaId && m.Yil == dto.Yil && m.Ay == dto.Ay)
                .Select(m => new MevcutMizanDto
                {
                    DosyaAdi = m.DosyaAdi,
                    YuklemeTarihi = m.YuklemeTarihi,
                    YukleyenKullanici = m.YukleyenKullanici,
                    Dengeli = m.Dengeli
                })
                .FirstOrDefaultAsync(ct);

            return (new AylikMizanOnizlemeDto
            {
                FirmaId = dto.FirmaId,
                FirmaAdi = firmaAdi,
                Yil = dto.Yil,
                Ay = dto.Ay,
                SatirSayisi = d.SatirSayisi,
                BorcToplami = d.BorcToplami,
                AlacakToplami = d.AlacakToplami,
                DengeFarki = d.DengeFarki,
                Dengeli = d.Dengeli,
                KapanisSonrasi = d.KapanisSonrasi,
                GiderYontemi = d.GiderYontemi,
                GelirTablosuVar = d.GelirTablosuVar,
                NetSatisKumulatif = d.NetSatis,
                SmmKumulatif = d.Smm,
                FaaliyetGiderleriKumulatif = d.FaaliyetGiderleri,
                Uyarilar = d.Uyarilar.ToList(),
                Mevcut = mevcut
            }, d);
        }

        /// <summary>Aynı yılın önceki ayının bakiyeleri; Ocak'ta ya da yüklenmemişse null.</summary>
        private async Task<IReadOnlyDictionary<string, decimal>?> OncekiAyAsync(int firmaId, int yil, int ay, CancellationToken ct)
        {
            if (ay <= 1) return null;

            var satirlar = await _db.AylikMizanSatirlari.AsNoTracking()
                .Where(s => _db.AylikMizanlar.Any(m => m.Id == s.AylikMizanId && m.FirmaId == firmaId && m.Yil == yil && m.Ay == ay - 1))
                .Select(s => new { s.HesapKodu, s.Bakiye })
                .ToListAsync(ct);

            if (satirlar.Count == 0)
            {
                var varMi = await _db.AylikMizanlar.AnyAsync(m => m.FirmaId == firmaId && m.Yil == yil && m.Ay == ay - 1, ct);
                if (!varMi) return null;
            }
            return satirlar.GroupBy(s => s.HesapKodu).ToDictionary(g => g.Key, g => g.Sum(s => s.Bakiye));
        }

        /// <summary>Firma, dönem ve satırları doğrular; satırları kayda hazır (bakiye sunucuda hesaplanmış) döner.</summary>
        private async Task<(string FirmaAdi, List<AylikMizanSatiri> Satirlar)> DogrulaAsync(AylikMizanKaydetDto dto, CancellationToken ct)
        {
            if (dto is null) throw new FirmaPortfoyuKuralException("dto", "İstek boş.");

            var firma = await _db.Firmalar.AsNoTracking()
                .Where(f => f.Id == dto.FirmaId)
                .Select(f => new { f.KisaAd, f.Unvan, f.Aktif })
                .FirstOrDefaultAsync(ct)
                ?? throw new FirmaPortfoyuKuralException("firmaId", "Firma bulunamadı.");
            if (!firma.Aktif)
                throw new FirmaPortfoyuKuralException("firmaId", "Firma pasif; portföye yalnız Firmalarım'daki aktif firmaların mizanı yüklenir.");

            YilDogrula(dto.Yil);
            if (dto.Ay is < 1 or > 12) throw new FirmaPortfoyuKuralException("ay", "Ay 1 ile 12 arasında olmalı.");

            var bugun = _saat.GetLocalNow();
            if (dto.Yil > bugun.Year || (dto.Yil == bugun.Year && dto.Ay > bugun.Month))
                throw new FirmaPortfoyuKuralException("ay", "Gelecek bir ayın mizanı yüklenemez.");

            if (dto.Satirlar is null || dto.Satirlar.Count == 0)
                throw new FirmaPortfoyuKuralException("satirlar", "Mizanda okunabilir ana hesap satırı yok.");
            if (dto.Satirlar.Count > AzamiSatir)
                throw new FirmaPortfoyuKuralException("satirlar", $"Bir mizanda en fazla {AzamiSatir} ana hesap satırı olabilir.");

            var satirlar = new List<AylikMizanSatiri>(dto.Satirlar.Count);
            var gorulen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in dto.Satirlar)
            {
                var kod = (s.HesapKodu ?? string.Empty).Trim();
                if (!AnaHesapKodu.IsMatch(kod))
                    throw new FirmaPortfoyuKuralException("satirlar",
                        $"Geçersiz hesap kodu '{kod}': yalnız 3 haneli ana hesaplar gönderilir.");
                if (!gorulen.Add(kod))
                    throw new FirmaPortfoyuKuralException("satirlar", $"Hesap kodu {kod} mizanda iki kez var.");

                var borc = Math.Round(s.BorcBakiye, 2);
                var alacak = Math.Round(s.AlacakBakiye, 2);
                satirlar.Add(new AylikMizanSatiri
                {
                    HesapKodu = kod,
                    HesapAdi = Kirp(s.HesapAdi, 200),
                    BorcBakiye = borc,
                    AlacakBakiye = alacak,
                    Bakiye = borc - alacak
                });
            }

            return (string.IsNullOrWhiteSpace(firma.KisaAd) ? firma.Unvan : firma.KisaAd, satirlar);
        }

        private static void YilDogrula(int yil)
        {
            if (yil is < 2000 or > 2100) throw new FirmaPortfoyuKuralException("yil", "Yıl 2000 ile 2100 arasında olmalı.");
        }

        private static string DosyaAdi(string? ad)
            => Kirp(ad, 260) is { Length: > 0 } s ? s : "(adsız)";

        private static string? Kirp(string? metin, int azami)
        {
            var s = metin?.Trim();
            if (string.IsNullOrEmpty(s)) return null;
            return s.Length <= azami ? s : s[..azami];
        }
    }
}
