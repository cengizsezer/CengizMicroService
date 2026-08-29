using CatalogService.Api.Features.Declarations;
using CatalogService.Api.Features.Declarations.Dtos;
using CatalogService.Api.Features.Declarations.Entities;
using CatalogService.Api.Features.Declarations.Services;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.BankaEkstre;

namespace CatalogService.UnitTests.Beyannameler
{
    /// <summary>
    /// Beyanname belgeleri. Dosyanın kendisi FileApiService'te; burada sınanan şey
    /// <b>kurallar</b>: yalnız PDF, boyut sınırı, dekontun ödeme şartı ve aynı türden
    /// ikinci belgenin eskisinin yerine geçmesi.
    ///
    /// Doğrulamanın sunucuda olması şart: istemcideki kontrol kullanıcıya hızlı geri
    /// bildirim için, kaydın doğruluğu için değil.
    /// </summary>
    public class BeyannameEkServiceTests
    {
        private const int OdenmemisId = 1;
        private const int OdenmisId = 2;

        private static CatalogContext Context()
        {
            var db = BankaEkstreTestOrtami.YeniContext();

            db.Declarations.Add(new Declaration
            {
                Id = OdenmemisId,
                TenantNo = "201",
                CompanyName = "ALPHA AHŞAP",
                DeclarationType = "0015 KDV-1",
                Year = 2026,
                Month = 8,
                Amount = 1000m,
                DueDate = new DateTime(2026, 8, 26),
                DeclarationStatus = DeclarationStatus.Approved,
                PaymentStatus = PaymentStatus.Pending,
                CustomerCompanyId = 1
            });

            db.Declarations.Add(new Declaration
            {
                Id = OdenmisId,
                TenantNo = "201",
                CompanyName = "CİTADEL GAYRİMENKUL",
                DeclarationType = "0015 KDV-1",
                Year = 2026,
                Month = 8,
                Amount = 2000m,
                DueDate = new DateTime(2026, 8, 26),
                DeclarationStatus = DeclarationStatus.Approved,
                PaymentStatus = PaymentStatus.Paid,
                CustomerCompanyId = 3
            });

            db.SaveChanges();
            return db;
        }

        private static BeyannameEkOlusturDto Istek(BeyannameEkTuru tur = BeyannameEkTuru.Tahakkuk,
                                                   int fileId = 100,
                                                   string ad = "tahakkuk.pdf",
                                                   string tip = "application/pdf",
                                                   long boyut = 2048) => new()
        {
            Tur = tur,
            FileId = fileId,
            FileName = ad,
            ContentType = tip,
            Length = boyut
        };

        // ---- Mutlu yol ----

        [Fact]
        public async Task Pdf_belge_eklenir()
        {
            using var db = Context();
            var servis = new BeyannameEkService(db);

            var sonuc = await servis.EkleAsync(OdenmemisId, Istek(), "test.kullanici");

            Assert.Equal(BeyannameEkTuru.Tahakkuk, sonuc.Ek.Tur);
            Assert.Equal(100, sonuc.Ek.FileId);
            Assert.Equal("test.kullanici", sonuc.Ek.YukleyenKullanici);
            Assert.Null(sonuc.ArtikFileId);

            var ekler = await servis.GetAsync(OdenmemisId);
            Assert.Single(ekler);
        }

        [Fact]
        public async Task Icerik_tipi_bos_gelse_de_uzanti_pdf_ise_kabul_edilir()
        {
            // Tarayıcı bazen application/octet-stream gönderiyor; dosya yine PDF.
            using var db = Context();
            var servis = new BeyannameEkService(db);

            var sonuc = await servis.EkleAsync(OdenmemisId,
                Istek(tip: "application/octet-stream", ad: "tahakkuk.PDF"), null);

            // Kayıtta içerik tipi normalize edilir; alan neyin kabul edildiğini gösterir.
            Assert.Equal("application/pdf", sonuc.Ek.ContentType);
        }

        // ---- Doğrulama ----

        [Fact]
        public async Task Pdf_olmayan_belge_reddedilir()
        {
            using var db = Context();
            var servis = new BeyannameEkService(db);

            var hata = await Assert.ThrowsAsync<BeyannameKuralException>(
                () => servis.EkleAsync(OdenmemisId, Istek(tip: "image/png", ad: "tahakkuk.png"), null));

            Assert.Contains("PDF", hata.Message);
        }

        [Fact]
        public async Task Sinirdan_buyuk_dosya_reddedilir()
        {
            using var db = Context();
            var servis = new BeyannameEkService(db);

            await Assert.ThrowsAsync<BeyannameKuralException>(
                () => servis.EkleAsync(OdenmemisId, Istek(boyut: BeyannameEkService.EnFazlaBayt + 1), null));
        }

        [Fact]
        public async Task Bos_dosya_reddedilir()
        {
            using var db = Context();
            var servis = new BeyannameEkService(db);

            await Assert.ThrowsAsync<BeyannameKuralException>(
                () => servis.EkleAsync(OdenmemisId, Istek(boyut: 0), null));
        }

