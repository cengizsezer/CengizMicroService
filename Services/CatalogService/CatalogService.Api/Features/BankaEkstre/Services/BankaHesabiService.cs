using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
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
        /// Firmanın hesap sahibi kimliği (unvan + diğer yazımlar). Alan hesap satırlarında
        /// durur ama firma bazlıdır; okurken dolu olan ilk hesaptan alınır, takma adlar
        /// tüm hesaplardan birleştirilir.
        /// </summary>
        Task<HesapSahibiKimlikDto> HesapSahibiGetAsync(CancellationToken ct = default);

        /// <summary>
        /// Kimliği firmanın <b>tüm</b> hesaplarına yazar. Tek bir hesaba yazılsaydı ekstresi
        /// başka bir hesaptan işlenen banka firmanın adını tanımaz, kendi unvanını karşı
        /// taraf sanardı.
        /// </summary>
        Task<HesapSahibiKimlikDto> HesapSahibiKaydetAsync(HesapSahibiKimlikYazDto dto, CancellationToken ct = default);

        /// <summary>
        /// Hesap sahibinin henüz eklenmemiş yazımları. Yüklenmiş ekstrelerin açıklamalarında
        /// unvan desenleriyle yakalanan metinler taranır; tanımlı yazımlarla en az iki ardışık
        /// kelime paylaşan ama kapsama kontrolüne takılmayanlar aday olarak döner.
        /// Kimlik firma bazlı okunur; hangi hesabın ekstresinden geldiği fark etmez.
        /// </summary>
        Task<List<HesapSahibiOnerisiDto>> HesapSahibiOnerileriAsync(CancellationToken ct = default);

        /// <summary>
        /// Firmada kullanılan banka adları ve hesap sayıları. Banka adı alanının açılır
        /// listesi buradan beslenir; pasif hesaplar da sayılır (adları düzeltilmeli).
        /// </summary>
        Task<List<BankaAdiDto>> BankaAdlariAsync(CancellationToken ct = default);

        /// <summary>
        /// Aynı bankanın farklı yazımlarını tek ada indirir ("Vakıf Bank Eur",
        /// "Vakıfbank Vadeli" → "Vakıfbank"). Yalnız <c>BankaAdi</c> alanı değişir;
        /// hesaplar, kodlar ve ekstreler olduğu gibi kalır.
        /// </summary>
        Task<BankaAdiBirlestirSonucDto> BankaAdiBirlestirAsync(BankaAdiBirlestirDto dto, CancellationToken ct = default);
    }

    /// <summary>
    /// Banka hesapları CRUD. Hesap aynı zamanda banka kayıt defteridir (Katman 3),
    /// bu yüzden ekstresi olan hesap silinmez, pasife çekilir.
    /// </summary>
    public class BankaHesabiService : IBankaHesabiService
    {
        private readonly CatalogContext _db;
        private readonly IEkstreParserSecici _parserSecici;
        private readonly IBankaFirmaKapsami _kapsam;

        public BankaHesabiService(CatalogContext db, IEkstreParserSecici parserSecici, IBankaFirmaKapsami kapsam)
        {
            _db = db;
            _parserSecici = parserSecici;
            _kapsam = kapsam;
        }

        /// <summary>
        /// Kapsamdaki banka hesapları. Kapsam belirtilmemişse (Aktar ekranının hesap
        /// listesi) tüm firmaların hesapları gelir; süzme her sorguda görünür yazılır.
        /// </summary>
        private IQueryable<BankaHesabi> Hesaplar
            => _db.EkstreBankaHesaplari.FirmayaGore(_kapsam);

        public async Task<List<BankaHesabiDto>> GetHepsiAsync(bool pasifDahil, CancellationToken ct = default)
        {
            var sorgu = Hesaplar.AsNoTracking();
            if (!pasifDahil) sorgu = sorgu.Where(h => h.Aktif);

            var kayitlar = await sorgu
                .OrderBy(h => h.BankaAdi).ThenBy(h => h.OrkaHesapKodu)
                .ToListAsync(ct);

            return kayitlar.Select(Esle).ToList();
        }

        public async Task<List<BankaAdiDto>> BankaAdlariAsync(CancellationToken ct = default)
        {
            // Pasif hesaplar da listelenir: yanlış yazımların bir kısmı pasife çekilmiş
            // hesaplarda duruyor ve birleştirme onları da düzeltmeli.
            var adlar = await Hesaplar.AsNoTracking()
                .Select(h => h.BankaAdi)
                .ToListAsync(ct);

            return adlar
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .GroupBy(a => a.Trim(), StringComparer.Ordinal)
                .Select(g => new BankaAdiDto { Ad = g.Key, HesapSayisi = g.Count() })
                .OrderBy(a => a.Ad, StringComparer.CurrentCulture)
                .ToList();
        }

        /// <summary>
        /// Birleştirme. Karşılaştırma <b>ordinal ve büyük/küçük harf duyarsız</b>: sekme
        /// şeridi ve "aynı banka önceliği" kuralı da tam olarak böyle grupluyor, yani
        /// birleştirmenin etkisi ekranda görülenle birebir aynı.
        /// </summary>
        public async Task<BankaAdiBirlestirSonucDto> BankaAdiBirlestirAsync(BankaAdiBirlestirDto dto,
                                                                            CancellationToken ct = default)
        {
            var hedef = (dto.Hedef ?? string.Empty).Trim();
            if (hedef.Length == 0)
                throw new BankaEkstreKuralException(nameof(dto.Hedef), "Hedef banka adı boş olamaz.");

            if (hedef.Length > 100)
                throw new BankaEkstreKuralException(nameof(dto.Hedef), "Banka adı en fazla 100 karakter olabilir.");

            var kaynaklar = (dto.Kaynaklar ?? new List<string>())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim())
                .Where(k => !string.Equals(k, hedef, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (kaynaklar.Count == 0)
                throw new BankaEkstreKuralException(nameof(dto.Kaynaklar),
                    "Birleştirilecek en az bir farklı yazım seçin.");

            var hesaplar = await Hesaplar.ToListAsync(ct);

            var etkilenen = hesaplar
                .Where(h => kaynaklar.Any(k => string.Equals(h.BankaAdi.Trim(), k, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var hesap in etkilenen) hesap.BankaAdi = hedef;

            if (etkilenen.Count > 0) await _db.SaveChangesAsync(ct);

            return new BankaAdiBirlestirSonucDto
            {
                Hedef = hedef,
                EtkilenenHesap = etkilenen.Count,
                BankaAdlari = await BankaAdlariAsync(ct)
            };
        }

        public async Task<BankaHesabiDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var hesap = await Hesaplar.AsNoTracking()
                .FirstOrDefaultAsync(h => h.Id == id, ct);

            return hesap is null ? null : Esle(hesap);
        }

        public async Task<BankaHesabiDto> CreateAsync(BankaHesabiYazDto dto, CancellationToken ct = default)
        {
            Dogrula(dto);
            await OrkaKoduTekilMi(dto.OrkaHesapKodu, null, ct);

            var hesap = new BankaHesabi { FirmaId = _kapsam.FirmaId };
            Uygula(hesap, dto);

            _db.EkstreBankaHesaplari.Add(hesap);
            await _db.SaveChangesAsync(ct);

            return Esle(hesap);
        }

        public async Task<BankaHesabiDto?> UpdateAsync(int id, BankaHesabiYazDto dto, CancellationToken ct = default)
        {
            Dogrula(dto);

            var hesap = await Hesaplar.FirstOrDefaultAsync(h => h.Id == id, ct);
            if (hesap is null) return null;

            await OrkaKoduTekilMi(dto.OrkaHesapKodu, id, ct);
            Uygula(hesap, dto);

            await _db.SaveChangesAsync(ct);
            return Esle(hesap);
        }

        /// <summary>Ekstresi olan hesap silinmez (geçmiş kayıtların bağı kopar); null = bulunamadı.</summary>
        public async Task<bool?> DeleteAsync(int id, CancellationToken ct = default)
        {
            var hesap = await Hesaplar.FirstOrDefaultAsync(h => h.Id == id, ct);
            if (hesap is null) return null;

            if (await _db.EkstreYuklemeler.AnyAsync(y => y.FirmaId == _kapsam.FirmaId && y.BankaHesabiId == id, ct))
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
            var cakisiyor = await Hesaplar
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

        public async Task<HesapSahibiKimlikDto> HesapSahibiGetAsync(CancellationToken ct = default)
        {
            var hesaplar = await Hesaplar.AsNoTracking()
                .OrderBy(h => h.Id)
                .Select(h => new { h.HesapSahibiUnvani, h.HesapSahibiTakmaAdlari })
                .ToListAsync(ct);

            // Unvan: dolu olan ilk hesap. Takma adlar birleştirilir — eski kurulumda farklı
            // hesaplara farklı yazımlar girilmiş olabilir, hiçbiri kaybolmasın.
            var takmaAdlar = hesaplar
                .SelectMany(h => HesapSahibiKimligi.Ayikla(h.HesapSahibiTakmaAdlari))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new HesapSahibiKimlikDto
            {
                Unvan = hesaplar.Select(h => h.HesapSahibiUnvani)
                                .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u)),
                TakmaAdlar = takmaAdlar.Count == 0 ? null : string.Join(Environment.NewLine, takmaAdlar),
                HesapSayisi = hesaplar.Count
            };
        }

        public async Task<HesapSahibiKimlikDto> HesapSahibiKaydetAsync(HesapSahibiKimlikYazDto dto,
                                                                      CancellationToken ct = default)
        {
            var unvan = string.IsNullOrWhiteSpace(dto.Unvan) ? null : Normalizasyon.Kirp(dto.Unvan, 200);
            var takmaAdlar = TakmaAdlariDuzenle(dto.TakmaAdlar);

            var hesaplar = await Hesaplar.ToListAsync(ct);
            foreach (var hesap in hesaplar)
            {
                hesap.HesapSahibiUnvani = unvan;
                hesap.HesapSahibiTakmaAdlari = takmaAdlar;
            }

            await _db.SaveChangesAsync(ct);

            return new HesapSahibiKimlikDto
            {
                Unvan = unvan,
                TakmaAdlar = takmaAdlar,
                HesapSayisi = hesaplar.Count
            };
        }

        public async Task<List<HesapSahibiOnerisiDto>> HesapSahibiOnerileriAsync(CancellationToken ct = default)
        {
            var mevcut = await HesapSahibiGetAsync(ct);

            var kimlik = HesapSahibiKimligi.Kur(mevcut.Unvan, mevcut.TakmaAdlar);
            if (kimlik.Bos) return new List<HesapSahibiOnerisiDto>();

            // Kaynak: yüklenmiş ekstrelerde desenlerin çıkardığı unvanlar. Ham açıklamayı
            // baştan taramak yerine çıkarılmış unvanlar kullanılır — bankanın firmayı nasıl
            // yazdığı zaten orada duruyor.
            // Satırın kendi FirmaId'si yok; kapsamı bağlı olduğu yüklemeden gelir.
            var kapsamliYuklemeler = _db.EkstreYuklemeler.FirmayaGore(_kapsam);

            var unvanlar = await _db.EkstreSatirlari.AsNoTracking()
                .Where(s => s.CikarilanUnvan != null && s.CikarilanUnvan != string.Empty)
                .Where(s => kapsamliYuklemeler.Any(y => y.Id == s.EkstreYuklemeId))
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
            FirmaId = h.FirmaId,
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
