using CatalogService.Api.Features.MevzuatGrafigi.Domain;
using CatalogService.Api.Features.MevzuatGrafigi.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.MevzuatGrafigi.Services
{
    public class MevzuatGrafigiService : IMevzuatGrafigiService
    {
        // Sınırlar entity configuration'daki HasMaxLength ile aynı; aşım DB hatası yerine Türkçe 400 dönsün.
        public const int BaslikMax = 200;
        public const int MaddeMax = 100;
        public const int OzetMax = 1000;
        public const int NotMax = 300;
        public const int TuvalAdMax = 100;

        private readonly CatalogContext _db;

        public MevzuatGrafigiService(CatalogContext db) => _db = db;

        // ---- Okuma ----

        public async Task<MevzuatGrafigiDto> GetAsync(CancellationToken ct = default)
        {
            var kartlar = await _db.MevzuatKartlar.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
            var baglantilar = await _db.MevzuatBaglantilar.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
            var dislamalar = await _db.MevzuatDislamalar.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
            var tuvaller = await _db.MevzuatTuvaller.AsNoTracking()
                .Include(x => x.Kartlar)
                .OrderBy(x => x.Sira).ThenBy(x => x.Id)
                .ToListAsync(ct);
            var takipler = await _db.MevzuatSartTakipleri.AsNoTracking().ToListAsync(ct);

            return new MevzuatGrafigiDto
            {
                Kartlar = kartlar.Select(ToDto).ToList(),
                Baglantilar = baglantilar.Select(ToDto).ToList(),
                Dislamalar = dislamalar.Select(ToDto).ToList(),
                Tuvaller = tuvaller.Select(ToDto).ToList(),
                SartTakipleri = takipler.Select(ToDto).ToList(),
            };
        }

        // ---- Kart ----

        public async Task<MevzuatKartDto> CreateKartAsync(MevzuatKartSaveDto dto, CancellationToken ct = default)
        {
            KartAlanlariniDogrula(dto);

            var entity = new MevzuatKart
            {
                Tur              = dto.Tur,
                Baslik           = dto.Baslik.Trim(),
                Madde            = Bosalt(dto.Madde),
                Ozet             = Bosalt(dto.Ozet),
                OlusturmaTarihi  = DateTime.Now,
                GuncellemeTarihi = null,
            };

            _db.MevzuatKartlar.Add(entity);
            await _db.SaveChangesAsync(ct);
            return ToDto(entity);
        }

        public async Task<MevzuatKartDto?> UpdateKartAsync(int id, MevzuatKartSaveDto dto, CancellationToken ct = default)
        {
            KartAlanlariniDogrula(dto);

            var entity = await _db.MevzuatKartlar.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return null;

            if (entity.Tur != dto.Tur)
                await TurDegisikliginiDogrula(entity, dto.Tur, ct);

            entity.Tur              = dto.Tur;
            entity.Baslik           = dto.Baslik.Trim();
            entity.Madde            = Bosalt(dto.Madde);
            entity.Ozet             = Bosalt(dto.Ozet);
            entity.GuncellemeTarihi = DateTime.Now;

            await _db.SaveChangesAsync(ct);
            return ToDto(entity);
        }

        /// <summary>
        /// Kartı ve ona bağlı her şeyi (bağlantılar, dışlamalar, tuval yerleşimleri, şart
        /// takipleri) tek SaveChanges'ta siler. Bağlantı/dışlama/şart takip FK'leri Restrict
        /// (SQL Server aynı tabloya çift cascade yolunu kabul etmiyor), bu yüzden temizlik
        /// veritabanına bırakılmıyor, burada yapılıyor.
        /// </summary>
        public async Task<bool> DeleteKartAsync(int id, CancellationToken ct = default)
        {
            var entity = await _db.MevzuatKartlar.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return false;

            _db.MevzuatBaglantilar.RemoveRange(
                await _db.MevzuatBaglantilar.Where(x => x.KaynakKartId == id || x.HedefKartId == id).ToListAsync(ct));
            _db.MevzuatDislamalar.RemoveRange(
                await _db.MevzuatDislamalar.Where(x => x.AKartId == id || x.BKartId == id).ToListAsync(ct));
            _db.MevzuatTuvalKartlari.RemoveRange(
                await _db.MevzuatTuvalKartlari.Where(x => x.KartId == id).ToListAsync(ct));
            _db.MevzuatSartTakipleri.RemoveRange(
                await _db.MevzuatSartTakipleri.Where(x => x.FirmaKartId == id || x.SartKartId == id).ToListAsync(ct));
            _db.MevzuatKartlar.Remove(entity);

            await _db.SaveChangesAsync(ct);
            return true;
        }

        // ---- Bağlantı ----

        public async Task<MevzuatBaglantiDto> CreateBaglantiAsync(MevzuatBaglantiSaveDto dto, CancellationToken ct = default)
        {
            if (dto.KaynakKartId == dto.HedefKartId)
                throw MevzuatGrafigiHatasi.Kural("Kart kendine bağlanamaz.");

            var kaynak = await KartGetir(dto.KaynakKartId, "Kaynak kart", ct);
            var hedef = await KartGetir(dto.HedefKartId, "Hedef kart", ct);

            if (!BaglantiKurali.IzinliMi(kaynak.Tur, hedef.Tur))
                throw MevzuatGrafigiHatasi.Kural(BaglantiKurali.RetMesaji(kaynak.Tur, hedef.Tur));

            var varMi = await _db.MevzuatBaglantilar
                .AnyAsync(x => x.KaynakKartId == dto.KaynakKartId && x.HedefKartId == dto.HedefKartId, ct);
            if (varMi)
                throw MevzuatGrafigiHatasi.Kural("Bu bağlantı zaten var.");

            var entity = new MevzuatBaglanti { KaynakKartId = dto.KaynakKartId, HedefKartId = dto.HedefKartId };
            _db.MevzuatBaglantilar.Add(entity);
            await _db.SaveChangesAsync(ct);
            return ToDto(entity);
        }

        public async Task<bool> DeleteBaglantiAsync(int id, CancellationToken ct = default)
        {
            var entity = await _db.MevzuatBaglantilar.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return false;
            _db.MevzuatBaglantilar.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        // ---- Dışlama ----

        public async Task<MevzuatDislamaDto> CreateDislamaAsync(MevzuatDislamaSaveDto dto, CancellationToken ct = default)
        {
            if (dto.AKartId == dto.BKartId)
                throw MevzuatGrafigiHatasi.Kural("Kart kendini dışlayamaz.");
            if (dto.Not?.Trim().Length > NotMax)
                throw MevzuatGrafigiHatasi.Kural($"Not en fazla {NotMax} karakter olabilir.");

            var kartA = await KartGetir(dto.AKartId, "Kart", ct);
            var kartB = await KartGetir(dto.BKartId, "Kart", ct);

            if (!DislamaKurali.TesvikMi(kartA.Tur) || !DislamaKurali.TesvikMi(kartB.Tur))
                throw MevzuatGrafigiHatasi.Kural(DislamaKurali.RetMesaji(kartA.Tur, kartB.Tur));

            // Yönsüz: çift sıralı saklanır, B-A isteği A-B kaydıyla çakışır.
            var a = Math.Min(dto.AKartId, dto.BKartId);
            var b = Math.Max(dto.AKartId, dto.BKartId);

            if (await _db.MevzuatDislamalar.AnyAsync(x => x.AKartId == a && x.BKartId == b, ct))
                throw MevzuatGrafigiHatasi.Kural("Bu iki kart arasında dışlama zaten var.");

            var entity = new MevzuatDislama { AKartId = a, BKartId = b, Not = Bosalt(dto.Not) };
            _db.MevzuatDislamalar.Add(entity);
            await _db.SaveChangesAsync(ct);
            return ToDto(entity);
        }

        public async Task<bool> DeleteDislamaAsync(int id, CancellationToken ct = default)
        {
            var entity = await _db.MevzuatDislamalar.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return false;
            _db.MevzuatDislamalar.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        // ---- Tuval ----

        public async Task<MevzuatTuvalDto> CreateTuvalAsync(MevzuatTuvalSaveDto dto, CancellationToken ct = default)
        {
            TuvalAdiniDogrula(dto);

            var sira = dto.Sira
                ?? ((await _db.MevzuatTuvaller.MaxAsync(x => (int?)x.Sira, ct)) ?? 0) + 1;

            var entity = new MevzuatTuval { Ad = dto.Ad.Trim(), Sira = sira };
            _db.MevzuatTuvaller.Add(entity);
            await _db.SaveChangesAsync(ct);
            return ToDto(entity);
        }

        public async Task<MevzuatTuvalDto?> UpdateTuvalAsync(int id, MevzuatTuvalSaveDto dto, CancellationToken ct = default)
        {
            TuvalAdiniDogrula(dto);

            var entity = await _db.MevzuatTuvaller.Include(x => x.Kartlar).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null) return null;

            entity.Ad = dto.Ad.Trim();
            if (dto.Sira is int sira) entity.Sira = sira;

            await _db.SaveChangesAsync(ct);
            return ToDto(entity);
        }

        public async Task<MevzuatYerlesimDto?> TuvaleKartEkleAsync(int tuvalId, MevzuatYerlesimDto dto, CancellationToken ct = default)
        {
            if (!await _db.MevzuatTuvaller.AnyAsync(x => x.Id == tuvalId, ct)) return null;
            await KartGetir(dto.KartId, "Kart", ct);

            if (await _db.MevzuatTuvalKartlari.AnyAsync(x => x.TuvalId == tuvalId && x.KartId == dto.KartId, ct))
                throw MevzuatGrafigiHatasi.Kural("Kart bu tuvalde zaten var.");

            var entity = new MevzuatTuvalKarti { TuvalId = tuvalId, KartId = dto.KartId, X = dto.X, Y = dto.Y };
            _db.MevzuatTuvalKartlari.Add(entity);
            await _db.SaveChangesAsync(ct);
            return ToDto(entity);
        }

        /// <summary>Yalnız yerleşim satırını siler; kart kütüphanede ve diğer tuvallerde kalır.</summary>
        public async Task<bool> TuvaldenKartKaldirAsync(int tuvalId, int kartId, CancellationToken ct = default)
        {
            var entity = await _db.MevzuatTuvalKartlari
                .FirstOrDefaultAsync(x => x.TuvalId == tuvalId && x.KartId == kartId, ct);
            if (entity is null) return false;
            _db.MevzuatTuvalKartlari.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        /// <summary>
        /// Sürükleme sonrası toplu konum güncellemesi. Yalnız tuvalde zaten olan kartların
        /// yeri değişir; ekleme ayrı uçtan yapılır. Listede tuvalde olmayan kart varsa hiçbiri yazılmaz.
        /// </summary>
        public async Task<List<MevzuatYerlesimDto>?> YerlesimGuncelleAsync(int tuvalId, List<MevzuatYerlesimDto> liste, CancellationToken ct = default)
        {
            if (!await _db.MevzuatTuvaller.AnyAsync(x => x.Id == tuvalId, ct)) return null;

            liste ??= new();
            if (liste.Select(x => x.KartId).Distinct().Count() != liste.Count)
                throw MevzuatGrafigiHatasi.Kural("Listede aynı kart birden çok kez geçiyor.");

            var mevcut = await _db.MevzuatTuvalKartlari.Where(x => x.TuvalId == tuvalId).ToListAsync(ct);
            var kartaGore = mevcut.ToDictionary(x => x.KartId);

            var eksik = liste.Where(x => !kartaGore.ContainsKey(x.KartId)).Select(x => x.KartId).ToList();
            if (eksik.Count > 0)
                throw MevzuatGrafigiHatasi.Kural($"Şu kartlar bu tuvalde değil: {string.Join(", ", eksik)}.");

            foreach (var y in liste)
            {
                var satir = kartaGore[y.KartId];
                satir.X = y.X;
                satir.Y = y.Y;
            }

            await _db.SaveChangesAsync(ct);
            return liste.Select(y => ToDto(kartaGore[y.KartId])).ToList();
        }

        // ---- Şart takip ----

        public async Task<MevzuatSartTakipDto> SartTakipKaydetAsync(MevzuatSartTakipDto dto, CancellationToken ct = default)
        {
            var firma = await KartGetir(dto.FirmaKartId, "Firma kartı", ct);
            var sart = await KartGetir(dto.SartKartId, "Şart kartı", ct);

            if (firma.Tur != MevzuatKartTuru.Firma)
                throw MevzuatGrafigiHatasi.Kural("Şart takibi yalnız Firma kartı için tutulur.");
            if (sart.Tur != MevzuatKartTuru.Sart)
                throw MevzuatGrafigiHatasi.Kural("Takip edilen kart Şart türünde olmalı.");

            var entity = await _db.MevzuatSartTakipleri
                .FirstOrDefaultAsync(x => x.FirmaKartId == dto.FirmaKartId && x.SartKartId == dto.SartKartId, ct);
            if (entity is null)
            {
                entity = new MevzuatSartTakip { FirmaKartId = dto.FirmaKartId, SartKartId = dto.SartKartId };
                _db.MevzuatSartTakipleri.Add(entity);
            }
            entity.Tamamlandi = dto.Tamamlandi;

            await _db.SaveChangesAsync(ct);
            return ToDto(entity);
        }

        // ---- Yardımcılar ----

        private async Task<MevzuatKart> KartGetir(int id, string ad, CancellationToken ct)
            => await _db.MevzuatKartlar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
               ?? throw MevzuatGrafigiHatasi.Yok($"{ad} bulunamadı (#{id}).");

        /// <summary>
        /// Tür değişince mevcut bağlantılar, dışlamalar ve şart takipleri yeni türle de geçerli
        /// kalmalı; aksi halde grafikte kurala aykırı kayıtlar oluşurdu.
        /// </summary>
        private async Task TurDegisikliginiDogrula(MevzuatKart kart, MevzuatKartTuru yeniTur, CancellationToken ct)
        {
            var kenarlar = await _db.MevzuatBaglantilar.AsNoTracking()
                .Where(x => x.KaynakKartId == kart.Id || x.HedefKartId == kart.Id)
                .ToListAsync(ct);

            if (kenarlar.Count > 0)
            {
                var digerIdler = kenarlar.Select(x => x.KaynakKartId == kart.Id ? x.HedefKartId : x.KaynakKartId).Distinct().ToList();
                var turler = await _db.MevzuatKartlar.AsNoTracking()
                    .Where(x => digerIdler.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, x => x.Tur, ct);

                foreach (var e in kenarlar)
                {
                    var (k, h) = e.KaynakKartId == kart.Id
                        ? (yeniTur, turler[e.HedefKartId])
                        : (turler[e.KaynakKartId], yeniTur);
                    if (!BaglantiKurali.IzinliMi(k, h))
                        throw MevzuatGrafigiHatasi.Kural(
                            $"Kartın türü {BaglantiKurali.Etiket(yeniTur)} yapılamaz: mevcut bağlantıları sıra kuralına aykırı olur. " +
                            $"Önce bağlantıları kaldırın. Sıra: {BaglantiKurali.SiraMetni}.");
                }
            }

            if (!DislamaKurali.TesvikMi(yeniTur)
                && await _db.MevzuatDislamalar.AsNoTracking().AnyAsync(x => x.AKartId == kart.Id || x.BKartId == kart.Id, ct))
                throw MevzuatGrafigiHatasi.Kural(
                    $"Kartın türü {BaglantiKurali.Etiket(yeniTur)} yapılamaz: kartın dışlamaları var; " +
                    "dışlama yalnız İstisna ve İndirim kartları arasında olur. Önce dışlamaları kaldırın.");

            var takipVar = await _db.MevzuatSartTakipleri.AsNoTracking().AnyAsync(x =>
                (x.FirmaKartId == kart.Id && yeniTur != MevzuatKartTuru.Firma) ||
                (x.SartKartId == kart.Id && yeniTur != MevzuatKartTuru.Sart), ct);
            if (takipVar)
                throw MevzuatGrafigiHatasi.Kural(
                    $"Kartın türü {BaglantiKurali.Etiket(yeniTur)} yapılamaz: kartın şart takip kayıtları var.");
        }

        private static void KartAlanlariniDogrula(MevzuatKartSaveDto dto)
        {
            if (!Enum.IsDefined(dto.Tur))
                throw MevzuatGrafigiHatasi.Kural("Geçerli bir kart türü seçin.");
            if (string.IsNullOrWhiteSpace(dto.Baslik))
                throw MevzuatGrafigiHatasi.Kural("Kart adı zorunlu.");
            if (dto.Baslik.Trim().Length > BaslikMax)
                throw MevzuatGrafigiHatasi.Kural($"Kart adı en fazla {BaslikMax} karakter olabilir.");
            if (dto.Madde?.Trim().Length > MaddeMax)
                throw MevzuatGrafigiHatasi.Kural($"Madde / etiket en fazla {MaddeMax} karakter olabilir.");
            if (dto.Ozet?.Trim().Length > OzetMax)
                throw MevzuatGrafigiHatasi.Kural($"Özet en fazla {OzetMax} karakter olabilir.");
        }

        private static void TuvalAdiniDogrula(MevzuatTuvalSaveDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Ad))
                throw MevzuatGrafigiHatasi.Kural("Tuval adı zorunlu.");
            if (dto.Ad.Trim().Length > TuvalAdMax)
                throw MevzuatGrafigiHatasi.Kural($"Tuval adı en fazla {TuvalAdMax} karakter olabilir.");
        }

        private static string? Bosalt(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static MevzuatKartDto ToDto(MevzuatKart x) => new()
        {
            Id               = x.Id,
            Tur              = x.Tur,
            Baslik           = x.Baslik,
            Madde            = x.Madde,
            Ozet             = x.Ozet,
            OlusturmaTarihi  = x.OlusturmaTarihi,
            GuncellemeTarihi = x.GuncellemeTarihi,
        };

        private static MevzuatBaglantiDto ToDto(MevzuatBaglanti x) => new()
        {
            Id = x.Id, KaynakKartId = x.KaynakKartId, HedefKartId = x.HedefKartId,
        };

        private static MevzuatDislamaDto ToDto(MevzuatDislama x) => new()
        {
            Id = x.Id, AKartId = x.AKartId, BKartId = x.BKartId, Not = x.Not,
        };

        private static MevzuatTuvalDto ToDto(MevzuatTuval x) => new()
        {
            Id = x.Id,
            Ad = x.Ad,
            Sira = x.Sira,
            Yerlesimler = x.Kartlar.OrderBy(k => k.KartId).Select(ToDto).ToList(),
        };

        private static MevzuatYerlesimDto ToDto(MevzuatTuvalKarti x) => new()
        {
            KartId = x.KartId, X = x.X, Y = x.Y,
        };

        private static MevzuatSartTakipDto ToDto(MevzuatSartTakip x) => new()
        {
            FirmaKartId = x.FirmaKartId, SartKartId = x.SartKartId, Tamamlandi = x.Tamamlandi,
        };
    }
}
