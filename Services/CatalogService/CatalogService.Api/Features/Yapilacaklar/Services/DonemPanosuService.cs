using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Infrastructure.Auth;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Yapilacaklar.Services
{
    public interface IDonemPanosuService
    {
        /// <summary>
        /// Ayın matrisi ve satır listesi. Ay verilmezse bir önceki ay (kapanan ay; içindeki ayın işi genelde
        /// bitmemiştir). <paramref name="firmaId"/> doluysa yalnız o firma — sayılar da yalnız onu sayar.
        /// </summary>
        Task<DonemPanosuDto> PanoAsync(int? yil, int? ay, CancellationToken ct = default, int? firmaId = null);

        /// <summary>
        /// Sekme şeridi ve Özet sheet: içinde bulunulan ay + önceki aylar (<see cref="DonemSekmeleri"/>).
        /// Her ayın sayıları o ayın matrisinden (<see cref="DonemPanosuKurucu"/>) gelir.
        /// </summary>
        Task<DonemOzetiDto> OzetAsync(CancellationToken ct = default);

        /// <summary>Hücre paneli: işaret, not, ekler ve geçen dönemin notu/eki.</summary>
        Task<DonemHucresiDto> HucreAsync(IsIsaretDto hucre, CancellationToken ct = default);

        /// <summary>
        /// "Bu döneme not". Yapılmamış dönemde de yazılır: kayıt yoksa zamanı BOŞ bir kayıt açılır
        /// (iş yapılmış sayılmaz). Not boşaltılır ve kayıtta başka bir şey kalmazsa kayıt silinir.
        /// </summary>
        Task NotKaydetAsync(DonemNotuDto dto, CancellationToken ct = default);

        /// <summary>Dönem eki; yapılmamış dönemde de eklenir (not gibi).</summary>
        Task<FirmaIsiEkiDto> EkEkleAsync(DonemEkiOlusturDto dto, CancellationToken ct = default);

        /// <returns>Silinen ekin FileApi Id'si (istemci oradan da siler).</returns>
        Task<int> EkSilAsync(int ekId, CancellationToken ct = default);

        /// <summary>
        /// "Bu dönem kaydını sil": işaret, not ve kanıtların tamamı. İşaret kaldırmak bunu YAPMAZ.
        /// </summary>
        /// <returns>Silinen eklerin FileApi Id'leri (istemci oradan da siler).</returns>
        Task<List<int>> KayitSilAsync(IsIsaretDto hucre, CancellationToken ct = default);
    }

    /// <summary>
    /// Dönem panosu. Matris <see cref="DonemPanosuKurucu"/>'da; işaretleme Yapılacaklar'ın
    /// <c>IsaretleAsync</c>'i (aynı tamamlama kaydı). Bu servis okur, dönem notu/ekini yazar ve
    /// kaydın tamamını siler. Not/kanıt yapılmamış dönemde de yazılır (zamanı boş kayıt).
    /// </summary>
    public class DonemPanosuService : IDonemPanosuService
    {
        private readonly CatalogContext _db;
        private readonly TimeProvider _saat;
        private readonly IYapilacaklarService _isler;
        private readonly IHttpCurrentUser? _kullanici;

        public DonemPanosuService(CatalogContext db, TimeProvider saat, IYapilacaklarService isler,
                                  IHttpCurrentUser? kullanici = null)
        {
            _db = db;
            _saat = saat;
            _isler = isler;
            _kullanici = kullanici;
        }

        public const int NotEnFazla = 2000;

        public async Task<DonemPanosuDto> PanoAsync(int? yil, int? ay, CancellationToken ct = default, int? firmaId = null)
        {
            var (y, a) = (yil, ay) switch
            {
                ({ } yy, { } aa) => (yy, aa),
                _ => VarsayilanDonem(_saat.GetLocalNow().Date)
            };
            if (a is < 1 or > 12 || y is < 2000 or > 2100)
                throw new YapilacaklarKuralException("ay", "Geçersiz dönem.");

            var (firmalar, takvim, ozel, tamamlar) = await GirdilerAsync(firmaId, ct);

            var tamamIdleri = tamamlar.Select(t => t.Id).ToList();
            var ekSayilari = await _db.IsTamamlamaEkleri.AsNoTracking()
                .Where(e => tamamIdleri.Contains(e.IsTamamlamaId))
                .GroupBy(e => e.IsTamamlamaId)
                .Select(g => new { g.Key, Sayi = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Sayi, ct);

            var sistemAdlari = await _db.Sistemler.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Ad, ct);

            var pano = DonemPanosuKurucu.Kur(y, a, firmalar, takvim, ozel, tamamlar, ekSayilari, sistemAdlari);
            pano.FirmaId = firmaId;
            return pano;
        }

        public async Task<DonemOzetiDto> OzetAsync(CancellationToken ct = default)
        {
            var (firmalar, takvim, ozel, tamamlar) = await GirdilerAsync(null, ct);
            return DonemOzetiKurucu.Kur(_saat.GetLocalNow().Date, firmalar, takvim, ozel, tamamlar);
        }

        /// <summary>
        /// Matrisin girdileri — pano ve özet AYNI okumayı kullanır. Pasif takvim satırı ve pasif iş
        /// de okunur: işaretliyse görünür (kurucunun kuralı).
        /// </summary>
        private async Task<(List<YapilacaklarKurucu.FirmaGirdisi> Firmalar, List<VergiTakvimi> Takvim,
                            List<FirmaIsi> Ozel, List<IsTamamlama> Tamamlar)> GirdilerAsync(int? firmaId, CancellationToken ct)
        {
            // Firma filtresi kurucudan ÖNCE uygulanır: matris, satırlar ve sayılar aynı kümeden çıkar.
            var firmalar = await YapilacaklarService.FirmaGirdileriAsync(_db, firmaId, ct);
            var idler = firmalar.Select(f => f.FirmaId).ToList();

            var takvim = await _db.VergiTakvimi.AsNoTracking().ToListAsync(ct);
            var ozel = await _db.FirmaIsleri.AsNoTracking().Include(i => i.Alicilar)
                .Where(i => idler.Contains(i.FirmaId)).ToListAsync(ct);
            var tamamlar = await _db.IsTamamlamalari.AsNoTracking().Where(t => idler.Contains(t.FirmaId)).ToListAsync(ct);
            return (firmalar, takvim, ozel, tamamlar);
        }

        /// <summary>Bugünün bir önceki ayı.</summary>
        public static (int Yil, int Ay) VarsayilanDonem(DateTime bugun)
        {
            var onceki = new IsDonemi(IsTekrari.Aylik, bugun.Year, bugun.Month).Onceki();
            return (onceki.Yil, onceki.No);
        }

        public async Task<DonemHucresiDto> HucreAsync(IsIsaretDto hucre, CancellationToken ct = default)
        {
            var anahtar = hucre.DonemAnahtari?.Trim() ?? string.Empty;
            await _isler.DonemDogrulaAsync(hucre, ct);

            var firma = await _db.Firmalar.AsNoTracking().FirstAsync(f => f.Id == hucre.FirmaId, ct);

            var dto = new DonemHucresiDto
            {
                FirmaId = hucre.FirmaId,
                FirmaAdi = Anasayfa.Services.FirmaPaneliKurucu.Ad(firma),
                KaynakTip = hucre.KaynakTip,
                KaynakId = hucre.KaynakId,
                DonemAnahtari = anahtar
            };

            // Başlık, dönem etiketi ve geçen dönemin kimliği (kaynak + anahtar).
            (int KaynakId, string Anahtar, string Etiket)? onceki = null;
            if (hucre.KaynakTip == IsKaynagi.Yasal)
            {
                var satir = await _db.VergiTakvimi.AsNoTracking().FirstAsync(t => t.Id == hucre.KaynakId, ct);
                var donem = new IsDonemi(satir.Tekrar, satir.Yil, satir.DonemNo);
                dto.Baslik = satir.Ad;
                dto.DonemEtiketi = donem.Etiket;
                dto.MukellefiyetKodu = satir.MukellefiyetKodu;
                dto.Tekrar = satir.Tekrar;
                dto.TarifAnahtari = IsTarifiKurucu.YasalAnahtar(satir.MukellefiyetKodu, satir.Tekrar);

                var o = donem.Onceki();
                var oncekiSatir = await _db.VergiTakvimi.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.MukellefiyetKodu == satir.MukellefiyetKodu && t.Tekrar == satir.Tekrar
                                              && t.Yil == o.Yil && t.DonemNo == o.No, ct);
                if (oncekiSatir is not null) onceki = (oncekiSatir.Id, o.Anahtar, o.Etiket);
            }
            else
            {
                var isi = await _db.FirmaIsleri.AsNoTracking().FirstAsync(i => i.Id == hucre.KaynakId, ct);
                dto.Baslik = isi.Baslik;
                dto.Tekrar = isi.Tekrar;
                dto.TarifAnahtari = IsTarifiKurucu.Anahtar(isi);
                if (IsDonemi.Coz(isi.Tekrar, anahtar) is { } donem)
                {
                    dto.DonemEtiketi = donem.Etiket;
                    var o = donem.Onceki();
                    onceki = (isi.Id, o.Anahtar, o.Etiket);   // tek seferlik işin geçen dönemi yok
                }
            }

            // Kayıt varsa not ve kanıt her durumda gösterilir; "yapıldı" yalnız zaman doluysa.
            var tamam = await TamamlamaAsync(hucre.KaynakTip, hucre.KaynakId, hucre.FirmaId, anahtar, ct);
            if (tamam is not null)
            {
                dto.KayitVar = true;
                dto.Yapildi = tamam.Yapildi;
                dto.TamamlanmaZamani = Utc(tamam.TamamlanmaZamani);
                dto.TamamlayanAdi = tamam.KullaniciAdi;
                dto.Not = tamam.Not;
                dto.Ekler = tamam.Ekler.OrderBy(e => e.YuklemeZamani).ThenBy(e => e.Id).Select(YapilacaklarKurucu.Ek).ToList();
            }

            // Geçen dönem: not ya da ek yoksa bölüm çıkmaz (yapılmamış dönemin notu da gösterilir —
            // "geçen ay neden gecikmişti" de işe yarar).
            if (onceki is { } g
                && await TamamlamaAsync(hucre.KaynakTip, g.KaynakId, hucre.FirmaId, g.Anahtar, ct) is { } gecen
                && (!string.IsNullOrWhiteSpace(gecen.Not) || gecen.Ekler.Count > 0))
            {
                dto.GecenDonem = new GecenDonemDto
                {
                    DonemAnahtari = g.Anahtar,
                    DonemEtiketi = g.Etiket,
                    Yapildi = gecen.Yapildi,
                    TamamlanmaZamani = Utc(gecen.TamamlanmaZamani),
                    TamamlayanAdi = gecen.KullaniciAdi,
                    Not = gecen.Not,
                    Ekler = gecen.Ekler.OrderBy(e => e.YuklemeZamani).ThenBy(e => e.Id).Select(YapilacaklarKurucu.Ek).ToList()
                };
            }

            return dto;
        }

        public async Task NotKaydetAsync(DonemNotuDto dto, CancellationToken ct = default)
        {
            var not = string.IsNullOrWhiteSpace(dto.Not) ? null : dto.Not.Replace("\r\n", "\n").Trim();
            if (not is { Length: > NotEnFazla })
                throw new YapilacaklarKuralException(nameof(dto.Not), $"Not en çok {NotEnFazla} karakter olabilir.");

            var tamam = await DonemKaydiAsync(dto, olustur: not is not null, ct);
            if (tamam is null || tamam.Not == not) return;

            tamam.Not = not;
            await BosKaydiTemizleAsync(tamam, ct);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<FirmaIsiEkiDto> EkEkleAsync(DonemEkiOlusturDto dto, CancellationToken ct = default)
        {
            // Prosedür ekiyle AYNI kural: türler ve Belgeler kartının boyut sınırı.
            var (ad, contentType) = YapilacaklarService.EkDogrula(dto.FileId, dto.DosyaAdi, dto.ContentType, dto.Boyut);

            var tamam = (await DonemKaydiAsync(dto, olustur: true, ct))!;
            if (tamam.Id == 0) await _db.SaveChangesAsync(ct);   // yeni kayıt: Id ek için gerekli

            var ek = new IsTamamlamaEki
            {
                IsTamamlamaId = tamam.Id,
                FileId = dto.FileId,
                DosyaAdi = ad,
                ContentType = contentType,
                Boyut = dto.Boyut,
                YuklemeZamani = _saat.GetUtcNow().UtcDateTime,
                YukleyenKullaniciId = KullaniciId(),
                YukleyenKullaniciAdi = _kullanici?.UserName is { Length: > 100 } k ? k[..100] : _kullanici?.UserName
            };
            _db.IsTamamlamaEkleri.Add(ek);
            await _db.SaveChangesAsync(ct);

            return YapilacaklarKurucu.Ek(ek);
        }

        public async Task<int> EkSilAsync(int ekId, CancellationToken ct = default)
        {
            var ek = await _db.IsTamamlamaEkleri.FirstOrDefaultAsync(e => e.Id == ekId, ct)
                     ?? throw new KeyNotFoundException("Ek bulunamadı.");

            _db.IsTamamlamaEkleri.Remove(ek);

            var tamam = await _db.IsTamamlamalari.FirstOrDefaultAsync(t => t.Id == ek.IsTamamlamaId, ct);
            if (tamam is not null) await BosKaydiTemizleAsync(tamam, ct, haricEkId: ek.Id);

            await _db.SaveChangesAsync(ct);
            return ek.FileId;
        }

        public async Task<List<int>> KayitSilAsync(IsIsaretDto hucre, CancellationToken ct = default)
        {
            var tamam = await DonemKaydiAsync(hucre, olustur: false, ct);
            if (tamam is null) return new List<int>();

            var ekler = await _db.IsTamamlamaEkleri.Where(e => e.IsTamamlamaId == tamam.Id).ToListAsync(ct);
            _db.IsTamamlamaEkleri.RemoveRange(ekler);   // InMemory cascade izlenmeyeni silmez; açıkça
            _db.IsTamamlamalari.Remove(tamam);
            await _db.SaveChangesAsync(ct);

            return ekler.Select(e => e.FileId).ToList();
        }

        /// <summary>
        /// Doğrulanmış firma-dönemin kaydı (izlenen). Yoksa ve <paramref name="olustur"/> ise zamanı
        /// BOŞ kayıt eklenir (kaydedilmez) — not/kanıt işi yapılmış saymaz.
        /// </summary>
        private async Task<IsTamamlama?> DonemKaydiAsync(IsIsaretDto hucre, bool olustur, CancellationToken ct)
        {
            await _isler.DonemDogrulaAsync(hucre, ct);
            var anahtar = hucre.DonemAnahtari?.Trim() ?? string.Empty;

            var kayit = await _db.IsTamamlamalari.FirstOrDefaultAsync(t => t.KaynakTip == hucre.KaynakTip && t.KaynakId == hucre.KaynakId
                                                                           && t.FirmaId == hucre.FirmaId && t.DonemAnahtari == anahtar, ct);
            if (kayit is not null || !olustur) return kayit;

            kayit = new IsTamamlama
            {
                KaynakTip = hucre.KaynakTip,
                KaynakId = hucre.KaynakId,
                FirmaId = hucre.FirmaId,
                DonemAnahtari = anahtar,
                TamamlanmaZamani = null
            };
            _db.IsTamamlamalari.Add(kayit);
            return kayit;
        }

        /// <summary>Yapılmamış, notsuz ve kanıtsız kalan kayıt anlamsızdır: silinir (henüz kaydedilmediyse eklenmez).</summary>
        private async Task BosKaydiTemizleAsync(IsTamamlama kayit, CancellationToken ct, int? haricEkId = null)
        {
            if (kayit.Yapildi || !string.IsNullOrWhiteSpace(kayit.Not)) return;
            if (kayit.Id != 0 && await _db.IsTamamlamaEkleri.AnyAsync(e => e.IsTamamlamaId == kayit.Id && e.Id != haricEkId, ct)) return;

            if (_db.Entry(kayit).State == EntityState.Added) _db.Entry(kayit).State = EntityState.Detached;
            else _db.IsTamamlamalari.Remove(kayit);
        }

        private static DateTime? Utc(DateTime? zaman) => zaman is { } z ? DateTime.SpecifyKind(z, DateTimeKind.Utc) : null;

        private Task<IsTamamlama?> TamamlamaAsync(IsKaynagi tip, int kaynakId, int firmaId, string anahtar, CancellationToken ct)
            => _db.IsTamamlamalari.AsNoTracking().Include(t => t.Ekler)
                  .FirstOrDefaultAsync(t => t.KaynakTip == tip && t.KaynakId == kaynakId && t.FirmaId == firmaId
                                            && t.DonemAnahtari == anahtar, ct);

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
