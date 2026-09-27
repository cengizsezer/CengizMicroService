using WebApp.Application.Services;
using WebApp.Domain.Models.FirmaKontrol;
using WebApp.Shared.Dto.FirmaKontrol;

namespace WebApp.UnitTests.FirmaKontrol
{
    /// <summary>
    /// Etiket kural motoru (madde 2) ve ÇİFT SAYIM KORUMASI (madde 3).
    ///
    /// Madde 3'ün değişmezi: her ana hesap için
    ///   etiket toplamları + Atanmamış = ana hesabın bakiyesi, HER ZAMAN.
    /// Bu eşitlik bozulursa algoritma yanlıştır.
    ///
    /// Senaryo (firmaId 3 / 2026 strateji mizanı):
    ///   600        alacak 21.458.746,99  → sunumda +21.458.746,99
    ///   600 1      aynı para
    ///   600 1 20   12.194.054,99   → Kurumsal Strateji
    ///   600 1 21    9.264.692,00   → PKF Aday
    /// </summary>
    public class EtiketMotoruTests
    {
        private const int KurumsalStrateji = 10;
        private const int PkfAday = 11;

        /// <summary>Gelir hesabı: mizanda alacak bakiyeli → ham bakiye negatif (borç−alacak).</summary>
        private static HesapDugumu Gelir(string kod, decimal sunumTutari, string ad = "Yurtiçi Satışlar") =>
            HesapDugumu.Olustur(kod, ad, borc: 0m, alacak: sunumTutari, bakiye: -sunumTutari)!;

        private static EtiketKuraliDto Kural(
            long id, int sira, EtiketEslesmeTipi tip, string desen, int degerId) =>
            new() { Id = id, BoyutId = 1, Sira = sira, EslesmeTipi = tip, Desen = desen, DegerId = degerId };

        private static List<HesapDugumu> AltiYuzAgaci() => new()
        {
            Gelir("600", 21_458_746.99m),
            Gelir("600 1", 21_458_746.99m),
            Gelir("600 1 20", 12_194_054.99m),
            Gelir("600 1 21", 9_264_692.00m),
        };

        // ── Madde 7: ana senaryo ──

        [Fact]
        public void Iki_kod_deseni_kurali_dogru_tutarlari_dagitir()
        {
            var kurallar = new List<EtiketKuraliDto>
            {
                Kural(1, 1, EtiketEslesmeTipi.KodDeseni, "600 1 20*", KurumsalStrateji),
                Kural(2, 2, EtiketEslesmeTipi.KodDeseni, "600 1 21*", PkfAday),
            };

            var d = EtiketMotoru.Dagit(AltiYuzAgaci(), kurallar);

            Assert.Equal(12_194_054.99m, d.DegerToplamlari[KurumsalStrateji]);
            Assert.Equal(9_264_692.00m, d.DegerToplamlari[PkfAday]);

            // Çift sayım yok: 600 ve 600 1 ayrıca sayılmadı.
            Assert.Equal(21_458_746.99m, d.EtiketliToplam);
            Assert.Equal(0m, d.Atanmamis);
            Assert.Equal(21_458_746.99m, d.GenelToplam);
        }

        [Fact]
        public void Etiket_toplami_arti_atanmamis_ana_hesap_bakiyesine_esittir()
        {
            var kurallar = new List<EtiketKuraliDto>
            {
                Kural(1, 1, EtiketEslesmeTipi.KodDeseni, "600 1 20*", KurumsalStrateji),
            };

            var d = EtiketMotoru.Dagit(AltiYuzAgaci(), kurallar);

            // 600 1 21 hiçbir kurala düşmedi → Atanmamış.
            Assert.Equal(12_194_054.99m, d.DegerToplamlari[KurumsalStrateji]);
            Assert.Equal(9_264_692.00m, d.Atanmamis);
            Assert.Equal(21_458_746.99m, d.GenelToplam);
        }

        // ── Madde 7: iç içe etiket ──

        [Fact]
        public void Ic_ice_etikette_ust_dugum_kazanir_alt_dugum_ayrica_sayilmaz()
        {
            // 600 1 etiketli VE 600 1 21 de etiketli: üst kazanır, alta inilmez.
            var kurallar = new List<EtiketKuraliDto>
            {
                Kural(1, 1, EtiketEslesmeTipi.KodDeseni, "600 1", KurumsalStrateji),
                Kural(2, 2, EtiketEslesmeTipi.KodDeseni, "600 1 21*", PkfAday),
            };

            var d = EtiketMotoru.Dagit(AltiYuzAgaci(), kurallar);

            Assert.Equal(21_458_746.99m, d.DegerToplamlari[KurumsalStrateji]);
            Assert.False(d.DegerToplamlari.ContainsKey(PkfAday));   // alt düğüm sayılmadı
            Assert.Equal(0m, d.Atanmamis);
            Assert.Equal(21_458_746.99m, d.GenelToplam);
        }

