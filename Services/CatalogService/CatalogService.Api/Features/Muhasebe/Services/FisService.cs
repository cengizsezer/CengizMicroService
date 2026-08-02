using System.Collections.Concurrent;
using System.Globalization;
using CatalogService.Api.Features.Muhasebe.Domain;
using CatalogService.Api.Features.Muhasebe.Dtos;
using CatalogService.Api.Infrastructure.Accessor;
using CatalogService.Api.Infrastructure.Auth;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CatalogService.Api.Features.Muhasebe.Services
{
    /// <inheritdoc cref="IFisService"/>
    public class FisService : IFisService
    {
        private const int ListeLimiti = 500;
        private const int SiraHaneSayisi = 6;

        /// <summary>
        /// Fiş numarası üretimini firma + dönem bazında süreç içinde de seri hâle getirir
        /// (iş kuralı 16). Çok örnekli dağıtımda asıl güvence veritabanı tarafındaki
        /// transaction + UPDLOCK/HOLDLOCK okuması ve <c>UQ_FisNo</c> benzersiz indeksidir;
        /// bu kilit aynı süreç içindeki eşzamanlı isteklerin gereksiz yere çakışmasını önler.
        /// </summary>
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> NumaraKilitleri = new();

        /// <summary>Hata mesajlarındaki tutar biçimi; sunucu kültüründen bağımsız olsun diye elle kurulur.</summary>
        private static readonly NumberFormatInfo ParaFormat = new()
        {
            NumberDecimalSeparator = ",",
            NumberGroupSeparator = ".",
            NumberDecimalDigits = 2,
            NumberGroupSizes = new[] { 3 }
        };

        private readonly CatalogContext _db;
        private readonly IHttpCurrentUser _user;
        private readonly IHttpCurrentTenant _tenant;

        public FisService(CatalogContext db, IHttpCurrentUser user, IHttpCurrentTenant tenant)
        {
            _db = db;
            _user = user;
            _tenant = tenant;
        }

        // ---- Okuma ----

        public async Task<List<FisOzetDto>> GetListeAsync(FisFiltreDto filtre, CancellationToken ct = default)
        {
            var q = _db.Fisler.AsNoTracking().AsQueryable();

            if (filtre.Bas is DateTime bas) q = q.Where(f => f.Tarih >= bas.Date);
            if (filtre.Bit is DateTime bit) q = q.Where(f => f.Tarih <= bit.Date);
            if (filtre.Durum is FisDurum durum) q = q.Where(f => f.Durum == durum);
            if (filtre.HesapId is int hesapId) q = q.Where(f => f.Satirlar.Any(s => s.HesapId == hesapId));

            return await q
                .OrderByDescending(f => f.Tarih)
                .ThenByDescending(f => f.FisNo)
                .Take(ListeLimiti)
                .Select(f => new FisOzetDto
                {
                    Id = f.Id,
                    DonemYil = f.DonemYil,
                    FisNo = f.FisNo,
                    Tarih = f.Tarih,
                    FisTuru = f.FisTuru,
                    BelgeNo = f.BelgeNo,
                    Aciklama = f.Aciklama,
                    Kaynak = f.Kaynak,
                    Durum = f.Durum,
                    SatirSayisi = f.Satirlar.Count,
                    ToplamBorc = f.Satirlar.Sum(s => s.Borc),
                    ToplamAlacak = f.Satirlar.Sum(s => s.Alacak)
                })
                .ToListAsync(ct);
        }

        public async Task<FisDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var fis = await _db.Fisler
                .AsNoTracking()
                .Include(f => f.Satirlar).ThenInclude(s => s.Hesap)
                .Include(f => f.Satirlar).ThenInclude(s => s.MasrafMerkezi)
                .FirstOrDefaultAsync(f => f.Id == id, ct);

            return fis is null ? null : ToDto(fis);
        }

        // ---- Yazma ----

        public async Task<FisDto> CreateAsync(FisYazDto dto, CancellationToken ct = default)
        {
            var tarih = TarihDogrula(dto.Tarih);
            var satirlar = await SatirlariHazirlaAsync(dto.Satirlar, ct);
            var simdi = DateTime.UtcNow;

            var fis = new Fis
            {
                DonemYil = (short)tarih.Year,
                Tarih = tarih,
                FisTuru = FisTuruDogrula(dto.FisTuru),
                BelgeNo = Kirp(dto.BelgeNo, 50, "belgeNo", "Belge no"),
                Aciklama = Kirp(dto.Aciklama, 250, "aciklama", "Açıklama"),
                Kaynak = dto.Kaynak,
                // Kural 15: kesinleşmiş fiş bir daha güncellenemez; kullanıcı bilinçli olarak seçer.
                Durum = dto.Kesinlestir ? FisDurum.Kesinlesmis : FisDurum.Taslak,
                OlusturanId = OlusturanId(),
                OlusturmaT = simdi
            };

            foreach (var satir in satirlar) fis.Satirlar.Add(satir);

            await NumaraUretipKaydetAsync(fis, ct);

            return (await GetByIdAsync(fis.Id, ct))!;
        }

        public async Task<FisDto?> UpdateAsync(int id, FisYazDto dto, CancellationToken ct = default)
        {
            var fis = await _db.Fisler.Include(f => f.Satirlar).FirstOrDefaultAsync(f => f.Id == id, ct);
            if (fis is null) return null;

            // Kural 15: kesinleşmiş fiş güncellenemez.
            if (fis.Durum == FisDurum.Kesinlesmis)
                throw new MuhasebeKuralException("durum",
                    $"'{fis.FisNo}' numaralı fiş kesinleşmiş; güncellenemez. Düzeltme için ters kayıt fişi oluşturun.");

            var tarih = TarihDogrula(dto.Tarih);
            var satirlar = await SatirlariHazirlaAsync(dto.Satirlar, ct);
            var yeniDonem = (short)tarih.Year;

            _db.FisSatirlar.RemoveRange(fis.Satirlar);
            fis.Satirlar.Clear();
            foreach (var satir in satirlar) fis.Satirlar.Add(satir);

            fis.Tarih = tarih;
            fis.FisTuru = FisTuruDogrula(dto.FisTuru);
            fis.BelgeNo = Kirp(dto.BelgeNo, 50, "belgeNo", "Belge no");
            fis.Aciklama = Kirp(dto.Aciklama, 250, "aciklama", "Açıklama");
            fis.Kaynak = dto.Kaynak;
            if (dto.Kesinlestir) fis.Durum = FisDurum.Kesinlesmis;
            fis.GuncellemeT = DateTime.UtcNow;

            if (yeniDonem != fis.DonemYil)
            {
                // Fiş başka bir döneme taşındı: numara o dönemin sırasından yeniden üretilir.
                fis.DonemYil = yeniDonem;
                await NumaraUretipKaydetAsync(fis, ct);
            }
            else
            {
                await _db.SaveChangesAsync(ct);
            }

            return await GetByIdAsync(fis.Id, ct);
        }

        public async Task<FisDto?> KesinlestirAsync(int id, CancellationToken ct = default)
        {
            var fis = await _db.Fisler.Include(f => f.Satirlar).FirstOrDefaultAsync(f => f.Id == id, ct);
            if (fis is null) return null;

            if (fis.Durum == FisDurum.Kesinlesmis)
                throw new MuhasebeKuralException("durum", $"'{fis.FisNo}' numaralı fiş zaten kesinleşmiş.");

            // Taslak kaydedildikten sonra hesap pasife alınmış olabilir; kurallar yeniden doğrulanır.
            await SatirlariHazirlaAsync(fis.Satirlar.Select(ToYazDto).ToList(), ct);

            fis.Durum = FisDurum.Kesinlesmis;
            fis.GuncellemeT = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return await GetByIdAsync(fis.Id, ct);
        }

        public async Task<FisSilmeSonuc> DeleteAsync(int id, CancellationToken ct = default)
        {
            var fis = await _db.Fisler.Include(f => f.Satirlar).FirstOrDefaultAsync(f => f.Id == id, ct);
            if (fis is null) return FisSilmeSonuc.Bulunamadi;

            // Kural 15: kesinleşmiş fiş silinemez.
            if (fis.Durum == FisDurum.Kesinlesmis) return FisSilmeSonuc.Kesinlesmis;

            _db.FisSatirlar.RemoveRange(fis.Satirlar);
            _db.Fisler.Remove(fis);
            await _db.SaveChangesAsync(ct);

            return FisSilmeSonuc.Silindi;
        }

        public async Task<FisDto?> TersKayitAsync(int id, TersKayitDto dto, CancellationToken ct = default)
        {
            var kaynak = await _db.Fisler
                .AsNoTracking()
                .Include(f => f.Satirlar)
                .FirstOrDefaultAsync(f => f.Id == id, ct);

            if (kaynak is null) return null;

            if (kaynak.Durum != FisDurum.Kesinlesmis)
                throw new MuhasebeKuralException("durum",
                    "Ters kayıt yalnızca kesinleşmiş fişler için oluşturulur. Taslak fişi doğrudan düzeltebilirsiniz.");

            var yaz = new FisYazDto
            {
                Tarih = dto.Tarih ?? kaynak.Tarih,
                FisTuru = kaynak.FisTuru,
                BelgeNo = kaynak.BelgeNo,
                Aciklama = string.IsNullOrWhiteSpace(dto.Aciklama)
                    ? $"'{kaynak.FisNo}' numaralı fişin ters kaydı"
                    : dto.Aciklama,
                Kaynak = kaynak.Kaynak,
                Kesinlestir = dto.Kesinlestir,
                // Borç ve alacak yer değiştirir; döviz tutarı ve kur aynen taşınır.
                Satirlar = kaynak.Satirlar
                    .OrderBy(s => s.SiraNo)
                    .Select(s => new FisSatirYazDto
                    {
                        HesapId = s.HesapId,
                        MasrafMerkeziId = s.MasrafMerkeziId,
                        Aciklama = s.Aciklama,
                        Borc = s.Alacak,
                        Alacak = s.Borc,
                        ParaBirimi = s.ParaBirimi,
                        Doviz = s.Doviz,
                        Kur = s.Kur
                    })
                    .ToList()
            };

            return await CreateAsync(yaz, ct);
        }

        // ---- Doğrulama ----

        /// <summary>
        /// Fiş satırlarını iş kuralları 10–14 ve 17'ye göre doğrular ve kalıcı satırlara dönüştürür.
        /// Kural sırası, kullanıcıya en anlamlı hatayı önce gösterecek şekilde seçilmiştir.
        /// </summary>
        private async Task<List<FisSatir>> SatirlariHazirlaAsync(List<FisSatirYazDto> girdi, CancellationToken ct)
        {
            var satirlar = girdi ?? new List<FisSatirYazDto>();

            // Kural 10: fiş en az iki satır içermeli.
            if (satirlar.Count < 2)
                throw new MuhasebeKuralException("satirlar",
                    "Fiş en az iki satır içermeli. Çift taraflı kayıt için en az bir borç ve bir alacak satırı girin.");

            var tutarlar = new (decimal Borc, decimal Alacak)[satirlar.Count];
            decimal toplamBorc = 0, toplamAlacak = 0;

            for (var i = 0; i < satirlar.Count; i++)
            {
                var no = i + 1;
                var borc = Yuvarla(satirlar[i].Borc);
                var alacak = Yuvarla(satirlar[i].Alacak);

                if (borc < 0 || alacak < 0)
                    throw new MuhasebeKuralException("satirlar", $"{no}. satırda tutar negatif olamaz.");

                // Kural 13: bir satırda ya borç ya alacak dolu olur, ikisi birden değil.
                if (borc > 0 && alacak > 0)
                    throw new MuhasebeKuralException("satirlar",
                        $"{no}. satırda hem borç hem alacak dolu. Bir satırda yalnızca biri girilebilir.");

                tutarlar[i] = (borc, alacak);
                toplamBorc += borc;
                toplamAlacak += alacak;
            }

            // Kural 12: toplam tutar sıfır olamaz.
            if (toplamBorc == 0 && toplamAlacak == 0)
                throw new MuhasebeKuralException("satirlar",
                    "Fiş tutarı sıfır olamaz. Satırlara borç ve alacak tutarlarını girin.");

            // Kural 13'ün diğer yüzü: tutarsız (boş) satır kalamaz.
            for (var i = 0; i < satirlar.Count; i++)
                if (tutarlar[i].Borc == 0 && tutarlar[i].Alacak == 0)
                    throw new MuhasebeKuralException("satirlar",
                        $"{i + 1}. satırda tutar yok. Borç veya alacak tutarını girin ya da satırı silin.");

            // Kural 11: borç toplamı = alacak toplamı.
            if (toplamBorc != toplamAlacak)
            {
                var fark = Math.Abs(toplamBorc - toplamAlacak);
                throw new MuhasebeKuralException("satirlar",
                    $"Fiş dengede değil. Borç {Para(toplamBorc)} · Alacak {Para(toplamAlacak)} · Fark {Para(fark)}. " +
                    "Farkı kapatıp tekrar kaydedin.");
            }

            var hesaplar = await HesaplariGetirAsync(satirlar, ct);
            var merkezler = await MasrafMerkezleriGetirAsync(satirlar, ct);

            var sonuc = new List<FisSatir>(satirlar.Count);

            for (var i = 0; i < satirlar.Count; i++)
            {
                var no = i + 1;
                var s = satirlar[i];

                if (!hesaplar.TryGetValue(s.HesapId, out var hesap))
                    throw new MuhasebeKuralException("satirlar",
                        $"{no}. satırdaki hesap bulunamadı. Listeden bir hesap seçin.");

                // Kural 14: yalnızca hareket gören ve aktif hesaba fiş kesilebilir.
                if (!hesap.Aktif)
                    throw new MuhasebeKuralException("satirlar",
                        $"{no}. satır: '{hesap.Kod} {hesap.Ad}' hesabı pasif; yeni fişlerde kullanılamaz. Aktif bir hesap seçin.");

                if (!hesap.HareketGorur)
                    throw new MuhasebeKuralException("satirlar",
                        $"{no}. satır: '{hesap.Kod} {hesap.Ad}' hesabı hareket görmüyor. Alt hesaplarından birini seçin.");

                if (s.MasrafMerkeziId is int mmId)
                {
                    if (!merkezler.TryGetValue(mmId, out var merkez))
                        throw new MuhasebeKuralException("satirlar",
                            $"{no}. satırdaki masraf merkezi bulunamadı.");

                    if (!merkez.Aktif)
                        throw new MuhasebeKuralException("satirlar",
                            $"{no}. satır: '{merkez.Kod} {merkez.Ad}' masraf merkezi pasif; yeni fişlerde kullanılamaz.");
                }

                var (paraBirimi, doviz, kur) = DovizDogrula(no, s);

                sonuc.Add(new FisSatir
                {
                    SiraNo = (short)no,
                    HesapId = hesap.Id,
                    MasrafMerkeziId = s.MasrafMerkeziId,
                    Aciklama = Kirp(s.Aciklama, 250, "satirlar", $"{no}. satırın açıklaması"),
                    Borc = tutarlar[i].Borc,
                    Alacak = tutarlar[i].Alacak,
                    ParaBirimi = paraBirimi,
                    Doviz = doviz,
                    Kur = kur
                });
            }

            return sonuc;
        }

        /// <summary>Kural 17: döviz satırında döviz tutarı ve kur zorunlu; Borc/Alacak TL karşılığıdır.</summary>
        private static (string ParaBirimi, decimal? Doviz, decimal? Kur) DovizDogrula(int no, FisSatirYazDto s)
        {
            var pb = (s.ParaBirimi ?? string.Empty).Trim().ToUpperInvariant();
            if (pb.Length == 0) pb = FisParaBirimi.Yerel;

            if (pb.Length != 3 || !pb.All(char.IsAsciiLetterUpper))
                throw new MuhasebeKuralException("satirlar",
                    $"{no}. satırda para birimi 3 harfli olmalı, ör. \"USD\".");

            // Yerel para birimli satırda döviz alanları taşınmaz.
            if (pb == FisParaBirimi.Yerel) return (pb, null, null);

            if (s.Doviz is not > 0 || s.Kur is not > 0)
                throw new MuhasebeKuralException("satirlar",
                    $"{no}. satır {pb} döviz satırı; döviz tutarı ve kur zorunlu ve sıfırdan büyük olmalı.");

            return (pb,
                    Math.Round(s.Doviz.Value, 4, MidpointRounding.AwayFromZero),
                    Math.Round(s.Kur.Value, 6, MidpointRounding.AwayFromZero));
        }

        private async Task<Dictionary<int, HesapPlani>> HesaplariGetirAsync(List<FisSatirYazDto> satirlar, CancellationToken ct)
        {
            var idler = satirlar.Select(s => s.HesapId).Distinct().ToList();

            // Query filter sayesinde başka firmanın hesabı hiç dönmez; "bulunamadı" hatası verilir.
            return await _db.HesapPlanlari
                .AsNoTracking()
                .Where(h => idler.Contains(h.Id))
                .ToDictionaryAsync(h => h.Id, ct);
        }

        private async Task<Dictionary<int, MasrafMerkezi>> MasrafMerkezleriGetirAsync(List<FisSatirYazDto> satirlar, CancellationToken ct)
        {
            var idler = satirlar.Where(s => s.MasrafMerkeziId is not null)
                                .Select(s => s.MasrafMerkeziId!.Value)
                                .Distinct()
                                .ToList();

            if (idler.Count == 0) return new Dictionary<int, MasrafMerkezi>();

            return await _db.MasrafMerkezleri
                .AsNoTracking()
                .Where(m => idler.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, ct);
        }

        // ---- Fiş numarası (kural 16) ----

        /// <summary>
        /// Fiş numarasını üretip fişi kaydeder. Numara firma + dönem bazında sıralıdır ve
        /// üretimi ile kaydı aynı transaction içinde, kilitli okuma ile yapılır; böylece
        /// eşzamanlı isteklerde aynı numara iki kez üretilmez.
        /// </summary>
        private async Task NumaraUretipKaydetAsync(Fis fis, CancellationToken ct)
        {
            var kilit = NumaraKilitleri.GetOrAdd(KilitAnahtari(fis.DonemYil), _ => new SemaphoreSlim(1, 1));
            await kilit.WaitAsync(ct);
            try
            {
                var strateji = _db.Database.CreateExecutionStrategy();
                await strateji.ExecuteAsync(async () =>
                {
                    IDbContextTransaction? tx = _db.Database.IsRelational()
                        ? await _db.Database.BeginTransactionAsync(ct)
                        : null;

                    await using (tx)
                    {
                        fis.FisNo = await SonrakiFisNoAsync(fis.DonemYil, ct);

                        if (_db.Entry(fis).State == EntityState.Detached)
                            _db.Fisler.Add(fis);

                        await _db.SaveChangesAsync(ct);

                        if (tx is not null) await tx.CommitAsync(ct);
                    }
                });
            }
            finally
            {
                kilit.Release();
            }
        }

        private string KilitAnahtari(short donemYil) => $"{_tenant.CurrentTenantNo}|{donemYil}";

        private async Task<string> SonrakiFisNoAsync(short donemYil, CancellationToken ct)
        {
            var sira = await SonSiraAsync(donemYil, ct) + 1;
            return $"{donemYil}/{sira.ToString(CultureInfo.InvariantCulture).PadLeft(SiraHaneSayisi, '0')}";
        }

        /// <summary>Dönemdeki en büyük fiş sırası. İlişkisel sağlayıcıda satırlar kilitli okunur.</summary>
        private async Task<int> SonSiraAsync(short donemYil, CancellationToken ct)
        {
            if (_db.Database.IsRelational())
            {
                const string sql = """
                    SELECT ISNULL(MAX(TRY_CONVERT(int, SUBSTRING(FisNo, CHARINDEX('/', FisNo) + 1, 20))), 0) AS Value
                    FROM catalog.Fisler WITH (UPDLOCK, HOLDLOCK)
                    WHERE TenantNo = {0} AND DonemYil = {1}
                    """;

                return await _db.Database
                    .SqlQueryRaw<int>(sql, _tenant.CurrentTenantNo ?? string.Empty, donemYil)
                    .SingleAsync(ct);
            }

            var numaralar = await _db.Fisler
                .AsNoTracking()
                .Where(f => f.DonemYil == donemYil)
                .Select(f => f.FisNo)
                .ToListAsync(ct);

            return numaralar.Count == 0 ? 0 : numaralar.Max(SiraCoz);
        }

        private static int SiraCoz(string fisNo)
        {
            var i = fisNo.LastIndexOf('/');
            var son = i >= 0 ? fisNo[(i + 1)..] : fisNo;
            return int.TryParse(son, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
        }

        // ---- Yardımcılar ----

        private static DateTime TarihDogrula(DateTime tarih)
        {
            if (tarih == default)
                throw new MuhasebeKuralException("tarih", "Fiş tarihi zorunlu.");

            return tarih.Date;
        }

        private static FisTuru FisTuruDogrula(FisTuru tur)
        {
            if (!Enum.IsDefined(tur))
                throw new MuhasebeKuralException("fisTuru", "Geçerli bir fiş türü seçin.");

            return tur;
        }

        private static string? Kirp(string? deger, int enFazla, string alan, string baslik)
        {
            var s = (deger ?? string.Empty).Trim();
            if (s.Length == 0) return null;

            if (s.Length > enFazla)
                throw new MuhasebeKuralException(alan, $"{baslik} en fazla {enFazla} karakter olabilir.");

            return s;
        }

        private static decimal Yuvarla(decimal tutar) => Math.Round(tutar, 2, MidpointRounding.AwayFromZero);

        private static string Para(decimal tutar) => tutar.ToString("N2", ParaFormat);

        /// <summary>Token'daki kullanıcı kimliği; anonim/eksik claim durumunda 0.</summary>
        private int OlusturanId()
        {
            try
            {
                return _user.IsAuthenticated && int.TryParse(_user.UserId, out var id) ? id : 0;
            }
            catch (InvalidOperationException)
            {
                return 0;
            }
        }

        /// <summary>Kesinleştirmede kayıtlı satırları yeniden doğrulamak için kullanılır.</summary>
        private static FisSatirYazDto ToYazDto(FisSatir s) => new()
        {
            HesapId = s.HesapId,
            MasrafMerkeziId = s.MasrafMerkeziId,
            Aciklama = s.Aciklama,
            Borc = s.Borc,
            Alacak = s.Alacak,
            ParaBirimi = s.ParaBirimi,
            Doviz = s.Doviz,
            Kur = s.Kur
        };

        private static FisDto ToDto(Fis f) => new()
        {
            Id = f.Id,
            DonemYil = f.DonemYil,
            FisNo = f.FisNo,
            Tarih = f.Tarih,
            FisTuru = f.FisTuru,
            BelgeNo = f.BelgeNo,
            Aciklama = f.Aciklama,
            Kaynak = f.Kaynak,
            Durum = f.Durum,
            OlusturanId = f.OlusturanId,
            OlusturmaT = f.OlusturmaT,
            GuncellemeT = f.GuncellemeT,
            ToplamBorc = f.Satirlar.Sum(s => s.Borc),
            ToplamAlacak = f.Satirlar.Sum(s => s.Alacak),
            Satirlar = f.Satirlar
                .OrderBy(s => s.SiraNo)
                .Select(s => new FisSatirDto
                {
                    Id = s.Id,
                    SiraNo = s.SiraNo,
                    HesapId = s.HesapId,
                    HesapKod = s.Hesap?.Kod ?? string.Empty,
                    HesapAd = s.Hesap?.Ad ?? string.Empty,
                    MasrafMerkeziId = s.MasrafMerkeziId,
                    MasrafMerkeziKod = s.MasrafMerkezi?.Kod,
                    MasrafMerkeziAd = s.MasrafMerkezi?.Ad,
                    Aciklama = s.Aciklama,
                    Borc = s.Borc,
                    Alacak = s.Alacak,
                    ParaBirimi = s.ParaBirimi,
                    Doviz = s.Doviz,
                    Kur = s.Kur
                })
                .ToList()
        };
    }
}
