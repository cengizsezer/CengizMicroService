using CatalogService.Api.Features.Ajanlar;
using CatalogService.Api.Features.Ajanlar.Domain;
using CatalogService.Api.Features.Ajanlar.Dtos;
using CatalogService.Api.Features.Ajanlar.Services;
using CatalogService.Api.Infrastructure.Accessor;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CatalogService.UnitTests.Ajanlar
{
    /// <summary>
    /// İşin ömrü boyunca uyulması gereken kurallar: tek iş, sahiplik, tekrarlanan
    /// bildirimin zararsızlığı, zaman aşımı ve kopan bağlantı.
    ///
    /// Hepsi gerçek bir soket olmadan sınanıyor — gönderim
    /// <see cref="IAjanIsGondericisi"/> arkasında.
    /// </summary>
    public class AjanIsServisiTests
    {
        private const string Ajan = "7";
        private const string BaskaAjan = "8";
        private const int FirmaId = 201;

        private static (AjanIsServisi Servis, CatalogContext Db, SahteGonderici Gonderici, AjanDeposu Depo, SahteSaat Saat)
            Kur(SahteSaat? saat = null, int zamanAsimiDakika = 15)
        {
            saat ??= new SahteSaat();

            var db = new CatalogContext(
                new DbContextOptionsBuilder<CatalogContext>()
                    .UseInMemoryDatabase($"ajan-is-{Guid.NewGuid():N}")
                    .Options,
                new FixedTenantAccessor("test"));

            var ayar = new SabitAyar<AgentHubAyarlari>(
                new AgentHubAyarlari { IsZamanAsimiDakika = zamanAsimiDakika });

            var depo = new AjanDeposu(ayar, saat);

            // Sahte gönderici de gerçeği gibi depoya bakıyor: "ajan bağlı mı"
            // sorusunun tek bir yanıtı olsun.
            var gonderici = new SahteGonderici { BagliMi = id => depo.AjanaGoreBul(id) is not null };

            // OrkayaAktar yükünü kuran servis ayrı sınanıyor (OrkaAktarimYukuTests);
            // buradaki testler SahteAktarim tipiyle çalışıyor ve ona hiç uğramıyor.
            var servis = new AjanIsServisi(db, depo, gonderici, new SahteAktarimYuku(),
                                           ayar, saat, NullLogger<AjanIsServisi>.Instance);
            return (servis, db, gonderici, depo, saat);
        }

        private static YeniAjanIsiDto Istek(string? ajanId = Ajan, int firmaId = FirmaId)
            => new() { AjanId = ajanId, FirmaId = firmaId, IsTipi = AjanIsTipleri.SahteAktarim };

        private static void AjaniBagla(AjanDeposu depo, string ajanId, string connectionId = "c1")
            => depo.Kaydet(new AjanKaydi
            {
                ConnectionId = connectionId,
                MakineId = "BANKA-PC-" + ajanId,
                MakineAdi = "BANKA-PC",
                AjanId = ajanId
            });

        [Fact]
        public async Task Bagli_ajana_is_olusturulup_gonderiliyor()
        {
            var (servis, db, gonderici, depo, _) = Kur();
            AjaniBagla(depo, Ajan);

            var sonuc = await servis.OlusturAsync(Istek(), "kullanici-1");

            Assert.NotNull(sonuc.Is);
            Assert.Null(sonuc.CakisanIs);
            Assert.Equal(AjanIsDurumu.Gonderildi, sonuc.Is!.Durum);
            Assert.True(sonuc.Is.AjanBagliydi);

            var paket = Assert.Single(gonderici.Gonderilenler);
            Assert.Equal(sonuc.Is.Id, paket.Paket.IsId);
            Assert.Equal(AjanIsTipleri.SahteAktarim, paket.Paket.IsTipi);
            Assert.Equal(FirmaId, paket.Paket.FirmaId);

            var kayit = await db.AjanIsleri.SingleAsync();
            Assert.NotNull(kayit.GonderimZamani);
        }

        [Fact]
        public async Task Ajan_bagli_degilken_is_bekliyor_kaliyor()
        {
            var (servis, _, gonderici, _, _) = Kur();

            var sonuc = await servis.OlusturAsync(Istek(), "kullanici-1");

            Assert.NotNull(sonuc.Is);
            Assert.Equal(AjanIsDurumu.Bekliyor, sonuc.Is!.Durum);
            Assert.False(sonuc.Is.AjanBagliydi);
            Assert.Empty(gonderici.Gonderilenler);
            Assert.Contains("bağlı değil", sonuc.Mesaj);
        }

        [Fact]
        public async Task Ajan_baglaninca_bekleyen_is_gonderiliyor()
        {
            var (servis, db, gonderici, depo, _) = Kur();
            var sonuc = await servis.OlusturAsync(Istek(), "kullanici-1");
            Assert.Empty(gonderici.Gonderilenler);

            AjaniBagla(depo, Ajan);
            await servis.BekleyenleriGonderAsync(Ajan);

            Assert.Single(gonderici.Gonderilenler);
            var kayit = await db.AjanIsleri.SingleAsync(x => x.Id == sonuc.Is!.Id);
            Assert.Equal(AjanIsDurumu.Gonderildi, kayit.Durum);
        }

        [Fact]
        public async Task Ayni_ajana_ikinci_is_reddediliyor()
        {
            // Robot tek ORKA penceresiyle çalışıyor; paralel iş anlamsız.
            var (servis, _, _, depo, _) = Kur();
            AjaniBagla(depo, Ajan);
            var ilk = await servis.OlusturAsync(Istek(), "kullanici-1");

            var ikinci = await servis.OlusturAsync(Istek(), "kullanici-1");

            Assert.Null(ikinci.Is);
            Assert.NotNull(ikinci.CakisanIs);
            Assert.Equal(ilk.Is!.Id, ikinci.CakisanIs!.Id);
        }

        [Fact]
        public async Task Onceki_is_bitince_yeni_is_acilabiliyor()
        {
            var (servis, _, _, depo, _) = Kur();
            AjaniBagla(depo, Ajan);
            var ilk = await servis.OlusturAsync(Istek(), "kullanici-1");
            await servis.BittiAsync(Ajan, ilk.Is!.Id, basarili: true, null, "{}");

            var ikinci = await servis.OlusturAsync(Istek(), "kullanici-1");

            Assert.NotNull(ikinci.Is);
            Assert.Null(ikinci.CakisanIs);
        }

        [Fact]
        public async Task Is_bitince_siradaki_bekleyen_gonderiliyor()
        {
            // Kuyruk kendiliğinden ilerlemeli: ajan boşa çıktığı anda sıradaki iş
            // gitsin, bir sonraki bağlanmayı beklemesin.
            var (servis, db, gonderici, depo, _) = Kur();
            AjaniBagla(depo, Ajan);

            var ilk = (await servis.OlusturAsync(Istek(), "k")).Is!;

            // İkinci iş açık iş varken reddedilir; sıraya elle koyuyoruz.
            db.AjanIsleri.Add(new AjanIsi
            {
                Id = Guid.NewGuid(),
                AjanId = Ajan,
                FirmaId = FirmaId,
                IsTipi = AjanIsTipleri.SahteAktarim,
                Durum = AjanIsDurumu.Bekliyor,
                OlusturmaZamani = DateTime.UtcNow.AddMinutes(1),
                OlusturanKullaniciId = "k"
            });
            await db.SaveChangesAsync();
            Assert.Single(gonderici.Gonderilenler);

            await servis.BittiAsync(Ajan, ilk.Id, basarili: true, null, "{}");

            Assert.Equal(2, gonderici.Gonderilenler.Count);
            Assert.Equal(1, await db.AjanIsleri.CountAsync(x => x.Durum == AjanIsDurumu.Gonderildi));
        }

        [Fact]
        public async Task Baska_ajanin_isi_guncellenemiyor()
        {
            var (servis, db, _, depo, _) = Kur();
            AjaniBagla(depo, Ajan);
            var sonuc = await servis.OlusturAsync(Istek(), "kullanici-1");

            Assert.False(await servis.BasladiAsync(BaskaAjan, sonuc.Is!.Id));
            Assert.False(await servis.IlerlemeAsync(BaskaAjan, sonuc.Is.Id, 50, "yarisi", 5));
            Assert.False(await servis.BittiAsync(BaskaAjan, sonuc.Is.Id, true, null, "{}"));

            var kayit = await db.AjanIsleri.SingleAsync();
            Assert.Equal(AjanIsDurumu.Gonderildi, kayit.Durum);
            Assert.Equal(0, kayit.IlerlemeYuzde);
        }

        [Fact]
        public async Task Ayni_ilerleme_iki_kez_gelince_durum_bozulmuyor()
        {
            // Ağ kopup yeniden bağlanan ajan son durumu tekrar gönderebiliyor.
            var (servis, db, _, depo, _) = Kur();
            AjaniBagla(depo, Ajan);
            var isDto = (await servis.OlusturAsync(Istek(), "kullanici-1")).Is!;

            await servis.BasladiAsync(Ajan, isDto.Id);
            await servis.BasladiAsync(Ajan, isDto.Id);
            await servis.IlerlemeAsync(Ajan, isDto.Id, 60, "altmis", 6);
            await servis.IlerlemeAsync(Ajan, isDto.Id, 60, "altmis", 6);

            var kayit = await db.AjanIsleri.SingleAsync();
            Assert.Equal(AjanIsDurumu.Calisiyor, kayit.Durum);
            Assert.Equal(60, kayit.IlerlemeYuzde);
            Assert.Equal(6, kayit.TamamlananAdim);
        }

        [Fact]
        public async Task Geciken_eski_ilerleme_cubugu_geri_sarmiyor()
        {
            var (servis, db, _, depo, _) = Kur();
            AjaniBagla(depo, Ajan);
            var isDto = (await servis.OlusturAsync(Istek(), "kullanici-1")).Is!;

            await servis.IlerlemeAsync(Ajan, isDto.Id, 80, "seksen", 8);
            await servis.IlerlemeAsync(Ajan, isDto.Id, 30, "otuz", 3);

            var kayit = await db.AjanIsleri.SingleAsync();
            Assert.Equal(80, kayit.IlerlemeYuzde);
            Assert.Equal(8, kayit.TamamlananAdim);
        }

        [Fact]
        public async Task Biten_ise_gelen_gec_bildirim_durumu_degistirmiyor()
        {
            var (servis, db, _, depo, _) = Kur();
            AjaniBagla(depo, Ajan);
            var isDto = (await servis.OlusturAsync(Istek(), "kullanici-1")).Is!;
            await servis.BittiAsync(Ajan, isDto.Id, basarili: true, null, "{\"YazilanSatir\":5}");

            await servis.IlerlemeAsync(Ajan, isDto.Id, 40, "gec kalan", 4);
            await servis.BittiAsync(Ajan, isDto.Id, basarili: false, "sonradan gelen hata", null);

            var kayit = await db.AjanIsleri.SingleAsync();
            Assert.Equal(AjanIsDurumu.Tamamlandi, kayit.Durum);
            Assert.Null(kayit.HataMesaji);
            Assert.Equal(100, kayit.IlerlemeYuzde);
        }

        [Fact]
        public async Task Ilerleme_gelmeyince_zaman_asimi_isaretleniyor()
        {
            var saat = new SahteSaat();
            var (servis, db, _, depo, _) = Kur(saat, zamanAsimiDakika: 15);
            AjaniBagla(depo, Ajan);
            var isDto = (await servis.OlusturAsync(Istek(), "kullanici-1")).Is!;
            await servis.BasladiAsync(Ajan, isDto.Id);

            saat.Ilerle(TimeSpan.FromMinutes(16));
            var okunan = await servis.GetirAsync(isDto.Id);

            Assert.Equal(AjanIsDurumu.ZamanAsimi, okunan!.Durum);
            Assert.Contains("zaman aşımına", okunan.HataMesaji);
            Assert.Equal(AjanIsDurumu.ZamanAsimi, (await db.AjanIsleri.SingleAsync()).Durum);
        }

        [Fact]
        public async Task Ilerleme_geldikce_zaman_asimi_erteleniyor()
        {
            var saat = new SahteSaat();
            var (servis, _, _, depo, _) = Kur(saat, zamanAsimiDakika: 15);
            AjaniBagla(depo, Ajan);
            var isDto = (await servis.OlusturAsync(Istek(), "kullanici-1")).Is!;

            for (var i = 0; i < 3; i++)
            {
                saat.Ilerle(TimeSpan.FromMinutes(10));
                await servis.IlerlemeAsync(Ajan, isDto.Id, 10 * (i + 1), $"adim {i}", i + 1);
            }

            var okunan = await servis.GetirAsync(isDto.Id);

            // Toplam 30 dakika geçti ama araları hep 10 dakikaydı.
            Assert.Equal(AjanIsDurumu.Calisiyor, okunan!.Durum);
        }

        [Fact]
        public async Task Zaman_asimina_ugrayan_is_ajani_mesgul_birakmiyor()
        {
            var saat = new SahteSaat();
            var (servis, _, _, depo, _) = Kur(saat, zamanAsimiDakika: 15);
            AjaniBagla(depo, Ajan);
            await servis.OlusturAsync(Istek(), "kullanici-1");

            saat.Ilerle(TimeSpan.FromMinutes(20));
            var yeni = await servis.OlusturAsync(Istek(), "kullanici-1");

            Assert.NotNull(yeni.Is);
            Assert.Null(yeni.CakisanIs);
        }

        [Fact]
        public async Task Ajan_baglantisi_kopunca_calisan_is_basarisiz_oluyor()
        {
            var (servis, db, _, depo, _) = Kur();
            AjaniBagla(depo, Ajan);
            var isDto = (await servis.OlusturAsync(Istek(), "kullanici-1")).Is!;
            await servis.BasladiAsync(Ajan, isDto.Id);

            await servis.BaglantiKoptuAsync(Ajan);

            var kayit = await db.AjanIsleri.SingleAsync();
            Assert.Equal(AjanIsDurumu.Basarisiz, kayit.Durum);
            Assert.Contains("bağlantısı koptu", kayit.HataMesaji);
            Assert.NotNull(kayit.BitisZamani);
        }

        [Fact]
        public async Task Iptal_edilen_is_ajana_bildiriliyor()
        {
            var (servis, db, gonderici, depo, _) = Kur();
            AjaniBagla(depo, Ajan);
            var isDto = (await servis.OlusturAsync(Istek(), "kullanici-1")).Is!;

            var iptal = await servis.IptalAsync(isDto.Id);

            Assert.Equal(AjanIsDurumu.IptalEdildi, iptal!.Durum);
            Assert.Equal(isDto.Id, Assert.Single(gonderici.IptalBildirilenler).IsId);
            Assert.Equal(AjanIsDurumu.IptalEdildi, (await db.AjanIsleri.SingleAsync()).Durum);
        }

        [Fact]
        public async Task Bitmis_is_iptal_edilemiyor()
        {
            var (servis, _, gonderici, depo, _) = Kur();
            AjaniBagla(depo, Ajan);
            var isDto = (await servis.OlusturAsync(Istek(), "kullanici-1")).Is!;
            await servis.BittiAsync(Ajan, isDto.Id, basarili: true, null, "{}");

            var iptal = await servis.IptalAsync(isDto.Id);

            Assert.Equal(AjanIsDurumu.Tamamlandi, iptal!.Durum);
            Assert.Empty(gonderici.IptalBildirilenler);
        }

        [Fact]
        public async Task Firma_secilmeden_is_olusturulamiyor()
        {
            // FirmaId kapsamı yazmayan kayıt SaveChanges'te de reddediliyor;
            // burada erken ve anlaşılır bir mesajla duruyor.
            var (servis, db, _, depo, _) = Kur();
            AjaniBagla(depo, Ajan);

            var sonuc = await servis.OlusturAsync(Istek(firmaId: 0), "kullanici-1");

            Assert.Null(sonuc.Is);
            Assert.Contains("Firma", sonuc.Mesaj);
            Assert.Empty(db.AjanIsleri);
        }

        [Fact]
        public async Task Hedef_verilmezse_tek_bagli_ajan_seciliyor()
        {
            var (servis, _, gonderici, depo, _) = Kur();
            AjaniBagla(depo, Ajan);

            var sonuc = await servis.OlusturAsync(Istek(ajanId: null), "kullanici-1");

            Assert.NotNull(sonuc.Is);
            Assert.Equal(Ajan, sonuc.Is!.AjanId);
            Assert.Single(gonderici.Gonderilenler);
        }

        [Fact]
        public async Task Hedef_verilmezse_ve_birden_fazla_ajan_bagliysa_reddediliyor()
        {
            // Yanlış makineye iş göndermek sessiz bir hata olurdu.
            var (servis, _, _, depo, _) = Kur();
            AjaniBagla(depo, Ajan, "c1");
            AjaniBagla(depo, BaskaAjan, "c2");

            var sonuc = await servis.OlusturAsync(Istek(ajanId: null), "kullanici-1");

            Assert.Null(sonuc.Is);
            Assert.Contains("seçin", sonuc.Mesaj);
        }

        [Fact]
        public async Task Hic_ajan_yoksa_anlasilir_mesaj_donuyor()
        {
            var (servis, _, _, _, _) = Kur();

            var sonuc = await servis.OlusturAsync(Istek(ajanId: null), "kullanici-1");

            Assert.Null(sonuc.Is);
            Assert.Contains("hiç ajan bağlanmadı", sonuc.Mesaj);
        }

        [Fact]
        public async Task Liste_firmaya_ve_duruma_gore_suzuluyor()
        {
            var (servis, _, _, depo, _) = Kur();
            AjaniBagla(depo, Ajan);
            var ilk = (await servis.OlusturAsync(Istek(firmaId: 201), "k"))!.Is!;
            await servis.BittiAsync(Ajan, ilk.Id, true, null, "{}");
            await servis.OlusturAsync(Istek(firmaId: 202), "k");

            var hepsi = await servis.ListeleAsync(null, null, null);
            var firma201 = await servis.ListeleAsync(201, null, null);
            var bitenler = await servis.ListeleAsync(null, AjanIsDurumu.Tamamlandi, null);

            Assert.Equal(2, hepsi.Count);
            Assert.Equal(201, Assert.Single(firma201).FirmaId);
            Assert.Equal(ilk.Id, Assert.Single(bitenler).Id);
        }

        // ---- test yardımcıları ---------------------------------------------

        /// <summary>OrkayaAktar yükünü kurmayan sahte; bu testler o yola girmiyor.</summary>
        private sealed class SahteAktarimYuku : IOrkaAktarimYuku
        {
            public Task<(string? Yuk, string? Hata)> HazirlaAsync(int ekstreYuklemeId, CancellationToken ct = default)
                => Task.FromResult<(string?, string?)>((null, "Bu testte ORKA aktarımı kullanılmıyor."));
        }

        private sealed class SahteGonderici : IAjanIsGondericisi
        {
            public List<(string AjanId, AjanIsPaketiDto Paket)> Gonderilenler { get; } = new();
            public List<(string AjanId, Guid IsId)> IptalBildirilenler { get; } = new();

            public Task<bool> GonderAsync(string ajanId, AjanIsPaketiDto paket, CancellationToken ct = default)
            {
                // Depoda bağlı görünüyorsa gönderilmiş sayılır; gerçek gönderici de
                // depoya bakıyor.
                if (!Bagli(ajanId)) return Task.FromResult(false);
                Gonderilenler.Add((ajanId, paket));
                return Task.FromResult(true);
            }

            public Task<bool> IptalBildirAsync(string ajanId, Guid isId, CancellationToken ct = default)
            {
                if (!Bagli(ajanId)) return Task.FromResult(false);
                IptalBildirilenler.Add((ajanId, isId));
                return Task.FromResult(true);
            }

            /// <summary>Ajan bağlı mı? Testlerde depoya bağlanıyor.</summary>
            public Func<string, bool>? BagliMi { get; init; }

            private bool Bagli(string ajanId) => BagliMi?.Invoke(ajanId) ?? true;
        }
    }
}