        // ── Madde 7: doğrudan üst hesaba yazılmış tutar ──

        [Fact]
        public void Ust_hesaba_dogrudan_yazilmis_fark_atanmamisa_gider()
        {
            // Ana hesap 1.000, tek çocuğu 600 → etiketlenen 600, Atanmamış 400.
            var agac = new List<HesapDugumu>
            {
                Gelir("640", 1_000m, "Diğer Gelirler"),
                Gelir("640 1", 600m, "Alt kırılım"),
            };

            var kurallar = new List<EtiketKuraliDto>
            {
                Kural(1, 1, EtiketEslesmeTipi.KodDeseni, "640 1*", KurumsalStrateji),
            };

            var d = EtiketMotoru.Dagit(agac, kurallar);

            Assert.Equal(600m, d.DegerToplamlari[KurumsalStrateji]);
            Assert.Equal(400m, d.Atanmamis);
            Assert.Equal(1_000m, d.GenelToplam);
        }

        [Fact]
        public void Kuralsiz_agacta_her_sey_atanmamista_toplanir()
        {
            var d = EtiketMotoru.Dagit(AltiYuzAgaci(), new List<EtiketKuraliDto>());

            Assert.Empty(d.DegerToplamlari);
            Assert.Equal(21_458_746.99m, d.Atanmamis);
            Assert.Equal(21_458_746.99m, d.GenelToplam);
        }

        // ── Madde 2: eşleşme kuralları ──

        [Fact]
        public void Ilk_eslesen_kural_kazanir_devam_edilmez()
        {
            var kurallar = new List<EtiketKuraliDto>
            {
                Kural(2, 2, EtiketEslesmeTipi.KodDeseni, "600*", PkfAday),
                Kural(1, 1, EtiketEslesmeTipi.KodDeseni, "600 1 20*", KurumsalStrateji),
            };

            var eslesen = EtiketMotoru.Eslesen(Gelir("600 1 20", 1m), kurallar);

            Assert.NotNull(eslesen);
            Assert.Equal(1, eslesen!.Sira);
            Assert.Equal(KurumsalStrateji, eslesen.DegerId);
        }

        [Theory]
        [InlineData("600 1 21", "600 1 21*", true)]      // kendisi
        [InlineData("600 1 21 5", "600 1 21*", true)]    // altındaki kod
        [InlineData("600 1 2", "600 1 21*", false)]      // farklı kod, önek tuzağı
        [InlineData("600 1 210", "600 1 21*", false)]    // boşluk sınırı korunur
        [InlineData("600 1 21", "600 1 21", true)]       // joker yok → tam eşleşme
        [InlineData("600 1 21 5", "600 1 21", false)]
        public void Kod_deseni_joker_bosluk_sinirina_saygi_duyar(string kod, string desen, bool bekleniyor)
        {
            Assert.Equal(bekleniyor, EtiketMotoru.KodDeseniUyuyor(kod, desen));
        }

        [Theory]
        [InlineData("PKF Aday Şirketler", "aday", true)]
        [InlineData("PKF ADAY", "aday", true)]
        [InlineData("pkf aday", "ADAY", true)]
        [InlineData("İŞTİRAK Gelirleri", "istirak", true)]   // İ/ı/i ve Ş/s duyarsız
        [InlineData("Çeşitli Gelirler", "cesitli", true)]
        [InlineData("Yurtiçi Satışlar", "yurtici", true)]
        [InlineData("Yurtdışı Satışlar", "yurtici", false)]
        public void Ad_icerir_turkce_karakter_duyarsizdir(string ad, string aranan, bool bekleniyor)
        {
            Assert.Equal(bekleniyor, EtiketMotoru.AdIceriyor(ad, aranan));
        }

        [Fact]
        public void Tek_secim_yalnizca_tam_kodu_eslestirir()
        {
            var kural = Kural(1, 1, EtiketEslesmeTipi.TekSecim, "600 1 21", PkfAday);

            Assert.True(EtiketMotoru.Uyuyor(Gelir("600 1 21", 1m), kural));
            Assert.False(EtiketMotoru.Uyuyor(Gelir("600 1 21 5", 1m), kural));
            Assert.False(EtiketMotoru.Uyuyor(Gelir("600 1", 1m), kural));
        }

        // ── Kapsam: payda boyutun kapsam tanımıdır ──

