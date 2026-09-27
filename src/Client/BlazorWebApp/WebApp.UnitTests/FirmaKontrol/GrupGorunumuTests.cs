using WebApp.Application.Services;
using WebApp.Application.Services.Interfaces;
using WebApp.Domain.Models.FirmaKontrol;
using WebApp.Shared.Dto.FirmaKontrol;

namespace WebApp.UnitTests.FirmaKontrol
{
    /// <summary>
    /// PROMPT 3B — yönetsel görünüm ve grup matrisi.
    ///
    /// Senaryo (madde 8, firmaId 3 / 2026): "Grup Şirketi" boyutu, kapsam 600,601,610.
    ///   Kurumsal Strateji → FirmaId = 3 (bu firma)
    ///   PKF Aday          → FirmaId = 7 (PKF Aday firması)
    ///   Kurallar: 600 1 20* → Kurumsal, 600 1 21* → Aday.
    /// </summary>
    public class GrupGorunumuTests
    {
        private const int BuFirma = 3;
        private const int AdayFirma = 7;

        private const int KurumsalStrateji = 10;
        private const int PkfAday = 11;

        private const decimal YasalNetSatislar = 22_020_812.69m;

        private static HesapDugumu Gelir(string kod, decimal sunumTutari, string ad = "Yurtiçi Satışlar") =>
            HesapDugumu.Olustur(kod, ad, borc: 0m, alacak: sunumTutari, bakiye: -sunumTutari)!;

        private static List<HesapDugumu> StratejiAgaci() => new()
        {
            Gelir("600", 21_458_746.99m),
            Gelir("600 1", 21_458_746.99m),
            Gelir("600 1 20", 12_194_054.99m),
            Gelir("600 1 21", 9_264_692.00m),
            Gelir("601", 1_228_856.20m, "Yurtdışı Satışlar"),
            Gelir("610", -666_790.50m, "Satıştan İadeler (-)"),

            // Kapsam dışı: gider, ciroya girmemeli.
            Gelir("770", -2_374_451.51m, "Genel Yönetim Giderleri"),
        };

        private static EtiketBoyutuDto Boyut() => new()
        {
            Id = 1,
            Ad = "Grup Şirketi",
            KapsamHesaplari = "600,601,610",
            Degerler = new()
            {
                new() { Id = KurumsalStrateji, BoyutId = 1, Ad = "Kurumsal Strateji", Renk = "#2a78d6", Sira = 1, FirmaId = BuFirma },
                new() { Id = PkfAday, BoyutId = 1, Ad = "PKF Aday", Renk = "#eb6834", Sira = 2, FirmaId = AdayFirma },
            },
            Kurallar = new()
            {
                new() { Id = 1, BoyutId = 1, Sira = 1, EslesmeTipi = EtiketEslesmeTipi.KodDeseni, Desen = "600 1 20*", DegerId = KurumsalStrateji },
                new() { Id = 2, BoyutId = 1, Sira = 2, EslesmeTipi = EtiketEslesmeTipi.KodDeseni, Desen = "600 1 21*", DegerId = PkfAday },
            }
        };

        private static GrupGorunumu.Defter KendiDefteri(EtiketBoyutuDto boyut) =>
            GrupGorunumu.DefterOlustur(BuFirma, "Kurumsal Strateji", Yuklu(StratejiAgaci()),
                boyut.KapsamHesaplari, boyut.Kurallar);

        private static HesapAgaciSonucu Yuklu(List<HesapDugumu> agac) => new(HesapAgaciDurumu.Yuklu, agac);

        private static readonly HesapAgaciSonucu Yuklenmedi =
            new(HesapAgaciDurumu.MizanYuklenmedi, Array.Empty<HesapDugumu>());

        // ── Yönetsel görünüm ──

