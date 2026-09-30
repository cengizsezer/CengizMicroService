using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.FirmaBilgileri.Domain;
using CatalogService.Api.Features.FirmaKontrol.Domain;
using CatalogService.Api.Features.Firmalar;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.BankaEkstre;

namespace CatalogService.UnitTests.Anasayfa
{
    /// <summary>
    /// Anasayfa künyesi — sol raydaki eksik alan rozeti ve "Eksik bilgi" şeridi. İkisi
    /// aynı listeden (<see cref="FirmaEksikBilgi"/>) besleniyor; sayı ile şerit ayrışamaz.
    /// </summary>
    public class FirmaEksikBilgiTests
    {
        private static readonly DateTime Bugun = new(2026, 9, 28);

        private const int Dgr = 3;
        private const int Tam = 5;

        private static CatalogContext Context()
        {
            var db = BankaEkstreTestOrtami.YeniContext();

            // DGR — doğrulama listesindeki değerler: MERSİS, işe başlama ve sorumlu boş.
            db.Firmalar.Add(new Firma
            {
                Id = Dgr, Unvan = "DGR A.Ş.", KisaAd = "DGR", VergiKimlikNo = "2951070824",
                VergiDairesi = "SARIYER", TicaretSicilNo = "305811-5", OrkaFirmaKodu = "0521", Aktif = true
            });
            db.FirmaSicilBilgileri.Add(new FirmaSicilBilgisi
            {
                Id = 1, FirmaId = Dgr, NaceKodu = "661907", Sermaye = 500_000, KurulusTarihi = new DateTime(2021, 4, 14),
                Adres = "Sarıyer, İstanbul", EFatura = true, EDefter = true,
                MukellefiyetTurleri = "0003, 0010, 0015, 0033, 0040"
            });
            db.FirmaOrtaklari.Add(new FirmaOrtak { Id = 1, FirmaId = Dgr, Ad = "Ortak", PayOrani = 100, PayTutari = 500_000 });
            db.FirmaImzaYetkilileri.Add(new FirmaImzaYetkilisi
            {
                Id = 1, FirmaId = Dgr, Ad = "Zeynep POTUR", YetkiBitis = new DateTime(2028, 12, 22)
            });

            // Künyesi tam firma.
            db.Firmalar.Add(new Firma
            {
                Id = Tam, Unvan = "TAM A.Ş.", KisaAd = "TAM", VergiKimlikNo = "1111111111", VergiDairesi = "Kadıköy",
                TicaretSicilNo = "1-1", Aktif = true, DefterUsulu = DefterUsulu.BilancoEsasi,
                VergiTuru = VergiTuru.KurumlarVergisi, MizanFormati = "Luca", HesapDonemi = HesapDonemi.TakvimYili,
                SorumluKullaniciId = 16, SorumluKullaniciAdi = "cengiz"
            });
            db.FirmaSicilBilgileri.Add(new FirmaSicilBilgisi
            {
                Id = 2, FirmaId = Tam, MersisNo = "0111", NaceKodu = "69.20", IseBaslamaTarihi = new DateTime(2020, 1, 1),
                Sermaye = 10_000, KurulusTarihi = new DateTime(2020, 1, 1), Adres = "Kadıköy"
            });
            db.FirmaOrtaklari.Add(new FirmaOrtak { Id = 2, FirmaId = Tam, Ad = "O", PayOrani = 100 });
            db.FirmaImzaYetkilileri.Add(new FirmaImzaYetkilisi { Id = 2, FirmaId = Tam, Ad = "Y" });
            db.FirmaKontrolMizanAgaclari.Add(new FirmaKontrolMizanAgac
            {
                FirmaId = Tam, Donem = 1, Yil = 2026, DugumSayisi = 1, UploadedAt = Bugun
            });

            db.SaveChanges();
            return db;
        }

        private static Task<FirmaPaneliDto> Panel(CatalogContext db, int firmaId)
            => new FirmaPaneliService(db, () => Bugun).PanelAsync(firmaId);

        [Fact]
        public void Kontrol_listesi_on_alti_alan_arti_kosullu_cari_mizan()
            => Assert.Equal(17, FirmaEksikBilgi.KontrolSayisi);