        /// <summary>
        /// Kapsam olmadan payda bütün gelir tablosunun NETİ olurdu; gelirler artı,
        /// giderler eksi olduğu için birbirini yer ve yüzde %100'ü aşar.
        /// Kapsam "600,601,610" ile payda 22.020.812,69 çıkar ve oran %97,45 olur.
        /// </summary>
        [Fact]
        public void Kapsam_paydasi_yalnizca_kapsamdaki_hesaplardan_olusur()
        {
            var agac = new List<HesapDugumu>
            {
                Gelir("600", 21_458_746.99m),
                Gelir("600 1", 21_458_746.99m),
                Gelir("600 1 20", 12_194_054.99m),
                Gelir("600 1 21", 9_264_692.00m),
                Gelir("601", 1_228_856.20m, "Yurtdışı Satışlar"),
                Gelir("610", -666_790.50m, "Satıştan İadeler (-)"),

                // Kapsam dışı: gider hesapları paydayı yememeli.
                Gelir("770", -2_374_451.51m, "Genel Yönetim Giderleri"),
                Gelir("632", -1_000_000.00m, "Pazarlama Giderleri"),
            };

            var kurallar = new List<EtiketKuraliDto>
            {
                Kural(1, 1, EtiketEslesmeTipi.KodDeseni, "600 1 20*", KurumsalStrateji),
                Kural(2, 2, EtiketEslesmeTipi.KodDeseni, "600 1 21*", PkfAday),
            };

            var kapsamli = EtiketMotoru.Kapsa(agac, "600,601,610");
            var d = EtiketMotoru.Dagit(kapsamli, kurallar);

            Assert.Equal(12_194_054.99m, d.DegerToplamlari[KurumsalStrateji]);
            Assert.Equal(9_264_692.00m, d.DegerToplamlari[PkfAday]);
            Assert.Equal(562_065.70m, d.Atanmamis);              // 1.228.856,20 − 666.790,50
            Assert.Equal(22_020_812.69m, d.GenelToplam);         // kapsam toplamı

            // Etiketli + Atanmamış = kapsam toplamı, HER ZAMAN.
            Assert.Equal(d.GenelToplam, d.EtiketliToplam + d.Atanmamis);

            var oran = (double)(d.EtiketliToplam / d.GenelToplam) * 100d;
            Assert.Equal(97.45d, Math.Round(oran, 2));
            Assert.True(oran <= 100d, "Kapsam yüzdesi %100'ü aşamaz.");

            // Uyarı yalnızca 601 ve 610 için çıkmalı — gider hesapları için değil.
            Assert.Equal(new[] { "601", "610" }, d.EtiketsizDugumler.Select(x => x.Kod).OrderBy(x => x).ToArray());
        }

        [Theory]
        [InlineData("600,601,610", "600", true)]
        [InlineData("600,601,610", "610", true)]
        [InlineData("600,601,610", "602", false)]
        [InlineData("600,601,610", "770", false)]
        [InlineData("6*", "600", true)]
        [InlineData("6*", "610", true)]
        [InlineData("6*", "770", false)]
        [InlineData("60*", "601", true)]
        [InlineData("60*", "610", false)]
        [InlineData("", "600", false)]        // kapsam tanımsızsa hiçbir şey kapsamda değil
        public void Kapsam_deseni_ana_hesap_koduna_uygulanir(string kapsam, string anaKod, bool bekleniyor)
        {
            var dugum = Gelir(anaKod + " 1 20", 1m);
            var desenler = EtiketMotoru.KapsamDesenleri(kapsam);

            Assert.Equal(bekleniyor, EtiketMotoru.KapsamdaMi(dugum, desenler));
        }

        // ── Madde 7: etiketsiz hesaplar listelenir ──

        [Fact]
        public void Etiketsiz_bakiyeli_hesaplar_listelenir()
        {
            var agac = new List<HesapDugumu>
            {
                Gelir("600", 21_458_746.99m),
                Gelir("600 1", 21_458_746.99m),
                Gelir("600 1 20", 12_194_054.99m),
                Gelir("600 1 21", 9_264_692.00m),
                Gelir("601", 1_228_856.20m, "Yurtdışı Satışlar"),
                Gelir("610", -666_790.50m, "Satıştan İadeler (-)"),
            };

            var kurallar = new List<EtiketKuraliDto>
            {
                Kural(1, 1, EtiketEslesmeTipi.KodDeseni, "600 1 20*", KurumsalStrateji),
                Kural(2, 2, EtiketEslesmeTipi.KodDeseni, "600 1 21*", PkfAday),
            };

            var d = EtiketMotoru.Dagit(agac, kurallar);

            var etiketsizKodlar = d.EtiketsizDugumler.Select(x => x.Kod).ToList();
            Assert.Contains("601", etiketsizKodlar);
            Assert.Contains("610", etiketsizKodlar);

            // Denklik üç ana hesabın toplamı üzerinden de bozulmaz.
            Assert.Equal(21_458_746.99m + 1_228_856.20m - 666_790.50m, d.GenelToplam);
            Assert.Equal(1_228_856.20m - 666_790.50m, d.Atanmamis);
        }
    }
}
