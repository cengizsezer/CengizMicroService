using CatalogService.Api.Features.Anasayfa.Domain;
using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.FirmaKontrol.Domain;
using CatalogService.Api.Features.FirmaKontrol.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CatalogService.Api.Features.FirmaKontrol.Services
{
    public class FirmaKontrolMizanService : IFirmaKontrolMizanService
    {
        /// <summary>Ağaç JSON'u için tek biçim; alan adları camelCase, Türkçe karakterler kaçırılmaz.</summary>
        private static readonly JsonSerializerOptions JsonAyar = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private readonly CatalogContext _db;
        private readonly IFirmaOlayYazici? _olay;

        public FirmaKontrolMizanService(CatalogContext db, IFirmaOlayYazici? olay = null)
        {
            _db = db;
            _olay = olay;
        }

        public async Task<List<FirmaKontrolMizanSatirDto>> GetSatirlarAsync(int firmaId, int yil, CancellationToken ct = default)
        {
            return await _db.FirmaKontrolMizanSatirlari
                .AsNoTracking()
                .Where(m => m.FirmaId == firmaId && m.Yil == yil)
                .Select(m => new FirmaKontrolMizanSatirDto
                {
                    Donem = m.Donem,
                    Yil = m.Yil,
                    Kod = m.Kod,
                    Ad = m.Ad,
                    Bakiye = m.Bakiye
                })
                .ToListAsync(ct);
        }

        public async Task<List<FirmaKontrolMizanAgacDto>> GetAgaclarAsync(int firmaId, int yil, CancellationToken ct = default)
        {
            var kayitlar = await _db.FirmaKontrolMizanAgaclari
                .AsNoTracking()
                .Where(a => a.FirmaId == firmaId && a.Yil == yil)
                .ToListAsync(ct);

            var sonuc = new List<FirmaKontrolMizanAgacDto>(kayitlar.Count);

            foreach (var k in kayitlar)
            {
                List<MizanHamDugumDto>? dugumler = null;
                try
                {
                    dugumler = JsonSerializer.Deserialize<List<MizanHamDugumDto>>(k.DugumlerJson, JsonAyar);
                }
                catch (JsonException)
                {
                    // Bozuk/eski biçimli kayıt yüklemeyi bozmasın; ağaç yokmuş gibi davranılır.
                    dugumler = null;
                }

                sonuc.Add(new FirmaKontrolMizanAgacDto
                {
                    Donem = k.Donem,
                    Yil = k.Yil,
                    Dugumler = dugumler ?? new List<MizanHamDugumDto>()
                });
            }

            return sonuc;
        }

        public async Task KaydetAsync(int firmaId, MizanKaydetRequest req, CancellationToken ct = default)
        {
            await EnsureFirmaExistsAsync(firmaId, ct);

            // Idempotent: aynı (FirmaId, Donem, Yil) için tüm satırları sil, yenilerini yaz.
            var mevcut = await _db.FirmaKontrolMizanSatirlari
                .Where(m => m.FirmaId == firmaId && m.Donem == req.Donem && m.Yil == req.Yil)
                .ToListAsync(ct);

            if (mevcut.Count > 0)
                _db.FirmaKontrolMizanSatirlari.RemoveRange(mevcut);

            // Aynı yükleme içinde tekrar eden kodları teke indir (unique index koruması).
            var eklenenKodlar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;

            foreach (var s in req.Satirlar)
            {
                if (string.IsNullOrWhiteSpace(s.Kod)) continue;
                var kod = s.Kod.Trim();
                if (!eklenenKodlar.Add(kod)) continue;

                _db.FirmaKontrolMizanSatirlari.Add(new FirmaKontrolMizanSatir
                {
                    FirmaId = firmaId,
                    Donem = req.Donem,
                    Yil = req.Yil,
                    Kod = kod,
                    Ad = s.Ad?.Trim() ?? string.Empty,
                    Bakiye = s.Bakiye,
                    UploadedAt = now
                });
            }

            // Kırılım ağacı — ham satırlarla AYNI istekte, aynı anahtarla, aynı idempotent
            // akışta. Düğümler tek JSON listesi olarak yazılır; satır başına kayıt açılmaz
            // (erişim deseni her zaman "şu dönemin ağacının tamamı"dır).
            var mevcutAgac = await _db.FirmaKontrolMizanAgaclari
                .FirstOrDefaultAsync(a => a.FirmaId == firmaId && a.Donem == req.Donem && a.Yil == req.Yil, ct);

            if (req.Dugumler.Count == 0)
            {
                // Ağaç gönderilmediyse eski ağaç ortada kalmasın (ham satırlarla tutarsız olurdu).
                if (mevcutAgac is not null)
                    _db.FirmaKontrolMizanAgaclari.Remove(mevcutAgac);
            }
            else
            {
                var json = JsonSerializer.Serialize(req.Dugumler, JsonAyar);

                // Yükleme özeti bir kez burada yazılır; anasayfa okur, hesaplamaz.
                var ozet = MizanYuklemeOzeti.Hesapla(req.Dugumler);

                if (mevcutAgac is null)
                {
                    mevcutAgac = new FirmaKontrolMizanAgac
                    {
                        FirmaId = firmaId,
                        Donem = req.Donem,
                        Yil = req.Yil
                    };
                    _db.FirmaKontrolMizanAgaclari.Add(mevcutAgac);
                }

                mevcutAgac.DugumlerJson = json;
                mevcutAgac.DugumSayisi = req.Dugumler.Count;
                mevcutAgac.UploadedAt = now;
                mevcutAgac.SeviyeSayisi = ozet.SeviyeSayisi;
                mevcutAgac.BorcToplam = ozet.BorcToplam;
                mevcutAgac.AlacakToplam = ozet.AlacakToplam;
            }

            await _db.SaveChangesAsync(ct);

            // "Son işlemler" — asıl kayıt bittikten sonra, ayrı try/catch'li yazıcıyla.
            if (_olay is not null)
            {
                var veriYili = req.Donem == 1 ? req.Yil : req.Yil - 1;
                var donem = req.Donem == 1 ? "cari dönem" : "önceki dönem";
                var satir = req.Dugumler.Count > 0 ? req.Dugumler.Count : eklenenKodlar.Count;
                await _olay.YazAsync(firmaId, FirmaOlayTipi.MizanYuklendi,
                                     $"{veriYili} mizanı yüklendi ({donem}, {satir:N0} satır)", ct);
            }
        }

        public async Task SifirlaAsync(int firmaId, int yil, CancellationToken ct = default)
        {
            var mevcut = await _db.FirmaKontrolMizanSatirlari
                .Where(m => m.FirmaId == firmaId && m.Yil == yil)
                .ToListAsync(ct);

            // Ağaç ham satırların yanında durur; sıfırlamada o da gider. Ham satır
            // kalmamış olsa bile ağaç kaydı ortada kalmasın diye erken çıkış yok.
            var agaclar = await _db.FirmaKontrolMizanAgaclari
                .Where(a => a.FirmaId == firmaId && a.Yil == yil)
                .ToListAsync(ct);

            if (mevcut.Count == 0 && agaclar.Count == 0) return;

            if (mevcut.Count > 0)
                _db.FirmaKontrolMizanSatirlari.RemoveRange(mevcut);

            if (agaclar.Count > 0)
                _db.FirmaKontrolMizanAgaclari.RemoveRange(agaclar);

            await _db.SaveChangesAsync(ct);
        }

        private async Task EnsureFirmaExistsAsync(int firmaId, CancellationToken ct)
        {
            var exists = await _db.Firmalar.AnyAsync(f => f.Id == firmaId, ct);
            if (!exists)
                throw new KeyNotFoundException($"Firma bulunamadı: Id={firmaId}");
        }
    }
}
