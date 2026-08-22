using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.Api.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    public interface IBankaHesabiService
    {
        Task<List<BankaHesabiDto>> GetHepsiAsync(bool pasifDahil, CancellationToken ct = default);
        Task<BankaHesabiDto?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<BankaHesabiDto> CreateAsync(BankaHesabiYazDto dto, CancellationToken ct = default);
        Task<BankaHesabiDto?> UpdateAsync(int id, BankaHesabiYazDto dto, CancellationToken ct = default);
        Task<bool?> DeleteAsync(int id, CancellationToken ct = default);
        List<ParserSecenekDto> GetParserSecenekleri();

        /// <summary>Hesap adından eşleştirme anahtarı önerisi; form yeni hesapta bunu doldurur.</summary>
        string? AnahtarOner(string? hesapAdi, string? bankaAdi);

        /// <summary>
        /// Hesap sahibinin henüz eklenmemiş yazımları. Yüklenmiş ekstrelerin açıklamalarında
        /// unvan desenleriyle yakalanan metinler taranır; tanımlı yazımlarla en az iki ardışık
        /// kelime paylaşan ama kapsama kontrolüne takılmayanlar aday olarak döner.
        /// </summary>
        Task<List<HesapSahibiOnerisiDto>> HesapSahibiOnerileriAsync(int hesapId, CancellationToken ct = default);
    }

    /// <summary>
    /// Banka hesapları CRUD. Hesap aynı zamanda banka kayıt defteridir (Katman 3),
    /// bu yüzden ekstresi olan hesap silinmez, pasife çekilir.
    /// </summary>
    public class BankaHesabiService : IBankaHesabiService
    {
        private readonly CatalogContext _db;
        private readonly IEkstreParserSecici _parserSecici;

        public BankaHesabiService(CatalogContext db, IEkstreParserSecici parserSecici)
        {
            _db = db;
            _parserSecici = parserSecici;
        }

        public async Task<List<BankaHesabiDto>> GetHepsiAsync(bool pasifDahil, CancellationToken ct = default)
        {
            var sorgu = _db.EkstreBankaHesaplari.AsNoTracking();
            if (!pasifDahil) sorgu = sorgu.Where(h => h.Aktif);

            var kayitlar = await sorgu
                .OrderBy(h => h.BankaAdi).ThenBy(h => h.OrkaHesapKodu)
                .ToListAsync(ct);

            return kayitlar.Select(Esle).ToList();
        }

        public async Task<BankaHesabiDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var hesap = await _db.EkstreBankaHesaplari.AsNoTracking()
                .FirstOrDefaultAsync(h => h.Id == id, ct);

            return hesap is null ? null : Esle(hesap);
        }

        public async Task<BankaHesabiDto> CreateAsync(BankaHesabiYazDto dto, CancellationToken ct = default)
        {
            Dogrula(dto);
            await OrkaKoduTekilMi(dto.OrkaHesapKodu, null, ct);

            var hesap = new BankaHesabi();
            Uygula(hesap, dto);

            _db.EkstreBankaHesaplari.Add(hesap);
            await _db.SaveChangesAsync(ct);

            return Esle(hesap);
        }

        public async Task<BankaHesabiDto?> UpdateAsync(int id, BankaHesabiYazDto dto, CancellationToken ct = default)
        {
            Dogrula(dto);

            var hesap = await _db.EkstreBankaHesaplari.FirstOrDefaultAsync(h => h.Id == id, ct);
            if (hesap is null) return null;

            await OrkaKoduTekilMi(dto.OrkaHesapKodu, id, ct);
            Uygula(hesap, dto);

            await _db.SaveChangesAsync(ct);
            return Esle(hesap);
        }

        /// <summary>Ekstresi olan hesap silinmez (geçmiş kayıtların bağı kopar); null = bulunamadı.</summary>
        public async Task<bool?> DeleteAsync(int id, CancellationToken ct = default)
        {
            var hesap = await _db.EkstreBankaHesaplari.FirstOrDefaultAsync(h => h.Id == id, ct);
            if (hesap is null) return null;

            if (await _db.EkstreYuklemeler.AnyAsync(y => y.BankaHesabiId == id, ct))
                return false;

            _db.EkstreBankaHesaplari.Remove(hesap);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public List<ParserSecenekDto> GetParserSecenekleri()
            => _parserSecici.Hepsi
                .Select(p => new ParserSecenekDto { Tip = p.ParserTipi, Ad = p.Ad })
                .ToList();

        public string? AnahtarOner(string? hesapAdi, string? bankaAdi)
            => EslestirmeAnahtari.Oner(hesapAdi, bankaAdi);

        // ---- Yardımcılar ----

        private void Dogrula(BankaHesabiYazDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.BankaAdi))
                throw new BankaEkstreKuralException(nameof(dto.BankaAdi), "Banka adı zorunludur.");

            if (string.IsNullOrWhiteSpace(dto.OrkaHesapKodu))
                throw new BankaEkstreKuralException(nameof(dto.OrkaHesapKodu), "ORKA hesap kodu zorunludur.");

            // Ayrıştırıcı isteğe bağlı: hesapların çoğuna ekstre yüklenmiyor, yalnız karşı
            // hesap olarak bulunabilmek için tanımlılar. Girildiyse tanınmalı.
            if (!string.IsNullOrWhiteSpace(dto.ParserTipi) && _parserSecici.Sec(dto.ParserTipi) is null)
                throw new BankaEkstreKuralException(nameof(dto.ParserTipi),
                    $"Tanımsız ayrıştırıcı: '{dto.ParserTipi}'. Boş bırakılabilir; seçilebilir tipler: " +
                    string.Join(", ", _parserSecici.Hepsi.Select(p => p.ParserTipi)) + ".");
        }

        private async Task OrkaKoduTekilMi(string kod, int? haricId, CancellationToken ct)
        {
            var normal = Normalizasyon.HesapKoduNormalize(kod);
            var cakisiyor = await _db.EkstreBankaHesaplari
                .AnyAsync(h => h.OrkaHesapKodu == normal && (haricId == null || h.Id != haricId), ct);

            if (cakisiyor)
                throw new DuplicateRecordException(nameof(BankaHesabi.OrkaHesapKodu),
                    $"'{normal}' kodlu bir banka hesabı zaten var.");
        }

        private static void Uygula(BankaHesabi hesap, BankaHesabiYazDto dto)
        {
            hesap.BankaAdi = dto.BankaAdi.Trim();
            hesap.HesapAdi = string.IsNullOrWhiteSpace(dto.HesapAdi) ? null : Normalizasyon.Kirp(dto.HesapAdi, 200);
            hesap.EslestirmeAnahtarlari = EslestirmeAnahtari.Duzenle(dto.EslestirmeAnahtarlari);
            hesap.HesapSahibiUnvani = string.IsNullOrWhiteSpace(dto.HesapSahibiUnvani)
                ? null
                : Normalizasyon.Kirp(dto.HesapSahibiUnvani, 200);
            // Takma adlar satır satır saklanır; Kirp boşlukları teke indirdiği için satır
            // sonları korunacak biçimde tek tek temizlenir.
            hesap.HesapSahibiTakmaAdlari = TakmaAdlariDuzenle(dto.HesapSahibiTakmaAdlari);
            hesap.HesapTipi = dto.HesapTipi;
            hesap.ParaBirimi = string.IsNullOrWhiteSpace(dto.ParaBirimi) ? "TRY" : dto.ParaBirimi.Trim().ToUpperInvariant();
            hesap.Iban = string.IsNullOrWhiteSpace(dto.Iban) ? null : dto.Iban.Trim().Replace(" ", string.Empty).ToUpperInvariant();
            // Kod boşluklu saklanır; format değiştirilmez, ORKA tanımaz.
            hesap.OrkaHesapKodu = Normalizasyon.HesapKoduNormalize(dto.OrkaHesapKodu);
            // Boş ayrıştırıcı "yok" demek; "" ile null aynı anlama gelmesin diye null saklanır.
            hesap.ParserTipi = string.IsNullOrWhiteSpace(dto.ParserTipi) ? null : dto.ParserTipi.Trim();
            hesap.Aktif = dto.Aktif;
            // Katmanlar varsayılan kapalı; kullanıcı bilerek açar (bkz. BankaHesabi yorumları).
            hesap.IbanKatmaniAktif = dto.IbanKatmaniAktif;
            hesap.VknKatmaniAktif = dto.VknKatmaniAktif;
        }

        /// <summary>
        /// Takma adları satır satır normalleştirir: boş satırlar atılır, tekrarlar tek kez
        /// kalır, her satır 200 karaktere kırpılır.
        /// </summary>
        private static string? TakmaAdlariDuzenle(string? ham)
        {
            var satirlar = HesapSahibiKimligi.Ayikla(ham)
                .Select(y => Normalizasyon.Kirp(y, 200))
                .Where(y => y.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (satirlar.Count == 0) return null;

            var birlesik = string.Join(Environment.NewLine, satirlar);
            return birlesik.Length <= 1000 ? birlesik : birlesik[..1000];
        }

        public async Task<List<HesapSahibiOnerisiDto>> HesapSahibiOnerileriAsync(int hesapId, CancellationToken ct = default)
        {
            var hesap = await _db.EkstreBankaHesaplari.AsNoTracking().FirstOrDefaultAsync(h => h.Id == hesapId, ct);
            if (hesap is null) return new List<HesapSahibiOnerisiDto>();

            var kimlik = HesapSahibiKimligi.Kur(hesap.HesapSahibiUnvani, hesap.HesapSahibiTakmaAdlari);
            if (kimlik.Bos) return new List<HesapSahibiOnerisiDto>();

            // Kaynak: yüklenmiş ekstrelerde desenlerin çıkardığı unvanlar. Ham açıklamayı
            // baştan taramak yerine çıkarılmış unvanlar kullanılır — bankanın firmayı nasıl
            // yazdığı zaten orada duruyor.
            var unvanlar = await _db.EkstreSatirlari.AsNoTracking()
                .Where(s => s.CikarilanUnvan != null && s.CikarilanUnvan != string.Empty)
                .Select(s => s.CikarilanUnvan!)
                .ToListAsync(ct);

            var sayaclar = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var unvan in unvanlar)
            {
                // Zaten elenen yazımlar öneri değildir.
                if (kimlik.Kendisi(unvan)) continue;
                if (!AyniFirmaOlabilir(kimlik, unvan)) continue;

                sayaclar[unvan] = sayaclar.TryGetValue(unvan, out var adet) ? adet + 1 : 1;
            }

            return sayaclar
                .OrderByDescending(p => p.Value)
                .ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .Select(p => new HesapSahibiOnerisiDto { Yazim = p.Key, Adet = p.Value })
                .ToList();
        }

        /// <summary>
        /// Yazım hesap sahibinin bir varyantı olabilir mi? Ölçüt: tanımlı çekirdeklerden
        /// biriyle <b>en az iki ardışık kelime</b> paylaşması. "ADAY BAĞIMSIZ DENETİM VE
        /// SMMM A.Ş." bu yolla bulunur (paylaşılan dizi "ADAY BAGIMSIZ DENETIM"); rastgele
        /// bir cari ise tek kelime bile paylaşmaz.
        /// </summary>
        private static bool AyniFirmaOlabilir(HesapSahibiKimligi kimlik, string unvan)
        {
            var tokenlar = Normalizasyon.UnvanCekirdek(unvan)
                                        .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokenlar.Length < 2) return false;

            for (var i = 0; i + 2 <= tokenlar.Length; i++)
            {
                var ikili = tokenlar[i] + " " + tokenlar[i + 1];
                if (kimlik.Cekirdekler.Any(c => Normalizasyon.IfadeVarMi(c, ikili))) return true;
            }

            return false;
        }

        private static BankaHesabiDto Esle(BankaHesabi h) => new()
        {
            Id = h.Id,
            BankaAdi = h.BankaAdi,
            HesapAdi = h.HesapAdi,
            EslestirmeAnahtarlari = h.EslestirmeAnahtarlari,
            HesapSahibiUnvani = h.HesapSahibiUnvani,
            HesapSahibiTakmaAdlari = h.HesapSahibiTakmaAdlari,
            HesapTipi = h.HesapTipi,
            ParaBirimi = h.ParaBirimi,
            Iban = h.Iban,
            OrkaHesapKodu = h.OrkaHesapKodu,
            ParserTipi = h.ParserTipi ?? string.Empty,
            Aktif = h.Aktif,
            IbanKatmaniAktif = h.IbanKatmaniAktif,
            VknKatmaniAktif = h.VknKatmaniAktif
        };
    }
}
