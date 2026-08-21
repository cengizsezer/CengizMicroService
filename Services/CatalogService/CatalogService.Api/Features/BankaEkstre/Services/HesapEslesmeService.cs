using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    public interface IHesapEslesmeService
    {
        Task<List<HesapEslesmesiDto>> AraAsync(string? q, int enFazla, CancellationToken ct = default);
        Task<HesapEslesmesiDto?> GuncelleAsync(int id, HesapEslesmesiYazDto dto, CancellationToken ct = default);
        Task<bool> SilAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Onaydan öğrenir: firma bazlı hesap eşlemesini ve global kimlik kaydını yazar.
        /// Kaydetmez — çağıran <c>SaveChangesAsync</c> ile birlikte yazar.
        /// </summary>
        Task OgrenAsync(EkstreSatiri satir, string kod, string? ad, CancellationToken ct = default);
    }

    /// <summary>
    /// Öğrenilen eşleşmelerin yönetimi. Modülün asıl değeri burada birikir; ama yanlış
    /// onaylanan bir eşleşme bir daha sorulmadan tekrarlanacağı için düzenlenebilir ve
    /// silinebilir olmak zorunda.
    ///
    /// Tablo ikiye ayrıldı: <see cref="KimlikKaydi"/> global (bir unvanın kim olduğu her
    /// firmada aynı), <see cref="HesapEslesmesi"/> firma bazlı (hangi koda gittiği firmaya özel).
    /// </summary>
    public class HesapEslesmeService : IHesapEslesmeService
    {
        private readonly CatalogContext _db;

        public HesapEslesmeService(CatalogContext db) => _db = db;

        public async Task<List<HesapEslesmesiDto>> AraAsync(string? q, int enFazla, CancellationToken ct = default)
        {
            var sorgu = _db.EkstreHesapEslesmeleri.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var arama = q.Trim();
                var normal = Normalizasyon.UnvanCekirdek(arama);

                sorgu = sorgu.Where(e => e.AnahtarCekirdek.Contains(arama) ||
                                         e.AnahtarCekirdek.Contains(normal) ||
                                         e.HesapKodu.Contains(arama) ||
                                         (e.HesapAdi != null && e.HesapAdi.Contains(arama)));
            }

            var kayitlar = await sorgu
                .OrderByDescending(e => e.SonKullanim)
                .ThenBy(e => e.AnahtarCekirdek)
                .Take(enFazla <= 0 ? 100 : Math.Min(enFazla, 500))
                .ToListAsync(ct);

            return kayitlar.Select(Esle).ToList();
        }

        /// <summary>
        /// Yanlış öğrenilmiş kaydın düzeltilmesi. Anahtar değişmez (o, karşı tarafın
        /// kimliğidir); yalnız gittiği hesap, yön ve ayırt edici ek düzenlenir.
        /// </summary>
        public async Task<HesapEslesmesiDto?> GuncelleAsync(int id, HesapEslesmesiYazDto dto, CancellationToken ct = default)
        {
            var kayit = await _db.EkstreHesapEslesmeleri.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (kayit is null) return null;

            var kod = Normalizasyon.HesapKoduNormalize(dto.HesapKodu);
            if (kod.Length == 0)
                throw new BankaEkstreKuralException(nameof(dto.HesapKodu), "Hesap kodu boş olamaz.");

            var plan = await _db.EkstreHesapPlani.AsNoTracking().FirstOrDefaultAsync(h => h.Kod == kod, ct);
            if (plan is null && await _db.EkstreHesapPlani.AnyAsync(ct))
                throw new BankaEkstreKuralException(nameof(dto.HesapKodu),
                    $"'{kod}' hesap planında yok. Doğrulanmamış kod öğrenme tablosuna yazılmaz.");

            var ek = string.IsNullOrWhiteSpace(dto.AyirtEdiciEk)
                ? null
                : Normalizasyon.UnvanCekirdek(dto.AyirtEdiciEk);

            var cakisiyor = await _db.EkstreHesapEslesmeleri.AnyAsync(e =>
                e.Id != id &&
                e.AnahtarTipi == kayit.AnahtarTipi &&
                e.AnahtarCekirdek == kayit.AnahtarCekirdek &&
                (e.AyirtEdiciEk ?? string.Empty) == (ek ?? string.Empty) &&
                e.Yon == dto.Yon, ct);

            if (cakisiyor)
                throw new BankaEkstreKuralException(nameof(dto.AyirtEdiciEk),
                    "Aynı anahtar ve yön için başka bir eşleşme zaten var; onu düzenleyin.");

            kayit.HesapKodu = kod;
            kayit.HesapAdi = plan?.Ad ?? kayit.HesapAdi;
            kayit.Yon = dto.Yon;
            kayit.AyirtEdiciEk = ek;
            kayit.SonKullanim = DateTime.Now;

            await _db.SaveChangesAsync(ct);
            return Esle(kayit);
        }

        public async Task<bool> SilAsync(int id, CancellationToken ct = default)
        {
            var kayit = await _db.EkstreHesapEslesmeleri.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (kayit is null) return false;

            // Kimlik kaydı (global) silinmez: başka firmada hâlâ geçerli olabilir.
            _db.EkstreHesapEslesmeleri.Remove(kayit);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task OgrenAsync(EkstreSatiri satir, string kod, string? ad, CancellationToken ct = default)
        {
            var cekirdek = satir.AnahtarCekirdek;
            if (string.IsNullOrWhiteSpace(cekirdek)) return;

            var ek = string.IsNullOrWhiteSpace(satir.AyirtEdiciEk) ? null : satir.AyirtEdiciEk;

            await EslesmeYazAsync(AnahtarTipi.UnvanCekirdek, cekirdek, ek, satir.Yon, kod, ad, ct);
            await KimlikYazAsync(AnahtarTipi.UnvanCekirdek, cekirdek, satir.CikarilanUnvan, ct);

            // IBAN/VKN katmanları kapalı olduğu için o anahtarlar yazılmaz. Bir banka hesabında
            // katman açılırsa öğrenme de o anda başlar; kapalı katmanın veri biriktirmesi,
            // sonradan açıldığında doğrulanmamış eşleşmelerin güven 1.0 ile geçmesi demekti.
        }

        // ---- Yardımcılar ----

        private async Task EslesmeYazAsync(
            AnahtarTipi tip, string cekirdek, string? ek, Yon yon, string kod, string? ad, CancellationToken ct)
        {
            var mevcut = await _db.EkstreHesapEslesmeleri.FirstOrDefaultAsync(e =>
                e.AnahtarTipi == tip &&
                e.AnahtarCekirdek == cekirdek &&
                (e.AyirtEdiciEk ?? string.Empty) == (ek ?? string.Empty) &&
                e.Yon == yon, ct);

            if (mevcut is null)
            {
                _db.EkstreHesapEslesmeleri.Add(new HesapEslesmesi
                {
                    AnahtarTipi = tip,
                    AnahtarCekirdek = cekirdek,
                    AyirtEdiciEk = ek,
                    Yon = yon,
                    HesapKodu = kod,
                    HesapAdi = ad,
                    KullanimSayisi = 1,
                    SonKullanim = DateTime.Now
                });
                return;
            }

            // Farklı kod seçildiyse eski kayıt ezilir; sayaç yeniden başlar çünkü
            // eski koda ait kullanım artık bu anahtarı temsil etmiyor. Satır geçmiş
            // onaydan çözülmüşse düzeltme buraya da yansır — aksi hâlde hata gelecek ay geri gelirdi.
            if (!string.Equals(mevcut.HesapKodu, kod, StringComparison.Ordinal))
            {
                mevcut.HesapKodu = kod;
                mevcut.HesapAdi = ad;
                mevcut.KullanimSayisi = 1;
            }
            else
            {
                mevcut.KullanimSayisi++;
                if (!string.IsNullOrWhiteSpace(ad)) mevcut.HesapAdi = ad;
            }

            mevcut.SonKullanim = DateTime.Now;
        }

        /// <summary>Global kimlik: bir unvanın kim olduğu her firmada aynıdır.</summary>
        private async Task KimlikYazAsync(AnahtarTipi tip, string anahtar, string? unvan, CancellationToken ct)
        {
            var mevcut = await _db.EkstreKimlikKayitlari
                .FirstOrDefaultAsync(k => k.AnahtarTipi == tip && k.Anahtar == anahtar, ct);

            if (mevcut is null)
            {
                _db.EkstreKimlikKayitlari.Add(new KimlikKaydi
                {
                    AnahtarTipi = tip,
                    Anahtar = anahtar,
                    NormalizeUnvan = Normalizasyon.Kirp(Normalizasyon.UnvanNormalize(unvan), 200) is { Length: > 0 } n ? n : null,
                    KullanimSayisi = 1,
                    SonKullanim = DateTime.Now
                });
                return;
            }

            mevcut.KullanimSayisi++;
            mevcut.SonKullanim = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(unvan))
                mevcut.NormalizeUnvan = Normalizasyon.Kirp(Normalizasyon.UnvanNormalize(unvan), 200);
        }

        private static HesapEslesmesiDto Esle(HesapEslesmesi e) => new()
        {
            Id = e.Id,
            AnahtarCekirdek = e.AnahtarCekirdek,
            AyirtEdiciEk = e.AyirtEdiciEk,
            TamAnahtar = e.TamAnahtar,
            AnahtarTipi = e.AnahtarTipi,
            HesapKodu = e.HesapKodu,
            HesapAdi = e.HesapAdi,
            Yon = e.Yon,
            KullanimSayisi = e.KullanimSayisi,
            SonKullanim = e.SonKullanim
        };
    }
}
