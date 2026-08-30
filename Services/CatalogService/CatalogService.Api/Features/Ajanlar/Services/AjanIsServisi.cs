using CatalogService.Api.Features.Ajanlar.Domain;
using CatalogService.Api.Features.Ajanlar.Dtos;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CatalogService.Api.Features.Ajanlar.Services
{
    /// <summary>
    /// İş kurallarının tamamı burada; hub ile controller yalnız kapı.
    ///
    /// <b>Zaman aşımı arka plan servisiyle değil, okuma anında</b> işaretleniyor —
    /// ajan listesindeki zaman aşımıyla aynı yaklaşım (KARARLAR §102). Takılmış bir
    /// işin varlığı ancak birinin ona bakmasıyla ya da aynı ajana yeni iş
    /// açılmasıyla önem kazanıyor; ikisi de bu süzgeçten geçiyor. Ayrı bir
    /// <c>BackgroundService</c> aynı işi kendi zamanlayıcısı ve hata yönetimiyle
    /// yapardı.
    /// </summary>
    public class AjanIsServisi : IAjanIsServisi
    {
        private readonly CatalogContext _db;
        private readonly IAjanDeposu _depo;
        private readonly IAjanIsGondericisi _gonderici;
        private readonly IOrkaAktarimYuku _aktarimYuku;
        private readonly IOptionsMonitor<AgentHubAyarlari> _ayarlar;
        private readonly TimeProvider _saat;
        private readonly ILogger<AjanIsServisi> _log;

        public AjanIsServisi(
            CatalogContext db,
            IAjanDeposu depo,
            IAjanIsGondericisi gonderici,
            IOrkaAktarimYuku aktarimYuku,
            IOptionsMonitor<AgentHubAyarlari> ayarlar,
            TimeProvider saat,
            ILogger<AjanIsServisi> log)
        {
            _db = db;
            _depo = depo;
            _gonderici = gonderici;
            _aktarimYuku = aktarimYuku;
            _ayarlar = ayarlar;
            _saat = saat;
            _log = log;
        }

        private DateTime Simdi => _saat.GetUtcNow().UtcDateTime;

        private TimeSpan ZamanAsimi =>
            TimeSpan.FromMinutes(Math.Max(1, _ayarlar.CurrentValue.IsZamanAsimiDakika));

        public async Task<AjanIsiOlusturSonucuDto> OlusturAsync(
            YeniAjanIsiDto istek, string kullaniciId, CancellationToken ct = default)
        {
            await ZamanAsimlariniIsaretleAsync(ct);

            if (istek.FirmaId <= 0)
                return Sonuc(null, null, "Firma seçilmeden iş oluşturulamaz.");

            var ajanId = (istek.AjanId ?? string.Empty).Trim();
            if (ajanId.Length == 0)
            {
                var (bulunan, hata) = await TekAdayiBulAsync(ct);
                if (bulunan is null) return Sonuc(null, null, hata!);
                ajanId = bulunan;
            }

            // Robot tek ORKA penceresiyle çalışıyor; paralel iş anlamsız.
            var acikIs = await _db.AjanIsleri.AsNoTracking()
                .Where(x => x.AjanId == ajanId &&
                            (x.Durum == AjanIsDurumu.Bekliyor ||
                             x.Durum == AjanIsDurumu.Gonderildi ||
                             x.Durum == AjanIsDurumu.Calisiyor))
                .OrderBy(x => x.OlusturmaZamani)
                .FirstOrDefaultAsync(ct);

            if (acikIs is not null)
                return Sonuc(null, Dto(acikIs), "Bu ajanda hâlâ süren bir iş var. Bitmesini bekleyin ya da iptal edin.");

            var isTipi = string.IsNullOrWhiteSpace(istek.IsTipi) ? AjanIsTipleri.SahteAktarim : istek.IsTipi.Trim();
            var yuk = string.IsNullOrWhiteSpace(istek.Yuk) ? "{}" : istek.Yuk;

            if (isTipi == AjanIsTipleri.OrkayaAktar)
            {
                // Yükü SUNUCU kuruyor: firma kodu, hesap kodu ve satır sayısı
                // tarayıcıdan gelseydi robot, doğruluğu kimsenin denetlemediği
                // değerlerle ORKA'ya yazardı.
                var (hazir, hata) = await _aktarimYuku.HazirlaAsync(EkstreYuklemeIdCoz(yuk), ct);
                if (hazir is null) return Sonuc(null, null, hata ?? "İş paketi hazırlanamadı.");
                yuk = hazir;
            }

            var kayit = new AjanIsi
            {
                Id = Guid.NewGuid(),
                AjanId = ajanId,
                FirmaId = istek.FirmaId,
                IsTipi = isTipi,
                Yuk = yuk,
                Durum = AjanIsDurumu.Bekliyor,
                OlusturanKullaniciId = kullaniciId,
                OlusturmaZamani = Simdi
            };

            _db.AjanIsleri.Add(kayit);
            await _db.SaveChangesAsync(ct);

            var gonderildi = await GonderAsync(kayit, ct);
            if (gonderildi) await _db.SaveChangesAsync(ct);

            var dto = Dto(kayit);
            dto.AjanBagliydi = gonderildi;

            return Sonuc(dto, null, gonderildi
                ? "İş ajana gönderildi."
                : "Ajan şu anda bağlı değil. İş sıraya alındı; ajan bağlanınca çalışacak.");
        }

        public async Task<AjanIsDto?> GetirAsync(Guid id, CancellationToken ct = default)
        {
            await ZamanAsimlariniIsaretleAsync(ct);

            var kayit = await _db.AjanIsleri.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            return kayit is null ? null : Dto(kayit);
        }

        public async Task<List<AjanIsDto>> ListeleAsync(int? firmaId, AjanIsDurumu? durum, string? ajanId,
                                                        int enFazla = 50, CancellationToken ct = default)
        {
            await ZamanAsimlariniIsaretleAsync(ct);

            var sorgu = _db.AjanIsleri.AsNoTracking().AsQueryable();

            // Kapsam okuma tarafında isteğe bağlı: firma bir oturum bağlamı değil,
            // verinin bir boyutu (KARARLAR §99).
            if (firmaId is > 0) sorgu = sorgu.Where(x => x.FirmaId == firmaId);
            if (durum is not null) sorgu = sorgu.Where(x => x.Durum == durum);
            if (!string.IsNullOrWhiteSpace(ajanId)) sorgu = sorgu.Where(x => x.AjanId == ajanId);

            var kayitlar = await sorgu
                .OrderByDescending(x => x.OlusturmaZamani)
                .Take(Math.Clamp(enFazla, 1, 200))
                .ToListAsync(ct);

            return kayitlar.Select(Dto).ToList();
        }

        public async Task<AjanIsDto?> IptalAsync(Guid id, CancellationToken ct = default)
        {
            var kayit = await _db.AjanIsleri.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (kayit is null) return null;

            if (kayit.Bitti) return Dto(kayit);   // bitmiş işin durumu değişmez

            kayit.Durum = AjanIsDurumu.IptalEdildi;
            kayit.BitisZamani = Simdi;
            kayit.IlerlemeMesaji = "İptal edildi.";
            await _db.SaveChangesAsync(ct);

            // Ajan bağlıysa haber ver; bağlı değilse zaten iş gönderilmemişti ya da
            // bağlantı koptuğunda ajan işi bırakacak.
            await _gonderici.IptalBildirAsync(kayit.AjanId, kayit.Id, ct);

            _log.LogInformation("Ajan işi iptal edildi: {IsId} (ajan {AjanId})", kayit.Id, kayit.AjanId);

            // İptal de ajanı boşa çıkarıyor; sıradaki bekleyen iş gitsin.
            await BekleyenleriGonderAsync(kayit.AjanId, ct);

            return Dto(kayit);
        }

        // ---- ajan bildirimleri ---------------------------------------------

        public async Task<bool> BasladiAsync(string ajanId, Guid isId, CancellationToken ct = default)
        {
            var kayit = await KendiIsiniBulAsync(ajanId, isId, ct);
            if (kayit is null) return false;

            // Tekrar gelen bildirim zararsız: yalnız henüz başlamamış iş ilerler.
            if (kayit.Durum is AjanIsDurumu.Bekliyor or AjanIsDurumu.Gonderildi)
            {
                kayit.Durum = AjanIsDurumu.Calisiyor;
                kayit.BaslamaZamani = Simdi;
                kayit.SonIlerlemeZamani = Simdi;
                await _db.SaveChangesAsync(ct);
            }

            return true;
        }

        public async Task<bool> IlerlemeAsync(string ajanId, Guid isId, int yuzde, string? mesaj,
                                              int? tamamlananAdim, CancellationToken ct = default)
        {
            var kayit = await KendiIsiniBulAsync(ajanId, isId, ct);
            if (kayit is null) return false;

            // Bitmiş işe ilerleme yazılmaz: ağ kopup yeniden bağlanan ajan eski
            // bildirimleri tekrar gönderebiliyor.
            if (kayit.Bitti) return true;

            if (kayit.Durum != AjanIsDurumu.Calisiyor)
            {
                kayit.Durum = AjanIsDurumu.Calisiyor;
                kayit.BaslamaZamani ??= Simdi;
            }

            // Yüzde geriye gitmiyor; tekrarlanan eski bildirim çubuğu geri sarmasın.
            kayit.IlerlemeYuzde = Math.Clamp(Math.Max(kayit.IlerlemeYuzde, yuzde), 0, 100);
            if (!string.IsNullOrWhiteSpace(mesaj)) kayit.IlerlemeMesaji = Kirp(mesaj, 300);
            if (tamamlananAdim is not null)
                kayit.TamamlananAdim = Math.Max(kayit.TamamlananAdim, tamamlananAdim.Value);

            kayit.SonIlerlemeZamani = Simdi;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> BittiAsync(string ajanId, Guid isId, bool basarili, string? hataMesaji,
                                           string? sonucOzetiJson, string? hataEkraniDosyaId = null,
                                           CancellationToken ct = default)
        {
            var kayit = await KendiIsiniBulAsync(ajanId, isId, ct);
            if (kayit is null) return false;

            // İlk biten hâli kalır: iptal edilmiş bir işi "tamamlandı" yapmak,
            // kullanıcının verdiği kararı silmek olurdu.
            if (kayit.Bitti) return true;

            kayit.Durum = basarili ? AjanIsDurumu.Tamamlandi : AjanIsDurumu.Basarisiz;
            kayit.BitisZamani = Simdi;
            kayit.SonIlerlemeZamani = Simdi;
            kayit.HataMesaji = basarili ? null : Kirp(hataMesaji, 2000);
            kayit.SonucOzeti = sonucOzetiJson;
            if (!string.IsNullOrWhiteSpace(hataEkraniDosyaId)) kayit.HataEkraniDosyaId = hataEkraniDosyaId;
            if (basarili) kayit.IlerlemeYuzde = 100;

            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Ajan işi bitti: {IsId} (ajan {AjanId}) -> {Durum}",
                kayit.Id, kayit.AjanId, kayit.Durum);

            // Ajan artık boşta: sırada bekleyen varsa hemen gönder. Yoksa kuyruk
            // ancak bir sonraki bağlanmada ya da yeni bir istekte hareket ederdi.
            await BekleyenleriGonderAsync(ajanId, ct);

            return true;
        }

        // ---- hub olayları ---------------------------------------------------

        public async Task BekleyenleriGonderAsync(string ajanId, CancellationToken ct = default)
        {
            var bekleyenler = await _db.AjanIsleri
                .Where(x => x.AjanId == ajanId && x.Durum == AjanIsDurumu.Bekliyor)
                .OrderBy(x => x.OlusturmaZamani)
                .ToListAsync(ct);

            if (bekleyenler.Count == 0) return;

            // Tek ORKA penceresi: bağlanınca yalnız en eski bekleyen gönderiliyor,
            // kalanlar o bitince sıradan alınır.
            var ilk = bekleyenler[0];
            if (await GonderAsync(ilk, ct))
            {
                await _db.SaveChangesAsync(ct);
                _log.LogInformation("Ajan bağlandı, bekleyen iş gönderildi: {IsId} (ajan {AjanId})", ilk.Id, ajanId);
            }
        }

        public async Task BaglantiKoptuAsync(string ajanId, CancellationToken ct = default)
        {
            var acikIsler = await _db.AjanIsleri
                .Where(x => x.AjanId == ajanId &&
                            (x.Durum == AjanIsDurumu.Gonderildi || x.Durum == AjanIsDurumu.Calisiyor))
                .ToListAsync(ct);

            if (acikIsler.Count == 0) return;

            foreach (var kayit in acikIsler)
            {
                kayit.Durum = AjanIsDurumu.Basarisiz;
                kayit.BitisZamani = Simdi;
                kayit.HataMesaji = "Ajan bağlantısı koptu; iş yarım kaldı. " +
                                   "ORKA'da kaydedilmemiş giriş kalmış olabilir, kaydetmeden kontrol edin.";
            }

            await _db.SaveChangesAsync(ct);
            _log.LogWarning("Ajan bağlantısı koptu, {Sayi} iş başarısız işaretlendi (ajan {AjanId})",
                acikIsler.Count, ajanId);
        }

        // ---- iç yardımcılar --------------------------------------------------

        /// <summary>
        /// İşi ajana iletir. Ajan bağlı değilse kayıt <c>Bekliyor</c> kalır ve false
        /// döner — çağıran taraf <c>SaveChanges</c> yapıp yapmayacağına buna bakar.
        /// </summary>
        private async Task<bool> GonderAsync(AjanIsi kayit, CancellationToken ct)
        {
            var paket = new AjanIsPaketiDto
            {
                IsId = kayit.Id,
                IsTipi = kayit.IsTipi,
                FirmaId = kayit.FirmaId,
                Yuk = kayit.Yuk
            };

            if (!await _gonderici.GonderAsync(kayit.AjanId, paket, ct))
                return false;

            kayit.Durum = AjanIsDurumu.Gonderildi;
            kayit.GonderimZamani = Simdi;
            kayit.SonIlerlemeZamani = Simdi;
            return true;
        }

        /// <summary>
        /// Hedef ajan verilmediğinde tek adayı bulur: önce bağlı ajanlar, yoksa
        /// geçmiş işlerdeki ajanlar. Birden çok aday varsa seçim kullanıcıya
        /// bırakılıyor — yanlış makineye iş göndermek sessiz bir hata olurdu.
        /// </summary>
        private async Task<(string? AjanId, string? Hata)> TekAdayiBulAsync(CancellationToken ct)
        {
            var bagliOlanlar = _depo.Baglilar()
                .Select(a => a.AjanId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (bagliOlanlar.Count == 1) return (bagliOlanlar[0], null);
            if (bagliOlanlar.Count > 1)
                return (null, "Birden fazla ajan bağlı; işin hangi makineye gideceğini seçin.");

            var gecmistekiler = await _db.AjanIsleri.AsNoTracking()
                .Select(x => x.AjanId)
                .Distinct()
                .Take(2)
                .ToListAsync(ct);

            if (gecmistekiler.Count == 1) return (gecmistekiler[0], null);
            if (gecmistekiler.Count > 1)
                return (null, "Hiçbir ajan bağlı değil ve birden fazla ajan tanımlı; hedefi seçin.");

            return (null, "Bu sunucuya hiç ajan bağlanmadı. Ofisteki makinede PkfRobot'u " +
                          "--ajan ile başlatın, sonra tekrar deneyin.");
        }

        private Task<AjanIsi?> KendiIsiniBulAsync(string ajanId, Guid isId, CancellationToken ct)
            // Sahiplik kontrolü sorgunun içinde: başka ajanın işi hiç yüklenmiyor.
            => _db.AjanIsleri.FirstOrDefaultAsync(x => x.Id == isId && x.AjanId == ajanId, ct);

        private async Task ZamanAsimlariniIsaretleAsync(CancellationToken ct)
        {
            var esik = Simdi - ZamanAsimi;

            var takilanlar = await _db.AjanIsleri
                .Where(x => (x.Durum == AjanIsDurumu.Calisiyor || x.Durum == AjanIsDurumu.Gonderildi) &&
                            x.SonIlerlemeZamani != null && x.SonIlerlemeZamani < esik)
                .ToListAsync(ct);

            if (takilanlar.Count == 0) return;

            foreach (var kayit in takilanlar)
            {
                kayit.Durum = AjanIsDurumu.ZamanAsimi;
                kayit.BitisZamani = Simdi;
                kayit.HataMesaji =
                    $"{ZamanAsimi.TotalMinutes:0} dakikadır ilerleme bildirilmedi; iş zaman aşımına uğradı. " +
                    "ORKA'da yarım kalmış giriş olabilir, kaydetmeden kontrol edin.";
            }

            await _db.SaveChangesAsync(ct);
            _log.LogWarning("{Sayi} ajan işi zaman aşımına uğradı.", takilanlar.Count);
        }

        /// <summary>
        /// İstemcinin gönderdiği yükten yalnız ekstre kimliğini alıyoruz; gerisini
        /// sunucu dolduruyor.
        /// </summary>
        private static int EkstreYuklemeIdCoz(string yuk)
        {
            try
            {
                using var belge = System.Text.Json.JsonDocument.Parse(yuk);
                return belge.RootElement.TryGetProperty("EkstreYuklemeId", out var d) && d.TryGetInt32(out var id)
                    ? id
                    : 0;
            }
            catch (System.Text.Json.JsonException)
            {
                return 0;
            }
        }

        private static AjanIsiOlusturSonucuDto Sonuc(AjanIsDto? isDto, AjanIsDto? cakisan, string mesaj)
            => new() { Is = isDto, CakisanIs = cakisan, Mesaj = mesaj };

        private static string? Kirp(string? metin, int enFazla)
            => metin is null ? null : (metin.Length <= enFazla ? metin : metin[..enFazla]);

        private static AjanIsDto Dto(AjanIsi x) => new()
        {
            Id = x.Id,
            AjanId = x.AjanId,
            FirmaId = x.FirmaId,
            IsTipi = x.IsTipi,
            Durum = x.Durum,
            DurumAdi = DurumAdi(x.Durum),
            IlerlemeYuzde = x.IlerlemeYuzde,
            IlerlemeMesaji = x.IlerlemeMesaji,
            ToplamAdim = x.ToplamAdim,
            TamamlananAdim = x.TamamlananAdim,
            OlusturmaZamani = x.OlusturmaZamani,
            BaslamaZamani = x.BaslamaZamani,
            BitisZamani = x.BitisZamani,
            HataMesaji = x.HataMesaji,
            SonucOzeti = x.SonucOzeti,
            HataEkraniDosyaId = x.HataEkraniDosyaId,
            Bitti = x.Bitti,
            AjanBagliydi = x.GonderimZamani is not null
        };

        public static string DurumAdi(AjanIsDurumu durum) => durum switch
        {
            AjanIsDurumu.Bekliyor => "Bekliyor",
            AjanIsDurumu.Gonderildi => "Gönderildi",
            AjanIsDurumu.Calisiyor => "Çalışıyor",
            AjanIsDurumu.Tamamlandi => "Tamamlandı",
            AjanIsDurumu.Basarisiz => "Başarısız",
            AjanIsDurumu.IptalEdildi => "İptal edildi",
            AjanIsDurumu.ZamanAsimi => "Zaman aşımı",
            _ => durum.ToString()
        };
    }
}
