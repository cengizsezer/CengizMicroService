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

        Task<YapilacaklarDto> ListeAsync(IsFiltresi filtre, CancellationToken ct = default);
        Task<YapilacaklarOzetDto> OzetAsync(CancellationToken ct = default);

        /// <summary>Tek ya da çok firma-dönemi birlikte işaretler / işaretini kaldırır. Aynı durumdaki satır atlanır.</summary>
        Task IsaretleAsync(IsIsaretleDto dto, CancellationToken ct = default);

        Task<FirmaIsiDto> IsEkleAsync(int firmaId, FirmaIsiKaydetDto dto, CancellationToken ct = default);
        Task<FirmaIsiDto> IsGuncelleAsync(int firmaId, int isId, FirmaIsiKaydetDto dto, CancellationToken ct = default);
        Task IsSilAsync(int firmaId, int isId, CancellationToken ct = default);
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
            var tanimlar = await _db.FirmaIsleri.AsNoTracking().Where(i => i.FirmaId == firmaId).ToListAsync(ct);
            var kodlar = firmalar.FirstOrDefault(f => f.FirmaId == firmaId)?.Kodlar ?? Array.Empty<string>();

            return YapilacaklarKurucu.Kart(firmaId, satirlar, tanimlar, kodlar, takvimKodlari, Bugun);
        }

        public async Task<YapilacaklarDto> ListeAsync(IsFiltresi filtre, CancellationToken ct = default)
        {
            if (!Enum.IsDefined(filtre))
                throw new YapilacaklarKuralException(nameof(filtre), "Geçersiz filtre.");

            var (satirlar, _, _) = await SatirlarAsync(null, ct);
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

        /// <summary>Aktif firmaların (ya da tek firmanın) görünen iş satırları.</summary>
        private async Task<(List<IsSatiriDto> Satirlar, List<YapilacaklarKurucu.FirmaGirdisi> Firmalar, List<string> TakvimKodlari)>
            SatirlarAsync(int? firmaId, CancellationToken ct)
        {
            var firmaSorgu = _db.Firmalar.AsNoTracking().Where(f => f.Aktif);
            if (firmaId is not null) firmaSorgu = firmaSorgu.Where(f => f.Id == firmaId);
            var firmalar = await firmaSorgu.ToListAsync(ct);
            var idler = firmalar.Select(f => f.Id).ToList();

            var mukellefiyetler = await _db.FirmaSicilBilgileri.AsNoTracking()
                .Where(s => idler.Contains(s.FirmaId))
                .Select(s => new { s.FirmaId, s.MukellefiyetTurleri })
                .ToListAsync(ct);
            var metin = mukellefiyetler.GroupBy(m => m.FirmaId).ToDictionary(g => g.Key, g => g.First().MukellefiyetTurleri);

            var girdiler = firmalar.Select(f => new YapilacaklarKurucu.FirmaGirdisi(
                    f.Id, Anasayfa.Services.FirmaPaneliKurucu.Ad(f), f.SorumluKullaniciId, f.SorumluKullaniciAdi,
                    MukellefiyetSiniflandirici.Kodlar(metin.GetValueOrDefault(f.Id))))
                .ToList();

            var takvim = await _db.VergiTakvimi.AsNoTracking().Where(t => t.Aktif).ToListAsync(ct);
            var ozel = await _db.FirmaIsleri.AsNoTracking().Where(i => i.Aktif && idler.Contains(i.FirmaId)).ToListAsync(ct);
            var tamamlar = await _db.IsTamamlamalari.AsNoTracking().Where(t => idler.Contains(t.FirmaId)).ToListAsync(ct);

            var satirlar = YapilacaklarKurucu.Satirlar(Bugun, girdiler, takvim, ozel, tamamlar);

            // Tamamlayanın adı: satırda gösterilen dönemin kaydından.
            var tamamIndex = tamamlar.ToDictionary(t => (t.KaynakTip, t.KaynakId, t.FirmaId, t.DonemAnahtari));
            foreach (var s in satirlar.Where(s => s.Tamamlandi))
            {
                if (!tamamIndex.TryGetValue((s.KaynakTip, s.KaynakId, s.FirmaId, s.DonemAnahtari), out var t)) continue;
                s.TamamlanmaZamani = DateTime.SpecifyKind(t.TamamlanmaZamani, DateTimeKind.Utc);
                s.TamamlayanAdi = t.KullaniciAdi;
            }

            return (satirlar, girdiler, takvim.Select(t => t.MukellefiyetKodu).Distinct().ToList());
        }

        // ---- İşaretleme ----

        public async Task IsaretleAsync(IsIsaretleDto dto, CancellationToken ct = default)
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

            var degisen = new List<(IsIsaretDto Is, string Aciklama)>();
            foreach (var i in istekler)
            {
                var kayit = mevcut.FirstOrDefault(t => t.KaynakTip == i.KaynakTip && t.KaynakId == i.KaynakId
                                                       && t.FirmaId == i.FirmaId && t.DonemAnahtari == i.DonemAnahtari);
                var aciklama = aciklamalar[(i.KaynakTip, i.KaynakId, i.FirmaId, i.DonemAnahtari)];

                if (dto.Yapildi && kayit is null)
                {
                    _db.IsTamamlamalari.Add(new IsTamamlama
                    {
                        KaynakTip = i.KaynakTip,
                        KaynakId = i.KaynakId,
                        FirmaId = i.FirmaId,
                        DonemAnahtari = i.DonemAnahtari,
                        TamamlanmaZamani = _saat.GetUtcNow().UtcDateTime,
                        KullaniciId = KullaniciId(),
                        KullaniciAdi = _kullanici?.UserName
                    });
                    degisen.Add((i, aciklama));
                }
                else if (!dto.Yapildi && kayit is not null)
                {
                    // İşaret kaldırılınca satır silinir: tablo yalnız "yapıldı"yı tutar.
                    _db.IsTamamlamalari.Remove(kayit);
                    degisen.Add((i, aciklama));
                }
            }

            if (degisen.Count == 0) return;
            await _db.SaveChangesAsync(ct);

            if (_olay is null) return;
            foreach (var (i, aciklama) in degisen)
                await _olay.YazAsync(i.FirmaId, dto.Yapildi ? FirmaOlayTipi.IsYapildi : FirmaOlayTipi.IsIsaretiKaldirildi,
                                     (dto.Yapildi ? "Yapıldı: " : "İşaret kaldırıldı: ") + aciklama, ct);
        }

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
                    if (isi is null || isi.FirmaId != i.FirmaId)
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
            await FirmaVarAsync(firmaId, ct);

            var isi = new FirmaIsi
            {
                FirmaId = firmaId,
                OlusturanKullaniciId = KullaniciId(),
                OlusturmaZamani = _saat.GetUtcNow().UtcDateTime
            };
            Uygula(isi, dto);

            _db.FirmaIsleri.Add(isi);
            await _db.SaveChangesAsync(ct);

            await OlayAsync(firmaId, FirmaOlayTipi.IsEklendi, $"İş eklendi: {isi.Baslik}", ct);
            return YapilacaklarKurucu.Tanim(isi);
        }

        public async Task<FirmaIsiDto> IsGuncelleAsync(int firmaId, int isId, FirmaIsiKaydetDto dto,
                                                       CancellationToken ct = default)
        {
            Dogrula(dto);

            var isi = await _db.FirmaIsleri.FirstOrDefaultAsync(i => i.Id == isId && i.FirmaId == firmaId, ct)
                      ?? throw new KeyNotFoundException("İş bulunamadı.");

            Uygula(isi, dto);
            if (!_db.Entry(isi).Properties.Any(p => p.IsModified)) return YapilacaklarKurucu.Tanim(isi);

            await _db.SaveChangesAsync(ct);
            await OlayAsync(firmaId, FirmaOlayTipi.IsGuncellendi, $"İş güncellendi: {isi.Baslik}", ct);
            return YapilacaklarKurucu.Tanim(isi);
        }

        /// <summary>Tanım ve bütün tamamlamaları silinir (tamamlamada FK yok, servis temizler).</summary>
        public async Task IsSilAsync(int firmaId, int isId, CancellationToken ct = default)
        {
            var isi = await _db.FirmaIsleri.FirstOrDefaultAsync(i => i.Id == isId && i.FirmaId == firmaId, ct)
                      ?? throw new KeyNotFoundException("İş bulunamadı.");

            var tamamlar = await _db.IsTamamlamalari
                .Where(t => t.KaynakTip == IsKaynagi.Ozel && t.KaynakId == isId)
                .ToListAsync(ct);

            _db.IsTamamlamalari.RemoveRange(tamamlar);
            _db.FirmaIsleri.Remove(isi);
            await _db.SaveChangesAsync(ct);

            await OlayAsync(firmaId, FirmaOlayTipi.IsSilindi, $"İş silindi: {isi.Baslik}", ct);
        }

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

            if (!Enum.IsDefined(dto.GunKurali))
                throw new YapilacaklarKuralException(nameof(dto.GunKurali), "Geçersiz gün kuralı.");

            if (dto.GunKurali == IsGunKurali.AyinGunu && dto.AyinGunu is not (>= 1 and <= 31))
                throw new YapilacaklarKuralException(nameof(dto.AyinGunu), "Ayın günü 1 ile 31 arasında olmalı.");

            if (dto.SorumluKullaniciId is <= 0)
                throw new YapilacaklarKuralException(nameof(dto.SorumluKullaniciId), "Geçersiz sorumlu.");
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