        [Fact]
        public void Bos_firmada_on_altisi_da_eksik()
        {
            var eksik = FirmaEksikBilgi.Eksikler(new FirmaKunyeGirdisi(new Firma(), null, 0, 0));

            Assert.Equal(16, eksik.Count);
            Assert.Equal(16, eksik.Select(e => e.Odak).Distinct().Count());
        }

        [Fact]
        public async Task DGR_mersis_sorumlu_ve_ise_baslama_eksik_rayda_ayni_sayi()
        {
            using var db = Context();
            await FirmaSiniflandirmaSeed.SeedAsync(db);

            var panel = await Panel(db, Dgr);
            var detay = panel.Secili!;
            var odaklar = detay.Eksikler.Select(e => e.Odak).ToList();

            Assert.Contains("mersisNo", odaklar);
            Assert.Contains("sorumlu", odaklar);
            Assert.Contains("iseBaslama", odaklar);
            Assert.True(detay.Eksikler.Count >= 3);

            // Sınıflandırma 0010'dan doldu → defter usulü / vergi türü eksik DEĞİL.
            Assert.DoesNotContain("defterUsulu", odaklar);
            Assert.DoesNotContain("vergiTuru", odaklar);

            // Ray rozeti şeritle aynı liste.
            Assert.Equal(detay.Eksikler.Count, panel.Firmalar.Single(f => f.FirmaId == Dgr).EksikSayisi);
        }

        [Fact]
        public async Task Seritte_kart_adi_ve_alan_birlikte()
        {
            using var db = Context();

            var eksikler = (await Panel(db, Dgr)).Secili!.Eksikler;

            var mersis = eksikler.Single(e => e.Odak == "mersisNo");
            Assert.Equal("Sicil", mersis.Kart);
            Assert.Equal("MERSİS no", mersis.Alan);

            var sorumlu = eksikler.Single(e => e.Odak == "sorumlu");
            Assert.Equal("Takip", sorumlu.Kart);
            Assert.Equal("sorumlu atanmamış", sorumlu.Alan);
        }

        [Fact]
        public async Task Kunyesi_tam_firmada_eksik_yok()
        {
            using var db = Context();

            var panel = await Panel(db, Tam);

            Assert.Empty(panel.Secili!.Eksikler);
            var satir = panel.Firmalar.Single(f => f.FirmaId == Tam);
            Assert.Equal(0, satir.EksikSayisi);
            Assert.False(satir.MizanYok);
            Assert.Equal("Luca", satir.MizanFormati);
        }

        [Fact]
        public async Task Cari_yil_mizani_yoksa_mizan_yok_isaretli()
        {
            using var db = Context();

            var satir = (await Panel(db, Dgr)).Firmalar.Single(f => f.FirmaId == Dgr);

            Assert.True(satir.MizanYok);
            Assert.Null(satir.MizanFormati);     // belirsiz → ekranda "—"
        }

        [Fact]
        public async Task Yalniz_onceki_yil_mizani_varsa_kirmizi_degil_sari_sayi_bir_artar()
        {
            using var db = Context();
            var once = (await Panel(db, Dgr)).Firmalar.Single(f => f.FirmaId == Dgr);
            Assert.True(once.MizanYok);                   // hiç mizan yok → kırmızı

            db.FirmaKontrolMizanAgaclari.Add(new FirmaKontrolMizanAgac
            {
                FirmaId = Dgr, Donem = 0, Yil = 2026, DugumSayisi = 1, UploadedAt = Bugun   // 2025'in mizanı
            });
            db.SaveChanges();

            var panel = await Panel(db, Dgr);
            var sonra = panel.Firmalar.Single(f => f.FirmaId == Dgr);

            // Geçmiş dönemi var, cari (2026) yok: kırmızı DEĞİL, eksik sayısı bir artmış.
            Assert.False(sonra.MizanYok);
            Assert.Equal(once.EksikSayisi + 1, sonra.EksikSayisi);
            var madde = panel.Secili!.Eksikler.Single(e => e.Odak == "cariMizan");
            Assert.Equal("Mizan", madde.Kart);
        }

        [Fact]
        public async Task Belirsiz_mizan_formati_eksik_sayiliyor()
        {
            using var db = Context();
            db.Firmalar.Find(Tam)!.MizanFormati = MizanFormatlari.Belirsiz;
            db.SaveChanges();

            var eksik = Assert.Single((await Panel(db, Tam)).Secili!.Eksikler);
            Assert.Equal("mizanFormati", eksik.Odak);
        }
    }
}
