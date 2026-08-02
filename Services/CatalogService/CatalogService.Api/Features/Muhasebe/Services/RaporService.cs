using CatalogService.Api.Features.Muhasebe.Domain;
using CatalogService.Api.Features.Muhasebe.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Muhasebe.Services
{
    /// <inheritdoc cref="IRaporService"/>
    public class RaporService : IRaporService
    {
        private readonly CatalogContext _db;

        public RaporService(CatalogContext db) => _db = db;

        /// <summary>Bir hesabın kendi hareket toplamı; alt ağaç dâhil değildir.</summary>
        private sealed class HesapToplam
        {
            public int HesapId { get; init; }
            public decimal Borc { get; init; }
            public decimal Alacak { get; init; }
        }

        // ---- Mizan ----

        public async Task<MizanDto> GetMizanAsync(RaporFiltreDto filtre, byte? seviye, CancellationToken ct = default)
        {
            var (bas, bit) = Aralik(filtre);

            var hesaplar = await _db.HesapPlanlari
                .AsNoTracking()
                .OrderBy(h => h.KodDuz)
                .ToListAsync(ct);

            // Kural 18: bakiye saklanmaz, her istekte FisSatir'dan hesaplanır.
            // Kural 21: mizana yalnızca kesinleşmiş fişler girer.
            var kendiToplamlari = await HesapToplamlariAsync(bas, bit, FisDurum.Kesinlesmis, ct);

            // Kural 19: üst hesabın tutarları alt ağacının toplamıdır. Yol materialized path
            // olduğu için her hareket, hesabın kendisine ve Yol'daki tüm atalarına eklenir.
            var birikim = new Dictionary<int, (decimal Borc, decimal Alacak)>();
            var yolSozlugu = hesaplar.ToDictionary(h => h.Id, h => h.Yol);

            foreach (var t in kendiToplamlari)
            {
                Biriktir(birikim, t.HesapId, t.Borc, t.Alacak);

                if (!yolSozlugu.TryGetValue(t.HesapId, out var yol)) continue;
                foreach (var ataId in AtaIdler(yol))
                    Biriktir(birikim, ataId, t.Borc, t.Alacak);
            }

            // Yaprak = altında hesap olmayan düğüm; tüm ağaç üzerinden belirlenir, mizana
            // giren satırlardan değil (çocuğun hareketi olmayabilir).
            var ustIdler = hesaplar.Where(h => h.UstHesapId is not null)
                                   .Select(h => h.UstHesapId!.Value)
                                   .ToHashSet();

            var satirlar = new List<MizanSatirDto>();

            foreach (var h in hesaplar)
            {
                // Hareketsiz hesaplar mizana yazılmaz; alt ağacında hareket olan üstler yazılır.
                if (!birikim.TryGetValue(h.Id, out var toplam)) continue;
                if (seviye is byte enDerin && h.Seviye > enDerin) continue;

                satirlar.Add(new MizanSatirDto
                {
                    HesapId = h.Id,
                    UstHesapId = h.UstHesapId,
                    Kod = h.Kod,
                    Ad = h.Ad,
                    Seviye = h.Seviye,
                    HesapTuru = h.HesapTuru,
                    Karakter = h.Karakter,
                    HareketGorur = h.HareketGorur,
                    // Kural 8: pasif hesap geçmiş raporlarda görünmeye devam eder.
                    Aktif = h.Aktif,
                    YaprakMi = !ustIdler.Contains(h.Id),
                    ToplamBorc = toplam.Borc,
                    ToplamAlacak = toplam.Alacak,
                    BorcBakiye = BakiyeKurali.BorcBakiye(toplam.Borc, toplam.Alacak),
                    AlacakBakiye = BakiyeKurali.AlacakBakiye(toplam.Borc, toplam.Alacak),
                    Bakiye = BakiyeKurali.Bakiye(h.Karakter, toplam.Borc, toplam.Alacak),
                    Yon = BakiyeKurali.Yon(toplam.Borc, toplam.Alacak)
                });
            }

            return new MizanDto
            {
                Bas = bas,
                Bit = bit,
                Seviye = seviye,
                Satirlar = satirlar,
                // Genel toplam hareket gören hesaplar üzerinden alınır; satırlar alt ağaç
                // toplamı taşıdığı için satırların toplanması mükerrer sayım olurdu.
                GenelToplam = new MizanToplamDto
                {
                    ToplamBorc = kendiToplamlari.Sum(t => t.Borc),
                    ToplamAlacak = kendiToplamlari.Sum(t => t.Alacak),
                    BorcBakiye = kendiToplamlari.Sum(t => BakiyeKurali.BorcBakiye(t.Borc, t.Alacak)),
                    AlacakBakiye = kendiToplamlari.Sum(t => BakiyeKurali.AlacakBakiye(t.Borc, t.Alacak))
                },
                Taslak = await TaslakOzetiAsync(bas, bit, null, ct)
            };
        }

        // ---- Ekstre (T cetveli) ----

        public async Task<EkstreDto?> GetEkstreAsync(int hesapId, RaporFiltreDto filtre, CancellationToken ct = default)
        {
            var hesap = await _db.HesapPlanlari.AsNoTracking().FirstOrDefaultAsync(h => h.Id == hesapId, ct);
            if (hesap is null) return null;

            var (bas, bit) = Aralik(filtre);
            var altAgacIdler = await AltAgacIdlerAsync(hesap, ct);

            var hareketler = await HareketlerAsync(bas, bit, FisDurum.Kesinlesmis, altAgacIdler, ct);
            var taslaklar = await HareketlerAsync(bas, bit, FisDurum.Taslak, altAgacIdler, ct);

            var borclar = hareketler.Where(h => h.Tutar > 0 && h.Borc).ToList();
            var alacaklar = hareketler.Where(h => h.Tutar > 0 && !h.Borc).ToList();

            var toplamBorc = borclar.Sum(h => h.Tutar);
            var toplamAlacak = alacaklar.Sum(h => h.Tutar);

            // Devir: dönem başından önceki kesinleşmiş hareketler. Aralık başlangıcı
            // verilmemişse rapor zaten tüm geçmişi kapsar, devir yoktur.
            var (devirBorc, devirAlacak) = bas is DateTime donemBasi
                ? await DevirAsync(donemBasi, altAgacIdler, ct)
                : (0m, 0m);

            var kapanisBorc = devirBorc + toplamBorc;
            var kapanisAlacak = devirAlacak + toplamAlacak;

            return new EkstreDto
            {
                HesapId = hesap.Id,
                Kod = hesap.Kod,
                Ad = hesap.Ad,
                Seviye = hesap.Seviye,
                HesapTuru = hesap.HesapTuru,
                Karakter = hesap.Karakter,
                HareketGorur = hesap.HareketGorur,
                Aktif = hesap.Aktif,
                Bas = bas,
                Bit = bit,
                BorcHareketleri = borclar.Select(ToEkstreSatir).ToList(),
                AlacakHareketleri = alacaklar.Select(ToEkstreSatir).ToList(),
                DevirBorc = devirBorc,
                DevirAlacak = devirAlacak,
                DevirBakiye = BakiyeKurali.Bakiye(hesap.Karakter, devirBorc, devirAlacak),
                ToplamBorc = toplamBorc,
                ToplamAlacak = toplamAlacak,
                KapanisBorc = kapanisBorc,
                KapanisAlacak = kapanisAlacak,
                // Kural 20: yön hesabın karakterine göre belirlenir. Kapanış bakiyesi
                // devir + dönem hareketleridir.
                Bakiye = BakiyeKurali.Bakiye(hesap.Karakter, kapanisBorc, kapanisAlacak),
                Yon = BakiyeKurali.Yon(kapanisBorc, kapanisAlacak),
                // Kural 21: taslaklar T cetveline karışmaz, ayrı listelenir.
                TaslakHareketler = taslaklar.Select(ToEkstreSatir).ToList(),
                Taslak = new TaslakOzetDto
                {
                    FisSayisi = taslaklar.Select(h => h.FisId).Distinct().Count(),
                    ToplamBorc = taslaklar.Where(h => h.Borc).Sum(h => h.Tutar),
                    ToplamAlacak = taslaklar.Where(h => !h.Borc).Sum(h => h.Tutar)
                }
            };
        }

        // ---- Masraf merkezi ----

        public async Task<MasrafMerkeziRaporDto> GetMasrafMerkeziAsync(RaporFiltreDto filtre, CancellationToken ct = default)
        {
            var (bas, bit) = Aralik(filtre);

            var kirilim = await (from s in SatirSorgusu(bas, bit, FisDurum.Kesinlesmis)
                                 group s by new { s.MasrafMerkeziId, s.HesapId } into g
                                 select new
                                 {
                                     g.Key.MasrafMerkeziId,
                                     g.Key.HesapId,
                                     Borc = g.Sum(x => x.Borc),
                                     Alacak = g.Sum(x => x.Alacak)
                                 })
                                .ToListAsync(ct);

            var hesapIdler = kirilim.Select(k => k.HesapId).Distinct().ToList();
            var hesaplar = await _db.HesapPlanlari
                .AsNoTracking()
                .Where(h => hesapIdler.Contains(h.Id))
                .ToDictionaryAsync(h => h.Id, ct);

            var merkezler = await _db.MasrafMerkezleri.AsNoTracking().ToDictionaryAsync(m => m.Id, ct);

            var satirlar = new List<MasrafMerkeziSatirDto>();
            MasrafMerkeziSatirDto? dagitilmamis = null;

            foreach (var grup in kirilim.GroupBy(k => k.MasrafMerkeziId))
            {
                var borc = grup.Sum(k => k.Borc);
                var alacak = grup.Sum(k => k.Alacak);

                var satir = new MasrafMerkeziSatirDto
                {
                    ToplamBorc = borc,
                    ToplamAlacak = alacak,
                    Bakiye = borc - alacak,
                    Hesaplar = grup
                        .Select(k => new MasrafMerkeziHesapDto
                        {
                            HesapId = k.HesapId,
                            Kod = hesaplar.TryGetValue(k.HesapId, out var h) ? h.Kod : string.Empty,
                            Ad = hesaplar.TryGetValue(k.HesapId, out var h2) ? h2.Ad : string.Empty,
                            Borc = k.Borc,
                            Alacak = k.Alacak
                        })
                        .OrderBy(h => h.Kod)
                        .ToList()
                };

                if (grup.Key is int mmId && merkezler.TryGetValue(mmId, out var merkez))
                {
                    satir.MasrafMerkeziId = merkez.Id;
                    satir.Kod = merkez.Kod;
                    satir.Ad = merkez.Ad;
                    satir.Aktif = merkez.Aktif;
                    satirlar.Add(satir);
                }
                else
                {
                    // Masraf merkezi seçilmemiş (ya da silinmiş) satırlar dağıtılmamış sayılır.
                    satir.Ad = "Dağıtılmamış";
                    satir.Aktif = true;
                    dagitilmamis = satir;
                }
            }

            return new MasrafMerkeziRaporDto
            {
                Bas = bas,
                Bit = bit,
                Satirlar = satirlar.OrderBy(s => s.Kod).ToList(),
                ToplamBorc = kirilim.Sum(k => k.Borc),
                ToplamAlacak = kirilim.Sum(k => k.Alacak),
                Dagitilmamis = dagitilmamis
            };
        }

        // ---- Sorgu yardımcıları ----

        /// <summary>
        /// Verilen durumdaki fişlerin satırları. Tenant izolasyonu <c>Fisler</c> üzerindeki
        /// query filter ile gelir; <c>FisSatirlar</c> her zaman fiş üzerinden okunur.
        /// </summary>
        private IQueryable<FisSatir> SatirSorgusu(DateTime? bas, DateTime? bit, FisDurum durum)
            => from s in _db.FisSatirlar.AsNoTracking()
               join f in _db.Fisler.AsNoTracking() on s.FisId equals f.Id
               where f.Durum == durum
                     && (bas == null || f.Tarih >= bas)
                     && (bit == null || f.Tarih <= bit)
               select s;

        private async Task<List<HesapToplam>> HesapToplamlariAsync(
            DateTime? bas, DateTime? bit, FisDurum durum, CancellationToken ct)
            => await (from s in SatirSorgusu(bas, bit, durum)
                      group s by s.HesapId into g
                      select new HesapToplam
                      {
                          HesapId = g.Key,
                          Borc = g.Sum(x => x.Borc),
                          Alacak = g.Sum(x => x.Alacak)
                      })
                     .ToListAsync(ct);

        /// <summary>Ekstre satırının ham hâli; borç/alacak kolonlarına ayrılmadan önce.</summary>
        private sealed class Hareket
        {
            public int FisId { get; init; }
            public string FisNo { get; init; } = string.Empty;
            public DateTime Tarih { get; init; }
            public FisTuru FisTuru { get; init; }
            public string? Aciklama { get; init; }
            public int HesapId { get; init; }
            public string HesapKod { get; init; } = string.Empty;
            public decimal Tutar { get; init; }
            public bool Borc { get; init; }
            public short SiraNo { get; init; }
        }

        private async Task<List<Hareket>> HareketlerAsync(
            DateTime? bas, DateTime? bit, FisDurum durum, List<int> hesapIdler, CancellationToken ct)
        {
            var ham = await (from s in _db.FisSatirlar.AsNoTracking()
                             join f in _db.Fisler.AsNoTracking() on s.FisId equals f.Id
                             join h in _db.HesapPlanlari.AsNoTracking() on s.HesapId equals h.Id
                             where f.Durum == durum && hesapIdler.Contains(s.HesapId)
                                   && (bas == null || f.Tarih >= bas)
                                   && (bit == null || f.Tarih <= bit)
                             select new
                             {
                                 f.Id,
                                 f.FisNo,
                                 f.Tarih,
                                 f.FisTuru,
                                 FisAciklama = f.Aciklama,
                                 SatirAciklama = s.Aciklama,
                                 s.HesapId,
                                 HesapKod = h.Kod,
                                 s.Borc,
                                 s.Alacak,
                                 s.SiraNo
                             })
                            .ToListAsync(ct);

            return ham
                .Select(x => new Hareket
                {
                    FisId = x.Id,
                    FisNo = x.FisNo,
                    Tarih = x.Tarih,
                    FisTuru = x.FisTuru,
                    Aciklama = string.IsNullOrWhiteSpace(x.SatirAciklama) ? x.FisAciklama : x.SatirAciklama,
                    HesapId = x.HesapId,
                    HesapKod = x.HesapKod,
                    Tutar = x.Borc > 0 ? x.Borc : x.Alacak,
                    Borc = x.Borc > 0,
                    SiraNo = x.SiraNo
                })
                .OrderBy(h => h.Tarih)
                .ThenBy(h => h.FisNo)
                .ThenBy(h => h.SiraNo)
                .ToList();
        }

        /// <summary>
        /// Dönem başından önceki kesinleşmiş hareketlerin toplamı (kural 21).
        /// Hesap listesi alt ağacı kapsar, dolayısıyla devir de alt ağaç toplamıdır (kural 19).
        /// </summary>
        private async Task<(decimal Borc, decimal Alacak)> DevirAsync(
            DateTime bas, List<int> hesapIdler, CancellationToken ct)
        {
            var toplam = await (from s in _db.FisSatirlar.AsNoTracking()
                                join f in _db.Fisler.AsNoTracking() on s.FisId equals f.Id
                                where f.Durum == FisDurum.Kesinlesmis
                                      && f.Tarih < bas
                                      && hesapIdler.Contains(s.HesapId)
                                group s by 1 into g
                                select new { Borc = g.Sum(x => x.Borc), Alacak = g.Sum(x => x.Alacak) })
                               .FirstOrDefaultAsync(ct);

            return toplam is null ? (0m, 0m) : (toplam.Borc, toplam.Alacak);
        }

        private async Task<TaslakOzetDto> TaslakOzetiAsync(
            DateTime? bas, DateTime? bit, List<int>? hesapIdler, CancellationToken ct)
        {
            var q = SatirSorgusu(bas, bit, FisDurum.Taslak);
            if (hesapIdler is not null) q = q.Where(s => hesapIdler.Contains(s.HesapId));

            var satirlar = await q.Select(s => new { s.FisId, s.Borc, s.Alacak }).ToListAsync(ct);

            return new TaslakOzetDto
            {
                FisSayisi = satirlar.Select(s => s.FisId).Distinct().Count(),
                ToplamBorc = satirlar.Sum(s => s.Borc),
                ToplamAlacak = satirlar.Sum(s => s.Alacak)
            };
        }

        /// <summary>
        /// Kural 19: hesabın kendisi + alt ağacı. Alt ağaç, materialized path üzerinde
        /// <c>Yol LIKE '{ust.Yol}{ust.Id}/%'</c> ile bulunur.
        /// </summary>
        private async Task<List<int>> AltAgacIdlerAsync(HesapPlani hesap, CancellationToken ct)
        {
            var altYol = $"{hesap.Yol}{hesap.Id}/";

            return await _db.HesapPlanlari
                .AsNoTracking()
                .Where(h => h.Id == hesap.Id || h.Yol.StartsWith(altYol))
                .Select(h => h.Id)
                .ToListAsync(ct);
        }

        // ---- Yardımcılar ----

        private static (DateTime? Bas, DateTime? Bit) Aralik(RaporFiltreDto? filtre)
            => (filtre?.Bas?.Date, filtre?.Bit?.Date);

        /// <summary>Materialized path'teki ata hesap Id'leri: "/1/2/" → 1, 2.</summary>
        private static IEnumerable<int> AtaIdler(string yol)
        {
            foreach (var parca in yol.Split('/', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(parca, out var id))
                    yield return id;
        }

        private static void Biriktir(Dictionary<int, (decimal Borc, decimal Alacak)> birikim,
                                     int hesapId, decimal borc, decimal alacak)
        {
            var mevcut = birikim.TryGetValue(hesapId, out var v) ? v : (0m, 0m);
            birikim[hesapId] = (mevcut.Item1 + borc, mevcut.Item2 + alacak);
        }

        private static EkstreSatirDto ToEkstreSatir(Hareket h) => new()
        {
            FisId = h.FisId,
            FisNo = h.FisNo,
            Tarih = h.Tarih,
            FisTuru = h.FisTuru,
            Aciklama = h.Aciklama,
            Tutar = h.Tutar,
            HesapId = h.HesapId,
            HesapKod = h.HesapKod
        };
    }
}
