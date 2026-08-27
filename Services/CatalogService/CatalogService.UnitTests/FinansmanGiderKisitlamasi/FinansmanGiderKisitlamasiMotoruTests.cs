using CatalogService.Api.Features.FinansmanGiderKisitlamasi.Services;

namespace CatalogService.UnitTests.FinansmanGiderKisitlamasi
{
    /// <summary>
    /// Finansman gider kısıtlaması motoru. Kabul kriterlerindeki her kenar kuralı için
    /// en az bir test.
    /// </summary>
    public class FinansmanGiderKisitlamasiMotoruTests
    {
        private const int Yil = 2026;

        private static FinansmanGiderKisitlamasiMotoru.Girdi Girdi(
            decimal ozsermaye,
            decimal yabanciKaynak,
            decimal finansmanGideri = 0m,
            decimal ortuluVeGelir = 0m,
            decimal? kisitlamaOrani = 10m) => new()
            {
                Yil = Yil,
                Ozsermaye = ozsermaye,
                YabanciKaynakToplami = yabanciKaynak,
                FinansmanGideri = finansmanGideri,
                OrtuluSermayeVeFinansmanGeliri = ortuluVeGelir,
                KisitlamaOrani = kisitlamaOrani
            };

        // ── Normal senaryo: yabancı kaynak özsermayeyi aşıyor ──

        [Fact]
        public void YabanciKaynakOzsermayeyiAsiyor_KkegDogruHesaplaniyor()
        {
            // 2 − 1 = 400.000 aşan; 400.000 / 1.000.000 = %40
            // 7 = 200.000 − 50.000 = 150.000; 8 = %40 × 150.000 = 60.000; 9 = %10 × 60.000 = 6.000
            var sonuc = FinansmanGiderKisitlamasiMotoru.Hesapla(
                Girdi(600_000m, 1_000_000m, finansmanGideri: 200_000m, ortuluVeGelir: 50_000m));

            Assert.True(sonuc.KisitlamaVar);
            Assert.Null(sonuc.Aciklama);
            Assert.Equal(600_000m, sonuc.Ozsermaye);
            Assert.Equal(1_000_000m, sonuc.YabanciKaynakToplami);
            Assert.Equal(400_000m, sonuc.AsanYabanciKaynak);
            Assert.Equal(40m, sonuc.AsanKisimOrani);
            Assert.Equal(150_000m, sonuc.DikkateAlinacakFinansmanGideri);
            Assert.Equal(60_000m, sonuc.AsanKismaIsabetEdenGider);
            Assert.Equal(6_000m, sonuc.Kkeg);
            Assert.Equal(10m, sonuc.KisitlamaOrani);
        }

        // ── Kenar: yabancı kaynak özsermayeyi aşmıyor → 4–9 sıfır ──

        [Fact]
        public void YabanciKaynakOzsermayeyiAsmiyor_TumSonucSatirlariSifir()
        {
            var sonuc = FinansmanGiderKisitlamasiMotoru.Hesapla(
                Girdi(1_500_000m, 1_000_000m, finansmanGideri: 200_000m, ortuluVeGelir: 20_000m));

            Assert.False(sonuc.KisitlamaVar);
            Assert.Equal(FinansmanGiderKisitlamasiMotoru.KisitlamaYokAciklamasi, sonuc.Aciklama);

            // 3. satır ham fark olarak duruyor (negatif); 4–9 sıfır.
            Assert.Equal(-500_000m, sonuc.AsanYabanciKaynak);
            Assert.Equal(0m, sonuc.AsanKisimOrani);
            Assert.Equal(0m, sonuc.AsanKismaIsabetEdenGider);
            Assert.Equal(0m, sonuc.Kkeg);

            // Girişler olduğu gibi geri dönüyor.
            Assert.Equal(200_000m, sonuc.FinansmanGideri);
            Assert.Equal(20_000m, sonuc.OrtuluSermayeVeFinansmanGeliri);
        }

        [Fact]
        public void OzsermayeYabanciKaynagaEsit_KisitlamaYok()
        {
            var sonuc = FinansmanGiderKisitlamasiMotoru.Hesapla(
                Girdi(1_000_000m, 1_000_000m, finansmanGideri: 100_000m));

            Assert.False(sonuc.KisitlamaVar);
            Assert.Equal(0m, sonuc.AsanYabanciKaynak);
            Assert.Equal(0m, sonuc.Kkeg);
        }

        // ── Kenar: özsermaye negatif → sıfır kabul edilip hesaplanıyor ──

