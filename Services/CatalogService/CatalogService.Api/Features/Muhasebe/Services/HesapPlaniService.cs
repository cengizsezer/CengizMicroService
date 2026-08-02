using System.Linq.Expressions;
using CatalogService.Api.Features.Muhasebe.Domain;
using CatalogService.Api.Features.Muhasebe.Dtos;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.Api.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Muhasebe.Services
{
    /// <inheritdoc cref="IHesapPlaniService"/>
    public class HesapPlaniService : IHesapPlaniService
    {
        private const int AramaLimiti = 100;

        private readonly CatalogContext _db;

        public HesapPlaniService(CatalogContext db) => _db = db;

        private static readonly Expression<Func<HesapPlani, HesapPlaniDto>> Projeksiyon = h => new HesapPlaniDto
        {
            Id = h.Id,
            UstHesapId = h.UstHesapId,
            Kod = h.Kod,
            KodDuz = h.KodDuz,
            SegmentKod = h.SegmentKod,
            Ad = h.Ad,
            Seviye = h.Seviye,
            HesapTuru = h.HesapTuru,
            Karakter = h.Karakter,
            HareketGorur = h.HareketGorur,
            SistemHesabi = h.SistemHesabi,
            ParaBirimi = h.ParaBirimi,
            BankaKodu = h.BankaKodu,
            Iban = h.Iban,
            Yol = h.Yol,
            Aktif = h.Aktif
        };

        // ---- Okuma ----

        public Task<List<HesapPlaniDto>> GetHepsiAsync(CancellationToken ct = default)
            => _db.HesapPlanlari
                  .AsNoTracking()
                  .OrderBy(h => h.KodDuz)
                  .Select(Projeksiyon)
                  .ToListAsync(ct);

        public async Task<List<HesapPlaniDto>> AraAsync(string? q, CancellationToken ct = default)
        {
            var arama = (q ?? string.Empty).Trim();
            if (arama.Length == 0) return new List<HesapPlaniDto>();

            // Kullanıcı kodu ayraçlı da ayraçsız da yazabilir; ikisini de ara.
            var maske = await MaskeAsync(ct);
            var aramaDuz = arama.Replace(maske.Ayrac, string.Empty);
            if (aramaDuz.Length == 0) aramaDuz = arama;

            return await _db.HesapPlanlari
                .AsNoTracking()
                .Where(h => h.Ad.Contains(arama) || h.Kod.Contains(arama) || h.KodDuz.Contains(aramaDuz))
                .OrderBy(h => h.KodDuz)
                .Take(AramaLimiti)
                .Select(Projeksiyon)
                .ToListAsync(ct);
        }

        public Task<List<HesapPlaniDto>> GetHareketGorenlerAsync(CancellationToken ct = default)
            => _db.HesapPlanlari
                  .AsNoTracking()
                  .Where(h => h.HareketGorur && h.Aktif)
                  .OrderBy(h => h.KodDuz)
                  .Select(Projeksiyon)
                  .ToListAsync(ct);

        public async Task<HesapPlaniDto?> GetByIdAsync(int id, CancellationToken ct = default)
            => await _db.HesapPlanlari
                        .AsNoTracking()
                        .Where(h => h.Id == id)
                        .Select(Projeksiyon)
                        .FirstOrDefaultAsync(ct);

        public async Task<SonrakiKodDto?> GetSonrakiKodAsync(int ustId, CancellationToken ct = default)
        {
            var ust = await _db.HesapPlanlari.AsNoTracking().FirstOrDefaultAsync(h => h.Id == ustId, ct);
            if (ust is null) return null;

            var maske = await MaskeAsync(ct);
            var seviye = SonrakiSeviye(ust, maske);
            var uzunluk = maske.SegmentUzunlugu(seviye);

            var kullanilan = await KullanilanSegmentlerAsync(ustId, ct);

            // Muavin numaralandırması 1'den, sınıf/grup/kebir 0'dan başlar (100, 101...).
            var baslangic = seviye <= maske.KebirSeviyesi ? 0 : 1;
            var enBuyuk = (int)Math.Pow(10, uzunluk) - 1;

            for (var i = baslangic; i <= enBuyuk; i++)
            {
                if (kullanilan.Contains(i)) continue;

                var segment = i.ToString().PadLeft(uzunluk, '0');
                return new SonrakiKodDto
                {
                    UstHesapId = ustId,
                    Seviye = seviye,
                    Segment = segment,
                    Kod = KodBirlestir(ust, segment, seviye, maske)
                };
            }

            throw new MuhasebeKuralException("segment",
                $"'{ust.Kod} {ust.Ad}' hesabının altında boş kod kalmadı. Bir üst seviyede yeni hesap açın.");
        }

        public async Task<List<BosKebirDto>?> GetBosKebirlerAsync(int grupId, CancellationToken ct = default)
        {
            var grup = await _db.HesapPlanlari.AsNoTracking().FirstOrDefaultAsync(h => h.Id == grupId, ct);
            if (grup is null) return null;

            var maske = await MaskeAsync(ct);
            if (grup.Seviye != maske.KebirSeviyesi - 1)
                throw new MuhasebeKuralException("grupId",
                    "Boş kebir listesi yalnızca grup hesapları için alınabilir. Önce bir grup hesabı seçin.");

            var kullanilan = await KullanilanSegmentlerAsync(grupId, ct);

            return Enumerable.Range(0, 10)
                .Where(i => !kullanilan.Contains(i))
                .Select(i => new BosKebirDto
                {
                    Segment = i.ToString(),
                    Kod = grup.Kod + i
                })
                .ToList();
        }

        // ---- Yazma ----

        public async Task<HesapPlaniDto?> CreateAsync(HesapPlaniCreateDto dto, CancellationToken ct = default)
        {
            HesapPlani? ust = null;
            if (dto.UstHesapId is not null)
            {
                ust = await _db.HesapPlanlari.FirstOrDefaultAsync(h => h.Id == dto.UstHesapId, ct);
                if (ust is null) return null;
            }

            var ad = AdDogrula(dto.Ad);
            var maske = await MaskeAsync(ct);
            var seviye = ust is null ? (byte)1 : SonrakiSeviye(ust, maske);

            // Kural 2: hareketi olan hesabın altına çocuk eklenemez.
            if (ust is not null && await _db.FisSatirlar.AnyAsync(s => s.HesapId == ust.Id, ct))
                throw new MuhasebeKuralException("ustHesapId",
                    $"'{ust.Kod} {ust.Ad}' hesabının hareketi var; önce hareketleri alt hesaba taşıyın.");

            // Kural 3 + 4: kullanıcıdan yalnızca son segment alınır, tam kodu servis birleştirir.
            var segment = SegmentDogrula(dto.Segment, maske.SegmentUzunlugu(seviye));
            var kod = KodBirlestir(ust, segment, seviye, maske);
            var kodDuz = (ust?.KodDuz ?? string.Empty) + segment;

            if (await _db.HesapPlanlari.AnyAsync(h => h.Kod == kod, ct))
                throw new DuplicateRecordException("segment",
                    $"'{kod}' kodu zaten kullanılıyor. Boş bir kod seçin.");

            var hesap = new HesapPlani
            {
                UstHesapId = ust?.Id,
                Kod = kod,
                KodDuz = kodDuz,
                SegmentKod = segment,
                Ad = ad,
                Seviye = seviye,
                HesapTuru = maske.TuruBul(seviye),
                // Kural 5: karakter kullanıcıdan alınmaz, üst hesaptan miras alınır.
                Karakter = ust?.Karakter ?? MaskeBilgisi.KokKarakter(kod[0]),
                HareketGorur = dto.HareketGorur,
                SistemHesabi = false,
                ParaBirimi = ParaBirimiDogrula(dto.ParaBirimi),
                BankaKodu = BankaKoduDogrula(dto.BankaKodu),
                Iban = IbanDuzenle(dto.Iban),
                // Kural 9: Yol üst hesaptan türetilir.
                Yol = ust is null ? "/" : $"{ust.Yol}{ust.Id}/",
                Aktif = true
            };

            // Kural 1: yalnızca yaprak hesap hareket görür.
            if (ust is not null) ust.HareketGorur = false;

            _db.HesapPlanlari.Add(hesap);
            await _db.SaveChangesAsync(ct);

            return ToDto(hesap);
        }

        public async Task<HesapPlaniDto?> UpdateAsync(int id, HesapPlaniUpdateDto dto, CancellationToken ct = default)
        {
            var hesap = await _db.HesapPlanlari.FirstOrDefaultAsync(h => h.Id == id, ct);
            if (hesap is null) return null;

            var ad = AdDogrula(dto.Ad);
            var maske = await MaskeAsync(ct);

            var yeniSegment = string.IsNullOrWhiteSpace(dto.Segment)
                ? hesap.SegmentKod
                : SegmentDogrula(dto.Segment, maske.SegmentUzunlugu(hesap.Seviye));

            var paraBirimi = ParaBirimiDogrula(dto.ParaBirimi);
            var bankaKodu = BankaKoduDogrula(dto.BankaKodu);
            var iban = IbanDuzenle(dto.Iban);

            // Kural 6: sistem hesabının kodu değiştirilemez, yalnızca adı güncellenebilir.
            if (hesap.SistemHesabi)
            {
                if (yeniSegment != hesap.SegmentKod)
                    throw new MuhasebeKuralException("segment",
                        $"'{hesap.Kod}' standart bir hesap; kodu değiştirilemez. Yalnızca hesap adını güncelleyebilirsiniz.");

                if (dto.HareketGorur != hesap.HareketGorur ||
                    paraBirimi != hesap.ParaBirimi ||
                    bankaKodu != hesap.BankaKodu ||
                    iban != hesap.Iban)
                    throw new MuhasebeKuralException("sistemHesabi",
                        $"'{hesap.Kod}' standart bir hesap; yalnızca hesap adı güncellenebilir.");

                hesap.Ad = ad;
                await _db.SaveChangesAsync(ct);
                return ToDto(hesap);
            }

            var cocukVar = await _db.HesapPlanlari.AnyAsync(h => h.UstHesapId == id, ct);

            if (yeniSegment != hesap.SegmentKod)
            {
                // Alt ağaç kodları üst koddan türediği için, çocuğu olan hesabın kodu değişemez
                // (bu fazda hesap taşıma / alt ağaç güncelleme yok — kural 9).
                if (cocukVar)
                    throw new MuhasebeKuralException("segment",
                        $"'{hesap.Kod}' hesabının alt hesapları var; kodu değiştirilemez.");

                var ust = hesap.UstHesapId is null
                    ? null
                    : await _db.HesapPlanlari.AsNoTracking().FirstOrDefaultAsync(h => h.Id == hesap.UstHesapId, ct);

                var yeniKod = KodBirlestir(ust, yeniSegment, hesap.Seviye, maske);

                if (await _db.HesapPlanlari.AnyAsync(h => h.Kod == yeniKod && h.Id != id, ct))
                    throw new DuplicateRecordException("segment",
                        $"'{yeniKod}' kodu zaten kullanılıyor. Boş bir kod seçin.");

                hesap.SegmentKod = yeniSegment;
                hesap.Kod = yeniKod;
                hesap.KodDuz = (ust?.KodDuz ?? string.Empty) + yeniSegment;
            }

            hesap.Ad = ad;
            // Kural 1: çocuğu olan hesap hareket göremez.
            hesap.HareketGorur = dto.HareketGorur && !cocukVar;
            hesap.ParaBirimi = paraBirimi;
            hesap.BankaKodu = bankaKodu;
            hesap.Iban = iban;

            await _db.SaveChangesAsync(ct);
            return ToDto(hesap);
        }

        public async Task<HesapPlaniDto?> PasifeAlAsync(int id, CancellationToken ct = default)
        {
            var hesap = await _db.HesapPlanlari.FirstOrDefaultAsync(h => h.Id == id, ct);
            if (hesap is null) return null;

            // Pasif üst hesabın altında aktif hesap kalmasın: alt ağaç da pasife çekilir.
            var altYol = $"{hesap.Yol}{hesap.Id}/";
            var altAgac = await _db.HesapPlanlari.Where(h => h.Yol.StartsWith(altYol)).ToListAsync(ct);

            hesap.Aktif = false;
            foreach (var alt in altAgac) alt.Aktif = false;

            await _db.SaveChangesAsync(ct);
            return ToDto(hesap);
        }

        public async Task<HesapSilmeSonuc> DeleteAsync(int id, CancellationToken ct = default)
        {
            var hesap = await _db.HesapPlanlari.FirstOrDefaultAsync(h => h.Id == id, ct);
            if (hesap is null) return HesapSilmeSonuc.Bulunamadi;

            // Kural 6: sistem hesabı silinemez.
            if (hesap.SistemHesabi) return HesapSilmeSonuc.SistemHesabi;

            if (await _db.HesapPlanlari.AnyAsync(h => h.UstHesapId == id, ct))
                return HesapSilmeSonuc.AltHesapVar;

            // Kural 8: hareketi olan hesap silinmez, pasife çekilir.
            if (await _db.FisSatirlar.AnyAsync(s => s.HesapId == id, ct))
                return HesapSilmeSonuc.HareketVar;

            var ustId = hesap.UstHesapId;
            _db.HesapPlanlari.Remove(hesap);
            await _db.SaveChangesAsync(ct);

            // Son çocuğu silinen üst hesap yeniden yaprak olur (kural 1'in tersi).
            if (ustId is not null && !await _db.HesapPlanlari.AnyAsync(h => h.UstHesapId == ustId, ct))
            {
                var ust = await _db.HesapPlanlari.FirstOrDefaultAsync(h => h.Id == ustId, ct);
                if (ust is not null)
                {
                    ust.HareketGorur = true;
                    await _db.SaveChangesAsync(ct);
                }
            }

            return HesapSilmeSonuc.Silindi;
        }

        // ---- Yardımcılar ----

        private async Task<MaskeBilgisi> MaskeAsync(CancellationToken ct)
        {
            var maske = await _db.KodMaskeleri.AsNoTracking().FirstOrDefaultAsync(ct);
            return MaskeBilgisi.Coz(maske);
        }

        private async Task<HashSet<int>> KullanilanSegmentlerAsync(int ustId, CancellationToken ct)
        {
            var segmentler = await _db.HesapPlanlari
                .AsNoTracking()
                .Where(h => h.UstHesapId == ustId)
                .Select(h => h.SegmentKod)
                .ToListAsync(ct);

            var kullanilan = new HashSet<int>();
            foreach (var s in segmentler)
                if (int.TryParse(s, out var n)) kullanilan.Add(n);

            return kullanilan;
        }

        private static byte SonrakiSeviye(HesapPlani ust, MaskeBilgisi maske)
        {
            var seviye = ust.Seviye + 1;
            if (seviye > maske.MaksimumSeviye)
                throw new MuhasebeKuralException("ustHesapId",
                    $"'{ust.Kod} {ust.Ad}' hesabı maskenin izin verdiği en alt seviyede; altına hesap açılamaz.");

            return (byte)seviye;
        }

        private static string KodBirlestir(HesapPlani? ust, string segment, byte seviye, MaskeBilgisi maske)
        {
            if (ust is null) return segment;
            return ust.Kod + (maske.AyracKullanir(seviye) ? maske.Ayrac : string.Empty) + segment;
        }

        /// <summary>Kural 4: segment yalnızca rakam içerir, maskedeki uzunluğu aşamaz, sola sıfır doldurulur.</summary>
        private static string SegmentDogrula(string? segment, int uzunluk)
        {
            var s = (segment ?? string.Empty).Trim();

            if (s.Length == 0)
                throw new MuhasebeKuralException("segment", "Kod segmenti zorunlu.");

            if (!s.All(char.IsAsciiDigit))
                throw new MuhasebeKuralException("segment", "Kod segmenti yalnızca rakam içerebilir.");

            if (s.Length > uzunluk)
                throw new MuhasebeKuralException("segment",
                    $"Bu seviyede kod segmenti en fazla {uzunluk} hane olabilir.");

            return s.PadLeft(uzunluk, '0');
        }

        private static string AdDogrula(string? ad)
        {
            var s = (ad ?? string.Empty).Trim();
            if (s.Length == 0)
                throw new MuhasebeKuralException("ad", "Hesap adı zorunlu.");

            return s;
        }

        private static string? ParaBirimiDogrula(string? paraBirimi)
        {
            var s = (paraBirimi ?? string.Empty).Trim().ToUpperInvariant();
            if (s.Length == 0) return null;

            if (s.Length != 3 || !s.All(char.IsAsciiLetterUpper))
                throw new MuhasebeKuralException("paraBirimi", "Para birimi 3 harfli olmalı, ör. \"USD\".");

            return s;
        }

        private static string? BankaKoduDogrula(string? bankaKodu)
        {
            var s = (bankaKodu ?? string.Empty).Trim();
            if (s.Length == 0) return null;

            if (!s.All(char.IsAsciiDigit) || s.Length > 4)
                throw new MuhasebeKuralException("bankaKodu", "Banka kodu en fazla 4 haneli, yalnızca rakamdan oluşur.");

            return s.PadLeft(4, '0');
        }

        private static string? IbanDuzenle(string? iban)
        {
            var s = (iban ?? string.Empty).Replace(" ", string.Empty).Trim().ToUpperInvariant();
            if (s.Length == 0) return null;

            if (s.Length > 34 || !s.All(char.IsAsciiLetterOrDigit))
                throw new MuhasebeKuralException("iban", "IBAN geçersiz. Boşluksuz, en fazla 34 karakter girin.");

            return s;
        }

        private static HesapPlaniDto ToDto(HesapPlani h) => new()
        {
            Id = h.Id,
            UstHesapId = h.UstHesapId,
            Kod = h.Kod,
            KodDuz = h.KodDuz,
            SegmentKod = h.SegmentKod,
            Ad = h.Ad,
            Seviye = h.Seviye,
            HesapTuru = h.HesapTuru,
            Karakter = h.Karakter,
            HareketGorur = h.HareketGorur,
            SistemHesabi = h.SistemHesabi,
            ParaBirimi = h.ParaBirimi,
            BankaKodu = h.BankaKodu,
            Iban = h.Iban,
            Yol = h.Yol,
            Aktif = h.Aktif
        };
    }
}
