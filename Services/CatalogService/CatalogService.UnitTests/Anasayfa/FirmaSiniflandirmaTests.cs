using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Features.FirmaBilgileri.Domain;
using CatalogService.Api.Features.FirmaBilgileri.Dtos;
using CatalogService.Api.Features.FirmaBilgileri.Services;
using CatalogService.Api.Features.FirmaKontrol.Domain;
using CatalogService.Api.Features.FirmaKontrol.Dtos;
using CatalogService.Api.Features.FirmaKontrol.Services;
using CatalogService.Api.Features.Firmalar;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.BankaEkstre;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.UnitTests.Anasayfa
{
    /// <summary>
    /// Anasayfa künyesi — "Sınıflandırma ve mizan" kartı.
    ///
    /// Asıl sınanan: vergi türü / defter usulü mükellefiyet <b>kodundan</b> önerilir, adından
    /// değil. DGR'de hem 0003 ("GELİR VERGİSİ S. (MUHTASAR)") hem 0010 var; ad üzerinden
    /// eşleşen bir kural onu gelir vergisi mükellefi sanardı.
    /// </summary>
    public class FirmaSiniflandirmaTests
    {
        private const int Dgr = 3;
        private const int Sahis = 4;

        /// <summary>DGR'nin mükellefiyet satırı, adlarıyla birlikte.</summary>
        private const string DgrMukellefiyet =
            "0003 - GELİR VERGİSİ S. (MUHTASAR), 0010 - KURUMLAR VERGİSİ, 0015 - KATMA DEĞER VERGİSİ, " +
            "0033 - DAMGA VERGİSİ, 0040 - BANKA VE SİGORTA MUAMELELERİ VERGİSİ";

        private static CatalogContext Context()
        {
            var db = BankaEkstreTestOrtami.YeniContext();

            db.Firmalar.AddRange(
                new Firma
                {
                    Id = Dgr, Unvan = "DGR A.Ş.", KisaAd = "DGR", VergiKimlikNo = "2951070824",
                    VergiDairesi = "SARIYER", TicaretSicilNo = "305811-5", OrkaFirmaKodu = "0521", Aktif = true
                },
                new Firma
                {
                    Id = Sahis, Unvan = "AHMET YILMAZ", KisaAd = "AHMET YILMAZ", VergiKimlikNo = "12345678901",
                    VergiDairesi = "Kadıköy", Aktif = true
                });

            db.FirmaSicilBilgileri.AddRange(
                new FirmaSicilBilgisi
                {
                    Id = 1, FirmaId = Dgr, MukellefiyetTurleri = DgrMukellefiyet, NaceKodu = "661907",
                    Sermaye = 500_000, KurulusTarihi = new DateTime(2021, 4, 14), EFatura = true, EDefter = true
                },
                new FirmaSicilBilgisi { Id = 2, FirmaId = Sahis, MukellefiyetTurleri = "0001 - GELİR VERGİSİ, 0015 - KDV" });

            db.SaveChanges();
            return db;
        }

        private static Task<FirmaPaneliDto> Panel(CatalogContext db, int firmaId)
            => new FirmaPaneliService(db).PanelAsync(firmaId);

        // ---- Öneri kuralı (saf) ----

        [Fact]
        public void Kod_0003_siniflandirmada_kullanilmiyor_DGR_kurumlar_vergisi_mukellefi()
        {
            // Hem 0003 (adında "GELİR VERGİSİ" geçiyor) hem 0010 var: sonuç KURUMLAR.
            var oneri = MukellefiyetSiniflandirici.Oner(DgrMukellefiyet);

            Assert.Equal(VergiTuru.KurumlarVergisi, oneri.VergiTuru);
            Assert.Equal(DefterUsulu.BilancoEsasi, oneri.DefterUsulu);
            Assert.Equal("0010", oneri.KaynakKod);

            // Yalnız kodlar yazılmış hâli de aynı sonucu veriyor.
            Assert.Equal(VergiTuru.KurumlarVergisi, MukellefiyetSiniflandirici.Oner("0003, 0010, 0015, 0033, 0040").VergiTuru);
        }

        [Fact]
        public void Yalniz_0003_varsa_gelir_vergisi_SAYILMAZ()
        {
            // Ad "GELİR VERGİSİ S. (MUHTASAR)" — adla eşleşen kod burada gelir vergisi derdi.
            var oneri = MukellefiyetSiniflandirici.Oner("0003 - GELİR VERGİSİ S. (MUHTASAR), 0015 - KDV");

            Assert.Equal(VergiTuru.Belirsiz, oneri.VergiTuru);
            Assert.Equal(DefterUsulu.Belirsiz, oneri.DefterUsulu);
            Assert.Null(oneri.KaynakKod);
        }

        [Fact]
        public void Adinda_kurumlar_gecen_ama_kodu_olmayan_metin_siniflandirilmiyor()
        {
            // Eşleştirme adla değil: kodsuz serbest metin öneri üretmez.
            var oneri = MukellefiyetSiniflandirici.Oner("Kurumlar, KDV, Muhtasar");

            Assert.Equal(VergiTuru.Belirsiz, oneri.VergiTuru);
        }

        [Fact]
        public void Kod_0001_gelir_vergisi_defter_usulu_belirsiz()
        {
            // Şahıs hem bilanço hem işletme olabilir; kullanıcı seçer.
            var oneri = MukellefiyetSiniflandirici.Oner("0001 - GELİR VERGİSİ, 0015 - KDV");

            Assert.Equal(VergiTuru.GelirVergisi, oneri.VergiTuru);
            Assert.Equal(DefterUsulu.Belirsiz, oneri.DefterUsulu);
            Assert.Equal("0001", oneri.KaynakKod);
        }

        [Fact]
        public void Kod_0010_ve_0001_birlikteyse_0010_kazanir()
            => Assert.Equal(VergiTuru.KurumlarVergisi, MukellefiyetSiniflandirici.Oner("0001, 0010").VergiTuru);

        [Fact]
        public void Bes_haneli_sayinin_icindeki_0010_kod_sayilmiyor()
            => Assert.Empty(MukellefiyetSiniflandirici.Kodlar("100105 numaralı karar"));

        [Fact]
        public void Elle_degistirilen_alanin_uzerine_oneri_yazmaz()
        {
            var firma = new Firma
            {
                VergiTuru = VergiTuru.GelirVergisi, VergiTuruKaynagi = SiniflandirmaKaynagi.Elle,
                DefterUsulu = DefterUsulu.Belirsiz, DefterUsuluKaynagi = SiniflandirmaKaynagi.Yok
            };

            MukellefiyetSiniflandirici.Uygula(firma, "0010");

            Assert.Equal(VergiTuru.GelirVergisi, firma.VergiTuru);          // elle → dokunulmadı
            Assert.Equal(SiniflandirmaKaynagi.Elle, firma.VergiTuruKaynagi);
            Assert.Equal(DefterUsulu.BilancoEsasi, firma.DefterUsulu);      // boştu → önerildi
            Assert.Equal(SiniflandirmaKaynagi.Otomatik, firma.DefterUsuluKaynagi);
        }

        [Fact]
        public void Otomatik_deger_mukellefiyet_kaldirilinca_GERI_ALINMIYOR()
        {
            // 6C: dolu alan (otomatik dolmuş olsa bile) hiçbir koşulda Belirsiz'e çevrilmez.
            var firma = new Firma();
            MukellefiyetSiniflandirici.Uygula(firma, "0010");
            Assert.Equal(VergiTuru.KurumlarVergisi, firma.VergiTuru);

            var degisti = MukellefiyetSiniflandirici.Uygula(firma, "0015");

            Assert.False(degisti);
            Assert.Equal(VergiTuru.KurumlarVergisi, firma.VergiTuru);
            Assert.Equal(SiniflandirmaKaynagi.Otomatik, firma.VergiTuruKaynagi);
        }

        // ---- Kayıt yolları ----

        [Fact]
        public async Task Sicil_kaydedilince_mukellefiyetten_otomatik_dolar()
        {
            using var db = Context();
            var servis = new FirmaBilgiService(db, new SabitBankaFirmaKapsami(Dgr));

            var dto = await servis.SicilGetAsync();
            await servis.SicilKaydetAsync(dto);

            var firma = await db.Firmalar.AsNoTracking().SingleAsync(f => f.Id == Dgr);
            Assert.Equal(VergiTuru.KurumlarVergisi, firma.VergiTuru);
            Assert.Equal(DefterUsulu.BilancoEsasi, firma.DefterUsulu);
            Assert.Equal(SiniflandirmaKaynagi.Otomatik, firma.VergiTuruKaynagi);
        }

        [Fact]
        public async Task Mevcut_firmalara_seed_ile_uygulaniyor_ve_kartta_0010_notu_var()
        {
            using var db = Context();

            await FirmaSiniflandirmaSeed.SeedAsync(db);
            var kart = (await Panel(db, Dgr)).Secili!.Siniflandirma;

            Assert.Equal(DefterUsulu.BilancoEsasi, kart.DefterUsulu);
            Assert.Equal(VergiTuru.KurumlarVergisi, kart.VergiTuru);
            Assert.Equal("0010", kart.DefterUsuluOtomatikKod);
            Assert.Equal("0010", kart.VergiTuruOtomatikKod);
            Assert.Equal("0521", kart.OrkaFirmaKodu);

            // Şahıs: gelir vergisi otomatik, defter usulü kullanıcıya kaldı.
            var sahis = (await Panel(db, Sahis)).Secili!.Siniflandirma;
            Assert.Equal(VergiTuru.GelirVergisi, sahis.VergiTuru);
            Assert.Equal("0001", sahis.VergiTuruOtomatikKod);
            Assert.Equal(DefterUsulu.Belirsiz, sahis.DefterUsulu);
            Assert.Null(sahis.DefterUsuluOtomatikKod);
        }

        [Fact]
        public async Task Kurumlar_vergisinde_isletme_hesabi_reddedilir()
        {
            using var db = Context();
            var servis = new FirmaKunyeService(db);

            var ex = await Assert.ThrowsAsync<FirmaKunyeKuralException>(() => servis.SiniflandirmaKaydetAsync(Dgr,
                new FirmaSiniflandirmaKaydetDto { VergiTuru = VergiTuru.KurumlarVergisi, DefterUsulu = DefterUsulu.IsletmeHesabi }));

            Assert.Equal(nameof(FirmaSiniflandirmaKaydetDto.DefterUsulu), ex.Field);
        }

        [Fact]
        public async Task Elle_kayit_sonraki_mukellefiyet_okumasinda_korunuyor()
        {
            using var db = Context();
            await FirmaSiniflandirmaSeed.SeedAsync(db);

            // Şahıs: kullanıcı işletme hesabı seçiyor; vergi türü değişmediği için otomatik kalıyor.
            await new FirmaKunyeService(db).SiniflandirmaKaydetAsync(Sahis, new FirmaSiniflandirmaKaydetDto
            {
                VergiTuru = VergiTuru.GelirVergisi,
                DefterUsulu = DefterUsulu.IsletmeHesabi,
                MizanFormati = "Luca",
                HesapDonemi = HesapDonemi.TakvimYili
            });

            await FirmaSiniflandirmaSeed.SeedAsync(db);

            var firma = await db.Firmalar.AsNoTracking().SingleAsync(f => f.Id == Sahis);
            Assert.Equal(DefterUsulu.IsletmeHesabi, firma.DefterUsulu);
            Assert.Equal(SiniflandirmaKaynagi.Elle, firma.DefterUsuluKaynagi);
            Assert.Equal(SiniflandirmaKaynagi.Otomatik, firma.VergiTuruKaynagi);
            Assert.Equal("Luca", firma.MizanFormati);
        }

        [Fact]
        public async Task Listede_olmayan_mizan_formati_reddedilir()
        {
            using var db = Context();

            await Assert.ThrowsAsync<FirmaKunyeKuralException>(() => new FirmaKunyeService(db)
                .SiniflandirmaKaydetAsync(Dgr, new FirmaSiniflandirmaKaydetDto { MizanFormati = "Excel" }));
        }

        [Fact]
        public async Task Ozel_hesap_doneminde_tarihler_zorunlu()
        {
            using var db = Context();

            await Assert.ThrowsAsync<FirmaKunyeKuralException>(() => new FirmaKunyeService(db)
                .SiniflandirmaKaydetAsync(Dgr, new FirmaSiniflandirmaKaydetDto { HesapDonemi = HesapDonemi.OzelHesapDonemi }));
        }

        [Fact]
        public void Bos_sablon_yalniz_secili_formatta_uretiliyor()
        {
            Assert.NotEmpty(FirmaKunyeService.MizanSablonu("Luca"));
            Assert.Throws<FirmaKunyeKuralException>(() => FirmaKunyeService.MizanSablonu(MizanFormatlari.Belirsiz));
        }

        // ---- Mizan durumu ----

        private static MizanHamDugumDto D(string kod, decimal borc, decimal alacak)
            => new() { Kod = kod, BorcBakiye = borc, AlacakBakiye = alacak, Bakiye = borc - alacak };

        [Fact]
        public async Task Mizan_kaydinda_ozet_yaziliyor_ve_kartta_dengeli_gorunuyor()
        {
            using var db = Context();

            await new FirmaKontrolMizanService(db).KaydetAsync(Dgr, new MizanKaydetRequest
            {
                Donem = 1,
                Yil = 2026,
                Satirlar = { new MizanHamSatirDto { Kod = "100", Bakiye = 1000 }, new MizanHamSatirDto { Kod = "500", Bakiye = -1000 } },
                Dugumler =
                {
                    D("100", 1000, 0), D("100 01", 600, 0), D("100 01 001", 600, 0), D("100 02", 400, 0),
                    D("500", 0, 1000)
                }
            });

            var agac = await db.FirmaKontrolMizanAgaclari.AsNoTracking().SingleAsync();
            Assert.Equal(3, agac.SeviyeSayisi);
            Assert.Equal(1000m, agac.BorcToplam);     // alt kırılımlar ikinci kez sayılmadı
            Assert.Equal(1000m, agac.AlacakToplam);

            var mizan = (await Panel(db, Dgr)).Secili!.Siniflandirma.SonMizan!;
            Assert.Equal(2026, mizan.Yil);
            Assert.Equal(5, mizan.SatirSayisi);
            Assert.True(mizan.Dengeli);
            Assert.Equal(0m, mizan.Fark);
        }

        [Fact]
        public async Task Dengesiz_mizanda_fark_geliyor()
        {
            using var db = Context();
            db.FirmaKontrolMizanAgaclari.Add(new FirmaKontrolMizanAgac
            {
                FirmaId = Dgr, Donem = 1, Yil = 2026, DugumSayisi = 2, SeviyeSayisi = 1,
                BorcToplam = 1000m, AlacakToplam = 999.5m, UploadedAt = DateTime.UtcNow
            });
            db.SaveChanges();

            var mizan = (await Panel(db, Dgr)).Secili!.Siniflandirma.SonMizan!;

            Assert.False(mizan.Dengeli);
            Assert.Equal(0.5m, mizan.Fark);
        }

        [Fact]
        public async Task Eski_agac_kaydi_bozulmadan_okunuyor_ozet_alanlari_bos()
        {
            // Özet alanları eklenmeden önce yazılmış kayıt: silinmedi, hata vermiyor, "—".
            using var db = Context();
            db.FirmaKontrolMizanAgaclari.Add(new FirmaKontrolMizanAgac
            {
                FirmaId = Dgr, Donem = 1, Yil = 2026, DugumlerJson = "[{\"kod\":\"100\"}]", DugumSayisi = 234,
                UploadedAt = new DateTime(2026, 9, 27, 20, 28, 24)
            });
            db.SaveChanges();

            var mizan = (await Panel(db, Dgr)).Secili!.Siniflandirma.SonMizan!;

            Assert.Equal(234, mizan.SatirSayisi);
            Assert.Null(mizan.SeviyeSayisi);
            Assert.Null(mizan.BorcToplam);
            Assert.Null(mizan.Dengeli);
            Assert.Equal(DateTimeKind.Utc, mizan.YuklenmeZamani!.Value.Kind);

            // Ağaç kendisi olduğu gibi okunabiliyor.
            var agaclar = await new FirmaKontrolMizanService(db).GetAgaclarAsync(Dgr, 2026);
            Assert.Equal("100", Assert.Single(Assert.Single(agaclar).Dugumler).Kod);
        }

        [Fact]
        public async Task Agaci_olmayan_eski_yukleme_de_son_mizan_sayiliyor()
        {
            // Ağaç özelliğinden önce yüklenmiş mizan yalnız ham satır tablosunda.
            using var db = Context();
            db.FirmaKontrolMizanSatirlari.Add(new FirmaKontrolMizanSatir
            {
                FirmaId = Dgr, Donem = 0, Yil = 2026, Kod = "100", Ad = "Kasa", Bakiye = 5,
                UploadedAt = new DateTime(2026, 7, 1)
            });
            db.SaveChanges();

            var mizan = (await Panel(db, Dgr)).Secili!.Siniflandirma.SonMizan!;

            Assert.Equal(2025, mizan.Yil);        // önceki dönem yüklemesi → bir önceki yıl
            Assert.Equal(0, mizan.Donem);
            Assert.Null(mizan.SatirSayisi);
            Assert.Null(mizan.Dengeli);
        }

        [Fact]
        public async Task Mizan_yoksa_son_mizan_bos()
        {
            using var db = Context();

            Assert.Null((await Panel(db, Dgr)).Secili!.Siniflandirma.SonMizan);
        }
    }
}