        [Fact]
        public void OzsermayeNegatif_SifirKabulEdilipHesaplaniyor()
        {
            // 1 = 0 kabul edilir; aşan = 500.000, oran = %100
            var sonuc = FinansmanGiderKisitlamasiMotoru.Hesapla(
                Girdi(-250_000m, 500_000m, finansmanGideri: 80_000m));

            Assert.Equal(0m, sonuc.Ozsermaye);
            Assert.Equal(500_000m, sonuc.AsanYabanciKaynak);
            Assert.Equal(100m, sonuc.AsanKisimOrani);
            Assert.Equal(80_000m, sonuc.AsanKismaIsabetEdenGider);
            Assert.Equal(8_000m, sonuc.Kkeg);
        }

        // ── Kenar: yabancı kaynak sıfır → sıfıra bölme yok ──

        [Fact]
        public void YabanciKaynakSifir_SifiraBolmeYok()
        {
            var sonuc = FinansmanGiderKisitlamasiMotoru.Hesapla(
                Girdi(0m, 0m, finansmanGideri: 50_000m));

            Assert.False(sonuc.KisitlamaVar);
            Assert.Equal(0m, sonuc.AsanKisimOrani);
            Assert.Equal(0m, sonuc.Kkeg);
        }

        [Fact]
        public void OzsermayeNegatifVeYabanciKaynakSifir_SifiraBolmeYokAmaKisitlamaVar()
        {
            // Aşan tutar pozitif (0 − (−100.000)) ama bölen sıfır: oran sıfır kabul edilir.
            var sonuc = FinansmanGiderKisitlamasiMotoru.Hesapla(
                Girdi(-100_000m, 0m, finansmanGideri: 50_000m));

            Assert.False(sonuc.KisitlamaVar);
            Assert.Equal(0m, sonuc.AsanKisimOrani);
            Assert.Equal(0m, sonuc.Kkeg);
        }

        // ── Kenar: finansman geliri giderden fazla → 7. satır sıfır ──

        [Fact]
        public void FinansmanGeliriGiderdenFazla_DikkateAlinacakGiderSifir()
        {
            var sonuc = FinansmanGiderKisitlamasiMotoru.Hesapla(
                Girdi(400_000m, 1_000_000m, finansmanGideri: 30_000m, ortuluVeGelir: 45_000m));

            Assert.True(sonuc.KisitlamaVar);
            Assert.Equal(0m, sonuc.DikkateAlinacakFinansmanGideri);
            Assert.Equal(0m, sonuc.AsanKismaIsabetEdenGider);
            Assert.Equal(0m, sonuc.Kkeg);
        }

        // ── Kenar: yılın oranı tanımlı değil → anlaşılır hata ──

        [Fact]
        public void OranTanimliDegil_AnlasilirHataVeriyor()
        {
            var hata = Assert.Throws<FinansmanKisitlamaOraniYokException>(() =>
                FinansmanGiderKisitlamasiMotoru.Hesapla(
                    Girdi(600_000m, 1_000_000m, finansmanGideri: 200_000m, kisitlamaOrani: null)));

            Assert.Equal(Yil, hata.Yil);
            Assert.Contains(Yil.ToString(), hata.Message);
            Assert.Contains("kısıtlaması oranı tanımlı değil", hata.Message);
        }

        // ── Oran parametre: %10 dışında bir orana geçilince sonuç değişiyor ──

        [Fact]
        public void KisitlamaOraniParametre_FarkliOranFarkliKkeg()
        {
            var girdi = Girdi(600_000m, 1_000_000m, finansmanGideri: 200_000m, ortuluVeGelir: 50_000m, kisitlamaOrani: 25m);

            var sonuc = FinansmanGiderKisitlamasiMotoru.Hesapla(girdi);

            Assert.Equal(25m, sonuc.KisitlamaOrani);
            Assert.Equal(60_000m, sonuc.AsanKismaIsabetEdenGider);
            Assert.Equal(15_000m, sonuc.Kkeg);
        }

        // ── Yuvarlama: küsuratlı oranda kuruş ──

        [Fact]
        public void KusuratliOran_TutarlarIkiHaneyeYuvarlaniyor()
        {
            // aşan = 100.000 / 300.000 = %33,3333...  → 4. satır %33,33 gösterilir,
            // 8 ve 9 tam hassasiyetli oranla hesaplanır.
            var sonuc = FinansmanGiderKisitlamasiMotoru.Hesapla(
                Girdi(200_000m, 300_000m, finansmanGideri: 100_000m));

            Assert.Equal(33.33m, sonuc.AsanKisimOrani);
            Assert.Equal(33_333.33m, sonuc.AsanKismaIsabetEdenGider);
            Assert.Equal(3_333.33m, sonuc.Kkeg);
        }
    }
}
