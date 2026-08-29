using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Features.FirmaBilgileri.Domain;
using CatalogService.Api.Features.FirmaBilgileri.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.FirmaBilgileri.Services
{
    /// <summary>Firma Bilgileri modülünün iş kuralı ihlalleri; controller 400'e çevirir.</summary>
    public class FirmaBilgiKuralException : Exception
    {
        public string Field { get; }

        public FirmaBilgiKuralException(string field, string message) : base(message) => Field = field;
    }

    public interface IFirmaBilgiService
    {
        Task<FirmaSicilDto> SicilGetAsync(CancellationToken ct = default);
        Task<FirmaSicilDto> SicilKaydetAsync(FirmaSicilDto dto, CancellationToken ct = default);

        Task<FirmaOrtaklikDto> OrtaklarGetAsync(CancellationToken ct = default);
        Task<FirmaOrtaklikDto> OrtaklarKaydetAsync(List<FirmaOrtakDto> ortaklar, CancellationToken ct = default);

        Task<List<FirmaImzaYetkilisiDto>> YetkililerGetAsync(CancellationToken ct = default);
        Task<List<FirmaImzaYetkilisiDto>> YetkililerKaydetAsync(List<FirmaImzaYetkilisiDto> yetkililer,
                                                                CancellationToken ct = default);

        Task<List<FirmaBelgesiDto>> BelgelerGetAsync(CancellationToken ct = default);
        Task<FirmaBelgesiDto> BelgeEkleAsync(FirmaBelgesiOlusturDto istek, string? kullanici,
                                             CancellationToken ct = default);
        Task<int> BelgeSilAsync(int belgeId, CancellationToken ct = default);
    }

    /// <summary>
    /// Firma Bilgileri: sicil, ortaklık, imza yetkilileri ve belgeler.
    ///
    /// <b>Kapsam Banka Otomasyon'daki mekanizmanın aynısı</b>: firma isteğin
    /// <c>?firmaId=</c> parametresinden gelir, <see cref="IBankaFirmaKapsami"/> içinde
    /// taşınır ve <b>her sorguda görünür biçimde</b> yazılır — global query filter yok
    /// (bkz. KARARLAR §68–§72). Arayüzün adı tarihsel; mekanizma bankaya özel değil ve
    /// ikinci bir kopyası çıkarılmadı (KARARLAR §94).
    ///
    /// Her bölüm ayrı kaydedilir: sicil formu ortaklık tablosunu, ortaklık tablosu imza
    /// yetkililerini etkilemez. Tek büyük form olsaydı bir bölümdeki doğrulama hatası
    /// diğerlerinin kaydını da engellerdi.
    /// </summary>
    public class FirmaBilgiService : IFirmaBilgiService
    {
        /// <summary>Tek belge için üst sınır; beyanname ekleriyle aynı.</summary>
        public const long EnFazlaBayt = 20 * 1024 * 1024;

        public const string PdfTuru = "application/pdf";

        private readonly CatalogContext _db;
        private readonly IBankaFirmaKapsami _kapsam;

        public FirmaBilgiService(CatalogContext db, IBankaFirmaKapsami kapsam)
        {
            _db = db;
            _kapsam = kapsam;
        }

        private int FirmaId => _kapsam.Secili
            ? _kapsam.FirmaId
            : throw new FirmaBilgiKuralException("firmaId", "Firma seçilmeden firma bilgileri okunamaz.");

        // ---- Sicil ----

        public async Task<FirmaSicilDto> SicilGetAsync(CancellationToken ct = default)
        {
            var firmaId = FirmaId;

            var firma = await _db.Firmalar.AsNoTracking().FirstOrDefaultAsync(f => f.Id == firmaId, ct)
                        ?? throw new FirmaBilgiKuralException("firmaId", "Firma bulunamadı.");

            var sicil = await _db.FirmaSicilBilgileri.AsNoTracking()
                .FirstOrDefaultAsync(s => s.FirmaId == firmaId, ct);

            return new FirmaSicilDto
            {
                FirmaId = firmaId,
                Unvan = firma.Unvan,
                VergiKimlikNo = firma.VergiKimlikNo,
                VergiDairesi = firma.VergiDairesi,
                TicaretSicilNo = firma.TicaretSicilNo,
                Email = firma.Email,
                Telefon = firma.Telefon,
                MersisNo = sicil?.MersisNo,
                KurulusTarihi = sicil?.KurulusTarihi,
                Adres = sicil?.Adres,
                NaceKodu = sicil?.NaceKodu,
                Sermaye = sicil?.Sermaye,
                SermayeParaBirimi = sicil?.SermayeParaBirimi ?? "TRY"
            };
        }

        public async Task<FirmaSicilDto> SicilKaydetAsync(FirmaSicilDto dto, CancellationToken ct = default)
        {
            var firmaId = FirmaId;

            var firma = await _db.Firmalar.FirstOrDefaultAsync(f => f.Id == firmaId, ct)
                        ?? throw new FirmaBilgiKuralException("firmaId", "Firma bulunamadı.");

            if (string.IsNullOrWhiteSpace(dto.Unvan))
                throw new FirmaBilgiKuralException(nameof(dto.Unvan), "Unvan zorunlu.");

            var vkn = Rakamlar(dto.VergiKimlikNo);
            if (vkn.Length is not (10 or 11))
                throw new FirmaBilgiKuralException(nameof(dto.VergiKimlikNo),
                    "Vergi kimlik no 10 hane (tüzel) ya da 11 hane (gerçek kişi) olmalı.");

            if (dto.Sermaye is < 0)
                throw new FirmaBilgiKuralException(nameof(dto.Sermaye), "Sermaye negatif olamaz.");

            // catalog.Firmalar'daki alanlar orada güncellenir; kopyalanmaz.
            firma.Unvan = dto.Unvan.Trim();
            firma.VergiKimlikNo = vkn;
            firma.VergiDairesi = dto.VergiDairesi?.Trim() ?? string.Empty;
            firma.TicaretSicilNo = dto.TicaretSicilNo?.Trim() ?? string.Empty;
            firma.Email = dto.Email?.Trim() ?? string.Empty;
            firma.Telefon = dto.Telefon?.Trim() ?? string.Empty;
            firma.UpdatedAt = DateTime.Now;

            var sicil = await _db.FirmaSicilBilgileri.FirstOrDefaultAsync(s => s.FirmaId == firmaId, ct);
            if (sicil is null)
            {
                sicil = new FirmaSicilBilgisi { FirmaId = firmaId };
                _db.FirmaSicilBilgileri.Add(sicil);
            }

            sicil.MersisNo = Bos(dto.MersisNo);
            sicil.KurulusTarihi = dto.KurulusTarihi;
            sicil.Adres = Bos(dto.Adres);
            sicil.NaceKodu = Bos(dto.NaceKodu);
            sicil.Sermaye = dto.Sermaye;
            sicil.SermayeParaBirimi = Bos(dto.SermayeParaBirimi) ?? "TRY";
            sicil.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync(ct);

            return await SicilGetAsync(ct);
        }

        // ---- Ortaklık ----

        public async Task<FirmaOrtaklikDto> OrtaklarGetAsync(CancellationToken ct = default)
        {
            var firmaId = FirmaId;

            var ortaklar = await _db.FirmaOrtaklari.AsNoTracking()
                .Where(o => o.FirmaId == firmaId)
                .OrderBy(o => o.Sira).ThenBy(o => o.Id)
                .ToListAsync(ct);

            return Ortaklik(ortaklar);
        }

        /// <summary>
        /// Ortaklık tablosunu bütün olarak kaydeder: gönderilmeyen satır silinir.
        /// Satır satır kaydetmek, ekranda silinen bir ortağın veritabanında kalmasına
        /// (ve toplam pay oranının tutmamasına) yol açıyordu.
        /// </summary>
        public async Task<FirmaOrtaklikDto> OrtaklarKaydetAsync(List<FirmaOrtakDto> ortaklar,
                                                                CancellationToken ct = default)
        {
            var firmaId = FirmaId;

            foreach (var o in ortaklar)
            {
                if (string.IsNullOrWhiteSpace(o.Ad))
                    throw new FirmaBilgiKuralException(nameof(o.Ad), "Ortak adı zorunlu.");

                if (o.PayOrani is < 0 or > 100)
                    throw new FirmaBilgiKuralException(nameof(o.PayOrani), "Pay oranı 0 ile 100 arasında olmalı.");

                if (o.PayTutari < 0)
                    throw new FirmaBilgiKuralException(nameof(o.PayTutari), "Pay tutarı negatif olamaz.");

                var kimlik = Rakamlar(o.TcknVkn);
                if (kimlik.Length > 0 && kimlik.Length is not (10 or 11))
                    throw new FirmaBilgiKuralException(nameof(o.TcknVkn),
                        $"'{o.Ad}' için TCKN 11, VKN 10 hane olmalı.");
            }

            var mevcut = await _db.FirmaOrtaklari.Where(o => o.FirmaId == firmaId).ToListAsync(ct);
            var kalanIdler = ortaklar.Where(o => o.Id > 0).Select(o => o.Id).ToHashSet();

            foreach (var silinen in mevcut.Where(m => !kalanIdler.Contains(m.Id)))
                _db.FirmaOrtaklari.Remove(silinen);

            var sira = 0;
            foreach (var dto in ortaklar)
            {
                sira += 10;

                var kayit = dto.Id > 0 ? mevcut.FirstOrDefault(m => m.Id == dto.Id) : null;
                if (dto.Id > 0 && kayit is null)
                    throw new FirmaBilgiKuralException(nameof(dto.Id),
                        "Ortak kaydı bu firmada bulunamadı; sayfayı yenileyip tekrar deneyin.");

                if (kayit is null)
                {
                    kayit = new FirmaOrtak { FirmaId = firmaId };
                    _db.FirmaOrtaklari.Add(kayit);
                }

                kayit.Ad = dto.Ad.Trim();
                kayit.TcknVkn = Bos(Rakamlar(dto.TcknVkn));
                kayit.PayTutari = dto.PayTutari;
                kayit.PayOrani = dto.PayOrani;
                kayit.BaslangicTarihi = dto.BaslangicTarihi;
                kayit.Not = Bos(dto.Not);
                kayit.Sira = sira;
                kayit.UpdatedAt = DateTime.Now;
            }

            await _db.SaveChangesAsync(ct);
            return await OrtaklarGetAsync(ct);
        }

        /// <summary>Toplamlar ve %100 uyarısı. Uyarı kaydı engellemez; ekran gösterir.</summary>
        public static FirmaOrtaklikDto Ortaklik(IReadOnlyList<FirmaOrtak> ortaklar)
        {
            var dto = new FirmaOrtaklikDto
            {
                Ortaklar = ortaklar.Select(o => new FirmaOrtakDto
                {
                    Id = o.Id,
                    Ad = o.Ad,
                    TcknVkn = o.TcknVkn,
                    PayTutari = o.PayTutari,
                    PayOrani = o.PayOrani,
                    BaslangicTarihi = o.BaslangicTarihi,
                    Not = o.Not,
                    Sira = o.Sira
                }).ToList()
            };

            dto.ToplamPayTutari = ortaklar.Sum(o => o.PayTutari);
            dto.ToplamPayOrani = ortaklar.Sum(o => o.PayOrani);

            // Ortak yoksa uyarı da yok: "toplam %0" boş tabloda anlamsız bir alarm olurdu.
            // Kuruş farklarını uyarıya çevirmemek için 0,01 tolerans var.
            dto.PayOraniUyarisi = ortaklar.Count > 0 && Math.Abs(dto.ToplamPayOrani - 100m) > 0.01m;

            return dto;
        }

        // ---- İmza yetkilileri ----

        public async Task<List<FirmaImzaYetkilisiDto>> YetkililerGetAsync(CancellationToken ct = default)
        {
            var firmaId = FirmaId;

            var yetkililer = await _db.FirmaImzaYetkilileri.AsNoTracking()
                .Where(y => y.FirmaId == firmaId)
                .OrderBy(y => y.Sira).ThenBy(y => y.Id)
                .ToListAsync(ct);

            return yetkililer.Select(y => Dto(y, DateTime.Today)).ToList();
        }

        public async Task<List<FirmaImzaYetkilisiDto>> YetkililerKaydetAsync(
            List<FirmaImzaYetkilisiDto> yetkililer, CancellationToken ct = default)
        {
            var firmaId = FirmaId;

            foreach (var y in yetkililer)
            {
                if (string.IsNullOrWhiteSpace(y.Ad))
                    throw new FirmaBilgiKuralException(nameof(y.Ad), "Yetkili adı zorunlu.");

                var tckn = Rakamlar(y.Tckn);
                if (tckn.Length > 0 && tckn.Length != 11)
                    throw new FirmaBilgiKuralException(nameof(y.Tckn), $"'{y.Ad}' için TCKN 11 hane olmalı.");

                if (y.YetkiBaslangic is { } bas && y.YetkiBitis is { } bit && bit < bas)
                    throw new FirmaBilgiKuralException(nameof(y.YetkiBitis),
                        $"'{y.Ad}' için yetki bitişi başlangıçtan önce olamaz.");

                if (!Enum.IsDefined(typeof(TemsilSekli), y.TemsilSekli))
                    throw new FirmaBilgiKuralException(nameof(y.TemsilSekli), "Geçersiz temsil şekli.");
            }

            var mevcut = await _db.FirmaImzaYetkilileri.Where(y => y.FirmaId == firmaId).ToListAsync(ct);
            var kalanIdler = yetkililer.Where(y => y.Id > 0).Select(y => y.Id).ToHashSet();

            foreach (var silinen in mevcut.Where(m => !kalanIdler.Contains(m.Id)))
                _db.FirmaImzaYetkilileri.Remove(silinen);

            var sira = 0;
            foreach (var dto in yetkililer)
            {
                sira += 10;

                var kayit = dto.Id > 0 ? mevcut.FirstOrDefault(m => m.Id == dto.Id) : null;
                if (dto.Id > 0 && kayit is null)
                    throw new FirmaBilgiKuralException(nameof(dto.Id),
                        "Yetkili kaydı bu firmada bulunamadı; sayfayı yenileyip tekrar deneyin.");

                if (kayit is null)
                {
                    kayit = new FirmaImzaYetkilisi { FirmaId = firmaId };
                    _db.FirmaImzaYetkilileri.Add(kayit);
                }

                kayit.Ad = dto.Ad.Trim();
                kayit.Tckn = Bos(Rakamlar(dto.Tckn));
                kayit.Gorev = Bos(dto.Gorev);
                kayit.TemsilSekli = dto.TemsilSekli;
                kayit.YetkiBaslangic = dto.YetkiBaslangic;
                kayit.YetkiBitis = dto.YetkiBitis;
                kayit.Not = Bos(dto.Not);
                kayit.Sira = sira;
                kayit.UpdatedAt = DateTime.Now;
            }

            await _db.SaveChangesAsync(ct);
            return await YetkililerGetAsync(ct);
        }

        /// <summary>
        /// Yetkilinin DTO'su. <see cref="FirmaImzaYetkilisiDto.SuresiDoldu"/> sunucuda
        /// hesaplanıyor: istemcinin saatine bırakılsaydı iki kullanıcı aynı kaydı farklı
        /// görebilirdi.
        /// </summary>
        public static FirmaImzaYetkilisiDto Dto(FirmaImzaYetkilisi y, DateTime bugun) => new()
        {
            Id = y.Id,
            Ad = y.Ad,
            Tckn = y.Tckn,
            Gorev = y.Gorev,
            TemsilSekli = y.TemsilSekli,
            YetkiBaslangic = y.YetkiBaslangic,
            YetkiBitis = y.YetkiBitis,
            Not = y.Not,
            Sira = y.Sira,
            // Bitiş tarihi boşsa yetki süresizdir; bitiş GÜNÜ dahil geçerli sayılır.
            SuresiDoldu = y.YetkiBitis is { } bitis && bitis.Date < bugun.Date
        };

        // ---- Belgeler ----

        public async Task<List<FirmaBelgesiDto>> BelgelerGetAsync(CancellationToken ct = default)
        {
            var firmaId = FirmaId;

            return await _db.FirmaBelgeleri.AsNoTracking()
                .Where(b => b.FirmaId == firmaId)
                .OrderBy(b => b.Tur).ThenByDescending(b => b.CreatedAt)
                .Select(b => new FirmaBelgesiDto
                {
                    Id = b.Id,
                    Tur = b.Tur,
                    FileId = b.FileId,
                    FileName = b.FileName,
                    ContentType = b.ContentType,
                    Length = b.Length,
                    Aciklama = b.Aciklama,
                    CreatedAt = b.CreatedAt,
                    YukleyenKullanici = b.YukleyenKullanici
                })
                .ToListAsync(ct);
        }

        public async Task<FirmaBelgesiDto> BelgeEkleAsync(FirmaBelgesiOlusturDto istek, string? kullanici,
                                                          CancellationToken ct = default)
        {
            var firmaId = FirmaId;

            if (!Enum.IsDefined(typeof(FirmaBelgeTuru), istek.Tur))
                throw new FirmaBilgiKuralException(nameof(istek.Tur), "Geçersiz belge türü.");

            if (istek.FileId <= 0)
                throw new FirmaBilgiKuralException(nameof(istek.FileId),
                    "Dosya kimliği yok. Dosya önce FileApiService'e yüklenmeli.");

            if (!PdfMi(istek.ContentType, istek.FileName))
                throw new FirmaBilgiKuralException(nameof(istek.ContentType), "Yalnız PDF belge eklenebilir.");

            if (istek.Length <= 0)
                throw new FirmaBilgiKuralException(nameof(istek.Length), "Boş dosya eklenemez.");

            if (istek.Length > EnFazlaBayt)
                throw new FirmaBilgiKuralException(nameof(istek.Length),
                    $"Dosya {EnFazlaBayt / (1024 * 1024)} MB sınırını aşıyor.");

            // Aynı türden ikinci belge eskisinin yerine GEÇMEZ: vergi levhası her yıl
            // yenileniyor ve eskisi kayıtta kalmalı (beyanname eklerinden farkı bu).
            var kayit = new FirmaBelgesi
            {
                FirmaId = firmaId,
                Tur = istek.Tur,
                FileId = istek.FileId,
                FileName = Kirp(istek.FileName, 260),
                ContentType = PdfTuru,
                Length = istek.Length,
                Aciklama = Bos(istek.Aciklama),
                CreatedAt = DateTime.Now,
                YukleyenKullanici = Kirp(kullanici, 100)
            };

            _db.FirmaBelgeleri.Add(kayit);
            await _db.SaveChangesAsync(ct);

            return new FirmaBelgesiDto
            {
                Id = kayit.Id,
                Tur = kayit.Tur,
                FileId = kayit.FileId,
                FileName = kayit.FileName,
                ContentType = kayit.ContentType,
                Length = kayit.Length,
                Aciklama = kayit.Aciklama,
                CreatedAt = kayit.CreatedAt,
                YukleyenKullanici = kayit.YukleyenKullanici
            };
        }

        public async Task<int> BelgeSilAsync(int belgeId, CancellationToken ct = default)
        {
            var firmaId = FirmaId;

            var belge = await _db.FirmaBelgeleri.FirstOrDefaultAsync(b => b.Id == belgeId && b.FirmaId == firmaId, ct)
                        ?? throw new FirmaBilgiKuralException(nameof(belgeId), "Belge bulunamadı.");

            var fileId = belge.FileId;

            _db.FirmaBelgeleri.Remove(belge);
            await _db.SaveChangesAsync(ct);

            return fileId;
        }

        // ---- Yardımcılar ----

        private static bool PdfMi(string? contentType, string? fileName)
        {
            if (!string.IsNullOrWhiteSpace(contentType) &&
                contentType.Trim().StartsWith(PdfTuru, StringComparison.OrdinalIgnoreCase))
                return true;

            return !string.IsNullOrWhiteSpace(fileName) &&
                   fileName.Trim().EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        }

        private static string Rakamlar(string? metin)
            => string.IsNullOrWhiteSpace(metin) ? string.Empty : new string(metin.Where(char.IsDigit).ToArray());

        private static string? Bos(string? metin)
            => string.IsNullOrWhiteSpace(metin) ? null : metin.Trim();

        private static string Kirp(string? metin, int enFazla)
        {
            if (string.IsNullOrWhiteSpace(metin)) return string.Empty;
            var temiz = metin.Trim();
            return temiz.Length <= enFazla ? temiz : temiz[..enFazla];
        }
    }
}
