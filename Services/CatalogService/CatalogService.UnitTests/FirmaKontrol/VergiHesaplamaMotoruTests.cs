using CatalogService.Api.Features.FirmaKontrol.Dtos;
using CatalogService.Api.Features.FirmaKontrol.Services;

namespace CatalogService.UnitTests.FirmaKontrol
{
    /// <summary>
    /// Kurumlar vergisi hesaplama motoru. Kabul kriterlerinin her maddesi için en az bir test.
    /// </summary>
    public class VergiHesaplamaMotoruTests
    {
        private const short Donem = 2026;

        private static VergiHesaplamaMotoru.Girdi Girdi(
            decimal ticariKar,
            IEnumerable<(int KalemId, decimal Tutar)>? satirlar = null,
            IEnumerable<(short Yil, decimal Tutar)>? zararlar = null,
            decimal kvOrani = 25m,
            decimal? indirimliOran = null,
            decimal? indirimliMatrah = null,
            bool asgariKvHesapla = false) => new()
            {
                DonemYil = Donem,
                TicariKar = ticariKar,
                KvOrani = kvOrani,
                IndirimliOran = indirimliOran,
                IndirimliOranMatrahi = indirimliMatrah,
                AsgariKvHesapla = asgariKvHesapla,
                Kalemler = VergiTestKatalogu.Olustur(),
                Satirlar = (satirlar ?? Enumerable.Empty<(int, decimal)>())
                    .Select(s => new VergiSatirYazDto { VergiKalemiId = s.KalemId, Tutar = s.Tutar })
                    .ToList(),
                GecmisYilZararlari = (zararlar ?? Enumerable.Empty<(short, decimal)>())
                    .Select(z => new GecmisYilZarariYazDto { ZararYili = z.Yil, ZararTutari = z.Tutar })
                    .ToList()
            };

        // ── Ticari kâr ──

        [Fact]
        public void TicariKar_SonucaAynenGeciyor()
        {
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(500_000m));

            Assert.Equal(500_000m, sonuc.TicariKar);
            Assert.Equal(500_000m, sonuc.KarVeIlavelerToplami);
            Assert.Equal(500_000m, sonuc.Matrah);
        }

        // ── Kabul: istisnaya ilişkin KKEG bağlı istisnayı büyütüyor ──

        [Fact]
        public void IstisnayaIliskinKkeg_BagliIstisnayiBuyutuyor()
        {
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(1_000_000m, new[]
            {
                (VergiTestKatalogu.IstisnaTeknopark, 18_000m),
                (VergiTestKatalogu.KkegTeknopark, 3_100m)
            }));

            var teknopark = sonuc.ZararOlsaDahiIndirimler.Single(x => x.Kod == "IST-17");