        [Fact]
        public async Task Dosya_kimligi_yoksa_reddedilir()
        {
            using var db = Context();
            var servis = new BeyannameEkService(db);

            await Assert.ThrowsAsync<BeyannameKuralException>(
                () => servis.EkleAsync(OdenmemisId, Istek(fileId: 0), null));
        }

        [Fact]
        public async Task Olmayan_beyannameye_belge_eklenemez()
        {
            using var db = Context();
            var servis = new BeyannameEkService(db);

            await Assert.ThrowsAsync<BeyannameKuralException>(
                () => servis.EkleAsync(999, Istek(), null));
        }

        // ---- Dekont kuralı ----

        [Fact]
        public async Task Odenmemis_beyannameye_dekont_eklenemez()
        {
            using var db = Context();
            var servis = new BeyannameEkService(db);

            var hata = await Assert.ThrowsAsync<BeyannameKuralException>(
                () => servis.EkleAsync(OdenmemisId, Istek(BeyannameEkTuru.Dekont, ad: "dekont.pdf"), null));

            Assert.Contains("ödendi", hata.Message);
        }

        [Fact]
        public async Task Odenmis_beyannameye_dekont_eklenir()
        {
            using var db = Context();
            var servis = new BeyannameEkService(db);

            var sonuc = await servis.EkleAsync(OdenmisId, Istek(BeyannameEkTuru.Dekont, ad: "dekont.pdf"), null);

            Assert.Equal(BeyannameEkTuru.Dekont, sonuc.Ek.Tur);
        }

        // ---- Değiştirme ve silme ----

        [Fact]
        public async Task Ayni_turden_ikinci_belge_eskisinin_yerine_gecer()
        {
            using var db = Context();
            var servis = new BeyannameEkService(db);

            await servis.EkleAsync(OdenmemisId, Istek(fileId: 100, ad: "eski.pdf"), null);
            var sonuc = await servis.EkleAsync(OdenmemisId, Istek(fileId: 200, ad: "yeni.pdf"), null);

            // İkinci yükleme yeni satır AÇMAZ; ikonun hangi dosyayı açacağı belirsiz kalmasın.
            var ekler = await servis.GetAsync(OdenmemisId);
            Assert.Single(ekler);
            Assert.Equal(200, ekler[0].FileId);
            Assert.Equal("yeni.pdf", ekler[0].FileName);

            // Eski dosya artık sahipsiz; çağıran onu FileApiService'ten silecek.
            Assert.Equal(100, sonuc.ArtikFileId);
        }

        [Fact]
        public async Task Farkli_turler_ayri_kayitlarda_durur()
        {
            using var db = Context();
            var servis = new BeyannameEkService(db);

            await servis.EkleAsync(OdenmisId, Istek(BeyannameEkTuru.Tahakkuk, 100, "tahakkuk.pdf"), null);
            await servis.EkleAsync(OdenmisId, Istek(BeyannameEkTuru.Beyanname, 101, "beyanname.pdf"), null);
            await servis.EkleAsync(OdenmisId, Istek(BeyannameEkTuru.Dekont, 102, "dekont.pdf"), null);

            var ekler = await servis.GetAsync(OdenmisId);

            Assert.Equal(3, ekler.Count);
            Assert.Equal(new[] { BeyannameEkTuru.Tahakkuk, BeyannameEkTuru.Beyanname, BeyannameEkTuru.Dekont },
                         ekler.Select(e => e.Tur));
        }

        [Fact]
        public async Task Silinen_belgenin_dosya_kimligi_geri_doner()
        {
            using var db = Context();
            var servis = new BeyannameEkService(db);

            var eklenen = await servis.EkleAsync(OdenmemisId, Istek(fileId: 321), null);
            var fileId = await servis.SilAsync(OdenmemisId, eklenen.Ek.Id);

            Assert.Equal(321, fileId);
            Assert.Empty(await servis.GetAsync(OdenmemisId));
        }

        [Fact]
        public async Task Baska_beyannamenin_belgesi_silinemez()
        {
            using var db = Context();
            var servis = new BeyannameEkService(db);

            var eklenen = await servis.EkleAsync(OdenmemisId, Istek(), null);

            await Assert.ThrowsAsync<BeyannameKuralException>(
                () => servis.SilAsync(OdenmisId, eklenen.Ek.Id));
        }

        // ---- Seed ----

        [Fact]
        public async Task Tur_tanimlari_tohumlanir_ve_tekrarlanmaz()
        {
            using var db = BankaEkstreTestOrtami.YeniContext();

            await BeyannameTuruSeed.SeedAsync(db);
            var ilk = db.BeyannameTurleri.Count();

            await BeyannameTuruSeed.SeedAsync(db);

            Assert.Equal(BeyannameTuruSeed.Turler.Length, ilk);
            Assert.Equal(ilk, db.BeyannameTurleri.Count());

            // Eski ekrandaki liste birebir korundu: kurulu veritabanlarındaki kayıtlar
            // bu değerlerle yazılmış durumda.
            Assert.Contains(db.BeyannameTurleri, t => t.Deger == "0015 KDV-1" && t.Kod == "0015");
        }
    }
}
