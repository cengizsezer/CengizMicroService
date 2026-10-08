using WebApp.Pages.Hesaplamalar.OrtuluSermaye;
using WebApp.Pages.Hesaplamalar.OrtuluSermaye.Model;
using WebApp.Pages.Hesaplamalar.OrtuluSermaye.Services;

namespace WebApp.UnitTests.Hesaplamalar
{
    /// <summary>
    /// ORKA muavini yapıştırma (KARARLAR §156): başlık ada göre, başlıksız 9 sütun,
    /// iki sayı biçimi, okunamayan hücre asla 0 sayılmaz.
    /// </summary>
    public class MuavinTsvParserTests
    {
        private const string Baslik = "Acc. No\tAcc Name\tDate\tSlip Type\tSlip No\tDescription\tDebit\tCredit\tBalance";

        // ---------------- Sayı biçimi ----------------

        [Theory]
        // Boş hücre gerçekten 0
        [InlineData("", 0)]
        [InlineData("   ", 0)]
        // Ayırıcısız
        [InlineData("0", 0)]
        [InlineData("1234", 1234)]
        [InlineData("1234567", 1234567)]
        // İki ayırıcı birlikte: sağdaki ondalık
        [InlineData("1.234,56", 1234.56)]
        [InlineData("1,234.56", 1234.56)]
        [InlineData("1.234.567,89", 1234567.89)]
        [InlineData("1,234,567.89", 1234567.89)]
        [InlineData("12.345,6", 12345.6)]
        [InlineData("123,456.789", 123456.789)]
        // Tek tür, birden çok: binlik
        [InlineData("1.234.567", 1234567)]
        [InlineData("1,234,567", 1234567)]
        // Tek ayırıcı + tam 3 hane: binlik
        [InlineData("1,234", 1234)]
        [InlineData("1.234", 1234)]
        [InlineData("12,345", 12345)]
        [InlineData("999.000", 999000)]
        // Tek ayırıcı + 3'ten farklı hane: ondalık
        [InlineData("1,5", 1.5)]
        [InlineData("1.5", 1.5)]
        [InlineData("1234,56", 1234.56)]
        [InlineData("1234.56", 1234.56)]
        [InlineData("0,25", 0.25)]
        [InlineData("12,3456", 12.3456)]
        // Tam kısım 0 iken 3 hane: binlik okuması anlamsız, ondalık
        [InlineData("0,125", 0.125)]
        [InlineData("0.125", 0.125)]
        // İşaret
        [InlineData("-1.234,56", -1234.56)]
        [InlineData("-1,234.56", -1234.56)]
        [InlineData("(1.234,56)", -1234.56)]
        [InlineData("-5", -5)]
        // Boşluklar (bölünmez boşluk dahil) yok sayılır
        [InlineData(" 1.234,56 ", 1234.56)]
        [InlineData("1 234,56", 1234.56)]
        public void SayiOku_Gecerli(string metin, double beklenen)
        {
            Assert.True(MuavinTsvParser.SayiOku(metin, out var d), metin);
            Assert.Equal((decimal)beklenen, d);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("12a")]
        [InlineData("1.234,56 TL")]
        [InlineData("₺1.234")]
        [InlineData("1,2,3")]             // tek tür çoklu ama 3'lü grup değil
        [InlineData("1.23.456")]
        [InlineData("1234,567")]          // 3 hane → binlik, ama tam kısım grup olamaz
        [InlineData("1,234,56")]
        [InlineData("1.234,567.89")]      // ondalık ayırıcı iki kez
        [InlineData("1,23.45")]           // binlik grubu 3 hane değil
        [InlineData("12345,678.9")]       // ilk grup 3'ten uzun
        [InlineData(",5")]
        [InlineData("5,")]
        [InlineData(".")]
        [InlineData("-")]
        [InlineData("--5")]
        [InlineData("1-")]
        [InlineData("1e5")]
        public void SayiOku_Gecersiz(string metin)
            => Assert.False(MuavinTsvParser.SayiOku(metin, out _), metin);

        // ---------------- Tarih ----------------

        [Theory]
        [InlineData("31.03.2026")]
        [InlineData("31/03/2026")]
        [InlineData("1.3.2026", 1)]
        [InlineData("31.03.2026 00:00:00")]
        public void TarihOku_Gecerli(string metin, int gun = 31)
        {
            Assert.True(MuavinTsvParser.TarihOku(metin, out var t));
            Assert.Equal(new DateTime(2026, 3, gun), t);
        }

