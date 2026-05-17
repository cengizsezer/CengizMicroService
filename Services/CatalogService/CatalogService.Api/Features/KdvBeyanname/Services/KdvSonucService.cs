using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.KdvBeyanname.Domain;
using CatalogService.Api.Features.KdvBeyanname.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.KdvBeyanname.Services
{
    public interface IKdvSonucService
    {
        Task<KdvSonucDto> HesaplaAsync(int firmaId, string donem, CancellationToken ct);
    }

    public class KdvSonucService : IKdvSonucService
    {
        private readonly CatalogContext _db;

        // Bakiye karşılaştırma toleransı (TL).
        private const decimal Eps = 0.10m;

        public KdvSonucService(CatalogContext db)
        {
            _db = db;
        }

        public async Task<KdvSonucDto> HesaplaAsync(int firmaId, string donem, CancellationToken ct)
        {
            ValidateDonem(donem, out var yil, out var ay);

            var firma = await _db.Firmalar.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == firmaId, ct)
                ?? throw new KeyNotFoundException($"Firma bulunamadı: Id={firmaId}");

            var mizan = await _db.KdvBeyannameMizan
                .AsNoTracking()
                .Where(m => m.FirmaId == firmaId && m.Donem == donem)
                .ToListAsync(ct);

            if (mizan.Count == 0)
                throw new InvalidOperationException(
                    $"'{donem}' dönemi için mizan yüklenmemiş. " +
                    "Önce KDV Mizan sekmesinden dosyayı yükleyin.");

            var result = new KdvSonucDto
            {
                Donem = donem,
                Yil   = yil,
                Ay    = ay
            };

            // ── Ana hesap bakiyeleri ────────────────────────────────────────
            // 190/191 → Borç Kalan ; 391/600 → Alacak Kalan
            result.Hesaplanan391  = AnaHesapBakiye(mizan, "391", useAlacak: true);
            result.Indirilecek191 = AnaHesapBakiye(mizan, "191", useAlacak: false);
            result.Devreden190    = AnaHesapBakiye(mizan, "190", useAlacak: false);
            result.Satislar600    = AnaHesapBakiye(mizan, "600", useAlacak: true);

            // ── Hesaplama (README) ──────────────────────────────────────────
            result.ToplamIndirilecek = result.Indirilecek191 + result.Devreden190;
            result.Fark = result.Hesaplanan391 - result.ToplamIndirilecek;

            if (result.Fark > 0)
            {
                result.OdenmesiGerekenKDV      = result.Fark;
                result.SonrakiDonemeDevredenKDV = 0m;
            }
            else
            {
                result.OdenmesiGerekenKDV      = 0m;
                result.SonrakiDonemeDevredenKDV = Math.Abs(result.Fark);
            }

            // ── Oran bazlı kırılım (3-segment) ──────────────────────────────
            // 191 alt kırılımı → indirilecekKDVODler (bedel + KDVTutari)
            // 600 + 391 birleşimi → tevkifatUygulanmayanlar (matrah + vergi)
            //   • 600 satışlardan oran-matrah, 391 hesaplananlardan oran-vergi alınır.
            var dict391 = AltKirilim(mizan, "391", useAlacak: true)
                .ToDictionary(x => x.Oran);
            var dict191 = AltKirilim(mizan, "191", useAlacak: false);
            var dict600 = AltKirilim(mizan, "600", useAlacak: true);

            result.IndirilecekKdvODler = dict191
                .Select(x => new OranKirilimDto
                {
                    HesapKodu  = x.HesapKodu,
                    HesapAdi   = x.HesapAdi,
                    Oran       = x.Oran,
                    Bedel      = x.Oran == 0 ? 0m : Math.Round(x.Bakiye * 100m / x.Oran, 2),
                    KdvTutari  = x.Bakiye
                })
                .OrderBy(x => x.Oran)
                .ToList();

            result.TevkifatUygulanmayanlar = dict600
                .Select(s =>
                {
                    var matrah = s.Bakiye;
                    var vergi  = dict391.TryGetValue(s.Oran, out var v)
                        ? v.Bakiye
                        : Math.Round(matrah * s.Oran / 100m, 2);
                    return new OranKirilimDto
                    {
                        HesapKodu = s.HesapKodu,
                        HesapAdi  = s.HesapAdi,
                        Oran      = s.Oran,
                        Bedel     = matrah,
                        KdvTutari = vergi
                    };
                })
                .OrderBy(x => x.Oran)
                .ToList();

            // ── Eksiklik / validasyon kontrolleri ──────────────────────────
            result.Eksiklikler = await EksiklikleriToplaAsync(firma, mizan, ct);

            return result;
        }

        // ── Yardımcılar ────────────────────────────────────────────────────

        private static decimal AnaHesapBakiye(
            List<KdvBeyannameMizan> mizan, string anaKod, bool useAlacak)
        {
            // Ana hesap = tam eşleşen 1-segmentli kod (örn. "391").
            var ana = mizan.FirstOrDefault(m => m.HesapKodu == anaKod);
            if (ana != null)
                return useAlacak ? ana.AlacakKalan : ana.BorcKalan;

            // Ana hesap satırı yoksa: 3-segmentli alt kırılım toplamını döndür.
            return AltKirilim(mizan, anaKod, useAlacak).Sum(x => x.Bakiye);
        }

        private record AltKirilimSatiri(
            string HesapKodu, string HesapAdi, int Oran, decimal Bakiye);

        private static List<AltKirilimSatiri> AltKirilim(
            List<KdvBeyannameMizan> mizan, string anaKod, bool useAlacak)
        {
            var list = new List<AltKirilimSatiri>();
            var prefix = anaKod + " ";

            foreach (var m in mizan)
            {
                if (!m.HesapKodu.StartsWith(prefix, StringComparison.Ordinal)) continue;

                var parts = m.HesapKodu.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3) continue;            // sadece 3-segment
                if (!int.TryParse(parts[2], out var oran)) continue;

                var bakiye = useAlacak ? m.AlacakKalan : m.BorcKalan;
                if (bakiye == 0m) continue;                  // 0 olanları XML'e koymayalım

                list.Add(new AltKirilimSatiri(m.HesapKodu, m.HesapAdi, oran, bakiye));
            }

            return list;
        }

        private async Task<BdpEksiklikDto> EksiklikleriToplaAsync(
            Firma firma,
            List<KdvBeyannameMizan> mizan,
            CancellationToken ct)
        {
            var eksik = new BdpEksiklikDto();

            // ── Firma BDP alanları ──────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(firma.VergiDairesiKodu))
                eksik.FirmaAlanlari.Add("Vergi Dairesi Kodu boş, Firmalarım sayfasından düzenleyin");
            if (string.IsNullOrWhiteSpace(firma.YetkiliSoyadi))
                eksik.FirmaAlanlari.Add("Soyadı (Unvanı) alanı boş, Firmalarım sayfasından düzenleyin");
            if (string.IsNullOrWhiteSpace(firma.YetkiliAdi))
                eksik.FirmaAlanlari.Add("Adı (Unvanın Devamı) alanı boş, Firmalarım sayfasından düzenleyin");
            if (string.IsNullOrWhiteSpace(firma.TicaretSicilNo))
                eksik.FirmaAlanlari.Add("Ticaret Sicil No boş, Firmalarım sayfasından düzenleyin");

            // ── Düzenleyen ──────────────────────────────────────────────────
            if (firma.DuzenleyenId is null)
            {
                eksik.DuzenleyenAlanlari.Add(
                    "Bu firma için düzenleyen seçilmemiş, Firmalarım sayfasından seçin");
            }
            else
            {
                var duz = await _db.Duzenleyenler.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == firma.DuzenleyenId.Value, ct);

                if (duz is null || !duz.Aktif)
                {
                    eksik.DuzenleyenAlanlari.Add(
                        "Seçili düzenleyen bulunamadı veya pasif. Firmalarım sayfasından yeni düzenleyen seçin");
                }
                else
                {
                    void Check(string? alan, string alanAdi)
                    {
                        if (string.IsNullOrWhiteSpace(alan))
                            eksik.DuzenleyenAlanlari.Add(
                                $"Seçili düzenleyen '{duz.Kisaltma}' için {alanAdi} alanı boş, " +
                                "Yönetim → Düzenleyen Bilgileri sayfasından güncelleyin");
                    }

                    Check(duz.Vkn,            "VKN");
                    Check(duz.Soyadi,         "Soyadı");
                    Check(duz.Adi,            "Adı");
                    Check(duz.TicaretSicilNo, "Ticaret Sicil No");
                    Check(duz.Eposta,         "E-posta");
                    Check(duz.AlanKodu,       "Alan Kodu");
                    Check(duz.TelNo,          "Telefon No");
                }
            }

            // Mizan yapısal kontrolleri
            foreach (var anaKod in new[] { "191", "391", "600" })
            {
                var ana = mizan.FirstOrDefault(m => m.HesapKodu == anaKod);
                if (ana == null) continue;

                var useAlacak = anaKod is "391" or "600";
                var alt = AltKirilim(mizan, anaKod, useAlacak);

                if (alt.Count == 0)
                {
                    eksik.MizanHatalari.Add(
                        $"{anaKod} hesabının oran bazında kırılımı bulunamadı " +
                        $"(örn: {anaKod} 1 20 gibi alt hesap olmalı).");
                    continue;
                }

                var anaBakiye = useAlacak ? ana.AlacakKalan : ana.BorcKalan;
                var altToplam = alt.Sum(x => x.Bakiye);
                if (Math.Abs(anaBakiye - altToplam) > Eps)
                {
                    eksik.MizanUyarilari.Add(
                        $"{anaKod} ana hesap bakiyesi ({anaBakiye:N2}) ile " +
                        $"alt kırılım toplamı ({altToplam:N2}) uyuşmuyor.");
                }
            }

            return eksik;
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
