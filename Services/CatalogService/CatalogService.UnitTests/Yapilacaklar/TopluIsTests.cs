using CatalogService.Api.Features.Anasayfa.Services;
using CatalogService.Api.Features.FirmaBilgileri.Domain;
using CatalogService.Api.Features.Firmalar.Domain;
using CatalogService.Api.Features.Sistemler;
using CatalogService.Api.Features.Yapilacaklar;
using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;
using CatalogService.Api.Features.Yapilacaklar.Services;
using CatalogService.Api.Infrastructure.Context;
using CatalogService.UnitTests.Ajanlar;
using CatalogService.UnitTests.BankaEkstre;
using CatalogService.UnitTests.Muhasebe;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CatalogService.UnitTests.Yapilacaklar
{
    /// <summary>
    /// Prompt 11: Ctrl+V ile toplu iş ekleme, boş şablon, başka firmadan kopyalama. Doğrulama
    /// metni prompt'taki Excel yapıştırmasının birebir aynısı (hücreler sekmeyle ayrılmış).
    /// </summary>
    public class TopluIsTests
    {
        private const int Carriere = 5;
        private const int Dgr = 3;
        private const int Ben = 16;

        private const string DogrulamaMetni =
            "Başlık\tTekrar\tGün\tProgram\tAlıcı\r\n" +
            "Bordroyu hazırla ve Hollanda'ya gönder\tHer ay\t25\tLuca\tAd Soyad <ad@firma.nl>\r\n" +
            "KDV hesabını çıkar ve onaya gönder\ther ay\t15\tLuca\r\n" +
            "Banka ekstrelerini indir\tAylık\tay sonu\tGaranti\r\n" +
            "Mizanı müşteriden iste\ther ay\t20\tlucca\r\n" +
            "Geçici vergi hesabını hazırla\tüç ayda bir\t10\tLuca\r\n" +
            "Sözleşme yenileme\tHer yl\r\n" +
            "Defter tasdiki\tyılda bir\tay sonu\r\n";

        private static CatalogContext Context()
        {
            var db = BankaEkstreTestOrtami.YeniContext();
            db.Firmalar.AddRange(
                new Firma { Id = Carriere, Unvan = "CARRIERE", KisaAd = "CARRIERE", VergiKimlikNo = "1234567890", Aktif = true },
                new Firma { Id = Dgr, Unvan = "DGR A.Ş.", KisaAd = "DGR", VergiKimlikNo = "2951070824", Aktif = true });
            db.FirmaSicilBilgileri.Add(new FirmaSicilBilgisi { Id = 1, FirmaId = Dgr, MukellefiyetTurleri = "0015 - KDV" });
            db.SaveChanges();
            SistemSeed.SeedAsync(db).GetAwaiter().GetResult();
            VergiTakvimiSeed.SeedAsync(db, new DateTime(2026, 9, 28)).GetAwaiter().GetResult();
            return db;
        }

        private static YapilacaklarService Servis(CatalogContext db)
            => new(db, new SahteSaat(new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero)), new SabitKullanici(Ben),
                   new FirmaOlayYazici(db, new SabitKullanici(Ben), NullLogger<FirmaOlayYazici>.Instance));

        private static TopluSatirDto Satir(TopluOnizlemeDto o, string baslikBasi)
            => o.Satirlar.Single(s => o.Hucreler[s.Sira][0].StartsWith(baslikBasi));

        // ---- Doğrulama metni ----

        [Fact]
        public async Task Dogrulama_metni_ilk_satir_baslik_alti_eklenir_biri_kirmizi()
        {
            using var db = Context();

            var o = await Servis(db).TopluOnizleAsync(Carriere, new TopluOnizleIstekDto { Metin = DogrulamaMetni });

            Assert.True(o.IlkSatirBaslik);
            Assert.Equal(7, o.Satirlar.Count);
            Assert.Equal(4, o.Tamam);
            Assert.Equal(2, o.EksikBilgili);        // Garanti, lucca
            Assert.Equal(1, o.Okunamayan);          // "Her yl"
            Assert.Equal(6, o.Tamam + o.EksikBilgili);

            Assert.Equal(TopluSatirDurumu.Okunamadi, Satir(o, "Sözleşme").Durum);
            Assert.Contains(Satir(o, "Sözleşme").Sorunlar, s => s.Contains("Her yl"));

            // Eşleşme: Menü yolu, Açıklama, Alıcı e-posta yok → "eşleşmedi".
            Assert.Equal(4, o.Eslesme.Single(e => e.Alan == TopluAlan.Alici).Kolon);
            Assert.Equal("Alıcı", o.Eslesme.Single(e => e.Alan == TopluAlan.Alici).DosyadakiBaslik);
            Assert.Null(o.Eslesme.Single(e => e.Alan == TopluAlan.MenuYolu).Kolon);
        }

        [Fact]
        public async Task Dogrulama_metni_degerleri_turkce_esnek_cozumlenir()
        {
            using var db = Context();
            var o = await Servis(db).TopluOnizleAsync(Carriere, new TopluOnizleIstekDto { Metin = DogrulamaMetni });
            var luca = db.Sistemler.Single(s => s.Ad == "Luca").Id;

            var bordro = Satir(o, "Bordroyu").Is!;
            Assert.Equal(IsTekrari.Aylik, bordro.Tekrar);                                 // "Her ay"
            Assert.Equal((IsGunKurali.AyinGunu, (int?)25), (bordro.GunKurali, bordro.AyinGunu));
            Assert.Equal(luca, bordro.SistemId);
            var alici = Assert.Single(bordro.Alicilar!);
            Assert.Equal(("Ad Soyad", "ad@firma.nl"), (alici.AdSoyad, alici.Eposta));

            Assert.Equal(IsTekrari.Aylik, Satir(o, "KDV").Is!.Tekrar);                    // "her ay"
            Assert.Null(Satir(o, "KDV").Is!.Alicilar);                                    // alıcı yok, geçerli

            var banka = Satir(o, "Banka").Is!;
            Assert.Equal(IsTekrari.Aylik, banka.Tekrar);                                  // "Aylık"
            Assert.Equal(IsGunKurali.AySonu, banka.GunKurali);                            // "ay sonu"
            Assert.Null(banka.SistemId);                                                  // Garanti listede yok

            var mizan = Satir(o, "Mizan");
            Assert.Equal(TopluSatirDurumu.Eksik, mizan.Durum);                            // "lucca" amber
            Assert.Null(mizan.Is!.SistemId);                                              // program boş kalır
            Assert.Equal((IsGunKurali.AyinGunu, (int?)20), (mizan.Is.GunKurali, mizan.Is.AyinGunu));

            var gecici = Satir(o, "Geçici").Is!;
            Assert.Equal(IsTekrari.UcAylik, gecici.Tekrar);                               // "üç ayda bir"
            Assert.Equal(10, gecici.AyinGunu);

            var defter = Satir(o, "Defter");
            Assert.Equal(TopluSatirDurumu.Tamam, defter.Durum);
            Assert.Equal((IsTekrari.Yillik, IsGunKurali.AySonu), (defter.Is!.Tekrar, defter.Is.GunKurali));

            // Yeni Sistem kaydı otomatik açılmadı.
            Assert.Equal(7, await db.Sistemler.CountAsync());
        }

        [Fact]
        public async Task Kirmizi_satir_tekrar_duzeltilince_amber_gun_de_dolunca_yesil()
        {
            using var db = Context();
            var servis = Servis(db);
            var o = await servis.TopluOnizleAsync(Carriere, new TopluOnizleIstekDto { Metin = DogrulamaMetni });

            var kirmizi = Satir(o, "Sözleşme");
            var tekrarKolonu = o.Eslesme.Single(e => e.Alan == TopluAlan.Tekrar).Kolon!.Value;
            var gunKolonu = o.Eslesme.Single(e => e.Alan == TopluAlan.Gun).Kolon!.Value;
            o.Hucreler[kirmizi.Sira][tekrarKolonu] = "Her yıl";

            TopluOnizleIstekDto Istek() => new() { Hucreler = o.Hucreler, IlkSatirBaslik = o.IlkSatirBaslik, Eslesme = o.Eslesme };

            // Prompt 11B: gün yazılmamış → kırmızı değil AMBER (eklenir, "kontrol et" der).
            var tekrarli = await servis.TopluOnizleAsync(Carriere, Istek());
            var satir = Satir(tekrarli, "Sözleşme");
            Assert.Equal(TopluSatirDurumu.Eksik, satir.Durum);
            Assert.Equal(IsTekrari.Yillik, satir.Is!.Tekrar);
            Assert.Equal(IsGunKurali.DonemSonu, satir.Is.GunKurali);                     // varsayılan yine uygulandı
            Assert.Equal("dönem sonu (varsayılan — kontrol et)", satir.GunMetni);
            Assert.Equal(7, tekrarli.Tamam + tekrarli.EksikBilgili);

            // Gün de yazılınca yeşil.
            while (o.Hucreler[kirmizi.Sira].Count <= gunKolonu) o.Hucreler[kirmizi.Sira].Add(string.Empty);
            o.Hucreler[kirmizi.Sira][gunKolonu] = "ay sonu";
            var tam = await servis.TopluOnizleAsync(Carriere, Istek());
            Assert.Equal(TopluSatirDurumu.Tamam, Satir(tam, "Sözleşme").Durum);
        }

        [Fact]
        public async Task Tekrar_ve_gunu_dolu_satirlar_yesil_program_bulunamayan_amber_kalir()
        {
            using var db = Context();
            var o = await Servis(db).TopluOnizleAsync(Carriere, new TopluOnizleIstekDto { Metin = DogrulamaMetni });

            Assert.All(new[] { "Bordroyu", "KDV", "Geçici", "Defter" }, b => Assert.Equal(TopluSatirDurumu.Tamam, Satir(o, b).Durum));
            Assert.All(new[] { "Banka", "Mizan" }, b => Assert.Equal(TopluSatirDurumu.Eksik, Satir(o, b).Durum));
            Assert.Equal(TopluSatirDurumu.Okunamadi, Satir(o, "Sözleşme").Durum);
        }

        [Theory]
        [InlineData("Her ay", IsTekrari.Aylik)]
        [InlineData("AYLIK", IsTekrari.Aylik)]
        [InlineData("ayda bir", IsTekrari.Aylik)]
        [InlineData("Üç Ayda Bir", IsTekrari.UcAylik)]
        [InlineData("3 ayda bir", IsTekrari.UcAylik)]
        [InlineData("3 aylık", IsTekrari.UcAylik)]
        [InlineData("YILLIK", IsTekrari.Yillik)]
        [InlineData("senede bir", IsTekrari.Yillik)]
        [InlineData("bir kez", IsTekrari.TekSefer)]
        [InlineData("Tek", IsTekrari.TekSefer)]
        public void Tekrar_yazimlari(string ham, IsTekrari beklenen)
            => Assert.Equal(beklenen, TopluIsAyristirici.TekrarCoz(ham));

        [Theory]
        [InlineData("25", IsGunKurali.AyinGunu, 25)]
        [InlineData("25.", IsGunKurali.AyinGunu, 25)]
        [InlineData("25'i", IsGunKurali.AyinGunu, 25)]
        [InlineData("Ayın sonu", IsGunKurali.AySonu, null)]
        [InlineData("dönem sonu", IsGunKurali.DonemSonu, null)]
        public void Gun_yazimlari(string ham, IsGunKurali kural, int? gun)
        {
            var sonuc = TopluIsAyristirici.GunCoz(ham);
            Assert.Equal((kural, gun), (sonuc!.Kural!.Value, sonuc.AyinGunu));
        }

        [Fact]
        public void Gecersiz_gun_ve_tarih()
        {
            Assert.Null(TopluIsAyristirici.GunCoz("32"));
            Assert.Null(TopluIsAyristirici.GunCoz("bazen"));
            Assert.Equal(new DateTime(2026, 12, 31), TopluIsAyristirici.GunCoz("31.12.2026")!.Tarih);
        }

        [Fact]
        public async Task Tarih_tekrar_yazilmamissa_tek_seferlik_is_olur()
        {
            using var db = Context();
            var o = await Servis(db).TopluOnizleAsync(Carriere, new TopluOnizleIstekDto
            {
                Metin = "Başlık\tTekrar\tGün\nŞirket devri\t\t15.11.2026\nSayım\ther ay\t15.11.2026"
            });

            var devir = Satir(o, "Şirket").Is!;
            Assert.Equal((IsTekrari.TekSefer, (DateTime?)new DateTime(2026, 11, 15)), (devir.Tekrar, devir.TekSeferTarih));
            Assert.Equal(TopluSatirDurumu.Okunamadi, Satir(o, "Sayım").Durum);   // tarih + her ay çelişir
        }

        [Fact]
        public async Task Tek_kolonlu_yapistirma_her_satir_bir_baslik()
        {
            using var db = Context();

            var o = await Servis(db).TopluOnizleAsync(Carriere, new TopluOnizleIstekDto
            {
                Metin = "Bordro\nKDV hazırlık\n\nBanka ekstresi\n"
            });

            // Prompt 11B: hepsi AMBER ve hepsi eklenir; varsayılan yine uygulanır.
            Assert.False(o.IlkSatirBaslik);
            Assert.Equal(3, o.Satirlar.Count);
            Assert.All(o.Satirlar, s => Assert.Equal(TopluSatirDurumu.Eksik, s.Durum));
            Assert.Equal(o.Satirlar.Count, o.EksikBilgili);
            Assert.Equal(0, o.Tamam);
            Assert.All(o.Satirlar, s => Assert.Equal((IsTekrari.Aylik, IsGunKurali.DonemSonu), (s.Is!.Tekrar, s.Is.GunKurali)));
            Assert.Equal("her ay (varsayılan — kontrol et)", o.Satirlar[0].TekrarMetni);
            Assert.Equal("dönem sonu (varsayılan — kontrol et)", o.Satirlar[0].GunMetni);
        }

        [Fact]
        public async Task Ilk_satir_karari_ve_eslesme_kullanicidan_degistirilir()
        {
            using var db = Context();
            var servis = Servis(db);

            // Başlıksız iki kolon: varsayılan şablon sırası (Başlık, Tekrar); kullanıcı ikinciyi Gün'e çeviriyor.
            var o = await servis.TopluOnizleAsync(Carriere, new TopluOnizleIstekDto
            {
                Metin = "Bordro\t25\nKDV\t15",
                Eslesme = new() { new() { Alan = TopluAlan.Baslik, Kolon = 0 }, new() { Alan = TopluAlan.Gun, Kolon = 1 } }
            });

            Assert.False(o.IlkSatirBaslik);
            Assert.Equal(25, Satir(o, "Bordro").Is!.AyinGunu);
            Assert.Null(o.Eslesme.Single(e => e.Alan == TopluAlan.Tekrar).Kolon);

            // Kullanıcı ilk satırı başlık sayıyor: "Bordro" satırı veri olmaktan çıkar.
            var baslikli = await servis.TopluOnizleAsync(Carriere, new TopluOnizleIstekDto { Hucreler = o.Hucreler, IlkSatirBaslik = true,
                Eslesme = o.Eslesme });
            Assert.Single(baslikli.Satirlar);
        }

        [Fact]
        public void Alici_yazimlari()
        {
            var iki = TopluIsAyristirici.AliciCoz("Jan de Vries <jan@carriere.nl>; Ayşe Yılmaz", null);
            Assert.Equal(new[] { ("Jan de Vries", "jan@carriere.nl"), ("Ayşe Yılmaz", (string?)null) },
                         iki.Select(a => (a.AdSoyad, a.Eposta)));

            var ayri = TopluIsAyristirici.AliciCoz("Jan de Vries", "jan@carriere.nl");
            Assert.Equal("jan@carriere.nl", Assert.Single(ayri).Eposta);

            var amber = new List<string>();
            var bozuk = TopluIsAyristirici.AliciCoz("Jan <jan(at)carriere>", null, amber);
            Assert.Null(Assert.Single(bozuk).Eposta);
            Assert.Single(amber);
        }

        // ---- Ekleme ----

        [Fact]
        public async Task Onaylanan_satirlar_eklenir_uzerine_yazma_acikken_ayni_baslik_atlanir()
        {
            using var db = Context();
            var servis = Servis(db);
            await servis.IsEkleAsync(Carriere, new FirmaIsiKaydetDto
            {
                Baslik = "KDV hesabını çıkar ve onaya gönder", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AyinGunu,
                AyinGunu = 12, SorumluKullaniciId = 99, SorumluKullaniciAdi = "zeynep"
            });

            var o = await servis.TopluOnizleAsync(Carriere, new TopluOnizleIstekDto { Metin = DogrulamaMetni });
            Assert.NotNull(Satir(o, "KDV").MevcutIsId);

            var isler = o.Satirlar.Where(s => s.Durum != TopluSatirDurumu.Okunamadi).Select(s => s.Is!).ToList();
            var sonuc = await servis.TopluEkleAsync(Carriere, new TopluEkleDto { Isler = isler });   // varsayılan: üzerine yazma

            Assert.Equal(5, sonuc.Eklenen);
            Assert.Equal(0, sonuc.Guncellenen);
            var atlanan = Assert.Single(sonuc.Atlananlar);
            Assert.StartsWith("KDV hesabını", atlanan.Baslik);

            var kdv = await db.FirmaIsleri.SingleAsync(i => i.Baslik.StartsWith("KDV"));
            Assert.Equal(12, kdv.AyinGunu);                                    // dokunulmadı
            Assert.Equal(6, await db.FirmaIsleri.CountAsync(i => i.FirmaId == Carriere));

            var alici = await db.FirmaIsiAlicilari.SingleAsync();
            Assert.Equal(("Ad Soyad", "ad@firma.nl"), (alici.AdSoyad, alici.Eposta));
        }

        [Fact]
        public async Task Uzerine_yazma_kapaliysa_ayni_baslik_guncellenir_sorumlu_korunur()
        {
            using var db = Context();
            var servis = Servis(db);
            await servis.IsEkleAsync(Carriere, new FirmaIsiKaydetDto
            {
                Baslik = "kdv hesabını çıkar ve onaya gönder ", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AyinGunu,
                AyinGunu = 12, SorumluKullaniciId = 99, SorumluKullaniciAdi = "zeynep", NasilYapilir = "adım 1"
            });

            var o = await servis.TopluOnizleAsync(Carriere, new TopluOnizleIstekDto { Metin = DogrulamaMetni });
            var sonuc = await servis.TopluEkleAsync(Carriere, new TopluEkleDto
            {
                Isler = new() { Satir(o, "KDV").Is! }, UzerineYazma = false
            });

            Assert.Equal(1, sonuc.Guncellenen);
            var kdv = await db.FirmaIsleri.AsNoTracking().SingleAsync();
            Assert.Equal(15, kdv.AyinGunu);                                    // yapıştırmadaki değer
            Assert.Equal(99, kdv.SorumluKullaniciId);                          // yapıştırmada olmayan korunur
            Assert.Equal("adım 1", kdv.NasilYapilir);
        }

        // ---- Boş şablon ----

        [Fact]
        public void Bos_sablon_basliklari_dogru_ornek_satir_var_ve_geri_yapistirilinca_taninir()
        {
            using var kitap = new XLWorkbook(new MemoryStream(YapilacaklarService.IsSablonu()));
            var sayfa = kitap.Worksheet(1);

            var basliklar = Enumerable.Range(1, 7).Select(i => sayfa.Cell(1, i).GetString()).ToArray();
            Assert.Equal(new[] { "Başlık", "Tekrar", "Gün", "Program", "Menü yolu", "Alıcı", "Açıklama" }, basliklar);
            Assert.False(sayfa.Cell(2, 1).IsEmpty());

            // Excel'de doldur → geri yapıştır döngüsü: şablonun iki satırı aynen çözülür.
            var metin = string.Join("\r\n", Enumerable.Range(1, 2)
                .Select(r => string.Join("\t", Enumerable.Range(1, 7).Select(c => sayfa.Cell(r, c).GetString()))));
            var o = TopluIsAyristirici.Coz(new TopluOnizleIstekDto { Metin = metin },
                                           new[] { new Api.Features.Sistemler.Domain.Sistem { Id = 1, Ad = "Luca", Aktif = true } },
                                           Array.Empty<int>(), new Dictionary<string, int>());
            Assert.True(o.IlkSatirBaslik);
            Assert.Equal(TopluSatirDurumu.Tamam, Assert.Single(o.Satirlar).Durum);
            Assert.All(o.Eslesme.Where(e => e.Alan != TopluAlan.AliciEposta), e => Assert.NotNull(e.Kolon));
        }

        // ---- Başka firmadan kopyala ----

        [Fact]
        public async Task Kopyalama_yalniz_ozel_isleri_alir_ek_tamamlama_sorumlu_on_adim_kopyalanmaz()
        {
            using var db = Context();
            var servis = Servis(db);

            var bordro = await servis.IsEkleAsync(Dgr, new FirmaIsiKaydetDto
            {
                Baslik = "Bordro", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AyinGunu, AyinGunu = 25,
                SorumluKullaniciId = 99, SorumluKullaniciAdi = "zeynep",
                SistemId = db.Sistemler.Single(s => s.Ad == "Luca").Id, MenuYolu = "Bordro > Raporlar",
                NasilYapilir = "Puantajı gir\nGönder",
                Alicilar = new() { new() { AdSoyad = "Jan", Eposta = "jan@carriere.nl", Rol = "Finans", AliciTipi = AliciTipi.Kime } },
                OnAdimiYasalKodu = "0015", OnAdimiYasalTekrar = IsTekrari.Aylik
            });
            var rapor = await servis.IsEkleAsync(Dgr, new FirmaIsiKaydetDto
            {
                Baslik = "Rapor", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AySonu
            });
            await servis.EkEkleAsync(Dgr, bordro.Id, new FirmaIsiEkiOlusturDto { FileId = 1, DosyaAdi = "menu.png", Boyut = 5 });
            await servis.IsaretleAsync(new IsIsaretleDto
            {
                Yapildi = true,
                Isler = { new IsIsaretDto { KaynakTip = IsKaynagi.Ozel, KaynakId = bordro.Id, FirmaId = Dgr, DonemAnahtari = "2026-09" } }
            });

            // Hedefte "Rapor" zaten var → atlanır ve raporlanır. Yasal prosedür satırı (0015) seçilse de kopyalanmaz.
            await servis.IsEkleAsync(Carriere, new FirmaIsiKaydetDto { Baslik = "rapor", Tekrar = IsTekrari.Aylik, GunKurali = IsGunKurali.AySonu });
            var yasalProsedur = await db.FirmaIsleri.SingleAsync(i => i.FirmaId == Dgr && i.YasalMukellefiyetKodu != null);

            var sonuc = await servis.KopyalaAsync(Carriere, new IsKopyalaDto
            {
                KaynakFirmaId = Dgr, IsIdleri = new() { bordro.Id, rapor.Id, yasalProsedur.Id }
            });

            Assert.Equal(1, sonuc.Eklenen);
            Assert.Equal("Rapor", Assert.Single(sonuc.Atlananlar).Baslik);

            var kopya = await db.FirmaIsleri.Include(i => i.Alicilar).Include(i => i.Ekler)
                .SingleAsync(i => i.FirmaId == Carriere && i.Baslik == "Bordro");
            Assert.Equal((25, "Bordro > Raporlar", "Puantajı gir\nGönder"), (kopya.AyinGunu!.Value, kopya.MenuYolu, kopya.NasilYapilir));
            Assert.Equal(bordro.SistemId, kopya.SistemId);
            Assert.Equal(("Jan", "jan@carriere.nl", "Finans"), (kopya.Alicilar.Single().AdSoyad, kopya.Alicilar.Single().Eposta, kopya.Alicilar.Single().Rol));

            Assert.Null(kopya.SorumluKullaniciId);                                      // firmaya özgü
            Assert.Null(kopya.OnAdimiOlduguIsId);
            Assert.Empty(kopya.Ekler);
            Assert.Equal(0, await db.IsTamamlamalari.CountAsync(t => t.FirmaId == Carriere));
            Assert.Equal(0, await db.FirmaIsleri.CountAsync(i => i.FirmaId == Carriere && i.YasalMukellefiyetKodu != null));
        }

        [Fact]
        public async Task Firma_kendisinden_kopyalanamaz()
        {
            using var db = Context();
            await Assert.ThrowsAsync<YapilacaklarKuralException>(() =>
                Servis(db).KopyalaAsync(Carriere, new IsKopyalaDto { KaynakFirmaId = Carriere }));
        }
    }
}