        [Theory]
        [InlineData("")]
        [InlineData("2026-03-31")]
        [InlineData("03/31/2026")]
        [InlineData("31.13.2026")]
        [InlineData("dün")]
        public void TarihOku_Gecersiz(string metin)
            => Assert.False(MuavinTsvParser.TarihOku(metin, out _));

        // ---------------- Satır/sütun ----------------

        [Fact]
        public void Basliksiz_DokuzSutunVarsayilanSira()
        {
            var s = MuavinTsvParser.Ayristir(OrnekVeri.BorcMuavini);

            Assert.True(s.Basarili);
            Assert.False(s.BaslikVardi);
            Assert.Equal(7, s.Satirlar.Count);
            var ilk = s.Satirlar[1];
            Assert.Equal("331.01.001", ilk.HesapKodu);
            Assert.Equal("PKF Holding A.Ş.", ilk.HesapAdi);
            Assert.Equal(new DateTime(2026, 2, 15), ilk.Tarih);
            Assert.Equal("Mahsup", ilk.FisTuru);
            Assert.Equal("42", ilk.FisNo);
            Assert.Equal("Ortak borç girişi EUR 250.000", ilk.Aciklama);
            Assert.Equal(0m, ilk.Borc);
            Assert.Equal(9_375_000m, ilk.Alacak);
            Assert.Equal(18_375_000m, ilk.Bakiye);
        }

        [Fact]
        public void Baslikli_SatirAtlanir_CRLFOkunur()
        {
            var metin = Baslik + "\r\n" + OrnekVeri.GiderMuavini.Replace("\n", "\r\n") + "\r\n";

            var s = MuavinTsvParser.Ayristir(metin);

            Assert.True(s.Basarili, string.Join(" | ", s.Hatalar));
            Assert.True(s.BaslikVardi);
            Assert.Equal(7, s.Satirlar.Count);
            Assert.Equal(MuavinSinifi.KurFarkiGideri, s.Satirlar[0].Sinif);
            // Son sütun (Balance) satır sonundaki \r ile okunamasaydı hata dönerdi.
            Assert.Equal(159_000m, s.Satirlar[^1].Bakiye);
        }

        [Fact]
        public void Baslikli_SutunlarAdaGoreEslenir_SiraSerbest()
        {
            var metin =
                "Credit\tDebit\tDescription\tDate\tAcc. No\n" +
                "1.000,00\t\tOrtak borcu\t05.01.2026\t331.01\n";

            var s = MuavinTsvParser.Ayristir(metin);

            Assert.True(s.Basarili, string.Join(" | ", s.Hatalar));
            var r = Assert.Single(s.Satirlar);
            Assert.Equal("331.01", r.HesapKodu);
            Assert.Equal(1_000m, r.Alacak);
            Assert.Equal(0m, r.Borc);
            Assert.Equal("Ortak borcu", r.Aciklama);
            Assert.Equal("", r.HesapAdi);
        }

        [Fact]
        public void Baslikli_TurkceAdlarDaTaninir()
        {
            var metin =
                "Hesap Kodu\tHesap Adı\tTarih\tAçıklama\tBorç\tAlacak\n" +
                "336.01\tGrup\t05.01.2026\tFinansman\t\t2.500,00\n";

            var s = MuavinTsvParser.Ayristir(metin);

            Assert.True(s.Basarili, string.Join(" | ", s.Hatalar));
            Assert.Equal(2_500m, Assert.Single(s.Satirlar).Alacak);
        }

        [Fact]
        public void Baslikli_ZorunluSutunYoksaHata()
        {
            var s = MuavinTsvParser.Ayristir("Acc. No\tAcc Name\tDate\tDebit\n331\tX\t01.01.2026\t5\n");

            Assert.False(s.Basarili);
            Assert.Contains(s.Hatalar, h => h.Contains("Credit"));
            Assert.Empty(s.Satirlar);
        }

        [Fact]
        public void SutunSayisiTutmazsa_SatirNumarasiylaHata_HicSatirDonmez()
        {
            var metin =
                "331.01\tA\t01.01.2026\tAçılış\t1\tx\t\t100,00\t100,00\n" +
                "331.01\tA\t02.01.2026\tAçılış\t1\tx\t\t100,00\n";     // 8 sütun

            var s = MuavinTsvParser.Ayristir(metin);

            Assert.False(s.Basarili);
            var h = Assert.Single(s.Hatalar);
            Assert.StartsWith("Satır 2:", h);
            Assert.Contains("8 sütun", h);
            Assert.Empty(s.Satirlar);
        }