        [Fact]
        public void Yonetsel_net_satislar_baska_sirkete_etiketli_tutari_duser()
        {
            var boyut = Boyut();
            var kendi = KendiDefteri(boyut);
            var aday = GrupGorunumu.DefterOlustur(AdayFirma, "PKF Aday", Yuklenmedi, boyut.KapsamHesaplari, boyut.Kurallar);

            var y = GrupGorunumu.YonetselHesapla(BuFirma, YasalNetSatislar, kendi, new[] { kendi, aday }, boyut.Degerler);

            Assert.Equal(22_020_812.69m, y.Yasal);                       // yasal değişmedi
            Assert.Equal(9_264_692.00m, y.BaskaSirketeEtiketli);
            Assert.Equal(0m, y.DigerDefterlerdenGelen);
            Assert.Equal(12_756_120.69m, y.Yonetsel);

            // Aday mizanı yüklenmedi: sıfır yazıp geçilmez, eksik olarak işaretlenir.
            Assert.True(y.Eksik);
            Assert.Equal(AdayFirma, Assert.Single(y.EksikDefterler).FirmaId);
        }

        [Fact]
        public void Yonetsel_diger_defterde_bu_sirkete_etiketli_tutari_ekler()
        {
            var boyut = Boyut();
            var kendi = KendiDefteri(boyut);

            // Aday defterinde 600 1 20 (Kurumsal) 500 TL, 600 1 21 (Aday) 1.500 TL.
            var adayAgaci = new List<HesapDugumu>
            {
                Gelir("600", 2_000m),
                Gelir("600 1", 2_000m),
                Gelir("600 1 20", 500m),
                Gelir("600 1 21", 1_500m),
            };
            var aday = GrupGorunumu.DefterOlustur(AdayFirma, "PKF Aday", Yuklu(adayAgaci), boyut.KapsamHesaplari, boyut.Kurallar);

            var y = GrupGorunumu.YonetselHesapla(BuFirma, YasalNetSatislar, kendi, new[] { kendi, aday }, boyut.Degerler);

            Assert.False(y.Eksik);
            Assert.Equal(500m, y.DigerDefterlerdenGelen);
            Assert.Equal(12_756_120.69m + 500m, y.Yonetsel);
        }

        [Fact]
        public void Firmasi_tanimsiz_etiket_defterin_sahibinde_kalir()
        {
            var boyut = Boyut();
            boyut.Degerler[1].FirmaId = null;   // "PKF Aday" artık firması olmayan bir etiket

            var kendi = KendiDefteri(boyut);
            var y = GrupGorunumu.YonetselHesapla(BuFirma, YasalNetSatislar, kendi, new[] { kendi }, boyut.Degerler);

            Assert.Equal(0m, y.BaskaSirketeEtiketli);
            Assert.Equal(9_264_692.00m, y.FirmasizEtiketli);
            Assert.Equal(YasalNetSatislar, y.Yonetsel);
        }

        // ── Grup matrisi ──

        [Fact]
        public void Matris_kurumsal_satiri_madde_8_rakamlarini_verir()
        {
            var boyut = Boyut();
            var kendi = KendiDefteri(boyut);
            var aday = GrupGorunumu.DefterOlustur(AdayFirma, "PKF Aday", Yuklenmedi, boyut.KapsamHesaplari, boyut.Kurallar);

            var m = GrupGorunumu.MatrisOlustur(new[] { kendi, aday }, boyut.Degerler);

            var s = m.Satirlar[0];
            Assert.Equal(12_194_054.99m, s.Hucreler[KurumsalStrateji]);
            Assert.Equal(9_264_692.00m, s.Hucreler[PkfAday]);
            Assert.Equal(562_065.70m, s.Atanmamis);
            Assert.Equal(22_020_812.69m, s.SatirToplami);

            // Yüklenmemiş satır hücre üretmez, toplamlara girmez.
            Assert.False(m.Satirlar[1].Defter.Yuklu);
            Assert.Empty(m.Satirlar[1].Hucreler);

            // Çift sayım kontrolü: satır toplamları = sütun toplamları.
            Assert.Equal(22_020_812.69m, m.SatirToplamlariToplami);
            Assert.Equal(22_020_812.69m, m.SutunToplamlariToplami);
            Assert.False(m.CiftSayimVar);
        }

