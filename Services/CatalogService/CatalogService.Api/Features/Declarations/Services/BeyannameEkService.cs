using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Features.Declarations.Entities;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Declarations.Services
{
    /// <summary>
    /// Ek kaydı sonucu. <see cref="ArtikFileId"/> dolu geldiğinde çağıran, o dosyayı
    /// FileApiService'ten <b>silmelidir</b>: aynı türden eski bir belge değiştirildi ve
    /// artık hiçbir kayıt onu göstermiyor.
    ///
    /// Silme neden burada yapılmıyor? CatalogService dosyaları tutmuyor; FileApiService
    /// ayrı bir servis ve bu modülün ona giden bir istemcisi yok. Repodaki mevcut kalıp da
    /// aynı: yükleyen taraf (istemci) telafi silmesini kendisi yapıyor
    /// (<c>AddAppointmentPage</c>).
    /// </summary>
    public sealed record BeyannameEkSonuc(BeyannameEkDto Ek, int? ArtikFileId);

    public interface IBeyannameEkService
    {
        Task<List<BeyannameEkDto>> GetAsync(int declarationId, CancellationToken ct = default);

        Task<BeyannameEkSonuc> EkleAsync(int declarationId, BeyannameEkOlusturDto istek,
                                         string? kullanici, CancellationToken ct = default);

        /// <summary>Kaydı siler ve artık sahipsiz kalan FileApiService dosyasının kimliğini döner.</summary>
        Task<int> SilAsync(int declarationId, int ekId, CancellationToken ct = default);
    }

    public class BeyannameEkService : IBeyannameEkService
    {
        /// <summary>Tek belge için üst sınır. İstemci de aynı sınırla yüklüyor; burada kesin karar veriliyor.</summary>
        public const long EnFazlaBayt = 20 * 1024 * 1024;

        public const string PdfTuru = "application/pdf";

        private readonly CatalogContext _db;

        public BeyannameEkService(CatalogContext db) => _db = db;

        public async Task<List<BeyannameEkDto>> GetAsync(int declarationId, CancellationToken ct = default)
            => await _db.BeyannameEkleri.AsNoTracking()
                .Where(e => e.DeclarationId == declarationId)
                .OrderBy(e => e.Tur)
                .Select(e => Dto(e))
                .ToListAsync(ct);

        public async Task<BeyannameEkSonuc> EkleAsync(int declarationId, BeyannameEkOlusturDto istek,
                                                      string? kullanici, CancellationToken ct = default)
        {
            var beyanname = await _db.Declarations.FirstOrDefaultAsync(d => d.Id == declarationId, ct)
                            ?? throw new BeyannameKuralException(nameof(declarationId),
                                "Beyanname kaydı bulunamadı; belge eklenemedi.");

            Dogrula(istek, beyanname);

            // Aynı türden ikinci bir belge yeni satır AÇMAZ, eskisinin yerine geçer:
            // ikonun hangi dosyayı açacağı belirsiz kalmasın.
            var mevcut = await _db.BeyannameEkleri
                .FirstOrDefaultAsync(e => e.DeclarationId == declarationId && e.Tur == istek.Tur, ct);

            int? artik = null;

            if (mevcut is not null)
            {
                if (mevcut.FileId != istek.FileId) artik = mevcut.FileId;

                mevcut.FileId = istek.FileId;
                mevcut.FileName = Kirp(istek.FileName, 260);
                mevcut.ContentType = PdfTuru;
                mevcut.Length = istek.Length;
                mevcut.CreatedAt = DateTime.Now;
                mevcut.YukleyenKullanici = Kirp(kullanici, 100);

                await _db.SaveChangesAsync(ct);
                return new BeyannameEkSonuc(Dto(mevcut), artik);
            }

            var yeni = new BeyannameEk
            {
                DeclarationId = declarationId,
                Tur = istek.Tur,
                FileId = istek.FileId,
                FileName = Kirp(istek.FileName, 260),
                ContentType = PdfTuru,
                Length = istek.Length,
                CreatedAt = DateTime.Now,
                YukleyenKullanici = Kirp(kullanici, 100)
            };

            _db.BeyannameEkleri.Add(yeni);
            await _db.SaveChangesAsync(ct);

            return new BeyannameEkSonuc(Dto(yeni), artik);
        }

        public async Task<int> SilAsync(int declarationId, int ekId, CancellationToken ct = default)
        {
            var ek = await _db.BeyannameEkleri
                         .FirstOrDefaultAsync(e => e.Id == ekId && e.DeclarationId == declarationId, ct)
                     ?? throw new BeyannameKuralException(nameof(ekId), "Belge kaydı bulunamadı.");

            var fileId = ek.FileId;

            _db.BeyannameEkleri.Remove(ek);
            await _db.SaveChangesAsync(ct);

            return fileId;
        }

        /// <summary>
        /// Belge kuralları. Doğrulama <b>sunucuda</b> yapılıyor: istemcideki kontrol
        /// kullanıcıya hızlı geri bildirim için, kaydın doğruluğu için değil.
        /// </summary>
        private static void Dogrula(BeyannameEkOlusturDto istek, Declaration beyanname)
        {
            if (!Enum.IsDefined(typeof(BeyannameEkTuru), istek.Tur))
                throw new BeyannameKuralException(nameof(istek.Tur), "Geçersiz belge türü.");

            if (istek.FileId <= 0)
                throw new BeyannameKuralException(nameof(istek.FileId),
                    "Dosya kimliği yok. Dosya önce FileApiService'e yüklenmeli.");

            if (!PdfMi(istek))
                throw new BeyannameKuralException(nameof(istek.ContentType),
                    "Yalnız PDF belge eklenebilir.");

            if (istek.Length <= 0)
                throw new BeyannameKuralException(nameof(istek.Length), "Boş dosya eklenemez.");

            if (istek.Length > EnFazlaBayt)
                throw new BeyannameKuralException(nameof(istek.Length),
                    $"Dosya {EnFazlaBayt / (1024 * 1024)} MB sınırını aşıyor.");

            // Dekont ödemenin belgesi: ödenmemiş bir kayıtta istenmiyor ve kabul de edilmiyor.
            // Aksi hâlde "ödendi" işaretlenmeden dekont yüklenir, matris ödenmemiş görünür
            // ve iki gösterge birbirini tutmazdı.
            if (istek.Tur == BeyannameEkTuru.Dekont && beyanname.PaymentStatus != PaymentStatus.Paid)
                throw new BeyannameKuralException(nameof(istek.Tur),
                    "Dekont yalnız ödendi işaretli beyannameye eklenebilir. Önce ödeme durumunu güncelleyin.");
        }

        /// <summary>
        /// Dosya PDF mi? Önce içerik tipine, o güvenilmezse (tarayıcı bazen
        /// <c>application/octet-stream</c> gönderiyor) dosya adının uzantısına bakılır.
        /// </summary>
        private static bool PdfMi(BeyannameEkOlusturDto istek)
        {
            if (!string.IsNullOrWhiteSpace(istek.ContentType) &&
                istek.ContentType.Trim().StartsWith(PdfTuru, StringComparison.OrdinalIgnoreCase))
                return true;

            return !string.IsNullOrWhiteSpace(istek.FileName) &&
                   istek.FileName.Trim().EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        }

        private static string Kirp(string? metin, int enFazla)
        {
            if (string.IsNullOrWhiteSpace(metin)) return string.Empty;
            var temiz = metin.Trim();
            return temiz.Length <= enFazla ? temiz : temiz[..enFazla];
        }

        private static BeyannameEkDto Dto(BeyannameEk ek) => new()
        {
            Id = ek.Id,
            DeclarationId = ek.DeclarationId,
            Tur = ek.Tur,
            FileId = ek.FileId,
            FileName = ek.FileName,
            ContentType = ek.ContentType,
            Length = ek.Length,
            CreatedAt = ek.CreatedAt,
            YukleyenKullanici = ek.YukleyenKullanici
        };
    }
}