        [Fact]
        public void Baslikli_SutunSayisiBaslikKadarOlmali()
        {
            var metin = Baslik + "\n331.01\tA\t01.01.2026\n";

            var s = MuavinTsvParser.Ayristir(metin);

            Assert.False(s.Basarili);
            Assert.StartsWith("Satır 2:", Assert.Single(s.Hatalar));
        }

        [Fact]
        public void OkunamayanTutar_SifirSayilmaz_SatirNumarasiylaHata()
        {
            var metin =
                "\n" +
                "331.01\tA\t01.01.2026\tAçılış\t1\tx\t\t1.000,00\t1.000,00\n" +
                "331.01\tA\t02.01.2026\tMahsup\t2\tx\t\tbin lira\t1.000,00\n";

            var s = MuavinTsvParser.Ayristir(metin);

            Assert.False(s.Basarili);
            var h = Assert.Single(s.Hatalar);
            Assert.StartsWith("Satır 3:", h);
            Assert.Contains("Credit", h);
            Assert.Empty(s.Satirlar);
        }

        [Fact]
        public void OkunamayanTarih_Hata()
        {
            var s = MuavinTsvParser.Ayristir("331.01\tA\t2026-01-01\tAçılış\t1\tx\t\t1,00\t1,00");

            Assert.False(s.Basarili);
            Assert.Contains("tarih", Assert.Single(s.Hatalar));
        }

        [Fact]
        public void HesapKoduBosSatirlarAtlanirVeSayilir_BosSatirlarSayilmaz()
        {
            var metin =
                Baslik + "\n" +
                "331.01\tA\t01.01.2026\tAçılış\t1\tx\t\t1.000,00\t1.000,00\n" +
                "\n" +
                "\t\t\t\t\tToplam\t0,00\t1.000,00\t1.000,00\n" +
                "\t\t\t\t\t\t\t\t\n" +                 // yalnız tab: boş satır, sayılmaz
                "   \n" +
                "\t\tGenel Toplam\t\t\t\t0,00\t1.500,00\t\n" +
                "336.01\tB\t02.01.2026\tAçılış\t1\tx\t\t500,00\t500,00\n";

            var s = MuavinTsvParser.Ayristir(metin);

            Assert.True(s.Basarili, string.Join(" | ", s.Hatalar));
            Assert.Equal(2, s.Satirlar.Count);
            Assert.Equal(2, s.AtlananSatir);
        }

        [Fact]
        public void IkiSayiBicimiAyniYapistirmada()
        {
            var metin =
                "331.01\tA\t01.01.2026\tAçılış\t1\tx\t\t1,234.56\t1,234.56\n" +
                "331.01\tA\t02/01/2026\tMahsup\t2\tx\t\t1.000,50\t2.235,06\n";

            var s = MuavinTsvParser.Ayristir(metin);

            Assert.True(s.Basarili, string.Join(" | ", s.Hatalar));
            Assert.Equal(1_234.56m, s.Satirlar[0].Alacak);
            Assert.Equal(1_000.50m, s.Satirlar[1].Alacak);
            Assert.Equal(new DateTime(2026, 1, 2), s.Satirlar[1].Tarih);
        }

        [Fact]
        public void BosMetin_Hata()
        {
            Assert.False(MuavinTsvParser.Ayristir("").Basarili);
            Assert.False(MuavinTsvParser.Ayristir(null).Basarili);
            Assert.False(MuavinTsvParser.Ayristir("\n \n").Basarili);
        }

        [Fact]
        public void YalnizBaslik_Hata()
            => Assert.False(MuavinTsvParser.Ayristir(Baslik).Basarili);

        [Fact]
        public void CokHata_YirmiIleSinirlanir()
        {
            var metin = string.Join("\n", Enumerable.Range(1, 30).Select(_ => "331\tA\tx\tB\t1\td\t\t1\t1"));

            var s = MuavinTsvParser.Ayristir(metin);

            Assert.Equal(21, s.Hatalar.Count);
            Assert.Contains("10 hata daha", s.Hatalar[^1]);
        }
    }
}
