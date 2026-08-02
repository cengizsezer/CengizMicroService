using CatalogService.Api.Features.FirmaKontrol.Domain;
using CatalogService.Api.Features.FirmaKontrol.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.FirmaKontrol.Services
{
    /// <inheritdoc cref="IVergiBeyannameService"/>
    public class VergiBeyannameService : IVergiBeyannameService
    {
        private const int KodUzunluk = 20;
        private const int AdUzunluk = 200;

        private readonly CatalogContext _db;

        public VergiBeyannameService(CatalogContext db) => _db = db;

        // ───────────────────────── Kalem katalogu ─────────────────────────

        public async Task<List<VergiKalemiDto>> GetKalemlerAsync(bool pasifDahil = false, CancellationToken ct = default)
        {
            var kalemler = await _db.VergiKalemleri
                .AsNoTracking()
                .Include(k => k.BagliIstisnaKalemi)
                .Where(k => pasifDahil || k.Aktif)
                .OrderBy(k => k.Grup)
                .ThenBy(k => k.SiraNo)
                .ThenBy(k => k.Kod)
                .ToListAsync(ct);

            return kalemler.Select(ToDto).ToList();
        }

        public async Task<VergiKalemiDto?> GetKalemAsync(int id, CancellationToken ct = default)
        {
            var kalem = await _db.VergiKalemleri
                .AsNoTracking()
                .Include(k => k.BagliIstisnaKalemi)
                .FirstOrDefaultAsync(k => k.Id == id, ct);

            return kalem is null ? null : ToDto(kalem);
        }

        public async Task<VergiKalemiDto> KalemEkleAsync(VergiKalemiYazDto dto, CancellationToken ct = default)
        {
            var kod = (dto.Kod ?? string.Empty).Trim();
            var ad = (dto.Ad ?? string.Empty).Trim();

            DogrulaKodAd(kod, ad);

            if (await _db.VergiKalemleri.AnyAsync(k => k.Kod == kod, ct))
                throw new VergiKuralException("kod", $"\"{kod}\" kodlu kalem zaten var. Farklı bir kod girin.");

            await DogrulaBagliIstisnaAsync(dto, kalemId: null, ct);

            var entity = new VergiKalemi
            {
                Kod = kod,
                Ad = ad,
                Grup = dto.Grup,
                AltGrup = Kirp(dto.AltGrup),
                KanunMaddesi = Kirp(dto.KanunMaddesi),
                Aciklama = Kirp(dto.Aciklama),
                Hatirlatma = Kirp(dto.Hatirlatma),
                OranBilgisi = Kirp(dto.OranBilgisi),
                UstSinirTuru = dto.UstSinirTuru,
                UstSinirDeger = dto.UstSinirDeger,
                DevredebilirMi = dto.DevredebilirMi,
                IstisnayaIliskinMi = dto.IstisnayaIliskinMi,
                BagliIstisnaKalemiId = dto.BagliIstisnaKalemiId,
                AsgariMatrahtanDuser = dto.AsgariMatrahtanDuser,
                MukellefiyetTuru = MukellefiyetTuru.KurumlarVergisi,
                SiraNo = dto.SiraNo,
                SistemKalemi = false,   // kullanıcının eklediği kalem
                Aktif = true
            };

            _db.VergiKalemleri.Add(entity);
            await _db.SaveChangesAsync(ct);

            return ToDto(entity);
        }

        public async Task<VergiKalemiDto?> KalemGuncelleAsync(int id, VergiKalemiYazDto dto, CancellationToken ct = default)
        {
            var entity = await _db.VergiKalemleri.FirstOrDefaultAsync(k => k.Id == id, ct);
            if (entity is null) return null;

            var ad = (dto.Ad ?? string.Empty).Trim();
            if (ad.Length == 0)
                throw new VergiKuralException("ad", "Kalem adı boş bırakılamaz.");
            if (ad.Length > AdUzunluk)
                throw new VergiKuralException("ad", $"Kalem adı en fazla {AdUzunluk} karakter olabilir.");

            // Sistem kaleminde kod ve grup kilitli (seed ile gelen beyanname yapısı bozulmasın).
            if (!entity.SistemKalemi)
            {
                var kod = (dto.Kod ?? string.Empty).Trim();
                DogrulaKodAd(kod, ad);

                if (!string.Equals(kod, entity.Kod, StringComparison.OrdinalIgnoreCase) &&
                    await _db.VergiKalemleri.AnyAsync(k => k.Kod == kod && k.Id != id, ct))
                    throw new VergiKuralException("kod", $"\"{kod}\" kodlu kalem zaten var. Farklı bir kod girin.");

                entity.Kod = kod;
                entity.Grup = dto.Grup;
            }

            await DogrulaBagliIstisnaAsync(dto, id, ct);

            entity.Ad = ad;
            entity.AltGrup = Kirp(dto.AltGrup);
            entity.KanunMaddesi = Kirp(dto.KanunMaddesi);
            entity.Aciklama = Kirp(dto.Aciklama);
            entity.Hatirlatma = Kirp(dto.Hatirlatma);
            entity.OranBilgisi = Kirp(dto.OranBilgisi);
            entity.UstSinirTuru = dto.UstSinirTuru;
            entity.UstSinirDeger = dto.UstSinirDeger;
            entity.DevredebilirMi = dto.DevredebilirMi;
            entity.IstisnayaIliskinMi = dto.IstisnayaIliskinMi;
            entity.BagliIstisnaKalemiId = dto.BagliIstisnaKalemiId;
            entity.AsgariMatrahtanDuser = dto.AsgariMatrahtanDuser;
            entity.SiraNo = dto.SiraNo;

            await _db.SaveChangesAsync(ct);
            return ToDto(entity);
        }

        public async Task<VergiKalemiDto?> KalemPasifeAlAsync(int id, CancellationToken ct = default)
        {
            var entity = await _db.VergiKalemleri.FirstOrDefaultAsync(k => k.Id == id, ct);
            if (entity is null) return null;

            if (entity.Aktif)
            {
                entity.Aktif = false;
                await _db.SaveChangesAsync(ct);
            }

            return ToDto(entity);
        }

        public async Task<KalemSilmeSonuc> KalemSilAsync(int id, CancellationToken ct = default)
        {
            var entity = await _db.VergiKalemleri.FirstOrDefaultAsync(k => k.Id == id, ct);
            if (entity is null) return KalemSilmeSonuc.Bulunamadi;

            if (entity.SistemKalemi) return KalemSilmeSonuc.SistemKalemi;

            if (await _db.VergiHesaplamaSatirlari.AnyAsync(s => s.VergiKalemiId == id, ct))
                return KalemSilmeSonuc.Kullanilmis;

            _db.VergiKalemleri.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return KalemSilmeSonuc.Silindi;
        }

        public async Task SiralamayiKaydetAsync(List<VergiKalemSiraDto> sira, CancellationToken ct = default)
        {
            if (sira.Count == 0) return;

            var idler = sira.Select(s => s.KalemId).ToList();
            var kalemler = await _db.VergiKalemleri.Where(k => idler.Contains(k.Id)).ToListAsync(ct);
            var indeks = sira.ToDictionary(s => s.KalemId, s => s.SiraNo);

            foreach (var k in kalemler)
                if (indeks.TryGetValue(k.Id, out var yeni))
                    k.SiraNo = yeni;

            await _db.SaveChangesAsync(ct);
        }

        // ───────────────────────── Beyanname ─────────────────────────

        public async Task<VergiBeyannameDto?> GetBeyannameAsync(int firmaId, short donemYil, CancellationToken ct = default)
        {
            var entity = await _db.VergiHesaplamalar
                .AsNoTracking()
                .Include(h => h.Satirlar).ThenInclude(s => s.VergiKalemi)
                .Include(h => h.GecmisYilZararlari)
                .FirstOrDefaultAsync(h => h.FirmaId == firmaId && h.DonemYil == donemYil, ct);

            if (entity is null) return null;

            var kalemler = await AktifVeKullanilanKalemlerAsync(entity.Satirlar.Select(s => s.VergiKalemiId), ct);
            return ToDto(entity, kalemler);
        }

        public async Task<VergiSonucDto> OnizleAsync(VergiBeyannameYazDto dto, CancellationToken ct = default)
        {
            var kalemler = await AktifVeKullanilanKalemlerAsync(dto.Satirlar.Select(s => s.VergiKalemiId), ct);
            return VergiHesaplamaMotoru.Hesapla(MotorGirdisi(dto, kalemler));
        }

        public async Task<VergiBeyannameDto> KaydetAsync(int firmaId, VergiBeyannameYazDto dto, CancellationToken ct = default)
        {
            if (!await _db.Firmalar.AnyAsync(f => f.Id == firmaId, ct))
                throw new KeyNotFoundException($"Firma bulunamadı: Id={firmaId}");

            if (dto.DonemYil <= 0)
                throw new VergiKuralException("donemYil", "Dönem yılı geçersiz.");

            if (dto.KvOrani <= 0 || dto.KvOrani > 100)
                throw new VergiKuralException("kvOrani", "Kurumlar vergisi oranı 0 ile 100 arasında olmalıdır.");

            if (dto.IndirimliOran is decimal io && (io < 0 || io > 100))
                throw new VergiKuralException("indirimliOran", "İndirimli oran 0 ile 100 arasında olmalıdır.");

            var entity = await _db.VergiHesaplamalar
                .Include(h => h.Satirlar)
                .Include(h => h.GecmisYilZararlari)
                .FirstOrDefaultAsync(h => h.FirmaId == firmaId && h.DonemYil == dto.DonemYil, ct);

            if (entity is null)
            {
                entity = new VergiHesaplama { FirmaId = firmaId, DonemYil = dto.DonemYil };
                _db.VergiHesaplamalar.Add(entity);
            }

            entity.TicariKar = dto.TicariKar;
            entity.KvOrani = dto.KvOrani;
            entity.IndirimliOran = dto.IndirimliOran;
            entity.IndirimliOranMatrahi = dto.IndirimliOranMatrahi;
            entity.AsgariKvHesapla = dto.AsgariKvHesapla;
            entity.Notlar = dto.Notlar;
            entity.GuncellemeT = DateTime.UtcNow;

            var kalemler = await AktifVeKullanilanKalemlerAsync(dto.Satirlar.Select(s => s.VergiKalemiId), ct);
            var gecerliKalemIdler = kalemler.Select(k => k.Id).ToHashSet();

            // Satırlar: sıfır tutarlı ve tanımsız kalemler saklanmaz.
            _db.VergiHesaplamaSatirlari.RemoveRange(entity.Satirlar);
            entity.Satirlar.Clear();

            foreach (var s in dto.Satirlar
                         .Where(s => gecerliKalemIdler.Contains(s.VergiKalemiId))
                         .Where(s => s.Tutar != 0 || !string.IsNullOrWhiteSpace(s.Aciklama))
                         .GroupBy(s => s.VergiKalemiId)
                         .Select(g => g.Last()))
            {
                entity.Satirlar.Add(new VergiHesaplamaSatir
                {
                    VergiKalemiId = s.VergiKalemiId,
                    Tutar = s.Tutar,
                    OncekiDonem = s.OncekiDonem,
                    Aciklama = s.Aciklama
                });
            }

            // Geçmiş yıl zararları: mahsup edilen tutar motordan gelir, kullanıcıdan değil.
            var sonuc = VergiHesaplamaMotoru.Hesapla(MotorGirdisi(dto, kalemler));
            var mahsupIndeks = sonuc.ZararMahsuplari.ToDictionary(z => z.ZararYili, z => z.MahsupEdilen);

            _db.GecmisYilZararlari.RemoveRange(entity.GecmisYilZararlari);
            entity.GecmisYilZararlari.Clear();

            foreach (var z in dto.GecmisYilZararlari.GroupBy(z => z.ZararYili).Select(g => g.Last()))
            {
                entity.GecmisYilZararlari.Add(new GecmisYilZarari
                {
                    ZararYili = z.ZararYili,
                    ZararTutari = Math.Abs(z.ZararTutari),
                    MahsupEdilen = mahsupIndeks.GetValueOrDefault(z.ZararYili)
                });
            }

            await _db.SaveChangesAsync(ct);

            return (await GetBeyannameAsync(firmaId, dto.DonemYil, ct))!;
        }

        public async Task<(byte[] Icerik, string DosyaAdi)?> ExcelAsync(int firmaId, short donemYil, CancellationToken ct = default)
        {
            var beyanname = await GetBeyannameAsync(firmaId, donemYil, ct);
            if (beyanname is null) return null;

            var firma = await _db.Firmalar
                .AsNoTracking()
                .Where(f => f.Id == firmaId)
                .Select(f => new { f.Unvan, f.KisaAd })
                .FirstOrDefaultAsync(ct);

            var unvan = firma?.Unvan is { Length: > 0 } u ? u : firma?.KisaAd ?? $"Firma {firmaId}";
            var icerik = VergiBeyannameExcel.Olustur(beyanname, unvan);

            var temizAd = new string((firma?.KisaAd ?? $"firma-{firmaId}")
                .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c).ToArray());

            return (icerik, $"kurumlar-vergisi-{temizAd}-{donemYil}.xlsx");
        }

        // ───────────────────────── Yardımcılar ─────────────────────────

        /// <summary>
        /// Aktif kalemler + geçmişte kullanılmış ama sonradan pasife alınmış kalemler.
        /// Pasif kalem yeni girişte seçilemez ama kayıtlı beyanname bozulmamalıdır.
        /// </summary>
        private async Task<List<VergiKalemi>> AktifVeKullanilanKalemlerAsync(IEnumerable<int> kullanilanIdler, CancellationToken ct)
        {
            var idler = kullanilanIdler.Distinct().ToList();

            return await _db.VergiKalemleri
                .AsNoTracking()
                .Where(k => k.Aktif || idler.Contains(k.Id))
                .ToListAsync(ct);
        }

        private static VergiHesaplamaMotoru.Girdi MotorGirdisi(VergiBeyannameYazDto dto, List<VergiKalemi> kalemler) => new()
        {
            DonemYil = dto.DonemYil,
            TicariKar = dto.TicariKar,
            KvOrani = dto.KvOrani,
            IndirimliOran = dto.IndirimliOran,
            IndirimliOranMatrahi = dto.IndirimliOranMatrahi,
            AsgariKvHesapla = dto.AsgariKvHesapla,
            Kalemler = kalemler,
            Satirlar = dto.Satirlar,
            GecmisYilZararlari = dto.GecmisYilZararlari
        };

        private static void DogrulaKodAd(string kod, string ad)
        {
            if (kod.Length == 0)
                throw new VergiKuralException("kod", "Kalem kodu boş bırakılamaz.");
            if (kod.Length > KodUzunluk)
                throw new VergiKuralException("kod", $"Kalem kodu en fazla {KodUzunluk} karakter olabilir.");
            if (ad.Length == 0)
                throw new VergiKuralException("ad", "Kalem adı boş bırakılamaz.");
            if (ad.Length > AdUzunluk)
                throw new VergiKuralException("ad", $"Kalem adı en fazla {AdUzunluk} karakter olabilir.");
        }

        /// <summary>Bağlı istisna yalnızca Grup 2 kalemi olabilir ve kalem kendine bağlanamaz.</summary>
        private async Task DogrulaBagliIstisnaAsync(VergiKalemiYazDto dto, int? kalemId, CancellationToken ct)
        {
            if (dto.BagliIstisnaKalemiId is not int bagliId) return;

            if (kalemId is int id && bagliId == id)
                throw new VergiKuralException("bagliIstisnaKalemiId", "Kalem kendisine bağlanamaz.");

            var bagli = await _db.VergiKalemleri.AsNoTracking().FirstOrDefaultAsync(k => k.Id == bagliId, ct);

            if (bagli is null)
                throw new VergiKuralException("bagliIstisnaKalemiId", "Seçilen bağlı istisna kalemi bulunamadı.");

            if (bagli.Grup != VergiKalemGrubu.ZararOlsaDahi)
                throw new VergiKuralException("bagliIstisnaKalemiId",
                    "İstisnaya ilişkin KKEG yalnızca 'zarar olsa dahi indirilecek' grubundaki bir istisnaya bağlanabilir.");
        }

        private static string? Kirp(string? deger) => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();

        private static VergiKalemiDto ToDto(VergiKalemi k) => new()
        {
            Id = k.Id,
            Kod = k.Kod,
            Ad = k.Ad,
            Grup = k.Grup,
            AltGrup = k.AltGrup,
            KanunMaddesi = k.KanunMaddesi,
            Aciklama = k.Aciklama,
            Hatirlatma = k.Hatirlatma,
            OranBilgisi = k.OranBilgisi,
            UstSinirTuru = k.UstSinirTuru,
            UstSinirDeger = k.UstSinirDeger,
            DevredebilirMi = k.DevredebilirMi,
            IstisnayaIliskinMi = k.IstisnayaIliskinMi,
            BagliIstisnaKalemiId = k.BagliIstisnaKalemiId,
            BagliIstisnaKod = k.BagliIstisnaKalemi?.Kod,
            AsgariMatrahtanDuser = k.AsgariMatrahtanDuser,
            MukellefiyetTuru = k.MukellefiyetTuru,
            SiraNo = k.SiraNo,
            SistemKalemi = k.SistemKalemi,
            Aktif = k.Aktif
        };

        private static VergiBeyannameDto ToDto(VergiHesaplama e, List<VergiKalemi> kalemler)
        {
            var yaz = new VergiBeyannameYazDto
            {
                DonemYil = e.DonemYil,
                TicariKar = e.TicariKar,
                KvOrani = e.KvOrani,
                IndirimliOran = e.IndirimliOran,
                IndirimliOranMatrahi = e.IndirimliOranMatrahi,
                AsgariKvHesapla = e.AsgariKvHesapla,
                Notlar = e.Notlar,
                Satirlar = e.Satirlar.Select(s => new VergiSatirYazDto
                {
                    VergiKalemiId = s.VergiKalemiId,
                    Tutar = s.Tutar,
                    OncekiDonem = s.OncekiDonem,
                    Aciklama = s.Aciklama
                }).ToList(),
                GecmisYilZararlari = e.GecmisYilZararlari.Select(z => new GecmisYilZarariYazDto
                {
                    ZararYili = z.ZararYili,
                    ZararTutari = z.ZararTutari
                }).ToList()
            };

            return new VergiBeyannameDto
            {
                Id = e.Id,
                FirmaId = e.FirmaId,
                DonemYil = e.DonemYil,
                TicariKar = e.TicariKar,
                KvOrani = e.KvOrani,
                IndirimliOran = e.IndirimliOran,
                IndirimliOranMatrahi = e.IndirimliOranMatrahi,
                AsgariKvHesapla = e.AsgariKvHesapla,
                Notlar = e.Notlar,
                GuncellemeT = e.GuncellemeT,
                Satirlar = e.Satirlar.Select(s => new VergiSatirDto
                {
                    VergiKalemiId = s.VergiKalemiId,
                    Kod = s.VergiKalemi?.Kod ?? string.Empty,
                    Tutar = s.Tutar,
                    OncekiDonem = s.OncekiDonem,
                    Aciklama = s.Aciklama
                }).ToList(),
                GecmisYilZararlari = e.GecmisYilZararlari.Select(z => new GecmisYilZarariDto
                {
                    ZararYili = z.ZararYili,
                    ZararTutari = z.ZararTutari,
                    MahsupEdilen = z.MahsupEdilen
                }).ToList(),
                Sonuc = VergiHesaplamaMotoru.Hesapla(MotorGirdisi(yaz, kalemler))
            };
        }
    }
}
