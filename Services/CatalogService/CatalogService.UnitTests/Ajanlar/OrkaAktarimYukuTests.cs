using CatalogService.Api.Features.Ajanlar.Services;
using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Infrastructure.Accessor;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CatalogService.UnitTests.Ajanlar
{
    /// <summary>
    /// <c>OrkayaAktar</c> işinin yükünü <b>sunucu</b> kuruyor: firma kodu, hesap
    /// kodu ve satır sayısı tarayıcıdan gelseydi robot, doğruluğu kimsenin
    /// denetlemediği değerlerle ORKA'ya yazardı.
    ///
    /// Eksik bir şey varsa iş <b>hiç oluşturulmuyor</b> — ajanı yola çıkarıp orada
    /// durdurmaktansa burada durmak.
    /// </summary>
    public class OrkaAktarimYukuTests
    {
        private const int FirmaId = 201;

        private static CatalogContext Db() => new(
            new DbContextOptionsBuilder<CatalogContext>()
                .UseInMemoryDatabase($"orka-yuk-{Guid.NewGuid():N}")
                .Options,
            new FixedTenantAccessor("test"));

        /// <summary>Firma + banka hesabı + yükleme; parametrelerle eksiltilebilir.</summary>
        private static async Task<int> VeriKurAsync(CatalogContext db,
            string? orkaFirmaKodu = "0001", string orkaHesapKodu = "102 1 1 01")
        {
            db.Firmalar.Add(new Firma
            {
                Id = FirmaId,
                Unvan = "ÖRNEK A.Ş.",
                KisaAd = "ORNEK",
                OrkaFirmaKodu = orkaFirmaKodu
            });

            db.EkstreBankaHesaplari.Add(new BankaHesabi
            {
                Id = 5,
                FirmaId = FirmaId,
                BankaAdi = "VAKIFBANK",
                OrkaHesapKodu = orkaHesapKodu,
                ParaBirimi = "TRY"
            });

            db.EkstreYuklemeler.Add(new EkstreYukleme
            {
                Id = 12,
                FirmaId = FirmaId,
                BankaHesabiId = 5,
                DosyaAdi = "ekstre.xlsx",
                YuklemeTarihi = new DateTime(2026, 8, 30),
                SatirSayisi = 175
            });

            await db.SaveChangesAsync();
            return 12;
        }

        private static OrkaAktarimYuku Servis(CatalogContext db, SahteEkstreServisi ekstreler)
            => new(db, ekstreler, new BankaFirmaKapsami());

        [Fact]
        public async Task Yuk_sunucuda_kuruluyor()
        {
            using var db = Db();
            var yuklemeId = await VeriKurAsync(db);
            var ekstreler = new SahteEkstreServisi { SatirSayisi = 175 };

            var (yuk, hata) = await Servis(db, ekstreler).HazirlaAsync(yuklemeId);

            Assert.Null(hata);
            Assert.NotNull(yuk);

            using var belge = JsonDocument.Parse(yuk!);
            var kok = belge.RootElement;
            Assert.Equal(12, kok.GetProperty("EkstreYuklemeId").GetInt32());
            Assert.Equal(FirmaId, kok.GetProperty("FirmaId").GetInt32());
            Assert.Equal("0001", kok.GetProperty("FirmaKodu").GetString());
            Assert.Equal("102 1 1 01", kok.GetProperty("BankaHesabiOrkaKodu").GetString());
            Assert.Equal(175, kok.GetProperty("SatirSayisi").GetInt32());
        }

        [Fact]
        public async Task Satir_sayisi_disa_aktarimdan_geliyor_yuklemeden_degil()
        {
            // "Diğer bankada" işaretli satırlar ORKA'ya gitmiyor; ajanın
            // doğrulaması bu sayıya bakıyor.
            using var db = Db();
            var yuklemeId = await VeriKurAsync(db);
            var ekstreler = new SahteEkstreServisi { SatirSayisi = 170 };

            var (yuk, _) = await Servis(db, ekstreler).HazirlaAsync(yuklemeId);

            using var belge = JsonDocument.Parse(yuk!);
            Assert.Equal(170, belge.RootElement.GetProperty("SatirSayisi").GetInt32());
        }

        [Fact]
        public async Task Firmanin_orka_kodu_yoksa_is_olusmuyor()
        {
            using var db = Db();
            var yuklemeId = await VeriKurAsync(db, orkaFirmaKodu: null);

            var (yuk, hata) = await Servis(db, new SahteEkstreServisi()).HazirlaAsync(yuklemeId);

            Assert.Null(yuk);
            Assert.Contains("ORKA firma kodu", hata);

            // Mesaj hangi firmada eksik olduğunu ve alanın adını söylüyor: eski hâli
            // kullanıcıyı formda var olmayan bir alana yolluyordu.
            Assert.Contains("ORNEK", hata);
            Assert.Contains("Firmalarım", hata);
            Assert.Contains("ORKA Firma Kodu", hata);
        }

        [Fact]
        public async Task Orka_kodu_bosluktan_ibaretse_de_is_olusmuyor()
        {
            using var db = Db();
            var yuklemeId = await VeriKurAsync(db, orkaFirmaKodu: "   ");

            var (yuk, hata) = await Servis(db, new SahteEkstreServisi()).HazirlaAsync(yuklemeId);

            Assert.Null(yuk);
            Assert.Contains("ORKA firma kodu", hata);
        }

        [Fact]
        public async Task Banka_hesabinin_orka_kodu_yoksa_is_olusmuyor()
        {
            using var db = Db();
            var yuklemeId = await VeriKurAsync(db, orkaHesapKodu: "");

            var (yuk, hata) = await Servis(db, new SahteEkstreServisi()).HazirlaAsync(yuklemeId);

            Assert.Null(yuk);
            Assert.Contains("Banka hesabının ORKA kodu", hata);
        }

        [Fact]
        public async Task Olmayan_ekstre_icin_is_olusmuyor()
        {
            using var db = Db();
            await VeriKurAsync(db);

            var (yuk, hata) = await Servis(db, new SahteEkstreServisi()).HazirlaAsync(999);

            Assert.Null(yuk);
            Assert.Contains("bulunamadı", hata);
        }

        [Fact]
        public async Task Ekstre_secilmemisse_is_olusmuyor()
        {
            using var db = Db();

            var (yuk, hata) = await Servis(db, new SahteEkstreServisi()).HazirlaAsync(0);

            Assert.Null(yuk);
            Assert.Contains("seçilmedi", hata);
        }

        [Fact]
        public async Task Cozulemeyen_satir_varsa_is_olusmuyor()
        {
            // Robotu göndermenin anlamı yok: kod listesi zaten üretilemiyor.
            using var db = Db();
            var yuklemeId = await VeriKurAsync(db);
            var ekstreler = new SahteEkstreServisi
            {
                Hata = new BankaEkstreKuralException("satirlar", "12 satır onay bekliyor; dışa aktarım yapılamaz.")
            };

            var (yuk, hata) = await Servis(db, ekstreler).HazirlaAsync(yuklemeId);

            Assert.Null(yuk);
            Assert.Contains("onay bekliyor", hata);
        }

        [Fact]
        public async Task Aktarilacak_satir_yoksa_is_olusmuyor()
        {
            using var db = Db();
            var yuklemeId = await VeriKurAsync(db);

            var (yuk, hata) = await Servis(db, new SahteEkstreServisi { SatirSayisi = 0 }).HazirlaAsync(yuklemeId);

            Assert.Null(yuk);
            Assert.Contains("gidecek satır yok", hata);
        }

        /// <summary>
        /// Dışa aktarımın yalnız satır sayısı ve hata davranışı ilgilendiriyor;
        /// gerçek servis kendi testlerinde sınanıyor.
        /// </summary>
        private sealed class SahteEkstreServisi : IEkstreService
        {
            public int SatirSayisi { get; set; } = 175;
            public Exception? Hata { get; set; }

            public Task<DisaAktarimSonucDto?> DisaAktarAsync(int ekstreId, CancellationToken ct = default)
            {
                if (Hata is not null) throw Hata;

                return Task.FromResult<DisaAktarimSonucDto?>(new DisaAktarimSonucDto
                {
                    EkstreId = ekstreId,
                    SatirSayisi = SatirSayisi
                });
            }

            // ---- bu testlerin kullanmadigi uyeler ----
            public Task<List<EkstreYuklemeDto>> GetYuklemelerAsync(CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<EkstreYuklemeDto?> GetYuklemeAsync(int id, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<EkstreYuklemeDto> YukleAsync(int bankaHesabiId, Stream akis, string dosyaAdi, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<List<EkstreSatirDto>?> GetSatirlarAsync(int ekstreId, SatirDurum? durum, int? kategoriId, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<EkstreSatirDto?> OnaylaAsync(int satirId, string hesapKodu, bool kisiYonlendir, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<EkstreSatirDto?> DigerBankadaAsync(int satirId, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<EkstreDosyasi?> DuzeltilmisEkstreAsync(int ekstreId, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<EkstreDosyasi?> AnalizDokumuAsync(int ekstreId, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<bool> SilAsync(int ekstreId, CancellationToken ct = default)
                => throw new NotSupportedException();
        }
    }
}