        [Fact]
        public void Matris_iki_yuklu_defterde_satir_ve_sutun_toplamlari_esittir()
        {
            var boyut = Boyut();
            var kendi = KendiDefteri(boyut);
            var aday = GrupGorunumu.DefterOlustur(AdayFirma, "PKF Aday", Yuklu(new List<HesapDugumu>
            {
                Gelir("600", 2_000m),
                Gelir("600 1", 1_800m),          // 200 doğrudan ana hesaba yazılmış → Atanmamış
                Gelir("600 1 20", 500m),
                Gelir("600 1 21", 1_300m),
                Gelir("610", -100m, "Satıştan İadeler (-)"),
            }), boyut.KapsamHesaplari, boyut.Kurallar);

            var m = GrupGorunumu.MatrisOlustur(new[] { kendi, aday }, boyut.Degerler);

            Assert.Equal(12_194_054.99m + 500m, m.SutunToplamlari[KurumsalStrateji]);
            Assert.Equal(9_264_692.00m + 1_300m, m.SutunToplamlari[PkfAday]);
            Assert.Equal(562_065.70m + 200m - 100m, m.AtanmamisToplami);

            Assert.Equal(m.SatirToplamlariToplami, m.SutunToplamlariToplami);
            Assert.False(m.CiftSayimVar);
        }

        /// <summary>
        /// Bir tutar iki etikete birden yazılırsa (bir hesap iki kurala uymuş gibi) hücre
        /// toplamı ana hesap bakiyesini aşar; satır ≠ sütun olur ve kontrol bunu yakalar.
        /// </summary>
        [Fact]
        public void Cift_sayim_varsa_kontrol_yakalar()
        {
            var boyut = Boyut();
            var kendi = KendiDefteri(boyut);

            var bozuk = new GrupGorunumu.MatrisSatiri
            {
                Defter = kendi,
                Hucreler = new Dictionary<int, decimal>
                {
                    [KurumsalStrateji] = 12_194_054.99m,
                    // 600 1 21 hem Aday'a hem Kurumsal'a sayılmış gibi:
                    [PkfAday] = 9_264_692.00m + 9_264_692.00m,
                },
                Atanmamis = 562_065.70m,
                SatirToplami = 22_020_812.69m
            };

            var m = GrupGorunumu.MatrisTopla(new List<GrupGorunumu.MatrisSatiri> { bozuk }, boyut.Degerler);

            Assert.True(m.CiftSayimVar);
            Assert.NotEqual(m.SatirToplamlariToplami, m.SutunToplamlariToplami);
        }

        [Fact]
        public void Matris_satirlari_bakilan_firma_ve_firmasi_dolu_degerlerle_sinirlidir()
        {
            var degerler = new List<EtiketDegeriDto>
            {
                new() { Id = 1, Sira = 1, FirmaId = BuFirma },
                new() { Id = 2, Sira = 2, FirmaId = AdayFirma },
                new() { Id = 3, Sira = 3, FirmaId = null },       // "Grup dışı" — satır üretmez
                new() { Id = 4, Sira = 4, FirmaId = AdayFirma },  // tekrar — tekrarsız
            };

            Assert.Equal(new[] { BuFirma, AdayFirma }, GrupGorunumu.IlgiliFirmalar(BuFirma, degerler));
        }

        // ── Ciro kümesi ve yasal kırılım ──

        [Fact]
        public void Ciro_kumesi_kapsam_ile_net_satis_hesaplarinin_kesisimidir()
        {
            var ciro = GrupGorunumu.CiroAgaci(StratejiAgaci(), "6*,7*");

            Assert.DoesNotContain(ciro, d => d.AnaHesapKodu == "770");
            Assert.Equal(new[] { "770" }, GrupGorunumu.CiroDisiKapsamHesaplari(StratejiAgaci(), "6*,7*"));
        }

        [Fact]
        public void Yasal_kirilim_bilgi_amaclidir_ve_ana_hesap_bakiyesine_esittir()
        {
            var kirilim = GrupGorunumu.AnaHesapKirilimlari(StratejiAgaci(), Boyut());

            var k600 = kirilim["600"];
            Assert.Equal(12_194_054.99m, k600.Single(k => k.Ad == "Kurumsal Strateji").Tutar);
            Assert.Equal(9_264_692.00m, k600.Single(k => k.Ad == "PKF Aday").Tutar);
            Assert.Equal(21_458_746.99m, k600.Sum(k => k.Tutar));

            // Etiketi olmayan ana hesaplarda kırılım açılmaz.
            Assert.False(kirilim.ContainsKey("601"));
            Assert.False(kirilim.ContainsKey("610"));
        }
    }
}
