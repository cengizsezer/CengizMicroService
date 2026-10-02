using CatalogService.Api.Features.Anasayfa.Domain;
using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Infrastructure.Auth;
using CatalogService.Api.Infrastructure.Context;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Anasayfa.Services
{
    /// <summary>Anasayfa künye yazmalarının iş kuralı ihlali; controller 400'e çevirir.</summary>
    public class FirmaKunyeKuralException : Exception
    {
        public string Field { get; }

        public FirmaKunyeKuralException(string field, string message) : base(message) => Field = field;
    }

    public interface IFirmaKunyeService
    {
        Task SiniflandirmaKaydetAsync(int firmaId, FirmaSiniflandirmaKaydetDto dto, CancellationToken ct = default);

        Task SorumluAtaAsync(int firmaId, FirmaSorumluAtaDto dto, CancellationToken ct = default);

        Task<List<FirmaNotuDto>> NotlarAsync(int firmaId, CancellationToken ct = default);
        Task<FirmaNotuDto> NotEkleAsync(int firmaId, FirmaNotuEkleDto dto, CancellationToken ct = default);

        /// <summary>Yalnız notu yazan silebilir; başkasının notunda <see cref="UnauthorizedAccessException"/>.</summary>
        Task NotSilAsync(int firmaId, long notId, CancellationToken ct = default);

        Task<List<FirmaOlayDto>> OlaylarAsync(int firmaId, CancellationToken ct = default);
    }

    /// <summary>
    /// Anasayfa künyesinin kendi yazma uçları. Firma, isteğin rotasındaki kayıttan gelir
    /// ("aktif firma" yok — bkz. KARARLAR §99); kapsamı <c>BankaFirmaFiltresi</c> doğrular.
    /// </summary>
    public class FirmaKunyeService : IFirmaKunyeService
    {
        public const int NotEnFazla = 2000;

        private readonly CatalogContext _db;
        private readonly IFirmaOlayYazici? _olay;
        private readonly IHttpCurrentUser? _kullanici;

        public FirmaKunyeService(CatalogContext db, IFirmaOlayYazici? olay = null, IHttpCurrentUser? kullanici = null)
        {
            _db = db;
            _olay = olay;
            _kullanici = kullanici;
        }

        private Task OlayAsync(int firmaId, FirmaOlayTipi tip, string aciklama, CancellationToken ct)
            => _olay?.YazAsync(firmaId, tip, aciklama, ct) ?? Task.CompletedTask;

        /// <summary>
        /// Sınıflandırmayı kaydeder. Değeri <b>değişen</b> defter usulü / vergi türü "elle"
        /// işaretlenir ve sonraki mükellefiyet okuması üzerine yazmaz; değişmeyen alan
        /// otomatik kalır (formu açıp kapatmak öneriyi kilitlemesin).
        /// </summary>
        public async Task SiniflandirmaKaydetAsync(int firmaId, FirmaSiniflandirmaKaydetDto dto,
                                                   CancellationToken ct = default)
        {
            if (!Enum.IsDefined(dto.DefterUsulu))
                throw new FirmaKunyeKuralException(nameof(dto.DefterUsulu), "Geçersiz defter usulü.");

            if (!Enum.IsDefined(dto.VergiTuru))
                throw new FirmaKunyeKuralException(nameof(dto.VergiTuru), "Geçersiz vergi türü.");

            if (MukellefiyetSiniflandirici.KisitIhlali(dto.VergiTuru, dto.DefterUsulu))
                throw new FirmaKunyeKuralException(nameof(dto.DefterUsulu), MukellefiyetSiniflandirici.KisitMesaji);

            var format = string.IsNullOrWhiteSpace(dto.MizanFormati) ? null : dto.MizanFormati.Trim();
            if (!MizanFormatlari.Gecerli(format))
                throw new FirmaKunyeKuralException(nameof(dto.MizanFormati), "Mizan formatı listede yok.");

            if (dto.HesapDonemi is { } hd && !Enum.IsDefined(hd))
                throw new FirmaKunyeKuralException(nameof(dto.HesapDonemi), "Geçersiz hesap dönemi.");

            if (!Enum.IsDefined(dto.FirmaTipi))
                throw new FirmaKunyeKuralException(nameof(dto.FirmaTipi), "Geçersiz firma tipi.");
            if (!Enum.IsDefined(dto.SgkTesvikKademesi))
                throw new FirmaKunyeKuralException(nameof(dto.SgkTesvikKademesi), "Geçersiz SGK teşvik kademesi.");
            if (!Enum.IsDefined(dto.MuhtasarDonemi))
                throw new FirmaKunyeKuralException(nameof(dto.MuhtasarDonemi), "Geçersiz muhtasar dönemi.");

            DateTime? bas = null, bit = null;
            if (dto.HesapDonemi == HesapDonemi.OzelHesapDonemi)
            {
                if (dto.OzelDonemBas is null || dto.OzelDonemBit is null)
                    throw new FirmaKunyeKuralException(nameof(dto.OzelDonemBas),
                        "Özel hesap döneminde başlangıç ve bitiş tarihi zorunlu.");

                bas = dto.OzelDonemBas.Value.Date;
                bit = dto.OzelDonemBit.Value.Date;

                if (bit <= bas)
                    throw new FirmaKunyeKuralException(nameof(dto.OzelDonemBit),
                        "Özel hesap döneminin bitişi başlangıcından sonra olmalı.");
            }

            var firma = await _db.Firmalar.FirstOrDefaultAsync(f => f.Id == firmaId, ct)
                        ?? throw new FirmaKunyeKuralException("firmaId", "Firma bulunamadı.");

            if (firma.DefterUsulu != dto.DefterUsulu)
            {
                firma.DefterUsulu = dto.DefterUsulu;
                firma.DefterUsuluKaynagi = SiniflandirmaKaynagi.Elle;
            }

            if (firma.VergiTuru != dto.VergiTuru)
            {
                firma.VergiTuru = dto.VergiTuru;
                firma.VergiTuruKaynagi = SiniflandirmaKaynagi.Elle;
            }

            // Boş = dokunma: mizan formatı artık Kullanılan sistemler kartında yazılıyor.
            if (format is not null) firma.MizanFormati = format;
            firma.HesapDonemi = dto.HesapDonemi;
            firma.OzelDonemBas = bas;
            firma.OzelDonemBit = bit;
            firma.FirmaTipi = dto.FirmaTipi;
            firma.SgkTesvikKademesi = dto.SgkTesvikKademesi;
            firma.MuhtasarDonemi = dto.MuhtasarDonemi;

            // Elle onay: kodlarla uyuşmasa da kayıtlı değer kullanıcının kararıdır.
            if (dto.Onayla)
            {
                if (firma.VergiTuru != VergiTuru.Belirsiz) firma.VergiTuruKaynagi = SiniflandirmaKaynagi.Elle;
                if (firma.DefterUsulu != DefterUsulu.Belirsiz) firma.DefterUsuluKaynagi = SiniflandirmaKaynagi.Elle;
            }

            // Değişiklik yoksa ne kayıt ne olay: formu açıp kapatmak "güncellendi" yazmasın.
            if (!_db.Entry(firma).Properties.Any(p => p.IsModified)) return;

            firma.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            await OlayAsync(firmaId, FirmaOlayTipi.SiniflandirmaGuncellendi, "Sınıflandırma güncellendi", ct);
        }

        // ---- Takip ----

        public async Task SorumluAtaAsync(int firmaId, FirmaSorumluAtaDto dto, CancellationToken ct = default)
        {
            if (dto.KullaniciId is <= 0)
                throw new FirmaKunyeKuralException(nameof(dto.KullaniciId), "Geçersiz kullanıcı.");

            var ad = string.IsNullOrWhiteSpace(dto.KullaniciAdi) ? null : dto.KullaniciAdi.Trim();
            if (dto.KullaniciId is not null && ad is null)
                throw new FirmaKunyeKuralException(nameof(dto.KullaniciAdi), "Sorumlu kullanıcının adı eksik.");

            if (dto.KullaniciId is null) ad = null;
            if (ad is { Length: > 100 }) ad = ad[..100];

            var firma = await FirmaAsync(firmaId, ct);

            if (firma.SorumluKullaniciId == dto.KullaniciId && firma.SorumluKullaniciAdi == ad) return;

            firma.SorumluKullaniciId = dto.KullaniciId;
            firma.SorumluKullaniciAdi = ad;
            firma.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            await OlayAsync(firmaId, FirmaOlayTipi.SorumluAtandi,
                ad is null ? "Sorumlu kaldırıldı" : $"Sorumlu atandı: {ad}", ct);
        }

        public async Task<List<FirmaNotuDto>> NotlarAsync(int firmaId, CancellationToken ct = default)
        {
            var ben = KullaniciId();

            return await _db.FirmaNotlari.AsNoTracking()
                .Where(n => n.FirmaId == firmaId)
                .OrderByDescending(n => n.OlusturmaZamani).ThenByDescending(n => n.Id)
                .Select(n => new FirmaNotuDto
                {
                    Id = n.Id,
                    Metin = n.Metin,
                    OlusturanKullaniciAdi = n.OlusturanKullaniciAdi,
                    OlusturmaZamani = DateTime.SpecifyKind(n.OlusturmaZamani, DateTimeKind.Utc),
                    Silinebilir = ben != null && n.OlusturanKullaniciId == ben
                })
                .ToListAsync(ct);
        }

        public async Task<FirmaNotuDto> NotEkleAsync(int firmaId, FirmaNotuEkleDto dto, CancellationToken ct = default)
        {
            var metin = dto.Metin?.Trim() ?? string.Empty;
            if (metin.Length == 0)
                throw new FirmaKunyeKuralException(nameof(dto.Metin), "Not boş olamaz.");
            if (metin.Length > NotEnFazla)
                throw new FirmaKunyeKuralException(nameof(dto.Metin), $"Not en çok {NotEnFazla} karakter olabilir.");

            var ben = KullaniciId()
                      ?? throw new FirmaKunyeKuralException("kullanici", "Notu yazan kullanıcı belirlenemedi.");

            await FirmaAsync(firmaId, ct);

            var not = new FirmaNotu
            {
                FirmaId = firmaId,
                Metin = metin,
                OlusturanKullaniciId = ben,
                OlusturanKullaniciAdi = _kullanici?.UserName,
                OlusturmaZamani = DateTime.UtcNow
            };

            _db.FirmaNotlari.Add(not);
            await _db.SaveChangesAsync(ct);

            await OlayAsync(firmaId, FirmaOlayTipi.NotEklendi, "Not eklendi", ct);

            return new FirmaNotuDto
            {
                Id = not.Id,
                Metin = not.Metin,
                OlusturanKullaniciAdi = not.OlusturanKullaniciAdi,
                OlusturmaZamani = DateTime.SpecifyKind(not.OlusturmaZamani, DateTimeKind.Utc),
                Silinebilir = true
            };
        }

        public async Task NotSilAsync(int firmaId, long notId, CancellationToken ct = default)
        {
            var not = await _db.FirmaNotlari.FirstOrDefaultAsync(n => n.Id == notId && n.FirmaId == firmaId, ct)
                      ?? throw new KeyNotFoundException("Not bulunamadı.");

            var ben = KullaniciId();
            if (ben is null || not.OlusturanKullaniciId != ben)
                throw new UnauthorizedAccessException("Notu yalnız yazan kişi silebilir.");

            _db.FirmaNotlari.Remove(not);
            await _db.SaveChangesAsync(ct);
        }

        public Task<List<FirmaOlayDto>> OlaylarAsync(int firmaId, CancellationToken ct = default)
            => FirmaPaneliService.OlaylarAsync(_db, firmaId, adet: null, ct);

        private async Task<Firma> FirmaAsync(int firmaId, CancellationToken ct)
            => await _db.Firmalar.FirstOrDefaultAsync(f => f.Id == firmaId, ct)
               ?? throw new FirmaKunyeKuralException("firmaId", "Firma bulunamadı.");

        private string? KullaniciId()
        {
            if (_kullanici is not { IsAuthenticated: true }) return null;
            try
            {
                return _kullanici.UserId;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>Seçili formatın başlık satırından oluşan boş mizan şablonu (xlsx).</summary>
        public static byte[] MizanSablonu(string format)
        {
            var basliklar = MizanFormatlari.SablonBasliklari(format)
                            ?? throw new FirmaKunyeKuralException("format", MizanFormatlari.Secili(format)
                                   ? MizanFormatlari.SablonYokMesaji
                                   : "Şablon için bir mizan formatı seçin.");

            using var kitap = new XLWorkbook();
            var sayfa = kitap.Worksheets.Add("Mizan");

            for (var i = 0; i < basliklar.Length; i++)
                sayfa.Cell(1, i + 1).Value = basliklar[i];

            sayfa.Row(1).Style.Font.Bold = true;
            sayfa.Columns().AdjustToContents();

            using var akis = new MemoryStream();
            kitap.SaveAs(akis);
            return akis.ToArray();
        }
    }
}
