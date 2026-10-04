using CatalogService.Api.Features.Anasayfa.Domain;
using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Infrastructure.Auth;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Yapilacaklar.Services
{
    /// <summary>İş kuralı ihlali; controller 400'e çevirir.</summary>
    public class YapilacaklarKuralException : Exception
    {
        public string Field { get; }

        public YapilacaklarKuralException(string field, string message) : base(message) => Field = field;
    }

    public interface IYapilacaklarService
    {
        Task<FirmaIsleriKartDto> FirmaKartiAsync(int firmaId, CancellationToken ct = default);

        /// <param name="firmaId">Prompt 16: doluysa yalnız o firma (filtrelerle birlikte); varsayılan bütün firmalar.</param>
        Task<YapilacaklarDto> ListeAsync(IsFiltresi filtre, CancellationToken ct = default, int? firmaId = null);
        Task<YapilacaklarOzetDto> OzetAsync(CancellationToken ct = default);

        /// <summary>Tek ya da çok firma-dönemi birlikte işaretler / işaretini kaldırır. Aynı durumdaki satır atlanır.</summary>
        /// <remarks>
        /// İşaret kaldırma not/kanıtı SİLMEZ (Prompt 10B): kanıtlı kayıtta yalnız zaman boşalır.
        /// Kaydın tamamını <c>IDonemPanosuService.KayitSilAsync</c> siler.
        /// </remarks>
        /// <returns>FileApi'den silinecek dosyalar — işaretlemede her zaman boş (sözleşme korunuyor).</returns>
        Task<List<int>> IsaretleAsync(IsIsaretleDto dto, CancellationToken ct = default);

        /// <summary>Firma-dönemin geçerli olduğunu doğrular (kaynak var, dönem ona ait, firma sahibi).</summary>
        /// <returns>Okunur ad: "KDV beyannamesi (KDV-1) · Ağustos 2026".</returns>
        Task<string> DonemDogrulaAsync(IsIsaretDto i, CancellationToken ct = default);

        Task<FirmaIsiDto> IsEkleAsync(int firmaId, FirmaIsiKaydetDto dto, CancellationToken ct = default);
        Task<FirmaIsiDto> IsGuncelleAsync(int firmaId, int isId, FirmaIsiKaydetDto dto, CancellationToken ct = default);

        /// <returns>Silinen eklerin FileApiService Id'leri; istemci oradan da siler.</returns>
        Task<List<int>> IsSilAsync(int firmaId, int isId, CancellationToken ct = default);

        // ---- Toplu ekleme ve kopyalama (Prompt 11) ----

        /// <summary>Yapıştırılan tabloyu çözer; hiçbir şey eklemez (YAPIŞTIR → GÖSTER → ONAYLAT).</summary>
        Task<TopluOnizlemeDto> TopluOnizleAsync(int firmaId, TopluOnizleIstekDto istek, CancellationToken ct = default);

        /// <summary>Onaylanan satırları ekler; aynı başlıklı mevcut iş ya atlanır ya güncellenir.</summary>
        Task<TopluSonucDto> TopluEkleAsync(int firmaId, TopluEkleDto dto, CancellationToken ct = default);

        /// <summary>Başka firmanın ÖZEL işlerini kopyalar; tamamlama, ek, ön adım ve sorumlu kopyalanmaz.</summary>
        Task<TopluSonucDto> KopyalaAsync(int hedefFirmaId, IsKopyalaDto dto, CancellationToken ct = default);

        // ---- Prosedür (Prompt 8) ----

        /// <param name="kaynakId">Özelde <c>FirmaIsi.Id</c>, yasalda serinin herhangi bir <c>VergiTakvimi.Id</c>'si.</param>
        Task<IsProsedurDto> ProsedurAsync(int firmaId, IsKaynagi kaynakTip, int kaynakId, bool tumGecmis,
                                          CancellationToken ct = default);

        /// <summary>Yasal işin prosedür satırını açar (varsa olanı döner). Ek yüklemeden önce çağrılır.</summary>
        Task<FirmaIsiDto> YasalProsedurAcAsync(int firmaId, string kod, IsTekrari tekrar, CancellationToken ct = default);

        /// <summary>Yasal işin prosedürünü yazar; ad ve tarih takvimde kalır.</summary>
        Task<FirmaIsiDto> YasalProsedurKaydetAsync(int firmaId, string kod, IsTekrari tekrar, IsProsedurKaydetDto dto,
                                                   CancellationToken ct = default);

        Task<FirmaIsiEkiDto> EkEkleAsync(int firmaId, int isId, FirmaIsiEkiOlusturDto dto, CancellationToken ct = default);

        /// <returns>Silinen ekin FileApiService Id'si.</returns>
        Task<int> EkSilAsync(int firmaId, int isId, int ekId, CancellationToken ct = default);
    }

    /// <summary>
    /// Firma işleri ve Yapılacaklar. Sorgular burada, kurallar <see cref="YapilacaklarKurucu"/>'da.
    ///
    /// "Bugün" <see cref="TimeProvider"/>'dan okunur (yerel gün) — testler sabit tarihle
    /// çalışır, <c>DateTime.Now</c> doğrudan çağrılmaz.
    ///
    /// <b>E-posta göndermez.</b> "Yönetim raporu e-postası" bir iş başlığıdır; uygulama
    /// hatırlatır, işaretlemeyi tutar.
    /// </summary>
    public class YapilacaklarService : IYapilacaklarService
    {
        private readonly CatalogContext _db;
        private readonly TimeProvider _saat;
        private readonly IHttpCurrentUser? _kullanici;
        private readonly IFirmaOlayYazici? _olay;

        public YapilacaklarService(CatalogContext db, TimeProvider saat, IHttpCurrentUser? kullanici = null,
                                   IFirmaOlayYazici? olay = null)
        {
            _db = db;
            _saat = saat;
            _kullanici = kullanici;
            _olay = olay;
        }

        private DateTime Bugun => _saat.GetLocalNow().Date;

        // ---- Okuma ----

        public async Task<FirmaIsleriKartDto> FirmaKartiAsync(int firmaId, CancellationToken ct = default)
        {
            var (satirlar, firmalar, takvimKodlari) = await SatirlarAsync(firmaId, ct);
            var tanimlar = await _db.FirmaIsleri.AsNoTracking().AsSplitQuery()
                .Include(i => i.Alicilar).Include(i => i.Ekler)
                .Where(i => i.FirmaId == firmaId).ToListAsync(ct);
            var kodlar = firmalar.FirstOrDefault(f => f.FirmaId == firmaId)?.Kodlar ?? Array.Empty<string>();

            return YapilacaklarKurucu.Kart(firmaId, satirlar, tanimlar, kodlar, takvimKodlari, Bugun,
                                           await SistemBaglamiAsync(firmaId, ct));
        }

        /// <summary>Ortak sistem listesi + firmanın atamaları + mizan formatı (öneri için).</summary>
        private async Task<SistemBaglami> SistemBaglamiAsync(int firmaId, CancellationToken ct)
        {
            var liste = await _db.Sistemler.AsNoTracking().ToListAsync(ct);
            var firmaninki = await _db.FirmaSistemleri.AsNoTracking().Where(f => f.FirmaId == firmaId)
                .OrderBy(f => f.Sira).ThenBy(f => f.Id).Select(f => f.SistemId).ToListAsync(ct);
            var mizanFormati = await _db.Firmalar.AsNoTracking().Where(f => f.Id == firmaId)
                .Select(f => f.MizanFormati).FirstOrDefaultAsync(ct);

            return new SistemBaglami(liste, firmaninki, mizanFormati);
        }

        public async Task<YapilacaklarDto> ListeAsync(IsFiltresi filtre, CancellationToken ct = default, int? firmaId = null)
        {
            if (!Enum.IsDefined(filtre))
                throw new YapilacaklarKuralException(nameof(filtre), "Geçersiz filtre.");

            // Firma GRUPLAMADAN ÖNCE süzülür: grup sayıları (Bu ay / Sonra / Yapıldı) filtreye uyar.
            var (satirlar, _, _) = await SatirlarAsync(firmaId, ct);
            var suzulmus = YapilacaklarKurucu.Suz(satirlar, filtre, BenimId());

            return new YapilacaklarDto
            {
                Bugun = Bugun,
                Filtre = filtre,
                Gruplar = YapilacaklarKurucu.Grupla(suzulmus, Bugun)
            };
        }

        public async Task<YapilacaklarOzetDto> OzetAsync(CancellationToken ct = default)
        {
            var (satirlar, _, _) = await SatirlarAsync(null, ct);
            return YapilacaklarKurucu.Ozet(YapilacaklarKurucu.Grupla(satirlar, Bugun));
        }

        /// <summary>
        /// Aktif firmalar ve mükellefiyet kodları — Yapılacaklar ve Dönem panosu aynı girdiyi
        /// buradan alır (hangi firma, hangi kodlar: tek tanım).
        /// </summary>
        public static async Task<List<YapilacaklarKurucu.FirmaGirdisi>> FirmaGirdileriAsync(CatalogContext db, int? firmaId,
                                                                                         CancellationToken ct)
        {
            var firmaSorgu = db.Firmalar.AsNoTracking().Where(f => f.Aktif);
            if (firmaId is not null) firmaSorgu = firmaSorgu.Where(f => f.Id == firmaId);
            var firmalar = await firmaSorgu.ToListAsync(ct);
            var idler = firmalar.Select(f => f.Id).ToList();

            var mukellefiyetler = await db.FirmaSicilBilgileri.AsNoTracking()
                .Where(s => idler.Contains(s.FirmaId))
                .Select(s => new { s.FirmaId, s.MukellefiyetTurleri })
                .ToListAsync(ct);
            var metin = mukellefiyetler.GroupBy(m => m.FirmaId).ToDictionary(g => g.Key, g => g.First().MukellefiyetTurleri);

            return firmalar.Select(f => new YapilacaklarKurucu.FirmaGirdisi(
                    f.Id, Anasayfa.Services.FirmaPaneliKurucu.Ad(f), f.SorumluKullaniciId, f.SorumluKullaniciAdi,
                    MukellefiyetSiniflandirici.Kodlar(metin.GetValueOrDefault(f.Id))))
                .ToList();
        }

        /// <summary>Aktif firmaların (ya da tek firmanın) görünen iş satırları.</summary>
        private async Task<(List<IsSatiriDto> Satirlar, List<YapilacaklarKurucu.FirmaGirdisi> Firmalar, List<string> TakvimKodlari)>
            SatirlarAsync(int? firmaId, CancellationToken ct)
        {
            var girdiler = await FirmaGirdileriAsync(_db, firmaId, ct);
            var idler = girdiler.Select(f => f.FirmaId).ToList();

            var takvim = await _db.VergiTakvimi.AsNoTracking().Where(t => t.Aktif).ToListAsync(ct);
            var ozel = await _db.FirmaIsleri.AsNoTracking().Where(i => i.Aktif && idler.Contains(i.FirmaId)).ToListAsync(ct);
            var tamamlar = await _db.IsTamamlamalari.AsNoTracking().Where(t => idler.Contains(t.FirmaId)).ToListAsync(ct);

            var satirlar = YapilacaklarKurucu.Satirlar(Bugun, girdiler, takvim, ozel, tamamlar);

            // Tamamlayanın adı: satırda gösterilen dönemin kaydından.
            var tamamIndex = tamamlar.Where(t => t.Yapildi).ToDictionary(t => (t.KaynakTip, t.KaynakId, t.FirmaId, t.DonemAnahtari));
            foreach (var s in satirlar.Where(s => s.Tamamlandi))
            {
                if (!tamamIndex.TryGetValue((s.KaynakTip, s.KaynakId, s.FirmaId, s.DonemAnahtari), out var t)) continue;
                s.TamamlanmaZamani = DateTime.SpecifyKind(t.TamamlanmaZamani!.Value, DateTimeKind.Utc);
                s.TamamlayanAdi = t.KullaniciAdi;
            }

            return (satirlar, girdiler, takvim.Select(t => t.MukellefiyetKodu).Distinct().ToList());
        }

        // ---- İşaretleme ----

        public async Task<List<int>> IsaretleAsync(IsIsaretleDto dto, CancellationToken ct = default)
        {
            if (dto.Isler.Count == 0)
                throw new YapilacaklarKuralException(nameof(dto.Isler), "İşaretlenecek iş yok.");

            var istekler = dto.Isler
                .Select(i => new IsIsaretDto
                {
                    KaynakTip = i.KaynakTip,
                    KaynakId = i.KaynakId,
                    FirmaId = i.FirmaId,
                    DonemAnahtari = i.DonemAnahtari?.Trim() ?? string.Empty
                })
                .DistinctBy(i => (i.KaynakTip, i.KaynakId, i.FirmaId, i.DonemAnahtari))
                .ToList();

            // Doğrulama: kaynak var mı, dönem o kaynağa ait mi, firma o işin sahibi mi.
            var aciklamalar = new Dictionary<(IsKaynagi, int, int, string), string>();
            foreach (var i in istekler)
                aciklamalar[(i.KaynakTip, i.KaynakId, i.FirmaId, i.DonemAnahtari)] = await DogrulaAsync(i, ct);

            var firmaIdleri = istekler.Select(i => i.FirmaId).Distinct().ToList();
            var mevcut = await _db.IsTamamlamalari
                .Where(t => firmaIdleri.Contains(t.FirmaId))
                .ToListAsync(ct);

            // Kanıtı olan kayıtlar: işaret kaldırılınca satır silinmez, yalnız zaman boşalır.
            var kayitIdleri = mevcut.Select(t => t.Id).ToList();
            var kanitliIdler = dto.Yapildi ? new HashSet<long>()
                : (await _db.IsTamamlamaEkleri.AsNoTracking().Where(e => kayitIdleri.Contains(e.IsTamamlamaId))
                       .Select(e => e.IsTamamlamaId).Distinct().ToListAsync(ct)).ToHashSet();

            var degisen = new List<(IsIsaretDto Is, string Aciklama)>();
            foreach (var i in istekler)
            {
                var kayit = mevcut.FirstOrDefault(t => t.KaynakTip == i.KaynakTip && t.KaynakId == i.KaynakId
                                                       && t.FirmaId == i.FirmaId && t.DonemAnahtari == i.DonemAnahtari);
                var aciklama = aciklamalar[(i.KaynakTip, i.KaynakId, i.FirmaId, i.DonemAnahtari)];

                if (dto.Yapildi && kayit is not { Yapildi: true })
                {
                    // Not/kanıt taşıyan yapılmamış kayıt varsa aynı kayda zaman yazılır; not ve kanıt korunur.
                    if (kayit is null)
                    {
                        kayit = new IsTamamlama
                        {
                            KaynakTip = i.KaynakTip,
                            KaynakId = i.KaynakId,
                            FirmaId = i.FirmaId,
                            DonemAnahtari = i.DonemAnahtari
                        };
                        _db.IsTamamlamalari.Add(kayit);
                    }

                    kayit.TamamlanmaZamani = _saat.GetUtcNow().UtcDateTime;
                    kayit.KullaniciId = KullaniciId();
                    kayit.KullaniciAdi = _kullanici?.UserName;
                    degisen.Add((i, aciklama));
                }
                else if (!dto.Yapildi && kayit is { Yapildi: true })
                {
                    if (kanitliIdler.Contains(kayit.Id) || !string.IsNullOrWhiteSpace(kayit.Not))
                    {
                        // Not ve kanıt SİLİNMEZ: yalnız "yapıldı" geri alınır. Kaydın tamamını
                        // Dönem panosunun "Bu dönem kaydını sil"i siler (önce sorar).
                        kayit.TamamlanmaZamani = null;
                        kayit.KullaniciId = null;
                        kayit.KullaniciAdi = null;
                    }
                    else
                    {
                        // Notsuz, kanıtsız kayıt: tablo yalnız "yapıldı"yı tutuyordu, satır silinir.
                        _db.IsTamamlamalari.Remove(kayit);
                    }
                    degisen.Add((i, aciklama));
                }
            }

            if (degisen.Count == 0) return new List<int>();
            await _db.SaveChangesAsync(ct);

            if (_olay is not null)
                foreach (var (i, aciklama) in degisen)
                    await _olay.YazAsync(i.FirmaId, dto.Yapildi ? FirmaOlayTipi.IsYapildi : FirmaOlayTipi.IsIsaretiKaldirildi,
                                         (dto.Yapildi ? "Yapıldı: " : "İşaret kaldırıldı: ") + aciklama, ct);

            // İşaret kaldırmak artık dosya silmez; sözleşme (fileIds) aynı kaldı.
            return new List<int>();
        }

        public Task<string> DonemDogrulaAsync(IsIsaretDto i, CancellationToken ct = default)
            => DogrulaAsync(new IsIsaretDto
            {
                KaynakTip = i.KaynakTip,
                KaynakId = i.KaynakId,
                FirmaId = i.FirmaId,
                DonemAnahtari = i.DonemAnahtari?.Trim() ?? string.Empty
            }, ct);

        /// <returns>Olay kaydı için okunur ad: "KDV beyannamesi (KDV-1) · Ağustos 2026".</returns>
        private async Task<string> DogrulaAsync(IsIsaretDto i, CancellationToken ct)
        {
            var firma = await _db.Firmalar.AsNoTracking().FirstOrDefaultAsync(f => f.Id == i.FirmaId, ct)
                        ?? throw new YapilacaklarKuralException(nameof(i.FirmaId), "Firma bulunamadı.");

            switch (i.KaynakTip)
            {
                case IsKaynagi.Yasal:
                {
                    var satir = await _db.VergiTakvimi.AsNoTracking().FirstOrDefaultAsync(t => t.Id == i.KaynakId, ct)
                                ?? throw new YapilacaklarKuralException(nameof(i.KaynakId), "Takvim satırı bulunamadı.");

                    var donem = new IsDonemi(satir.Tekrar, satir.Yil, satir.DonemNo);
                    if (donem.Anahtar != i.DonemAnahtari)
                        throw new YapilacaklarKuralException(nameof(i.DonemAnahtari), "Dönem takvim satırıyla uyuşmuyor.");

                    var metin = await _db.FirmaSicilBilgileri.AsNoTracking()
                        .Where(s => s.FirmaId == i.FirmaId).Select(s => s.MukellefiyetTurleri).FirstOrDefaultAsync(ct);
                    if (!MukellefiyetSiniflandirici.Kodlar(metin).Contains(satir.MukellefiyetKodu))
                        throw new YapilacaklarKuralException(nameof(i.FirmaId),
                            $"{Anasayfa.Services.FirmaPaneliKurucu.Ad(firma)} firmasında {satir.MukellefiyetKodu} mükellefiyeti yok.");

                    return $"{satir.Ad} · {donem.Etiket}";
                }
                case IsKaynagi.Ozel:
                {
                    var isi = await _db.FirmaIsleri.AsNoTracking().FirstOrDefaultAsync(x => x.Id == i.KaynakId, ct);
                    if (isi is null || isi.FirmaId != i.FirmaId || isi.YasalMukellefiyetKodu is not null)
                        throw new YapilacaklarKuralException(nameof(i.KaynakId), "İş bulunamadı.");

                    if (isi.Tekrar == IsTekrari.TekSefer)
                    {
                        if (i.DonemAnahtari != IsDonemi.TekSeferAnahtari)
                            throw new YapilacaklarKuralException(nameof(i.DonemAnahtari), "Geçersiz dönem.");
                        return isi.Baslik;
                    }

                    var donem = IsDonemi.Coz(isi.Tekrar, i.DonemAnahtari)
                                ?? throw new YapilacaklarKuralException(nameof(i.DonemAnahtari), "Geçersiz dönem.");
                    return $"{isi.Baslik} · {donem.Etiket}";
                }
                default:
                    throw new YapilacaklarKuralException(nameof(i.KaynakTip), "Geçersiz kaynak.");
            }
        }

        // ---- Firmaya özel iş tanımı ----

        public async Task<FirmaIsiDto> IsEkleAsync(int firmaId, FirmaIsiKaydetDto dto, CancellationToken ct = default)
        {
            Dogrula(dto);
            ProsedurDogrula(dto);
            await FirmaVarAsync(firmaId, ct);

            var isi = new FirmaIsi
            {
                FirmaId = firmaId,
                OlusturanKullaniciId = KullaniciId(),
                OlusturmaZamani = _saat.GetUtcNow().UtcDateTime
            };
            Uygula(isi, dto);
            await ProsedurUygulaAsync(isi, dto, ct);

            _db.FirmaIsleri.Add(isi);
            await _db.SaveChangesAsync(ct);

            await OlayAsync(firmaId, FirmaOlayTipi.IsEklendi, $"İş eklendi: {isi.Baslik}", ct);
            return await TanimAsync(isi, ct);
        }

        public async Task<FirmaIsiDto> IsGuncelleAsync(int firmaId, int isId, FirmaIsiKaydetDto dto,
                                                       CancellationToken ct = default)
        {
            Dogrula(dto);
            ProsedurDogrula(dto);

            var isi = await _db.FirmaIsleri.Include(i => i.Alicilar).Include(i => i.Ekler)
                          .FirstOrDefaultAsync(i => i.Id == isId && i.FirmaId == firmaId, ct)
                      ?? throw new KeyNotFoundException("İş bulunamadı.");

            // Yasal işin adı ve tarihi takvimden gelir; prosedürü ayrı uçtan yazılır.
            if (isi.YasalMukellefiyetKodu is not null)
                throw new YapilacaklarKuralException(nameof(dto.Baslik), "Yasal işin adı ve tarihi değiştirilemez.");

            Uygula(isi, dto);
            var alicilarDegisti = await ProsedurUygulaAsync(isi, dto, ct);
            if (!alicilarDegisti && !_db.Entry(isi).Properties.Any(p => p.IsModified)) return await TanimAsync(isi, ct);

            await _db.SaveChangesAsync(ct);
            await OlayAsync(firmaId, FirmaOlayTipi.IsGuncellendi, $"İş güncellendi: {isi.Baslik}", ct);
            return await TanimAsync(isi, ct);
        }

        /// <summary>
        /// Tanım, bütün tamamlamaları, alıcıları ve ek kayıtları silinir (tamamlamada FK yok,
        /// servis temizler). Bu işi ön adım gösteren işlerin bağı boşaltılır.
        /// </summary>
        public async Task<List<int>> IsSilAsync(int firmaId, int isId, CancellationToken ct = default)
        {
            var isi = await _db.FirmaIsleri.Include(i => i.Ekler)
                          .FirstOrDefaultAsync(i => i.Id == isId && i.FirmaId == firmaId, ct)
                      ?? throw new KeyNotFoundException("İş bulunamadı.");

            if (isi.YasalMukellefiyetKodu is not null)
                throw new YapilacaklarKuralException(nameof(isId), "Yasal iş silinemez; mükellefiyetten türer.");

            var tamamlar = await _db.IsTamamlamalari
                .Where(t => t.KaynakTip == IsKaynagi.Ozel && t.KaynakId == isId)
                .ToListAsync(ct);

            var bagliIsler = await _db.FirmaIsleri.Where(i => i.OnAdimiOlduguIsId == isId).ToListAsync(ct);
            foreach (var b in bagliIsler) b.OnAdimiOlduguIsId = null;

            // Prosedür ekleri + dönemlerin kanıtları: hepsinin dosyası FileApi'den silinecek.
            var tamamIdleri = tamamlar.Select(t => t.Id).ToList();
            var donemEkleri = await _db.IsTamamlamaEkleri.Where(e => tamamIdleri.Contains(e.IsTamamlamaId)).ToListAsync(ct);
            var fileIdleri = isi.Ekler.Select(e => e.FileId).Concat(donemEkleri.Select(e => e.FileId)).ToList();

            _db.IsTamamlamaEkleri.RemoveRange(donemEkleri);
            _db.IsTamamlamalari.RemoveRange(tamamlar);
            _db.FirmaIsleri.Remove(isi);
            await _db.SaveChangesAsync(ct);

            await OlayAsync(firmaId, FirmaOlayTipi.IsSilindi, $"İş silindi: {isi.Baslik}", ct);
            return fileIdleri;
        }

        // ---- Toplu ekleme ve kopyalama ----

        public async Task<TopluOnizlemeDto> TopluOnizleAsync(int firmaId, TopluOnizleIstekDto istek, CancellationToken ct = default)
        {
            await FirmaVarAsync(firmaId, ct);
            if ((istek.Metin?.Length ?? 0) > TopluMetinEnFazla)
                throw new YapilacaklarKuralException(nameof(istek.Metin), "Yapıştırılan metin çok uzun (en çok 200.000 karakter).");

            var baglam = await SistemBaglamiAsync(firmaId, ct);
            return TopluIsAyristirici.Coz(istek, baglam.Liste, baglam.FirmaSistemIdleri.ToList(), await MevcutBasliklarAsync(firmaId, ct));
        }

        public const int TopluMetinEnFazla = 200_000;
        public const int TopluEnFazlaIs = 500;

        public async Task<TopluSonucDto> TopluEkleAsync(int firmaId, TopluEkleDto dto, CancellationToken ct = default)
        {
            await FirmaVarAsync(firmaId, ct);
            if (dto.Isler.Count > TopluEnFazlaIs)
                throw new YapilacaklarKuralException(nameof(dto.Isler), $"Bir seferde en çok {TopluEnFazlaIs} iş eklenebilir.");
            IlkDonemDogrula(dto.IlkDonem);

            var sonuc = new TopluSonucDto();
            var mevcut = await MevcutBasliklarAsync(firmaId, ct);

            foreach (var isDto in dto.Isler)
            {
                var anahtar = TopluIsAyristirici.BaslikAnahtari(isDto.Baslik);
                try
                {
                    if (mevcut.TryGetValue(anahtar, out var mevcutId))
                    {
                        if (dto.UzerineYazma)
                        {
                            sonuc.Atlananlar.Add(new TopluAtlananDto { Baslik = isDto.Baslik, Sebep = "aynı başlıklı iş var (üzerine yazılmadı)" });
                            continue;
                        }

                        await IsGuncelleAsync(firmaId, mevcutId, await BirlestirAsync(mevcutId, isDto, ct), ct);
                        sonuc.Guncellenen++;
                        continue;
                    }

                    // Prompt 17: bütün satırlar TEK başlangıç ayı alır (önizlemenin üstündeki seçim).
                    isDto.IlkDonem = dto.IlkDonem;
                    var eklenen = await IsEkleAsync(firmaId, isDto, ct);
                    mevcut[anahtar] = eklenen.Id;   // aynı istekte ikinci kez gelirse atlansın
                    sonuc.Eklenen++;
                }
                catch (YapilacaklarKuralException ex)
                {
                    // Önizlemeden sonra değişen durum (ör. sistem pasife alındı): satır atlanır, rapor edilir.
                    _db.ChangeTracker.Clear();
                    sonuc.Atlananlar.Add(new TopluAtlananDto { Baslik = isDto.Baslik, Sebep = ex.Message });
                }
            }

            return sonuc;
        }

        /// <summary>
        /// Güncellemede yapıştırmada OLMAYAN alanlar mevcut değerini korur: sorumlu, aktiflik,
        /// nasıl yapılır, ön adım, başlangıç ayı (Prompt 17 — mevcut işin geçmiş dönemleri
        /// kaybolmasın); boş gelen program/menü/açıklama da korunur. Alıcı listesi yapıştırmada
        /// yoksa dokunulmaz (null).
        /// </summary>
        private async Task<FirmaIsiKaydetDto> BirlestirAsync(int mevcutId, FirmaIsiKaydetDto yeni, CancellationToken ct)
        {
            var m = await _db.FirmaIsleri.AsNoTracking().FirstAsync(i => i.Id == mevcutId, ct);

            yeni.IlkDonem = m.IlkDonem;
            yeni.SorumluKullaniciId = m.SorumluKullaniciId;
            yeni.SorumluKullaniciAdi = m.SorumluKullaniciAdi;
            yeni.Aktif = m.Aktif;
            yeni.NasilYapilir = m.NasilYapilir;
            yeni.OnAdimiOlduguIsId = m.OnAdimiOlduguIsId;
            yeni.SistemId ??= m.SistemId;
            yeni.MenuYolu ??= m.MenuYolu;
            yeni.Aciklama ??= m.Aciklama;
            return yeni;
        }

        public async Task<TopluSonucDto> KopyalaAsync(int hedefFirmaId, IsKopyalaDto dto, CancellationToken ct = default)
        {
            await FirmaVarAsync(hedefFirmaId, ct);
            if (dto.KaynakFirmaId == hedefFirmaId)
                throw new YapilacaklarKuralException(nameof(dto.KaynakFirmaId), "Firma kendisinden kopyalanamaz.");
            await FirmaVarAsync(dto.KaynakFirmaId, ct);
            IlkDonemDogrula(dto.IlkDonem);

            // Yalnız ÖZEL işler: yasal işler mükellefiyetten türer, prosedür satırları (yasal) kopyalanmaz.
            var kaynaklar = await _db.FirmaIsleri.AsNoTracking().Include(i => i.Alicilar)
                .Where(i => i.FirmaId == dto.KaynakFirmaId && i.YasalMukellefiyetKodu == null && dto.IsIdleri.Contains(i.Id))
                .OrderBy(i => i.Id)
                .ToListAsync(ct);

            var pasifSistemler = (await _db.Sistemler.AsNoTracking().Where(s => !s.Aktif).Select(s => s.Id).ToListAsync(ct)).ToHashSet();
            var mevcut = await MevcutBasliklarAsync(hedefFirmaId, ct);
            var sonuc = new TopluSonucDto();

            foreach (var k in kaynaklar)
            {
                var anahtar = TopluIsAyristirici.BaslikAnahtari(k.Baslik);
                if (mevcut.ContainsKey(anahtar))
                {
                    sonuc.Atlananlar.Add(new TopluAtlananDto { Baslik = k.Baslik, Sebep = "bu firmada aynı başlıklı iş var" });
                    continue;
                }

                // Kopyalanan: başlık, açıklama, tekrar, gün kuralı, program, menü yolu, nasıl yapılır, alıcılar.
                // Kopyalanmayan: tamamlamalar, dönem/prosedür ekleri, ön adım bağı, sorumlu — firmaya özgü.
                // Başlangıç ayı da TAŞINMAZ (Prompt 17): kopya hedefte seçilen aydan başlar.
                var kopya = new FirmaIsiKaydetDto
                {
                    Baslik = k.Baslik,
                    Aciklama = k.Aciklama,
                    Tekrar = k.Tekrar,
                    GunKurali = k.GunKurali,
                    AyinGunu = k.AyinGunu,
                    TekSeferTarih = k.TekSeferTarih,
                    IlkDonem = dto.IlkDonem,
                    Aktif = k.Aktif,
                    SistemId = k.SistemId is { } s && !pasifSistemler.Contains(s) ? s : null,
                    MenuYolu = k.MenuYolu,
                    NasilYapilir = k.NasilYapilir,
                    Alicilar = k.Alicilar.OrderBy(a => a.Sira).ThenBy(a => a.Id)
                        .Select(a => new FirmaIsiAlicisiDto { AdSoyad = a.AdSoyad, Eposta = a.Eposta, Rol = a.Rol, AliciTipi = a.AliciTipi })
                        .ToList()
                };

                try
                {
                    var eklenen = await IsEkleAsync(hedefFirmaId, kopya, ct);
                    mevcut[anahtar] = eklenen.Id;
                    sonuc.Eklenen++;
                }
                catch (YapilacaklarKuralException ex)
                {
                    _db.ChangeTracker.Clear();
                    sonuc.Atlananlar.Add(new TopluAtlananDto { Baslik = k.Baslik, Sebep = ex.Message });
                }
            }

            return sonuc;
        }

        /// <summary>Firmanın özel işleri: başlık anahtarı → Id (yasal prosedür satırları hariç).</summary>
        private async Task<Dictionary<string, int>> MevcutBasliklarAsync(int firmaId, CancellationToken ct)
            => (await _db.FirmaIsleri.AsNoTracking()
                    .Where(i => i.FirmaId == firmaId && i.YasalMukellefiyetKodu == null)
                    .Select(i => new { i.Id, i.Baslik }).ToListAsync(ct))
               .GroupBy(i => TopluIsAyristirici.BaslikAnahtari(i.Baslik))
               .ToDictionary(g => g.Key, g => g.Min(i => i.Id));

        /// <summary>Boş şablon (xlsx): ayrıştırıcının tanıdığı başlık satırı + silinecek örnek satır.</summary>
        public static byte[] IsSablonu()
        {
            string[] basliklar = { "Başlık", "Tekrar", "Gün", "Program", "Menü yolu", "Alıcı", "Açıklama" };
            string[] ornek =
            {
                "Bordroyu hazırla ve gönder", "her ay", "25", "Luca", "Bordro > Raporlar > Ücret Bordrosu",
                "Ad Soyad <ad@firma.com>", "ÖRNEK SATIR — silip kendi işlerinizi yazın"
            };

            using var kitap = new ClosedXML.Excel.XLWorkbook();
            var sayfa = kitap.Worksheets.Add("Firma işleri");
            for (var i = 0; i < basliklar.Length; i++)
            {
                sayfa.Cell(1, i + 1).Value = basliklar[i];
                sayfa.Cell(2, i + 1).Value = ornek[i];
            }

            sayfa.Row(1).Style.Font.Bold = true;
            sayfa.Row(2).Style.Font.Italic = true;
            sayfa.Row(2).Style.Font.FontColor = ClosedXML.Excel.XLColor.Gray;

            // Bilgi sayfası: kabul edilen yazımlar (Excel'de doldururken bakılsın).
            var bilgi = kitap.Worksheets.Add("Yazımlar");
            bilgi.Cell(1, 1).Value = "Tekrar";
            bilgi.Cell(1, 2).Value = "her ay · aylık · ayda bir | üç ayda bir · 3 ayda bir · 3 aylık | yılda bir · yıllık · senede bir | tek sefer · bir kez";
            bilgi.Cell(2, 1).Value = "Gün";
            bilgi.Cell(2, 2).Value = "1–31 (ayın günü) | ay sonu | dönem sonu | gg.aa.yyyy (tek seferlik işte tarih)";
            bilgi.Cell(3, 1).Value = "Program";
            bilgi.Cell(3, 2).Value = "Yönetim → Sistemler listesindeki ad (ör. Luca, ORKA, DijitalPlanet). Listede yoksa boş kalır.";
            bilgi.Cell(4, 1).Value = "Alıcı";
            bilgi.Cell(4, 2).Value = "Ad Soyad <eposta@firma.com>; birden çok alıcı ; ile. Yalnız ad da olur.";
            bilgi.Column(1).Style.Font.Bold = true;

            sayfa.Columns().AdjustToContents();
            bilgi.Columns().AdjustToContents();

            using var akis = new MemoryStream();
            kitap.SaveAs(akis);
            return akis.ToArray();
        }

        // ---- Prosedür ----

        public async Task<IsProsedurDto> ProsedurAsync(int firmaId, IsKaynagi kaynakTip, int kaynakId, bool tumGecmis,
                                                       CancellationToken ct = default)
        {
            var firma = await _db.Firmalar.AsNoTracking().FirstOrDefaultAsync(f => f.Id == firmaId, ct)
                        ?? throw new KeyNotFoundException("Firma bulunamadı.");

            var tamamlar = await _db.IsTamamlamalari.AsNoTracking()
                .Where(t => t.FirmaId == firmaId && t.KaynakTip == kaynakTip).ToListAsync(ct);
            int? adet = tumGecmis ? null : 6;

            FirmaIsi? isi;
            IsProsedurDto dto;

            switch (kaynakTip)
            {
                case IsKaynagi.Ozel:
                {
                    isi = await ProsedurSatiriAsync(i => i.Id == kaynakId && i.FirmaId == firmaId && i.YasalMukellefiyetKodu == null, ct)
                          ?? throw new KeyNotFoundException("İş bulunamadı.");

                    var (gecmis, toplam) = YapilacaklarKurucu.OzelGecmis(isi, tamamlar, Bugun, adet);
                    dto = new IsProsedurDto
                    {
                        Baslik = isi.Baslik,
                        Tekrar = isi.Tekrar,
                        KuralMetni = IsTakvimHesabi.KuralMetni(isi.Tekrar, isi.GunKurali, isi.AyinGunu, isi.TekSeferTarih),
                        Gecmis = gecmis,
                        GecmisToplam = toplam
                    };
                    break;
                }
                case IsKaynagi.Yasal:
                {
                    var satir = await _db.VergiTakvimi.AsNoTracking().FirstOrDefaultAsync(t => t.Id == kaynakId, ct)
                                ?? throw new KeyNotFoundException("Takvim satırı bulunamadı.");

                    var seri = await _db.VergiTakvimi.AsNoTracking()
                        .Where(t => t.MukellefiyetKodu == satir.MukellefiyetKodu && t.Tekrar == satir.Tekrar).ToListAsync(ct);
                    var kod = satir.MukellefiyetKodu;
                    var tekrar = satir.Tekrar;
                    isi = await ProsedurSatiriAsync(i => i.FirmaId == firmaId && i.YasalMukellefiyetKodu == kod && i.Tekrar == tekrar, ct);

                    var (gecmis, toplam) = YapilacaklarKurucu.YasalGecmis(seri, firmaId, tamamlar, Bugun, adet);
                    dto = new IsProsedurDto
                    {
                        Baslik = SeriAdi(seri) ?? satir.Ad,
                        MukellefiyetKodu = kod,
                        Tekrar = tekrar,
                        KuralMetni = "son gün Vergi takviminden",
                        Gecmis = gecmis,
                        GecmisToplam = toplam
                    };
                    break;
                }
                default:
                    throw new YapilacaklarKuralException(nameof(kaynakTip), "Geçersiz kaynak.");
            }

            dto.FirmaId = firmaId;
            dto.KaynakTip = kaynakTip;
            dto.MizanFormati = firma.MizanFormati;

            // İş tarifi (Prompt 14): Nasıl yapılır sekmesiyle AYNI çözüm; düzenleme orada.
            dto.TarifAnahtari = dto.MukellefiyetKodu is { } tk ? IsTarifiKurucu.YasalAnahtar(tk, dto.Tekrar)
                                                                 : IsTarifiKurucu.OzelAnahtar(dto.Baslik, dto.Tekrar);
            if (await IsTarifiService.TarifCozAsync(_db, isi, dto.MukellefiyetKodu, dto.Tekrar, ct) is { } tarif)
            {
                dto.TarifId = tarif.Id;
                dto.TarifMenuYolu = tarif.MenuYolu;
                dto.TarifAdimlar = YapilacaklarKurucu.Adimlar(tarif.NasilYapilir);
                dto.TarifSistemAdi = tarif.SistemId is { } tsid
                    ? await _db.Sistemler.AsNoTracking().Where(s => s.Id == tsid).Select(s => s.Ad).FirstOrDefaultAsync(ct)
                    : null;
                dto.FirmayaOzel = isi is not null && IsTarifiKurucu.FirmayaOzelMetinVar(isi, tarif.MenuYolu, tarif.NasilYapilir);
            }

            if (isi is null) return dto;

            dto.IsId = isi.Id;
            dto.SistemId = isi.SistemId;
            if (isi.SistemId is { } sistemId)
            {
                var baglam = await SistemBaglamiAsync(firmaId, ct);
                var sistem = baglam.Liste.FirstOrDefault(s => s.Id == sistemId);
                dto.SistemAdi = sistem?.Ad;

                // İş bir muhasebe programında ama firmanın muhasebe programı başka: not düşülür.
                var firmaMuhasebe = baglam.FirmaSistemIdleri
                    .Select(id => baglam.Liste.FirstOrDefault(s => s.Id == id))
                    .Where(s => s is { Tur: Sistemler.Domain.SistemTuru.Muhasebe })
                    .ToList();
                if (sistem is { Tur: Sistemler.Domain.SistemTuru.Muhasebe } && firmaMuhasebe.Count > 0
                    && firmaMuhasebe.All(s => s!.Id != sistem.Id))
                    dto.FarkliMuhasebeProgrami = string.Join(", ", firmaMuhasebe.Select(s => s!.Ad));
            }
            dto.MenuYolu = isi.MenuYolu;
            dto.NasilYapilir = isi.NasilYapilir;
            dto.Adimlar = YapilacaklarKurucu.Adimlar(isi.NasilYapilir);
            dto.OnAdimiOlduguIsId = isi.OnAdimiOlduguIsId;
            dto.OnAdimiBaslik = isi.OnAdimiOlduguIsId is { } hedef
                ? await _db.FirmaIsleri.AsNoTracking().Where(i => i.Id == hedef).Select(i => i.Baslik).FirstOrDefaultAsync(ct)
                : null;
            dto.Alicilar = isi.Alicilar.OrderBy(a => a.Sira).ThenBy(a => a.Id).Select(YapilacaklarKurucu.Alici).ToList();
            dto.Ekler = isi.Ekler.OrderByDescending(e => e.YuklemeZamani).ThenByDescending(e => e.Id)
                .Select(YapilacaklarKurucu.Ek).ToList();
            return dto;
        }

        private Task<FirmaIsi?> ProsedurSatiriAsync(System.Linq.Expressions.Expression<Func<FirmaIsi, bool>> kosul,
                                                    CancellationToken ct)
            => _db.FirmaIsleri.AsNoTracking().AsSplitQuery()
                  .Include(i => i.Alicilar).Include(i => i.Ekler)
                  .FirstOrDefaultAsync(kosul, ct);

        /// <summary>Serinin güncel adı: aktif satırların son gününe en yakın olanı.</summary>
        private static string? SeriAdi(IEnumerable<VergiTakvimi> seri)
            => seri.OrderByDescending(t => t.Aktif).ThenByDescending(t => t.SonGun).Select(t => t.Ad).FirstOrDefault();

        public async Task<FirmaIsiDto> YasalProsedurAcAsync(int firmaId, string kod, IsTekrari tekrar,
                                                            CancellationToken ct = default)
        {
            var isi = await YasalSatirAsync(firmaId, kod, tekrar, ct);
            if (_db.Entry(isi).State == EntityState.Added) await _db.SaveChangesAsync(ct);
            return await TanimAsync(isi, ct);
        }

        public async Task<FirmaIsiDto> YasalProsedurKaydetAsync(int firmaId, string kod, IsTekrari tekrar,
                                                                IsProsedurKaydetDto dto, CancellationToken ct = default)
        {
            ProsedurDogrula(dto);

            var isi = await YasalSatirAsync(firmaId, kod, tekrar, ct);
            var yeni = _db.Entry(isi).State == EntityState.Added;

            var alicilarDegisti = await ProsedurUygulaAsync(isi, dto, ct);
            if (!yeni && !alicilarDegisti && !_db.Entry(isi).Properties.Any(p => p.IsModified)) return await TanimAsync(isi, ct);

            await _db.SaveChangesAsync(ct);
            await OlayAsync(firmaId, FirmaOlayTipi.IsGuncellendi, $"İş prosedürü güncellendi: {isi.Baslik}", ct);
            return await TanimAsync(isi, ct);
        }

        /// <summary>
        /// Yasal işin prosedür satırı (izlenen). Yoksa eklenir ama <b>kaydedilmez</b> — çağıran
        /// kaydeder. Firma o mükellefiyette değilse ya da takvimde seri yoksa satır açılmaz.
        /// </summary>
        private async Task<FirmaIsi> YasalSatirAsync(int firmaId, string? kod, IsTekrari tekrar, CancellationToken ct)
        {
            kod = kod?.Trim() ?? string.Empty;

            var mevcut = await _db.FirmaIsleri.Include(i => i.Alicilar).Include(i => i.Ekler)
                .FirstOrDefaultAsync(i => i.FirmaId == firmaId && i.YasalMukellefiyetKodu == kod && i.Tekrar == tekrar, ct);
            if (mevcut is not null) return mevcut;

            var firma = await _db.Firmalar.AsNoTracking().FirstOrDefaultAsync(f => f.Id == firmaId, ct)
                        ?? throw new YapilacaklarKuralException("firmaId", "Firma bulunamadı.");

            var metin = await _db.FirmaSicilBilgileri.AsNoTracking()
                .Where(s => s.FirmaId == firmaId).Select(s => s.MukellefiyetTurleri).FirstOrDefaultAsync(ct);
            if (!MukellefiyetSiniflandirici.Kodlar(metin).Contains(kod))
                throw new YapilacaklarKuralException("kod",
                    $"{Anasayfa.Services.FirmaPaneliKurucu.Ad(firma)} firmasında {kod} mükellefiyeti yok.");

            var seri = await _db.VergiTakvimi.AsNoTracking()
                .Where(t => t.MukellefiyetKodu == kod && t.Tekrar == tekrar).ToListAsync(ct);
            var ad = SeriAdi(seri) ?? throw new YapilacaklarKuralException("kod", "Takvimde bu yasal iş yok.");

            var isi = new FirmaIsi
            {
                FirmaId = firmaId,
                YasalMukellefiyetKodu = kod,
                Baslik = ad.Length > 200 ? ad[..200] : ad,
                Tekrar = tekrar,
                GunKurali = IsGunKurali.DonemSonu,
                OlusturanKullaniciId = KullaniciId(),
                OlusturmaZamani = _saat.GetUtcNow().UtcDateTime
            };
            _db.FirmaIsleri.Add(isi);
            return isi;
        }

        private static void ProsedurDogrula(IsProsedurKaydetDto dto)
        {
            if (dto.SistemId is <= 0)
                throw new YapilacaklarKuralException(nameof(dto.SistemId), "Geçersiz sistem.");
            if (dto.MenuYolu is { Length: > 300 })
                throw new YapilacaklarKuralException(nameof(dto.MenuYolu), "Menü yolu en çok 300 karakter olabilir.");
            if (dto.NasilYapilir is { Length: > 4000 })
                throw new YapilacaklarKuralException(nameof(dto.NasilYapilir), "\"Nasıl yapılır\" en çok 4000 karakter olabilir.");
            if (!string.IsNullOrWhiteSpace(dto.OnAdimiYasalKodu) && dto.OnAdimiYasalTekrar is null)
                throw new YapilacaklarKuralException(nameof(dto.OnAdimiYasalTekrar), "Ön adım yasal işinin tekrarı eksik.");

            foreach (var a in dto.Alicilar ?? new())
            {
                var ad = a.AdSoyad?.Trim() ?? string.Empty;
                if (ad.Length == 0)
                    throw new YapilacaklarKuralException("Alicilar", "Alıcının adı boş olamaz.");
                if (ad.Length > 150)
                    throw new YapilacaklarKuralException("Alicilar", "Alıcı adı en çok 150 karakter olabilir.");
                if (!Enum.IsDefined(a.AliciTipi))
                    throw new YapilacaklarKuralException("Alicilar", "Geçersiz alıcı tipi (Kime / Bilgi).");
                if (a.Rol is { Length: > 100 })
                    throw new YapilacaklarKuralException("Alicilar", "Rol en çok 100 karakter olabilir.");

                var eposta = a.Eposta?.Trim();
                if (!string.IsNullOrEmpty(eposta) && (eposta.Length > 200 || !EpostaGibi(eposta)))
                    throw new YapilacaklarKuralException("Alicilar", $"{ad}: e-posta adresi geçersiz.");
            }
        }

        /// <summary>Alıcı e-postası kuralı — dialog ve toplu ekleme (TopluIsAyristirici) aynı kuralı kullanır.</summary>
        internal static bool EpostaGibi(string s)
        {
            var at = s.IndexOf('@');
            return at > 0 && at == s.LastIndexOf('@') && s.IndexOf('.', at) > at + 1 && !s.EndsWith('.') && !s.Contains(' ');
        }

        /// <summary>
        /// Prosedür alanlarını işe yazar. Ön adım hedefi doğrulanır (aynı firma, kendisi değil,
        /// döngü yok); yasal hedefin satırı yoksa açılır.
        /// </summary>
        /// <returns>Alıcı listesi değişti mi (alan değişikliği <c>IsModified</c>'dan okunur).</returns>
        private async Task<bool> ProsedurUygulaAsync(FirmaIsi isi, IsProsedurKaydetDto dto, CancellationToken ct)
        {
            // Sistem: yeni seçilen pasif olamaz; zaten atanmış pasif sistem kalabilir (atama bozulmaz).
            if (dto.SistemId is { } sistemId && sistemId != isi.SistemId)
            {
                var sistem = await _db.Sistemler.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sistemId, ct)
                             ?? throw new YapilacaklarKuralException(nameof(dto.SistemId), "Sistem bulunamadı.");
                if (!sistem.Aktif)
                    throw new YapilacaklarKuralException(nameof(dto.SistemId), $"{sistem.Ad} pasife alınmış; yeni işte seçilemez.");
            }
            isi.SistemId = dto.SistemId;
            isi.MenuYolu = Bos(dto.MenuYolu);
            isi.NasilYapilir = string.IsNullOrWhiteSpace(dto.NasilYapilir) ? null : dto.NasilYapilir.Replace("\r\n", "\n").Trim();

            int? hedefId = dto.OnAdimiOlduguIsId;
            if (!string.IsNullOrWhiteSpace(dto.OnAdimiYasalKodu) && dto.OnAdimiYasalTekrar is { } hedefTekrar)
            {
                if (isi.YasalMukellefiyetKodu == dto.OnAdimiYasalKodu.Trim() && isi.Tekrar == hedefTekrar)
                    throw new YapilacaklarKuralException(nameof(dto.OnAdimiOlduguIsId), "İş kendisinin ön adımı olamaz.");

                var hedef = await YasalSatirAsync(isi.FirmaId, dto.OnAdimiYasalKodu, hedefTekrar, ct);
                if (_db.Entry(hedef).State == EntityState.Added) await _db.SaveChangesAsync(ct);
                hedefId = hedef.Id;
            }

            if (hedefId is { } h)
            {
                if (isi.Id != 0 && h == isi.Id)
                    throw new YapilacaklarKuralException(nameof(dto.OnAdimiOlduguIsId), "İş kendisinin ön adımı olamaz.");

                var zincir = await _db.FirmaIsleri.AsNoTracking().Where(i => i.FirmaId == isi.FirmaId)
                    .Select(i => new { i.Id, i.OnAdimiOlduguIsId }).ToDictionaryAsync(i => i.Id, i => i.OnAdimiOlduguIsId, ct);
                if (!zincir.ContainsKey(h))
                    throw new YapilacaklarKuralException(nameof(dto.OnAdimiOlduguIsId), "Ön adımı olduğu iş bulunamadı.");

                // Hedeften yukarı yürü: bu işe geri dönüyorsa döngü.
                var adim = (int?)h;
                for (var i = 0; adim is { } a && i <= zincir.Count; i++)
                {
                    if (isi.Id != 0 && a == isi.Id)
                        throw new YapilacaklarKuralException(nameof(dto.OnAdimiOlduguIsId),
                            "Ön adım bağı döngü oluşturuyor (iki iş birbirinin hazırlığı olamaz).");
                    adim = zincir.GetValueOrDefault(a);
                }
            }
            isi.OnAdimiOlduguIsId = hedefId;

            if (dto.Alicilar is null) return false;

            var yeni = dto.Alicilar
                .Select((a, i) => new FirmaIsiAlicisi
                {
                    AdSoyad = a.AdSoyad.Trim(),
                    Eposta = Bos(a.Eposta),
                    Rol = Bos(a.Rol),
                    AliciTipi = a.AliciTipi,
                    Sira = i + 1
                })
                .ToList();

            var eski = isi.Alicilar.OrderBy(a => a.Sira).ThenBy(a => a.Id).ToList();
            var ayni = eski.Count == yeni.Count && eski.Zip(yeni).All(p =>
                p.First.AdSoyad == p.Second.AdSoyad && p.First.Eposta == p.Second.Eposta &&
                p.First.Rol == p.Second.Rol && p.First.AliciTipi == p.Second.AliciTipi && p.First.Sira == p.Second.Sira);
            if (ayni) return false;

            // Prompt 14: her alıcı firmanın bir KİŞİSİNE bağlanır (aynı e-posta tek kişi; kural KisiEslestirici'de).
            // Alıcının ad/e-posta alanları kişinin okuma kopyasıdır — kişiden yazılır.
            await KisiService.KisilereBaglaAsync(_db, isi.FirmaId, yeni, ct);

            // Liste bütün olarak yazılır: gönderilmeyen alıcı silinir.
            _db.FirmaIsiAlicilari.RemoveRange(eski);
            isi.Alicilar.Clear();
            isi.Alicilar.AddRange(yeni);
            return true;
        }

        // ---- Ekler ----

        /// <summary>İzin verilen ek uzantıları. Boyut sınırı Belgeler kartıyla aynı sabit.</summary>
        public static readonly IReadOnlySet<string> EkUzantilari = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".pdf", ".xlsx", ".xls", ".docx", ".csv", ".txt"
        };

        public static long EkEnFazlaBayt => FirmaBilgileri.Services.FirmaBilgiService.EnFazlaBayt;

        /// <summary>
        /// Ek doğrulaması — prosedür eki (Prompt 8) ve dönem eki (Prompt 10) AYNI kuralı kullanır:
        /// aynı türler, Belgeler kartıyla aynı boyut sınırı.
        /// </summary>
        /// <returns>Temizlenmiş dosya adı ve içerik türü.</returns>
        public static (string Ad, string ContentType) EkDogrula(int fileId, string? dosyaAdi, string? contentType, long boyut)
        {
            if (fileId <= 0)
                throw new YapilacaklarKuralException("FileId", "Dosya kimliği yok. Dosya önce FileApiService'e yüklenmeli.");

            var ad = Path.GetFileName(dosyaAdi?.Trim() ?? string.Empty);
            if (ad.Length == 0)
                throw new YapilacaklarKuralException("DosyaAdi", "Dosya adı boş.");
            if (!EkUzantilari.Contains(Path.GetExtension(ad)))
                throw new YapilacaklarKuralException("DosyaAdi",
                    "Bu dosya türü eklenemez. İzin verilenler: png, jpg, pdf, xlsx, xls, docx, csv, txt.");
            if (boyut <= 0)
                throw new YapilacaklarKuralException("Boyut", "Boş dosya eklenemez.");
            if (boyut > EkEnFazlaBayt)
                throw new YapilacaklarKuralException("Boyut", $"Dosya {EkEnFazlaBayt / (1024 * 1024)} MB sınırını aşıyor.");

            var tur = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream"
                    : contentType.Trim() is { Length: > 128 } uzun ? uzun[..128] : contentType.Trim();
            return (ad.Length > 260 ? ad[..260] : ad, tur);
        }

        public async Task<FirmaIsiEkiDto> EkEkleAsync(int firmaId, int isId, FirmaIsiEkiOlusturDto dto, CancellationToken ct = default)
        {
            var (ad, contentType) = EkDogrula(dto.FileId, dto.DosyaAdi, dto.ContentType, dto.Boyut);

            var isi = await _db.FirmaIsleri.AsNoTracking().FirstOrDefaultAsync(i => i.Id == isId && i.FirmaId == firmaId, ct)
                      ?? throw new KeyNotFoundException("İş bulunamadı.");

            var ek = new FirmaIsiEki
            {
                FirmaIsiId = isi.Id,
                FileId = dto.FileId,
                DosyaAdi = ad,
                ContentType = contentType,
                Boyut = dto.Boyut,
                YuklemeZamani = _saat.GetUtcNow().UtcDateTime,
                YukleyenKullaniciId = KullaniciId(),
                YukleyenKullaniciAdi = _kullanici?.UserName is { Length: > 100 } k ? k[..100] : _kullanici?.UserName
            };

            _db.FirmaIsiEkleri.Add(ek);
            await _db.SaveChangesAsync(ct);

            await OlayAsync(firmaId, FirmaOlayTipi.IsGuncellendi, $"Ek eklendi: {ek.DosyaAdi} ({isi.Baslik})", ct);
            return YapilacaklarKurucu.Ek(ek);
        }

        public async Task<int> EkSilAsync(int firmaId, int isId, int ekId, CancellationToken ct = default)
        {
            var isi = await _db.FirmaIsleri.AsNoTracking().FirstOrDefaultAsync(i => i.Id == isId && i.FirmaId == firmaId, ct)
                      ?? throw new KeyNotFoundException("İş bulunamadı.");

            var ek = await _db.FirmaIsiEkleri.FirstOrDefaultAsync(e => e.Id == ekId && e.FirmaIsiId == isId, ct)
                     ?? throw new KeyNotFoundException("Ek bulunamadı.");

            _db.FirmaIsiEkleri.Remove(ek);
            await _db.SaveChangesAsync(ct);

            await OlayAsync(firmaId, FirmaOlayTipi.IsGuncellendi, $"Ek silindi: {ek.DosyaAdi} ({isi.Baslik})", ct);
            return ek.FileId;
        }

        /// <summary>Kaydedilmiş işin tanımı; ön adım adı ve ek sayısı veritabanından.</summary>
        private async Task<FirmaIsiDto> TanimAsync(FirmaIsi isi, CancellationToken ct)
        {
            var adlar = new Dictionary<int, string>();
            if (isi.OnAdimiOlduguIsId is { } hedef)
            {
                var ad = await _db.FirmaIsleri.AsNoTracking().Where(i => i.Id == hedef).Select(i => i.Baslik).FirstOrDefaultAsync(ct);
                if (ad is not null) adlar[hedef] = ad;
            }

            var sistemAdlari = new Dictionary<int, string>();
            if (isi.SistemId is { } sistemId)
            {
                var ad = await _db.Sistemler.AsNoTracking().Where(s => s.Id == sistemId).Select(s => s.Ad).FirstOrDefaultAsync(ct);
                if (ad is not null) sistemAdlari[sistemId] = ad;
            }

            var dto = YapilacaklarKurucu.Tanim(isi, adlar, sistemAdlari);
            dto.EkSayisi = await _db.FirmaIsiEkleri.CountAsync(e => e.FirmaIsiId == isi.Id, ct);
            return dto;
        }

        private static string? Bos(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static void Dogrula(FirmaIsiKaydetDto dto)
        {
            var baslik = dto.Baslik?.Trim() ?? string.Empty;
            if (baslik.Length == 0)
                throw new YapilacaklarKuralException(nameof(dto.Baslik), "Başlık boş olamaz.");
            if (baslik.Length > 200)
                throw new YapilacaklarKuralException(nameof(dto.Baslik), "Başlık en çok 200 karakter olabilir.");
            if (dto.Aciklama is { Length: > 1000 })
                throw new YapilacaklarKuralException(nameof(dto.Aciklama), "Açıklama en çok 1000 karakter olabilir.");

            if (!Enum.IsDefined(dto.Tekrar))
                throw new YapilacaklarKuralException(nameof(dto.Tekrar), "Geçersiz tekrar.");

            if (dto.Tekrar == IsTekrari.TekSefer)
            {
                if (dto.TekSeferTarih is null)
                    throw new YapilacaklarKuralException(nameof(dto.TekSeferTarih), "Tek seferlik işte tarih zorunlu.");
                return;
            }

            // Prompt 17: geçmiş ve gelecek ay serbest; yalnız biçim denetlenir.
            IlkDonemDogrula(dto.IlkDonem);

            if (!Enum.IsDefined(dto.GunKurali))
                throw new YapilacaklarKuralException(nameof(dto.GunKurali), "Geçersiz gün kuralı.");

            if (dto.GunKurali == IsGunKurali.AyinGunu && dto.AyinGunu is not (>= 1 and <= 31))
                throw new YapilacaklarKuralException(nameof(dto.AyinGunu), "Ayın günü 1 ile 31 arasında olmalı.");

            if (dto.SorumluKullaniciId is <= 0)
                throw new YapilacaklarKuralException(nameof(dto.SorumluKullaniciId), "Geçersiz sorumlu.");
        }

        private static void IlkDonemDogrula(string? ilkDonem)
        {
            if (YapilacaklarKurucu.IlkDonemNormalize(ilkDonem, IsTekrari.Aylik, DateTime.UtcNow).Gecersiz)
                throw new YapilacaklarKuralException("IlkDonem", "Başlangıç dönemi geçersiz (beklenen: 2026-09).");
        }

        /// <summary>Kurala uymayan alanlar boşaltılır: tek seferlikte gün kuralı, ayın günü dışında gün sayısı.</summary>
        private static void Uygula(FirmaIsi isi, FirmaIsiKaydetDto dto)
        {
            isi.Baslik = dto.Baslik.Trim();
            isi.Aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? null : dto.Aciklama.Trim();
            isi.Tekrar = dto.Tekrar;
            isi.GunKurali = dto.Tekrar == IsTekrari.TekSefer ? IsGunKurali.AyinGunu : dto.GunKurali;
            isi.AyinGunu = dto.Tekrar != IsTekrari.TekSefer && dto.GunKurali == IsGunKurali.AyinGunu ? dto.AyinGunu : null;
            isi.TekSeferTarih = dto.Tekrar == IsTekrari.TekSefer ? dto.TekSeferTarih!.Value.Date : null;
            isi.IlkDonem = YapilacaklarKurucu.IlkDonemNormalize(dto.IlkDonem, dto.Tekrar, isi.OlusturmaZamani).Deger;
            isi.SorumluKullaniciId = dto.SorumluKullaniciId;
            isi.SorumluKullaniciAdi = dto.SorumluKullaniciId is null ? null
                : string.IsNullOrWhiteSpace(dto.SorumluKullaniciAdi) ? null
                : dto.SorumluKullaniciAdi.Trim() is { Length: > 100 } uzun ? uzun[..100] : dto.SorumluKullaniciAdi.Trim();
            isi.Aktif = dto.Aktif;
        }

        private async Task FirmaVarAsync(int firmaId, CancellationToken ct)
        {
            if (!await _db.Firmalar.AnyAsync(f => f.Id == firmaId, ct))
                throw new YapilacaklarKuralException("firmaId", "Firma bulunamadı.");
        }

        private Task OlayAsync(int firmaId, FirmaOlayTipi tip, string aciklama, CancellationToken ct)
            => _olay?.YazAsync(firmaId, tip, aciklama, ct) ?? Task.CompletedTask;

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

        /// <summary>"Bende" filtresi: JWT <c>sub</c> = IdentityService kullanıcı Id'si (sayısal).</summary>
        private int? BenimId() => int.TryParse(KullaniciId(), out var id) ? id : null;
    }
}
