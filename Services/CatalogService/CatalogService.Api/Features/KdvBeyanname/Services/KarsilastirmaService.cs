using CatalogService.Api.Features.KdvBeyanname.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.KdvBeyanname.Services
{
    public interface IKarsilastirmaService
    {
        Task<KarsilastirmaSonucuDto> KarsilastirAsync(
            int firmaId, string donem, CancellationToken ct);
    }

    public class KarsilastirmaService : IKarsilastirmaService
    {
        private readonly CatalogContext _db;

        public KarsilastirmaService(CatalogContext db)
        {
            _db = db;
        }

        public async Task<KarsilastirmaSonucuDto> KarsilastirAsync(
            int firmaId, string donem, CancellationToken ct)
        {
            ValidateDonem(donem, out var yil, out var ay);

            // Dönem aralığı: [ayın 1'i, sonraki ayın 1'i)
            var donemBaslangic = new DateTime(yil, ay, 1);
            var donemBitis = donemBaslangic.AddMonths(1);

            // Gelen Faturalar — fatura tarihine göre filtrele.
            // (RED'ler aşağıda elenir; Tab 1 endpoint'i tüm satırları döndürmeye devam ediyor.)
            var gelenAll = await _db.GelenFaturalar
                .AsNoTracking()
                .Where(g => g.FirmaId == firmaId
                            && g.FaturaTarihi >= donemBaslangic
                            && g.FaturaTarihi <  donemBitis)
                .ToListAsync(ct);

            // "RED" geçen tüm statüler elenir (FATURA RED, REDDEDİLDİ, vb.).
            // Statu null/boş ise eski kayıt → dahil et.
            static bool IsReddedildi(string? statu) =>
                !string.IsNullOrEmpty(statu) && statu.ToUpperInvariant().Contains("RED");

            var reddedilenSayisi = gelenAll.Count(g => IsReddedildi(g.Statu));
            var gelen = gelenAll.Where(g => !IsReddedildi(g.Statu)).ToList();

            // Yevmiye — dönem alanı YYYY-MM, exact eşleşme + FaturaNo dolu olanlar.
            var yevmiyeRaw = await _db.KdvBeyannameYevmiye
                .AsNoTracking()
                .Where(y => y.FirmaId == firmaId
                            && y.Donem == donem
                            && y.FaturaNo != null
                            && y.FaturaNo != string.Empty)
                .Select(y => new
                {
                    y.FaturaNo,
                    y.HesapKodu,
                    y.Aciklama
                })
                .ToListAsync(ct);

            // Aynı fatura no birden çok yevmiye satırında olabilir (her hesap kodu için 1).
            // İlk satırı referans alıp distinct çekiyoruz; case-sensitive (Ordinal).
            var yevmiyeMap = yevmiyeRaw
                .GroupBy(y => y.FaturaNo!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var gelenMap = gelen
                .GroupBy(g => g.FaturaNo, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var result = new KarsilastirmaSonucuDto
            {
                Donem                 = donem,
                GelenFaturaSayisi     = gelenMap.Count,
                YevmiyeFaturaNoSayisi = yevmiyeMap.Count,
                ReddedilenSayisi      = reddedilenSayisi
            };

            foreach (var (faturaNo, g) in gelenMap)
            {
                if (yevmiyeMap.TryGetValue(faturaNo, out var y))
                {
                    result.Eslesen.Add(new KarsilastirmaSatiriDto
                    {
                        FaturaNo          = faturaNo,
                        GondericiVkn      = g.GondericiVkn,
                        GondericiUnvan    = g.GondericiUnvan,
                        FaturaTarihi      = g.FaturaTarihi,
                        ToplamTutar       = g.ToplamTutar,
                        KdvTutari         = g.KdvTutari,
                        YevmiyeHesapKodu  = y.HesapKodu,
                        YevmiyeAciklamasi = y.Aciklama
                    });
                }
                else
                {
                    // Gelen Faturalar'da var, yevmiyede yok → KRİTİK
                    result.Islenmemis.Add(new KarsilastirmaSatiriDto
                    {
                        FaturaNo       = faturaNo,
                        GondericiVkn   = g.GondericiVkn,
                        GondericiUnvan = g.GondericiUnvan,
                        FaturaTarihi   = g.FaturaTarihi,
                        ToplamTutar    = g.ToplamTutar,
                        KdvTutari      = g.KdvTutari
                    });
                }
            }

            foreach (var (faturaNo, y) in yevmiyeMap)
            {
                if (!gelenMap.ContainsKey(faturaNo))
                {
                    result.Fazla.Add(new KarsilastirmaSatiriDto
                    {
                        FaturaNo          = faturaNo,
                        YevmiyeHesapKodu  = y.HesapKodu,
                        YevmiyeAciklamasi = y.Aciklama
                    });
                }
            }

            return result;
        }

        private static void ValidateDonem(string donem, out int yil, out int ay)
        {
            if (string.IsNullOrWhiteSpace(donem) || donem.Length != 7 || donem[4] != '-')
                throw new ArgumentException(
                    $"Dönem formatı geçersiz: '{donem}'. Beklenen: YYYY-MM.");
            if (!int.TryParse(donem.AsSpan(0, 4), out yil) || yil < 2000 || yil > 2100)
                throw new ArgumentException($"Dönem yılı geçersiz: '{donem}'.");
            if (!int.TryParse(donem.AsSpan(5, 2), out ay) || ay < 1 || ay > 12)
                throw new ArgumentException($"Dönem ayı geçersiz: '{donem}'.");
        }
    }
}