            Assert.Equal(18_000m, teknopark.GirilenTutar);
            Assert.Equal(3_100m, teknopark.IliskiliKkeg);
            Assert.Equal(21_100m, teknopark.EfektifTutar);
        }

        // ── Kabul: istisnaya ilişkin KKEG'in matraha net etkisi sıfır ──

        [Fact]
        public void IstisnayaIliskinKkeg_MatrahaNetEtkisiSifir()
        {
            var kkegsiz = VergiHesaplamaMotoru.Hesapla(Girdi(1_000_000m, new[]
            {
                (VergiTestKatalogu.IstisnaTeknopark, 18_000m)
            }));

            var kkegli = VergiHesaplamaMotoru.Hesapla(Girdi(1_000_000m, new[]
            {
                (VergiTestKatalogu.IstisnaTeknopark, 18_000m),
                (VergiTestKatalogu.KkegTeknopark, 3_100m)
            }));

            Assert.Equal(kkegsiz.Matrah, kkegli.Matrah);
        }

        [Fact]
        public void MatrahiArtiranKkeg_MatrahiArtiriyor()
        {
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(1_000_000m, new[]
            {
                (VergiTestKatalogu.KkegCeza, 50_000m)
            }));

            Assert.Equal(1_050_000m, sonuc.Matrah);
        }

        [Fact]
        public void BagliIstisnasiOlmayanIstisnaKkegi_MatrahiArtirirVeUyariVerir()
        {
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(1_000_000m, new[]
            {
                (VergiTestKatalogu.KkegBagsiz, 7_000m)
            }));

            Assert.Equal(1_007_000m, sonuc.Matrah);
            Assert.Contains(sonuc.Uyarilar, u => u.KalemKodu == "KKEGI-05");
        }

        // ── Kabul: ham ilave toplamı ve matraha etki eden toplam ayrı ──

        [Fact]
        public void IlaveToplamlari_HamVeMatrahaEtkiEden_AyriHesaplaniyor()
        {
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(1_000_000m, new[]
            {
                (VergiTestKatalogu.KkegCeza, 50_000m),
                (VergiTestKatalogu.KkegTeknopark, 3_100m),
                (VergiTestKatalogu.IstisnaTeknopark, 18_000m)
            }));

            Assert.Equal(53_100m, sonuc.IlaveHamToplam);
            Assert.Equal(50_000m, sonuc.IlaveMatrahaEtkiEden);
        }

        // ── Kabul: Grup 2 matrahı negatife çekebilir ──

        [Fact]
        public void Grup2Indirimleri_MatrahiNegatifeCekebiliyor()
        {
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(100_000m, new[]
            {
                (VergiTestKatalogu.IstisnaDiger, 400_000m)
            }));

            Assert.Equal(-300_000m, sonuc.KarZarar);
            Assert.Equal(-300_000m, sonuc.Matrah);
            Assert.Equal(0m, sonuc.NormalVergi);
        }

        // ── Kabul: Grup 3 matrahı sıfırın altına indiremez ──

        [Fact]
        public void Grup3Indirimleri_MatrahiSifirinAltinaIndiremiyor()
        {
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(100_000m, new[]
            {
                (VergiTestKatalogu.IndirimArge, 250_000m)
            }));

            Assert.Equal(0m, sonuc.Matrah);
            Assert.Equal(100_000m, sonuc.KazancVarsaToplam);
        }

        [Fact]
        public void Grup3_ZararliDonemdeHicUygulanmiyor()
        {
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(100_000m, new[]
            {
                (VergiTestKatalogu.IstisnaDiger, 300_000m),
                (VergiTestKatalogu.IndirimArge, 50_000m)
            }));

            Assert.Equal(0m, sonuc.KazancVarsaToplam);
            Assert.Equal(-200_000m, sonuc.Matrah);
        }

        // ── Kabul: geçmiş yıl zararları en eski yıldan mahsup ──

        [Fact]
        public void GecmisYilZararlari_EnEskiYildanBaslayarakMahsupEdiliyor()
        {
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(
                300_000m,
                zararlar: new (short, decimal)[] { (2024, 200_000m), (2022, 250_000m) }));

            var y2022 = sonuc.ZararMahsuplari.Single(z => z.ZararYili == 2022);
            var y2024 = sonuc.ZararMahsuplari.Single(z => z.ZararYili == 2024);

            // Önce 2022 tamamen, kalan 50.000 ile 2024'ün bir kısmı.
            Assert.Equal(250_000m, y2022.MahsupEdilen);
            Assert.Equal(50_000m, y2024.MahsupEdilen);
            Assert.Equal(150_000m, y2024.DevredenTutar);
            Assert.Equal(0m, sonuc.Matrah);
        }

        // ── Kabul: 5 yıldan eski zarar mahsup edilemiyor + uyarı ──

        [Fact]
        public void BesYildanEskiZarar_MahsupEdilemiyorVeUyariVeriyor()
        {
            // 2026 dönemi için 2020 zararı 6 yıllık; sınır dışı.
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(
                500_000m,
                zararlar: new (short, decimal)[] { (2020, 100_000m), (2021, 40_000m) }));

            var eski = sonuc.ZararMahsuplari.Single(z => z.ZararYili == 2020);
            var gecerli = sonuc.ZararMahsuplari.Single(z => z.ZararYili == 2021);

            Assert.False(eski.MahsupEdilebilir);
            Assert.Equal(0m, eski.MahsupEdilen);
            Assert.NotNull(eski.Uyari);
            Assert.Contains(sonuc.Uyarilar, u => u.Mesaj.Contains("2020"));

            Assert.True(gecerli.MahsupEdilebilir);
            Assert.Equal(40_000m, gecerli.MahsupEdilen);
            Assert.Equal(40_000m, sonuc.ZararMahsupToplami);
        }

        // ── Kabul: bağış %5 sınırı aşımı ──

        [Fact]
        public void BagisUstSiniri_AsildigindaUyariVeAsanTutarHesaplaniyor()
        {
            // Kurum kazancı = ticari kâr − iştirak istisnası − geçmiş yıl zararı = 1.000.000
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(1_000_000m, new[]
            {
                (VergiTestKatalogu.IndirimBagis, 80_000m)
            }));

            var bagis = sonuc.KazancVarsaIndirimler.Single(x => x.Kod == "IND-05");

            Assert.Equal(50_000m, bagis.UstSinirTutari);       // %5
            Assert.Equal(50_000m, bagis.EfektifTutar);
            Assert.Equal(30_000m, bagis.SinirAsimi);
            Assert.Contains(sonuc.Uyarilar, u => u.KalemKodu == "IND-05");
            Assert.Equal(950_000m, sonuc.Matrah);
        }

        [Fact]
        public void UstSinirTabani_IstirakIstisnasiVeZararMahsubuDusulerekBulunuyor()
        {
            var sonuc = VergiHesaplamaMotoru.Hesapla(
                Girdi(1_000_000m,
                    new[] { (VergiTestKatalogu.IstisnaIstirak, 200_000m) },
                    zararlar: new (short, decimal)[] { (2025, 100_000m) }));

            // 1.000.000 − 200.000 (iştirak) − 100.000 (zarar mahsubu) = 700.000
            Assert.Equal(700_000m, sonuc.KurumKazanci);
        }

        // ── Kabul: devredebilir kalemlerde kullanılmayan tutar ayrı ──

        [Fact]
        public void DevredebilirKalem_KullanilamayanTutariDevredenOlarakGosteriyor()
        {
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(100_000m, new[]
            {
                (VergiTestKatalogu.IndirimArge, 250_000m)
            }));

            var arge = sonuc.KazancVarsaIndirimler.Single(x => x.Kod == "IND-01");

            Assert.Equal(100_000m, arge.EfektifTutar);
            Assert.Equal(150_000m, arge.KullanilamayanTutar);
            Assert.Equal(150_000m, arge.DevredenTutar);
            Assert.Equal(0m, arge.YananTutar);
        }

        [Fact]
        public void DevretmeyenKalem_KullanilamayanTutariYanikGosteriyor()
        {
            // Kurum kazancı 100.000 → bağış sınırı 5.000; kalan kazanç Ar-Ge ile tükeniyor.
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(100_000m, new[]
            {
                (VergiTestKatalogu.IndirimArge, 100_000m),
                (VergiTestKatalogu.IndirimBagis, 5_000m)
            }));

            var bagis = sonuc.KazancVarsaIndirimler.Single(x => x.Kod == "IND-05");

            Assert.Equal(0m, bagis.EfektifTutar);
            Assert.Equal(5_000m, bagis.KullanilamayanTutar);
            Assert.Equal(5_000m, bagis.YananTutar);
            Assert.Equal(0m, bagis.DevredenTutar);
        }

        // ── Kabul: asgari kurumlar vergisi paralel hesaplanıyor, yüksek olan uygulanıyor ──

        [Fact]
        public void AsgariKurumlarVergisi_YuksekOlanUygulaniyor()
        {
            // Ticari kâr 1.000.000, tamamı "diğer istisna" (asgari matrahtan düşmez) ile indiriliyor.
            // Normal matrah 0 → normal vergi 0; asgari matrah 1.000.000 → asgari vergi 100.000.
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(
                1_000_000m,
                new[] { (VergiTestKatalogu.IstisnaDiger, 1_000_000m) },
                asgariKvHesapla: true));

            Assert.Equal(0m, sonuc.NormalVergi);
            Assert.Equal(1_000_000m, sonuc.AsgariMatrah);
            Assert.Equal(100_000m, sonuc.AsgariVergi);
            Assert.True(sonuc.AsgariUygulandi);
            Assert.Equal(100_000m, sonuc.HesaplananVergi);
        }

        [Fact]
        public void AsgariMatrahtanDusenIstisna_AsgariMatrahiAzaltiyor()
        {
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(
                1_000_000m,
                new[] { (VergiTestKatalogu.IstisnaTeknopark, 1_000_000m) },
                asgariKvHesapla: true));

            Assert.Equal(0m, sonuc.AsgariMatrah);
            Assert.Equal(0m, sonuc.AsgariVergi);
            Assert.False(sonuc.AsgariUygulandi);
        }

        [Fact]
        public void AsgariKvKapaliysa_HesaplanmiyorVeNormalVergiUygulaniyor()
        {
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(
                1_000_000m,
                new[] { (VergiTestKatalogu.IstisnaDiger, 1_000_000m) },
                asgariKvHesapla: false));

            Assert.False(sonuc.AsgariKvHesaplandi);
            Assert.Equal(0m, sonuc.HesaplananVergi);
            Assert.False(sonuc.AsgariUygulandi);
        }

        // ── Kabul: vergi oranı değiştirilebiliyor, indirimli oran ayrı matrah alıyor ──

        [Fact]
        public void VergiOrani_Degistirilebiliyor()
        {
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(1_000_000m, kvOrani: 30m));

            Assert.Equal(300_000m, sonuc.NormalVergi);
        }

        [Fact]
        public void IndirimliOran_AyriMatrahUzerindenUygulaniyor()
        {
            // 400.000 indirimli (%20), kalan 600.000 genel (%25)
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(
                1_000_000m, kvOrani: 25m, indirimliOran: 20m, indirimliMatrah: 400_000m));

            Assert.Equal(400_000m, sonuc.IndirimliOranMatrahi);
            Assert.Equal(600_000m, sonuc.GenelOranMatrahi);
            Assert.Equal(230_000m, sonuc.NormalVergi);   // 600.000*0,25 + 400.000*0,20
        }

        [Fact]
        public void IndirimliOranMatrahi_ToplamMatrahiAsamaz()
        {
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(
                100_000m, kvOrani: 25m, indirimliOran: 10m, indirimliMatrah: 500_000m));

            Assert.Equal(100_000m, sonuc.IndirimliOranMatrahi);
            Assert.Equal(0m, sonuc.GenelOranMatrahi);
            Assert.Equal(10_000m, sonuc.NormalVergi);
        }

        // ── Mahsuplar ve sonuç ──

        [Fact]
        public void Mahsuplar_HesaplananVergidenDusuluyor()
        {
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(1_000_000m, new[]
            {
                (VergiTestKatalogu.MahsupGecici, 150_000m)
            }));

            Assert.Equal(250_000m, sonuc.HesaplananVergi);
            Assert.Equal(150_000m, sonuc.MahsupToplami);
            Assert.Equal(100_000m, sonuc.OdenecekVergi);
        }

        [Fact]
        public void MahsupHesaplananVergiyiAsarsa_IadeCikiyorVeBilgiUyarisiVeriliyor()
        {
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(100_000m, new[]
            {
                (VergiTestKatalogu.MahsupGecici, 50_000m)
            }));

            Assert.Equal(25_000m, sonuc.HesaplananVergi);
            Assert.Equal(-25_000m, sonuc.OdenecekVergi);
            Assert.Contains(sonuc.Uyarilar, u => u.Mesaj.Contains("iade"));
        }

        // ── Uçtan uca: beyanname sırasının tamamı ──

        [Fact]
        public void TamAkis_BeyannameSirasiniDogruUyguluyor()
        {
            var sonuc = VergiHesaplamaMotoru.Hesapla(Girdi(
                1_000_000m,
                new[]
                {
                    (VergiTestKatalogu.KkegCeza, 100_000m),        // + ilave
                    (VergiTestKatalogu.KkegTeknopark, 10_000m),    // + ilave (nötr)
                    (VergiTestKatalogu.IstisnaTeknopark, 90_000m), // − grup 2 (efektif 100.000)
                    (VergiTestKatalogu.IndirimArge, 200_000m),     // − grup 3
                    (VergiTestKatalogu.MahsupGecici, 60_000m)      // − mahsup
                },
                zararlar: new (short, decimal)[] { (2023, 150_000m) },
                asgariKvHesapla: true));

            Assert.Equal(1_110_000m, sonuc.KarVeIlavelerToplami);  // 1.000.000 + 110.000 ham ilave
            Assert.Equal(100_000m, sonuc.ZararOlsaDahiToplam);     // 90.000 + 10.000 ilişkili KKEG
            Assert.Equal(1_010_000m, sonuc.KarZarar);
            Assert.Equal(150_000m, sonuc.ZararMahsupToplami);
            Assert.Equal(860_000m, sonuc.MahsupSonrasiKazanc);
            Assert.Equal(200_000m, sonuc.KazancVarsaToplam);
            Assert.Equal(660_000m, sonuc.Matrah);
            Assert.Equal(165_000m, sonuc.NormalVergi);             // %25

            // Asgari matrah = 1.000.000 + 110.000 − (100.000 teknopark + 200.000 Ar-Ge) = 810.000
            Assert.Equal(810_000m, sonuc.AsgariMatrah);
            Assert.Equal(81_000m, sonuc.AsgariVergi);
            Assert.False(sonuc.AsgariUygulandi);

            Assert.Equal(165_000m, sonuc.HesaplananVergi);
            Assert.Equal(105_000m, sonuc.OdenecekVergi);
        }
    }
}
