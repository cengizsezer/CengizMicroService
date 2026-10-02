using CatalogService.Api.Features.Anasayfa.Domain;
using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.Sistemler.Domain;
using CatalogService.Api.Features.Sistemler.Dtos;
using CatalogService.Api.Infrastructure.Auth;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Sistemler.Services
{
    /// <summary>İş kuralı ihlali; controller 400'e çevirir.</summary>
    public class SistemKuralException : Exception
    {
        public string Field { get; }

        public SistemKuralException(string field, string message) : base(message) => Field = field;
    }

    /// <summary>Benzer adlı kayıt var ve ısrar edilmedi; controller 409'a çevirir.</summary>
    public class SistemBenzerException : Exception
    {
        public SistemDto Benzer { get; }

        public SistemBenzerException(SistemDto benzer)
            : base($"{benzer.Ad} zaten listede, onu mu demek istediniz?") => Benzer = benzer;
    }

    public interface ISistemService
    {
        /// <param name="tur">Boşsa bütün türler.</param>
        /// <param name="pasiflerDahil">Açılır liste <c>false</c> ister: pasif kayıt yeni seçimde çıkmaz.</param>
        Task<List<SistemDto>> ListeAsync(SistemTuru? tur, bool pasiflerDahil, CancellationToken ct = default);

        /// <exception cref="SistemBenzerException">Benzer ad var ve <see cref="SistemEkleDto.Israr"/> yok.</exception>
        Task<SistemDto> EkleAsync(SistemEkleDto dto, CancellationToken ct = default);

        Task<SistemDto> GuncelleAsync(int id, SistemGuncelleDto dto, CancellationToken ct = default);

        /// <summary>Kaynağı hedefe taşır: firma atamaları ve iş programları güncellenir, kaynak silinir.</summary>
        Task<SistemBirlestirmeSonucuDto> BirlestirAsync(int kaynakId, int hedefId, CancellationToken ct = default);

        /// <summary>Kartın formu: bütün atamalar + ORKA kodu + mizan formatı + not.</summary>
        Task FirmaSistemleriKaydetAsync(int firmaId, FirmaSistemleriKaydetDto dto, CancellationToken ct = default);
    }

    /// <summary>
    /// Kullanılan sistemler: ortak liste ve firma atamaları.
    ///
    /// <b>Benzer ad uyarısı zorunlu.</b> Liste ortak olduğu için "DijitalPlanet",
    /// "Dijital Planet" ve "dijitalplanet" üç kayıt olursa filtreleme ve arama çöker; yeni
    /// kayıt ve yeniden adlandırma <see cref="SistemAdi.Benzer"/> ile karşılaştırılır,
    /// kullanıcıya sorulmadan geçilmez.
    /// </summary>
    public class SistemService : ISistemService
    {
        public const int AdEnFazla = 100;
        public const int FirmaKoduEnFazla = 50;
        public const int OrkaKoduEnFazla = 20;
        public const int NotEnFazla = 500;

        private readonly CatalogContext _db;
        private readonly IHttpCurrentUser? _kullanici;
        private readonly IFirmaOlayYazici? _olay;

        public SistemService(CatalogContext db, IHttpCurrentUser? kullanici = null, IFirmaOlayYazici? olay = null)
        {
            _db = db;
            _kullanici = kullanici;
            _olay = olay;
        }

        // ---- Liste ----

        public async Task<List<SistemDto>> ListeAsync(SistemTuru? tur, bool pasiflerDahil, CancellationToken ct = default)
        {
            var sorgu = _db.Sistemler.AsNoTracking();
            if (tur is { } t) sorgu = sorgu.Where(s => s.Tur == t);
            if (!pasiflerDahil) sorgu = sorgu.Where(s => s.Aktif);

            var sistemler = await sorgu.ToListAsync(ct);
            var idler = sistemler.Select(s => s.Id).ToList();

            var atamalar = await (from fs in _db.FirmaSistemleri.AsNoTracking()
                                  join f in _db.Firmalar.AsNoTracking() on fs.FirmaId equals f.Id
                                  where idler.Contains(fs.SistemId)
                                  select new { fs.SistemId, f.KisaAd, f.Unvan }).ToListAsync(ct);
            var isler = await _db.FirmaIsleri.AsNoTracking()
                .Where(i => i.SistemId != null && idler.Contains(i.SistemId.Value))
                .GroupBy(i => i.SistemId!.Value).Select(g => new { g.Key, Sayi = g.Count() }).ToListAsync(ct);

            var tr = StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true);
            return sistemler
                .OrderBy(s => s.Tur).ThenByDescending(s => s.Aktif).ThenBy(s => s.Ad, tr)
                .Select(s =>
                {
                    var firmalar = atamalar.Where(a => a.SistemId == s.Id)
                        .Select(a => string.IsNullOrWhiteSpace(a.KisaAd) ? a.Unvan : a.KisaAd)
                        .OrderBy(a => a, tr).ToList();
                    var dto = Dto(s);
                    dto.FirmaSayisi = firmalar.Count;
                    dto.Firmalar = firmalar;
                    dto.IsSayisi = isler.FirstOrDefault(i => i.Key == s.Id)?.Sayi ?? 0;
                    return dto;
                })
                .ToList();
        }

        // ---- Ekleme / güncelleme ----

        public async Task<SistemDto> EkleAsync(SistemEkleDto dto, CancellationToken ct = default)
        {
            var ad = AdDogrula(dto.Ad);
            if (!Enum.IsDefined(dto.Tur))
                throw new SistemKuralException(nameof(dto.Tur), "Geçersiz sistem türü.");

            await BenzerKontrolAsync(ad, dto.Tur, haricId: null, dto.Israr, ct);

            var sistem = new Sistem
            {
                Ad = ad,
                Tur = dto.Tur,
                OlusturanKullaniciId = KullaniciId(),
                OlusturmaZamani = DateTime.UtcNow
            };
            _db.Sistemler.Add(sistem);
            await _db.SaveChangesAsync(ct);
            return Dto(sistem);
        }

        public async Task<SistemDto> GuncelleAsync(int id, SistemGuncelleDto dto, CancellationToken ct = default)
        {
            var sistem = await _db.Sistemler.FirstOrDefaultAsync(s => s.Id == id, ct)
                         ?? throw new KeyNotFoundException("Sistem bulunamadı.");

            var ad = AdDogrula(dto.Ad);
            if (ad != sistem.Ad) await BenzerKontrolAsync(ad, sistem.Tur, haricId: id, dto.Israr, ct);

            sistem.Ad = ad;
            sistem.Aktif = dto.Aktif;
            await _db.SaveChangesAsync(ct);
            return Dto(sistem);
        }

        private static string AdDogrula(string? ad)
        {
            var temiz = string.Join(' ', (ad ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (temiz.Length == 0) throw new SistemKuralException("Ad", "Sistem adı boş olamaz.");
            if (temiz.Length > AdEnFazla) throw new SistemKuralException("Ad", $"Sistem adı en çok {AdEnFazla} karakter olabilir.");
            return temiz;
        }

        /// <summary>
        /// Aynı türde benzer ad varsa: ısrar yoksa sorulur (409); ısrar olsa bile <b>birebir</b>
        /// aynı ad (büyük/küçük harf hariç) eklenmez — iki kaydı ayırt edecek bir şey kalmaz.
        /// Pasif kayıtlar da karşılaştırılır: pasif "DijitalPlanet"i bilmeden yenisi açılmasın.
        /// </summary>
        private async Task BenzerKontrolAsync(string ad, SistemTuru tur, int? haricId, bool israr, CancellationToken ct)
        {
            var ayniTur = await _db.Sistemler.AsNoTracking().Where(s => s.Tur == tur && s.Id != (haricId ?? 0)).ToListAsync(ct);

            var birebir = ayniTur.FirstOrDefault(s => string.Equals(s.Ad, ad, StringComparison.CurrentCultureIgnoreCase));
            if (birebir is not null)
                throw new SistemKuralException("Ad", $"{birebir.Ad} zaten listede{(birebir.Aktif ? "" : " (pasif)")}.");

            if (israr) return;

            var benzer = ayniTur.FirstOrDefault(s => SistemAdi.Benzer(s.Ad, ad));
            if (benzer is not null) throw new SistemBenzerException(Dto(benzer));
        }

        // ---- Birleştirme ----

        public async Task<SistemBirlestirmeSonucuDto> BirlestirAsync(int kaynakId, int hedefId, CancellationToken ct = default)
        {
            if (kaynakId == hedefId)
                throw new SistemKuralException(nameof(hedefId), "Bir kayıt kendisiyle birleştirilemez.");

            var kaynak = await _db.Sistemler.FirstOrDefaultAsync(s => s.Id == kaynakId, ct)
                         ?? throw new KeyNotFoundException("Birleştirilecek sistem bulunamadı.");
            var hedef = await _db.Sistemler.FirstOrDefaultAsync(s => s.Id == hedefId, ct)
                        ?? throw new KeyNotFoundException("Hedef sistem bulunamadı.");

            if (kaynak.Tur != hedef.Tur)
                throw new SistemKuralException(nameof(hedefId), "Yalnız aynı türdeki iki kayıt birleştirilebilir.");

            var sonuc = new SistemBirlestirmeSonucuDto();

            var kaynakAtamalari = await _db.FirmaSistemleri.Where(f => f.SistemId == kaynakId).ToListAsync(ct);
            var firmaIdleri = kaynakAtamalari.Select(a => a.FirmaId).ToList();
            var hedefAtamalari = await _db.FirmaSistemleri
                .Where(f => f.SistemId == hedefId && firmaIdleri.Contains(f.FirmaId)).ToListAsync(ct);

            foreach (var atama in kaynakAtamalari)
            {
                var mevcut = hedefAtamalari.FirstOrDefault(h => h.FirmaId == atama.FirmaId);
                if (mevcut is null)
                {
                    atama.SistemId = hedefId;
                    sonuc.TasinanAtama++;
                    continue;
                }

                // Firma iki kaydı da kullanıyordu: tek atama kalır, firma kodu kaybolmaz.
                mevcut.FirmaKodu ??= atama.FirmaKodu;
                _db.FirmaSistemleri.Remove(atama);
                sonuc.BirlesenAtama++;
            }

            var isler = await _db.FirmaIsleri.Where(i => i.SistemId == kaynakId).ToListAsync(ct);
            foreach (var isi in isler) isi.SistemId = hedefId;
            sonuc.TasinanIs = isler.Count;

            // Pasif hedefe birleştirmek atamaları "pasif sisteme" düşürür; kaynak aktifse hedef aktifleşir.
            if (kaynak.Aktif) hedef.Aktif = true;

            await _db.SaveChangesAsync(ct);

            _db.Sistemler.Remove(kaynak);
            await _db.SaveChangesAsync(ct);

            return sonuc;
        }

        // ---- Firma atamaları ----

        /// <summary>Panelin "Kullanılan sistemler" kartı için firmanın atamaları, sırayla.</summary>
        public static async Task<List<FirmaSistemAtamasiDto>> FirmaAtamalariAsync(CatalogContext db, int firmaId,
                                                                                  CancellationToken ct = default)
            => await (from fs in db.FirmaSistemleri.AsNoTracking()
                      join s in db.Sistemler.AsNoTracking() on fs.SistemId equals s.Id
                      where fs.FirmaId == firmaId
                      orderby fs.Sira, fs.Id
                      select new FirmaSistemAtamasiDto
                      {
                          SistemId = s.Id,
                          Ad = s.Ad,
                          Tur = s.Tur,
                          Aktif = s.Aktif,
                          FirmaKodu = fs.FirmaKodu,
                          Sira = fs.Sira
                      }).ToListAsync(ct);

        public async Task FirmaSistemleriKaydetAsync(int firmaId, FirmaSistemleriKaydetDto dto, CancellationToken ct = default)
        {
            var firma = await _db.Firmalar.FirstOrDefaultAsync(f => f.Id == firmaId, ct)
                        ?? throw new SistemKuralException("firmaId", "Firma bulunamadı.");

            var istek = dto.Atamalar ?? new();
            if (istek.GroupBy(a => a.SistemId).Any(g => g.Count() > 1))
                throw new SistemKuralException(nameof(dto.Atamalar), "Aynı sistem firmaya iki kez atanamaz.");
            if (istek.Any(a => a.FirmaKodu is { Length: > FirmaKoduEnFazla }))
                throw new SistemKuralException(nameof(dto.Atamalar), $"Firma kodu en çok {FirmaKoduEnFazla} karakter olabilir.");

            var orka = Bos(dto.OrkaFirmaKodu);
            if (orka is { Length: > OrkaKoduEnFazla })
                throw new SistemKuralException(nameof(dto.OrkaFirmaKodu), $"ORKA firma kodu en çok {OrkaKoduEnFazla} karakter olabilir.");

            var format = Bos(dto.MizanFormati);
            if (!MizanFormatlari.Gecerli(format))
                throw new SistemKuralException(nameof(dto.MizanFormati), "Mizan formatı listede yok.");

            var not = Bos(dto.SistemNotu);
            if (not is { Length: > NotEnFazla })
                throw new SistemKuralException(nameof(dto.SistemNotu), $"Sistem notu en çok {NotEnFazla} karakter olabilir.");

            var mevcut = await _db.FirmaSistemleri.Where(f => f.FirmaId == firmaId).ToListAsync(ct);

            // Yeni atanan sistem aktif olmalı; zaten atanmış pasif sistem kalabilir.
            var istenenIdler = istek.Select(a => a.SistemId).ToList();
            var sistemler = await _db.Sistemler.AsNoTracking().Where(s => istenenIdler.Contains(s.Id)).ToListAsync(ct);
            foreach (var a in istek)
            {
                var sistem = sistemler.FirstOrDefault(s => s.Id == a.SistemId)
                             ?? throw new SistemKuralException(nameof(dto.Atamalar), "Sistem bulunamadı.");
                if (!sistem.Aktif && mevcut.All(m => m.SistemId != a.SistemId))
                    throw new SistemKuralException(nameof(dto.Atamalar), $"{sistem.Ad} pasife alınmış; yeni atanamaz.");
            }

            var degisti = false;

            // Gönderilmeyen atama silinir; kalanların kodu ve sırası güncellenir.
            foreach (var eski in mevcut.Where(m => !istenenIdler.Contains(m.SistemId)))
            {
                _db.FirmaSistemleri.Remove(eski);
                degisti = true;
            }

            for (var i = 0; i < istek.Count; i++)
            {
                var a = istek[i];
                var kod = Bos(a.FirmaKodu);
                var kayit = mevcut.FirstOrDefault(m => m.SistemId == a.SistemId);
                if (kayit is null)
                {
                    _db.FirmaSistemleri.Add(new FirmaSistemi { FirmaId = firmaId, SistemId = a.SistemId, FirmaKodu = kod, Sira = i + 1 });
                    degisti = true;
                    continue;
                }

                if (kayit.FirmaKodu != kod || kayit.Sira != i + 1)
                {
                    kayit.FirmaKodu = kod;
                    kayit.Sira = i + 1;
                    degisti = true;
                }
            }

            // Firma kaydının KENDİ alanları: ORKA kodu (aktarım yükü okur) ve mizan formatı taşınmadı.
            firma.OrkaFirmaKodu = orka;
            firma.MizanFormati = format;
            firma.SistemNotu = not;
            if (_db.Entry(firma).Properties.Any(p => p.IsModified))
            {
                firma.UpdatedAt = DateTime.UtcNow;
                degisti = true;
            }

            if (!degisti) return;

            await _db.SaveChangesAsync(ct);

            if (_olay is not null)
                await _olay.YazAsync(firmaId, FirmaOlayTipi.SiniflandirmaGuncellendi, "Kullanılan sistemler güncellendi", ct);
        }

        // ---- Yardımcılar ----

        private static SistemDto Dto(Sistem s) => new() { Id = s.Id, Ad = s.Ad, Tur = s.Tur, Aktif = s.Aktif };

        private static string? Bos(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

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
