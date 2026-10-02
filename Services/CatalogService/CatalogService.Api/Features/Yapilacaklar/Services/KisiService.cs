using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Yapilacaklar.Services
{
    public interface IKisiService
    {
        /// <summary>Kişiler ve mailler sheet'i; ay verilmezse içinde bulunulan ay (mail takvimi).</summary>
        /// <param name="firmaId">Doluysa yalnız o firma (İş Takip Panosu'nun firma seçimi, Prompt 15).</param>
        Task<KisilerPanosuDto> PanoAsync(int? yil, int? ay, CancellationToken ct = default, int? firmaId = null);

        Task<KisiOzetDto> EkleAsync(KisiKaydetDto dto, CancellationToken ct = default);

        /// <summary>Kişi düzenlenince bağlı bütün alıcıların kopyası da düzelir (adres tek yerde).</summary>
        Task<KisiOzetDto> GuncelleAsync(int kisiId, KisiKaydetDto dto, CancellationToken ct = default);

        /// <summary>Kullanıcının kararıyla: kaynak kişinin alıcıları hedefe geçer, kaynak silinir.</summary>
        Task BirlestirAsync(int hedefId, int kaynakId, CancellationToken ct = default);

        /// <summary>
        /// İş dialogu (Prompt 14B): alıcılar kaydedilse hangi kişiye eşleşir — yazma yoluyla AYNI kural
        /// (<see cref="KisiEslestirici.Esle"/>), kişilerin kopyası üzerinde; hiçbir şey kaydedilmez.
        /// </summary>
        Task<List<AliciOnizlemeDto>> OnizleAsync(AliciOnizlemeIstegiDto istek, CancellationToken ct = default);
    }

    /// <summary>
    /// Firmanın kişileri (Prompt 14). Kişi tablosu kim / hangi e-posta / ne iş tutar; şifre, kullanıcı adı,
    /// erişim bilgisi TUTULMAZ. Eşleştirme kuralı <see cref="KisiEslestirici"/>'de; mail takvimi
    /// <see cref="KisiPanosuKurucu"/>'da TÜRETİLİR.
    /// </summary>
    public class KisiService : IKisiService
    {
        private readonly CatalogContext _db;
        private readonly TimeProvider _saat;

        public KisiService(CatalogContext db, TimeProvider saat)
        {
            _db = db;
            _saat = saat;
        }

        // ---- Alıcı yazması ve taşıma (statik: YapilacaklarService ve seed çağırır) ----

        /// <summary>
        /// Yazılmak üzere olan alıcıları firmanın kişilerine bağlar (yeni kişiyi ekler) ve alıcının ad/e-posta
        /// kopyasını kişiden yazar. Kişiye e-posta eklendiyse o kişinin diğer alıcıları da eşitlenir.
        /// </summary>
        public static async Task KisilereBaglaAsync(CatalogContext db, int firmaId, IEnumerable<FirmaIsiAlicisi> alicilar,
                                                    CancellationToken ct)
        {
            var kisiler = await db.Kisiler.Where(k => k.FirmaId == firmaId).ToListAsync(ct);
            kisiler.AddRange(db.Kisiler.Local.Where(k => k.FirmaId == firmaId && !kisiler.Contains(k)));

            var epostasiOnce = kisiler.ToDictionary(k => k, k => k.Eposta);
            foreach (var a in alicilar)
            {
                var (kisi, _) = KisiEslestirici.Esle(kisiler, firmaId, a.AdSoyad, a.Eposta, a.Rol);
                if (db.Entry(kisi).State == EntityState.Detached) db.Kisiler.Add(kisi);
                a.Kisi = kisi;
                a.AdSoyad = kisi.Ad;
                a.Eposta = kisi.Eposta;
            }

            var degisen = kisiler.Where(k => k.Id != 0 && epostasiOnce.TryGetValue(k, out var e) && e != k.Eposta)
                                 .Select(k => k.Id).ToList();
            if (degisen.Count > 0) await KopyalariEsitleAsync(db, degisen, ct);
        }

        /// <summary>Kişinin ad/e-postası değişti: alıcı kopyaları kişiden yazılır.</summary>
        private static async Task KopyalariEsitleAsync(CatalogContext db, IReadOnlyCollection<int> kisiIdleri, CancellationToken ct)
        {
            var kisiler = await db.Kisiler.Where(k => kisiIdleri.Contains(k.Id)).ToDictionaryAsync(k => k.Id, ct);
            foreach (var a in await db.FirmaIsiAlicilari.Where(a => a.KisiId != null && kisiIdleri.Contains(a.KisiId.Value)).ToListAsync(ct))
            {
                var k = kisiler[a.KisiId!.Value];
                a.AdSoyad = k.Ad;
                a.Eposta = k.Eposta;
            }
        }

        /// <summary>
        /// Taşıma (seed): kişiye bağlanmamış alıcı kayıtları. Aynı firmada aynı e-posta tek kişi; e-postasız ada
        /// göre; belirsiz ve şüpheli eşleşme birleştirilmez, raporlanır. Tekrar çalışması güvenli (yalnız boş KisiId).
        /// </summary>
        public static async Task<KisiTasimaRaporuDto> TasiAsync(CatalogContext db, CancellationToken ct = default)
        {
            var rapor = new KisiTasimaRaporuDto();
            var bagsiz = await db.FirmaIsiAlicilari.Where(a => a.KisiId == null).ToListAsync(ct);
            if (bagsiz.Count > 0)
            {
                var isFirmasi = await db.FirmaIsleri.AsNoTracking().Select(i => new { i.Id, i.FirmaId }).ToDictionaryAsync(i => i.Id, i => i.FirmaId, ct);
                var tumKisiler = await db.Kisiler.ToListAsync(ct);
                var yazimlar = new Dictionary<Kisi, HashSet<string>>();

                foreach (var firma in bagsiz.Where(a => isFirmasi.ContainsKey(a.FirmaIsiId))
                                            .GroupBy(a => isFirmasi[a.FirmaIsiId]).OrderBy(g => g.Key))
                {
                    var kisiler = tumKisiler.Where(k => k.FirmaId == firma.Key).ToList();
                    foreach (var a in firma.OrderBy(a => a.FirmaIsiId).ThenBy(a => a.Sira).ThenBy(a => a.Id))
                    {
                        var ad = a.AdSoyad;
                        var (kisi, sonuc) = KisiEslestirici.Esle(kisiler, firma.Key, a.AdSoyad, a.Eposta, a.Rol);
                        rapor.AliciKaydi++;
                        switch (sonuc)
                        {
                            case KisiEslestirici.Sonuc.EpostaIleBulundu: rapor.EpostaIleBirlesen++; break;
                            case KisiEslestirici.Sonuc.AdIleBulundu or KisiEslestirici.Sonuc.EpostaEklendi: rapor.AdIleBirlesen++; break;
                            case KisiEslestirici.Sonuc.YeniKisiBelirsiz: rapor.Belirsiz++; rapor.YeniKisi++; break;
                            default: rapor.YeniKisi++; break;
                        }
                        if (db.Entry(kisi).State == EntityState.Detached) db.Kisiler.Add(kisi);
                        (yazimlar.TryGetValue(kisi, out var y) ? y : yazimlar[kisi] = new()).Add(ad.Trim());
                        a.Kisi = kisi;
                        a.AdSoyad = kisi.Ad;
                        a.Eposta = kisi.Eposta;
                    }
                    tumKisiler.AddRange(kisiler.Where(k => !tumKisiler.Contains(k)));
                }

                rapor.FarkliAdYazimlari = yazimlar
                    .Where(p => p.Key.Eposta is not null && p.Value.Select(KisiEslestirici.AdAnahtari).Distinct().Count() > 1)
                    .Select(p => $"{p.Key.Eposta}: {string.Join(" / ", p.Value)}")
                    .ToList();

                await db.SaveChangesAsync(ct);
            }

            var firmaAdlari = (await db.Firmalar.AsNoTracking().ToListAsync(ct)).ToDictionary(f => f.Id, Anasayfa.Services.FirmaPaneliKurucu.Ad);
            rapor.Supheliler = KisiEslestirici.Supheliler(await db.Kisiler.AsNoTracking().ToListAsync(ct))
                .Select(s => $"{firmaAdlari.GetValueOrDefault(s.FirmaId, $"#{s.FirmaId}")} · "
                             + (s.Tur == KisiEslestirici.SupheTuru.AyniAd ? "aynı ad" : "benzer ad") + ": "
                             + string.Join(" / ", s.Kisiler.Select(k => k.Eposta is null ? k.Ad : $"{k.Ad} <{k.Eposta}>")))
                .ToList();
            return rapor;
        }

        // ---- Sheet ----

        public async Task<KisilerPanosuDto> PanoAsync(int? yil, int? ay, CancellationToken ct = default, int? firmaId = null)
        {
            var bugun = _saat.GetLocalNow().Date;
            var (y, a) = (yil, ay) switch
            {
                ({ } yy, { } aa) => (yy, aa),
                _ => (bugun.Year, bugun.Month)
            };
            if (a is < 1 or > 12 || y is < 2000 or > 2100)
                throw new YapilacaklarKuralException("ay", "Geçersiz ay.");

            // Firma seçimi kurucudan ÖNCE: tablo, takvim ve şüpheliler aynı kümeden.
            var firmalar = await YapilacaklarService.FirmaGirdileriAsync(_db, firmaId, ct);
            var firmaAdi = firmalar.ToDictionary(f => f.FirmaId, f => f.Ad);
            var idler = firmaAdi.Keys.ToList();

            // Kapsam: aktif özel işler + yasal prosedür satırları (alıcı ancak satırda tutulur).
            var isler = await _db.FirmaIsleri.AsNoTracking().Include(i => i.Alicilar).ThenInclude(x => x.Kisi)
                .Where(i => idler.Contains(i.FirmaId) && (i.YasalMukellefiyetKodu != null || i.Aktif))
                .ToListAsync(ct);
            var kisiler = await _db.Kisiler.AsNoTracking().Where(k => idler.Contains(k.FirmaId)).ToListAsync(ct);
            var takvim = await _db.VergiTakvimi.AsNoTracking().ToListAsync(ct);

            var dto = new KisilerPanosuDto
            {
                Yil = y,
                Ay = a,
                Etiket = new IsDonemi(IsTekrari.Aylik, y, a).Etiket,
                Satirlar = KisiPanosuKurucu.Satirlar(isler, kisiler, firmaAdi)
            };
            (dto.Takvim, dto.AlicisizIsler) = KisiPanosuKurucu.MailTakvimi(y, a, isler, takvim, firmaAdi);

            var aliciSayisi = isler.SelectMany(i => i.Alicilar).Where(x => x.KisiId != null)
                .GroupBy(x => x.KisiId!.Value).ToDictionary(g => g.Key, g => g.Count());
            dto.Supheliler = KisiEslestirici.Supheliler(kisiler)
                .Select(s => new SupheliKisiDto
                {
                    FirmaId = s.FirmaId,
                    FirmaAdi = firmaAdi.GetValueOrDefault(s.FirmaId, string.Empty),
                    Tur = s.Tur,
                    Kisiler = s.Kisiler.Select(k => new KisiOzetDto
                    {
                        Id = k.Id, Ad = k.Ad, Eposta = k.Eposta, AliciSayisi = aliciSayisi.GetValueOrDefault(k.Id)
                    }).ToList()
                })
                .ToList();
            return dto;
        }

        public async Task<List<AliciOnizlemeDto>> OnizleAsync(AliciOnizlemeIstegiDto istek, CancellationToken ct = default)
        {
            var kisiler = await _db.Kisiler.AsNoTracking().Where(k => k.FirmaId == istek.FirmaId).ToListAsync(ct);
            return KisiEslestirici.Onizle(kisiler, istek.FirmaId, istek.Alicilar.Select(a => (a.AdSoyad ?? string.Empty, a.Eposta)));
        }

        public async Task<KisiOzetDto> EkleAsync(KisiKaydetDto dto, CancellationToken ct = default)
        {
            if (!await _db.Firmalar.AnyAsync(f => f.Id == dto.FirmaId, ct))
                throw new YapilacaklarKuralException(nameof(dto.FirmaId), "Firma bulunamadı.");
            var kisi = new Kisi { FirmaId = dto.FirmaId };
            await UygulaAsync(kisi, dto, ct);
            _db.Kisiler.Add(kisi);
            await _db.SaveChangesAsync(ct);
            return new KisiOzetDto { Id = kisi.Id, Ad = kisi.Ad, Eposta = kisi.Eposta };
        }

        public async Task<KisiOzetDto> GuncelleAsync(int kisiId, KisiKaydetDto dto, CancellationToken ct = default)
        {
            var kisi = await _db.Kisiler.FirstOrDefaultAsync(k => k.Id == kisiId, ct)
                       ?? throw new KeyNotFoundException("Kişi bulunamadı.");
            await UygulaAsync(kisi, dto, ct);
            await _db.SaveChangesAsync(ct);

            // Adres tek yerde: bağlı işlerin alıcıları birden düzelir.
            await KopyalariEsitleAsync(_db, new[] { kisi.Id }, ct);
            await _db.SaveChangesAsync(ct);
            return new KisiOzetDto { Id = kisi.Id, Ad = kisi.Ad, Eposta = kisi.Eposta };
        }

        public async Task BirlestirAsync(int hedefId, int kaynakId, CancellationToken ct = default)
        {
            if (hedefId == kaynakId) throw new YapilacaklarKuralException("kaynakId", "Kişi kendisiyle birleştirilemez.");
            var hedef = await _db.Kisiler.FirstOrDefaultAsync(k => k.Id == hedefId, ct) ?? throw new KeyNotFoundException("Kişi bulunamadı.");
            var kaynak = await _db.Kisiler.FirstOrDefaultAsync(k => k.Id == kaynakId, ct) ?? throw new KeyNotFoundException("Kişi bulunamadı.");
            if (hedef.FirmaId != kaynak.FirmaId)
                throw new YapilacaklarKuralException("kaynakId", "Farklı firmaların kişileri birleştirilemez.");

            foreach (var a in await _db.FirmaIsiAlicilari.Where(a => a.KisiId == kaynakId).ToListAsync(ct))
                a.KisiId = hedefId;

            // Bilgi kaybolmasın: hedefte boş olanı kaynaktan al; iki farklı e-posta varsa kaynağınki nota düşer.
            var kaynakEposta = kaynak.Eposta;
            if (string.IsNullOrWhiteSpace(hedef.Rol)) hedef.Rol = kaynak.Rol;
            if (!string.IsNullOrWhiteSpace(kaynakEposta) && !string.IsNullOrWhiteSpace(hedef.Eposta)
                && KisiEslestirici.EpostaAnahtari(kaynakEposta) != KisiEslestirici.EpostaAnahtari(hedef.Eposta))
                hedef.Notu = Kes(string.Join("\n", new[] { hedef.Notu, kaynak.Notu, $"Birleştirilen kişinin e-postası: {kaynakEposta}" }
                                                   .Where(s => !string.IsNullOrWhiteSpace(s))), 1000);
            else if (!string.IsNullOrWhiteSpace(kaynak.Notu))
                hedef.Notu = Kes(string.Join("\n", new[] { hedef.Notu, kaynak.Notu }.Where(s => !string.IsNullOrWhiteSpace(s))), 1000);

            _db.Kisiler.Remove(kaynak);
            await _db.SaveChangesAsync(ct);   // önce kaynak gitsin (e-posta tekilliği)

            if (string.IsNullOrWhiteSpace(hedef.Eposta) && !string.IsNullOrWhiteSpace(kaynakEposta)) hedef.Eposta = kaynakEposta;
            await _db.SaveChangesAsync(ct);
            await KopyalariEsitleAsync(_db, new[] { hedefId }, ct);
            await _db.SaveChangesAsync(ct);
        }

        private async Task UygulaAsync(Kisi kisi, KisiKaydetDto dto, CancellationToken ct)
        {
            var ad = dto.Ad?.Trim() ?? string.Empty;
            if (ad.Length == 0) throw new YapilacaklarKuralException(nameof(dto.Ad), "Kişinin adı boş olamaz.");
            if (ad.Length > 150) throw new YapilacaklarKuralException(nameof(dto.Ad), "Ad en çok 150 karakter olabilir.");

            var eposta = string.IsNullOrWhiteSpace(dto.Eposta) ? null : dto.Eposta.Trim();
            if (eposta is { Length: > 200 }) throw new YapilacaklarKuralException(nameof(dto.Eposta), "E-posta en çok 200 karakter olabilir.");
            if (eposta is not null && (!eposta.Contains('@') || eposta.Contains(' ')))
                throw new YapilacaklarKuralException(nameof(dto.Eposta), "E-posta geçerli görünmüyor.");

            if (eposta is not null)
            {
                var e = KisiEslestirici.EpostaAnahtari(eposta);
                var digerleri = await _db.Kisiler.AsNoTracking().Where(k => k.FirmaId == kisi.FirmaId && k.Id != kisi.Id && k.Eposta != null)
                    .Select(k => new { k.Ad, k.Eposta }).ToListAsync(ct);
                if (digerleri.FirstOrDefault(k => KisiEslestirici.EpostaAnahtari(k.Eposta) == e) is { } ayni)
                    throw new YapilacaklarKuralException(nameof(dto.Eposta),
                        $"Bu e-posta bu firmada zaten {ayni.Ad} kişisinde. Aynı e-posta tek kişidir; gerekirse birleştirin.");
            }

            kisi.Ad = ad;
            kisi.Eposta = eposta;
            kisi.Rol = string.IsNullOrWhiteSpace(dto.Rol) ? null : Kes(dto.Rol.Trim(), 100);
            kisi.Notu = string.IsNullOrWhiteSpace(dto.Notu) ? null : Kes(dto.Notu.Trim(), 1000);
            kisi.Aktif = dto.Aktif;
        }

        private static string Kes(string s, int n) => s.Length > n ? s[..n] : s;
    }

    /// <summary>Kişiler sheet'inin saf hesapları: tablo satırları ve TÜRETİLEN mail takvimi.</summary>
    public static class KisiPanosuKurucu
    {
        /// <summary>
        /// Satırlar: her (iş × alıcı); alıcısı olmayan iş "eksik" satırı; hiçbir işe alıcı olmayan kişi. Sıra:
        /// firma, kişi (eksikler firmanın sonunda), iş.
        /// </summary>
        public static List<KisiSatiriDto> Satirlar(IEnumerable<FirmaIsi> isler, IEnumerable<Kisi> kisiler,
                                                   IReadOnlyDictionary<int, string> firmaAdi)
        {
            var satirlar = new List<KisiSatiriDto>();
            var kisiById = kisiler.ToDictionary(k => k.Id);
            var kullanilan = new HashSet<int>();

            foreach (var isi in isler.Where(i => firmaAdi.ContainsKey(i.FirmaId)))
            {
                var neZaman = NeZaman(isi);
                if (isi.Alicilar.Count == 0)
                {
                    satirlar.Add(new KisiSatiriDto
                    {
                        FirmaId = isi.FirmaId, FirmaAdi = firmaAdi[isi.FirmaId], FirmaIsiId = isi.Id, IsBaslik = isi.Baslik,
                        Yasal = isi.YasalMukellefiyetKodu is not null, NeZaman = neZaman, Eksik = true
                    });
                    continue;
                }

                foreach (var a in isi.Alicilar.OrderBy(a => a.Sira).ThenBy(a => a.Id))
                {
                    var k = a.KisiId is { } kid ? kisiById.GetValueOrDefault(kid) ?? a.Kisi : null;
                    if (k is not null) kullanilan.Add(k.Id);
                    satirlar.Add(new KisiSatiriDto
                    {
                        FirmaId = isi.FirmaId, FirmaAdi = firmaAdi[isi.FirmaId],
                        KisiId = k?.Id, KisiAdi = k?.Ad ?? a.AdSoyad, Eposta = k?.Eposta ?? a.Eposta,
                        KisiRolu = k?.Rol ?? a.Rol, Notu = k?.Notu, KisiAktif = k?.Aktif ?? true,
                        FirmaIsiId = isi.Id, IsBaslik = isi.Baslik, Yasal = isi.YasalMukellefiyetKodu is not null,
                        NeZaman = neZaman, AliciTipi = a.AliciTipi
                    });
                }
            }

            foreach (var k in kisiById.Values.Where(k => !kullanilan.Contains(k.Id) && firmaAdi.ContainsKey(k.FirmaId)))
                satirlar.Add(new KisiSatiriDto
                {
                    FirmaId = k.FirmaId, FirmaAdi = firmaAdi[k.FirmaId], KisiId = k.Id, KisiAdi = k.Ad, Eposta = k.Eposta,
                    KisiRolu = k.Rol, Notu = k.Notu, KisiAktif = k.Aktif
                });

            var tr = StringComparer.Create(IsTakvimHesabi.Tr, true);
            return satirlar
                .OrderBy(s => s.FirmaAdi, tr)
                .ThenBy(s => s.Eksik)
                .ThenBy(s => s.KisiAdi ?? string.Empty, tr)
                .ThenBy(s => s.IsBaslik ?? string.Empty, tr)
                .ToList();
        }

        public static string NeZaman(FirmaIsi isi)
            => isi.YasalMukellefiyetKodu is not null
                ? $"{IsTakvimHesabi.TekrarAdi(isi.Tekrar)} · Vergi takvimindeki son gün"
                : IsTakvimHesabi.KuralMetni(isi.Tekrar, isi.GunKurali, isi.AyinGunu, isi.TekSeferTarih);

        /// <summary>
        /// Ayın mail takvimi — TÜRETİLİR: işin gün kuralı (Yapılacaklar'la aynı <see cref="IsTakvimHesabi.SonGun"/>)
        /// ya da yasal işte takvimin son günü + alıcılar. İşin günü değişirse takvim kendiliğinden kayar.
        /// Aynı gün aynı iş (tarif grubu) birden çok firmada tek satırdır.
        /// </summary>
        public static (List<MailGunuDto> Takvim, List<AlicisizIsDto> Alicisiz) MailTakvimi(
            int yil, int ay, IEnumerable<FirmaIsi> isler, IEnumerable<VergiTakvimi> takvim, IReadOnlyDictionary<int, string> firmaAdi)
        {
            var seri = takvim.ToLookup(t => (t.MukellefiyetKodu, t.Tekrar));
            var girdiler = new List<(DateTime Gun, FirmaIsi Is)>();
            foreach (var isi in isler.Where(i => firmaAdi.ContainsKey(i.FirmaId)))
                foreach (var gun in AydakiSonGunler(isi, yil, ay, seri))
                    girdiler.Add((gun, isi));

            var tr = StringComparer.Create(IsTakvimHesabi.Tr, true);
            var gunler = girdiler
                .Where(g => g.Is.Alicilar.Count > 0)
                .GroupBy(g => g.Gun)
                .OrderBy(g => g.Key)
                .Select(g => new MailGunuDto
                {
                    Tarih = g.Key,
                    Isler = g.GroupBy(x => IsTarifiKurucu.Anahtar(x.Is))
                        .Select(ig => new MailIsiDto
                        {
                            Anahtar = ig.Key,
                            Baslik = ig.First().Is.Baslik.Trim(),
                            Kime = ig.SelectMany(x => x.Is.Alicilar).Where(a => a.AliciTipi == AliciTipi.Kime)
                                     .Select(a => a.AdSoyad).Distinct().OrderBy(s => s, tr).ToList(),
                            Bilgi = ig.SelectMany(x => x.Is.Alicilar).Where(a => a.AliciTipi == AliciTipi.Bilgi)
                                      .Select(a => a.AdSoyad).Distinct().OrderBy(s => s, tr).ToList(),
                            Firmalar = ig.Select(x => firmaAdi[x.Is.FirmaId]).Distinct().OrderBy(s => s, tr).ToList()
                        })
                        .OrderBy(m => m.Baslik, tr)
                        .ToList()
                })
                .ToList();

            var alicisiz = girdiler
                .Where(g => g.Is.Alicilar.Count == 0)
                .OrderBy(g => g.Gun).ThenBy(g => firmaAdi[g.Is.FirmaId], tr)
                .Select(g => new AlicisizIsDto
                {
                    FirmaId = g.Is.FirmaId, FirmaAdi = firmaAdi[g.Is.FirmaId], Baslik = g.Is.Baslik.Trim(), SonGun = g.Gun
                })
                .ToList();

            return (gunler, alicisiz);
        }

        /// <summary>İşin bu aya düşen son günleri (kuyrukla aynı tarih kuralları; pasif iş kapsam dışında).</summary>
        public static IEnumerable<DateTime> AydakiSonGunler(FirmaIsi isi, int yil, int ay,
                                                           ILookup<(string, IsTekrari), VergiTakvimi> seri)
        {
            bool BuAy(DateTime d) => d.Year == yil && d.Month == ay;

            if (isi.YasalMukellefiyetKodu is { } kod)
            {
                foreach (var t in seri[(kod, isi.Tekrar)].Where(t => BuAy(t.SonGun)))
                    yield return t.SonGun.Date;
                yield break;
            }

            if (isi.Tekrar == IsTekrari.TekSefer)
            {
                if (isi.TekSeferTarih is { } ts && BuAy(ts)) yield return ts.Date;
                yield break;
            }

            // Son gün her zaman kendi döneminin içinde: ayı içeren dönem yeter.
            var donem = IsDonemi.Icin(isi.Tekrar, new DateTime(yil, ay, 1));
            if (donem.Bas < YapilacaklarKurucu.OzelIlkDonem(isi).Bas) yield break;
            var sg = IsTakvimHesabi.SonGun(isi.GunKurali, isi.AyinGunu, donem);
            if (BuAy(sg)) yield return sg.Date;
        }
    }
}
