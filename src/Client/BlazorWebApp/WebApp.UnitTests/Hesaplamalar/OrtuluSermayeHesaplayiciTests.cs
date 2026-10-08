using WebApp.Pages.Hesaplamalar.OrtuluSermaye;
using WebApp.Pages.Hesaplamalar.OrtuluSermaye.Model;
using WebApp.Pages.Hesaplamalar.OrtuluSermaye.Services;

namespace WebApp.UnitTests.Hesaplamalar
{
    /// <summary>
    /// Örtülü sermaye hesabı (KARARLAR §156). Beklenen değerler maketteki örnek veriden
    /// elle hesaplandı: zirve 31.03.2026'da 22.695.000; sınır 15.000.000; aşan 7.695.000;
    /// oran 7.695.000 / 22.695.000 ≈ %33,906.
    /// </summary>
    public class OrtuluSermayeHesaplayiciTests
    {
        private static List<MuavinSatiri> Yukle(string tsv)
        {
            var sonuc = MuavinTsvParser.Ayristir(tsv);
            Assert.True(sonuc.Basarili, string.Join(" | ", sonuc.Hatalar));
            return sonuc.Satirlar.ToList();
        }

        private static OrtuluSermayeParametre OrnekParametre() => new()
        {
            DonemSonu = OrnekVeri.DonemSonu,
            DonemBasiOzSermaye = OrnekVeri.DonemBasiOzSermaye
        };

        private static MuavinSatiri Satir(string kod, string tarih, decimal borc, decimal alacak) => new()
        {
            HesapKodu = kod,
            Tarih = DateTime.ParseExact(tarih, "dd.MM.yyyy", null),
            Borc = borc,
            Alacak = alacak
        };

        private static decimal R(decimal d) => Math.Round(d, 2);

        // ---------------- Maketteki örnek veri ----------------

        [Fact]
        public void OrnekVeri_EnYuksekBorc_31MartToplamZirvesi()
        {
            var borc = Yukle(OrnekVeri.BorcMuavini);

            var z = OrtuluSermayeHesaplayici.EnYuksekBorc(borc, new HashSet<string>());

            Assert.Equal(22_695_000m, z.Tutar);
            Assert.Equal(new DateTime(2026, 3, 31), z.Tarih);
            Assert.Equal("118", z.ZirveSatiri!.FisNo);
        }

        [Fact]
        public void OrnekVeri_SiniflandirmaMaketleAyni()
        {
            var gider = Yukle(OrnekVeri.GiderMuavini);

            Assert.Equal(new[]
            {
                MuavinSinifi.KurFarkiGideri, MuavinSinifi.KurFarkiGeliri,
                MuavinSinifi.FaizGideri, MuavinSinifi.FaizGideri, MuavinSinifi.DamgaVergisi,
                MuavinSinifi.FaizKdv, MuavinSinifi.FaizKdv
            }, gider.Select(s => s.Sinif));
        }

        [Fact]
        public void OrnekVeri_TamMukellef_BeyannameTutarlari()
        {
            var s = OrtuluSermayeHesaplayici.Hesapla(
                OrnekParametre(), Yukle(OrnekVeri.BorcMuavini), new HashSet<string>(), Yukle(OrnekVeri.GiderMuavini));

            Assert.Equal(22_695_000m, s.EnYuksekBorc);
            Assert.Equal(22_695_000m, s.DikkateAlinanBorc);
            Assert.Equal(15_000_000m, s.Sinir);
            Assert.Equal(7_695_000m, s.AsanKisim);
            Assert.Equal(0.339061m, Math.Round(s.OrtuluOran, 6));
            Assert.True(s.OrtuluSermayeVar);

            Assert.Equal(210_218.11m, R(s.KurFarkiGideri));
            Assert.Equal(61_031.06m, R(s.KurFarkiGeliri));
            Assert.Equal(269_553.87m, R(s.FaizGideri));
            Assert.Equal(53_910.77m, R(s.FaizKdv));
            Assert.Equal(30_165.87m, R(s.DamgaVergisi));
            Assert.Equal(0m, s.FaizStopaji);
            Assert.Equal(563_848.62m, R(s.KkegToplam));

            Assert.Equal(620_000m, s.Toplamlar.KurFarkiGideri);
            Assert.Equal(180_000m, s.Toplamlar.KurFarkiGeliri);
            Assert.Equal(795_000m, s.Toplamlar.FaizGideri);
            Assert.Equal(159_000m, s.Toplamlar.FaizKdv);
            Assert.Equal(88_968.75m, s.Toplamlar.DamgaVergisi);
        }

        [Fact]
        public void KurFarkiGeliri_GidereMahsupEdilmez()
        {
            var s = OrtuluSermayeHesaplayici.Hesapla(
                OrnekParametre(), Yukle(OrnekVeri.BorcMuavini), new HashSet<string>(), Yukle(OrnekVeri.GiderMuavini));

            Assert.Equal(R(s.KurFarkiGideri + s.FaizGideri + s.FaizKdv + s.DamgaVergisi), R(s.KkegToplam));
            Assert.True(s.KurFarkiGeliri > 0);
        }

        [Fact]
        public void GercekKisiBorcVeren_FaizStopajiYalnizFaizden()
        {
            var p = OrnekParametre();
            p.BorcVerenTipi = BorcVerenTipi.GercekKisiVeyaDarMukellef;
            p.StopajOrani = 0.15m;

            var s = OrtuluSermayeHesaplayici.Hesapla(
                p, Yukle(OrnekVeri.BorcMuavini), new HashSet<string>(), Yukle(OrnekVeri.GiderMuavini));

            Assert.Equal(40_433.08m, R(s.FaizStopaji));
            // Stopaj KKEG'e girmez.
            Assert.Equal(563_848.62m, R(s.KkegToplam));
        }

        [Fact]
        public void IliskiliBanka_BorcunYarisiDikkateAlinir()
        {
            var p = OrnekParametre();
            p.IliskiliBankaMi = true;

            var s = OrtuluSermayeHesaplayici.Hesapla(
                p, Yukle(OrnekVeri.BorcMuavini), new HashSet<string>(), Yukle(OrnekVeri.GiderMuavini));

            Assert.Equal(11_347_500m, s.DikkateAlinanBorc);
            Assert.Equal(0m, s.AsanKisim);
            Assert.Equal(0m, s.OrtuluOran);
        }

        // ---------------- En yüksek borç kuralları ----------------

        [Fact]
        public void AyniGunCokluHareket_ZirveGunSonuBakiyesi()
        {
            // Gün içinde 1.000 girip 800 çıkıyor: ara bakiye 1.000 zirve SAYILMAZ, gün sonu 200.
            var satirlar = new List<MuavinSatiri>
            {
                Satir("331.01", "01.02.2026", 0, 1_000),
                Satir("331.01", "01.02.2026", 800, 0),
                Satir("331.01", "05.02.2026", 0, 100)
            };

            var z = OrtuluSermayeHesaplayici.EnYuksekBorc(satirlar, new HashSet<string>());

            Assert.Equal(300m, z.Tutar);
            Assert.Equal(new DateTime(2026, 2, 5), z.Tarih);
            Assert.Equal(1_000m, z.Kumulatif[satirlar[0]]);
            Assert.Equal(200m, z.Kumulatif[satirlar[1]]);
        }

        [Fact]
        public void AyniGunFarkliHesaplar_ZirveSatiriGununSonHareketi()
        {
            var a = Satir("331.01", "10.03.2026", 0, 500);
            var b = Satir("336.01", "10.03.2026", 0, 300);

            var z = OrtuluSermayeHesaplayici.EnYuksekBorc(new[] { a, b }, new HashSet<string>());

            Assert.Equal(800m, z.Tutar);
            Assert.Same(b, z.ZirveSatiri);
        }

        [Fact]
        public void HesapMaksimumlariToplanmaz()
        {
            // 331'in zirvesi 1.000 (Ocak), 336'nın zirvesi 1.000 (Mart); hiçbir gün toplam 2.000 olmuyor.
            var satirlar = new List<MuavinSatiri>
            {
                Satir("331.01", "10.01.2026", 0, 1_000),
                Satir("331.01", "10.02.2026", 1_000, 0),
                Satir("336.01", "10.03.2026", 0, 1_000)
            };

            var z = OrtuluSermayeHesaplayici.EnYuksekBorc(satirlar, new HashSet<string>());

            Assert.Equal(1_000m, z.Tutar);
            Assert.Equal(new DateTime(2026, 1, 10), z.Tarih);
        }

        [Fact]
        public void TarihSirasiYapistirmaSirasindanBagimsiz()
        {
            var satirlar = new List<MuavinSatiri>
            {
                Satir("331.01", "20.03.2026", 600, 0),
                Satir("331.01", "01.03.2026", 0, 1_000)
            };

            var z = OrtuluSermayeHesaplayici.EnYuksekBorc(satirlar, new HashSet<string>());

            Assert.Equal(1_000m, z.Tutar);
            Assert.Equal(new DateTime(2026, 3, 1), z.Tarih);
        }

        [Fact]
        public void Hesap320_VarsayilanHaric_IstenirseDahil()
        {
            var satirlar = new List<MuavinSatiri>
            {
                Satir("331.01", "01.01.2026", 0, 1_000),
                Satir("320.01.005", "15.01.2026", 0, 5_000),
                Satir("3201", "16.01.2026", 0, 1) // 320 ile başlıyor; kod olarak ayrı hesap ama aynı kural
            };

            var haric = OrtuluSermayeHesaplayici.VarsayilanHaricler(satirlar);
            Assert.Equal(new HashSet<string> { "320.01.005", "3201" }, haric);
            Assert.False(OrtuluSermayeHesaplayici.VarsayilanHaricMi("331.01"));

            var haricle = OrtuluSermayeHesaplayici.EnYuksekBorc(satirlar, haric);
            Assert.Equal(1_000m, haricle.Tutar);
            Assert.False(haricle.Kumulatif.ContainsKey(satirlar[1]));

            var dahil = OrtuluSermayeHesaplayici.EnYuksekBorc(satirlar, new HashSet<string>());
            Assert.Equal(6_001m, dahil.Tutar);
        }

        [Fact]
        public void BorcHicPozitifOlmadi_ZirveYok()
        {
            var z = OrtuluSermayeHesaplayici.EnYuksekBorc(
                new[] { Satir("331.01", "01.01.2026", 500, 0) }, new HashSet<string>());

            Assert.Equal(0m, z.Tutar);
            Assert.Null(z.Tarih);
            Assert.Null(z.ZirveSatiri);
        }

        // ---------------- Sınır aşılmıyor ----------------

        [Fact]
        public void SinirAsilmiyor_TumOrtuluTutarlarSifir()
        {
            var p = OrnekParametre();
            p.DonemBasiOzSermaye = 10_000_000m; // sınır 30.000.000 > 22.695.000
            p.BorcVerenTipi = BorcVerenTipi.GercekKisiVeyaDarMukellef;

            var s = OrtuluSermayeHesaplayici.Hesapla(
                p, Yukle(OrnekVeri.BorcMuavini), new HashSet<string>(), Yukle(OrnekVeri.GiderMuavini));

            Assert.False(s.OrtuluSermayeVar);
            Assert.Equal(22_695_000m, s.EnYuksekBorc);
            Assert.Equal(0m, s.AsanKisim);
            Assert.Equal(0m, s.OrtuluOran);
            Assert.Equal(0m, s.KurFarkiGideri);
            Assert.Equal(0m, s.KurFarkiGeliri);
            Assert.Equal(0m, s.FaizGideri);
            Assert.Equal(0m, s.FaizStopaji);
            Assert.Equal(0m, s.KkegToplam);
            // Oransız toplamlar yine görünür.
            Assert.Equal(795_000m, s.Toplamlar.FaizGideri);
        }

        [Fact]
        public void SinirTamEsit_OrtuluSermayeYok()
        {
            var p = OrnekParametre();
            p.DonemBasiOzSermaye = 22_695_000m / 3;

            var s = OrtuluSermayeHesaplayici.Hesapla(
                p, Yukle(OrnekVeri.BorcMuavini), new HashSet<string>(), Array.Empty<MuavinSatiri>());

            Assert.Equal(0m, s.AsanKisim);
        }

        [Fact]
        public void BosVeri_SifirBolmeYok()
        {
            var s = OrtuluSermayeHesaplayici.Hesapla(
                OrnekParametre(), Array.Empty<MuavinSatiri>(), new HashSet<string>(), Array.Empty<MuavinSatiri>());

            Assert.Equal(0m, s.OrtuluOran);
            Assert.Null(s.EnYuksekBorcTarihi);
        }

        [Fact]
        public void NegatifOzSermaye_BorcunTamamiOrtulu()
        {
            var p = OrnekParametre();
            p.DonemBasiOzSermaye = -1_000_000m;

            var s = OrtuluSermayeHesaplayici.Hesapla(
                p, Yukle(OrnekVeri.BorcMuavini), new HashSet<string>(), Array.Empty<MuavinSatiri>());

            Assert.Equal(0m, s.Sinir);
            Assert.Equal(1m, s.OrtuluOran);
        }

        // ---------------- Sınıf toplamlarının işareti ----------------

        [Fact]
        public void Toplamlar_GiderdeBorcEksiAlacak_GelirVeStopajdaAlacakEksiBorc()
        {
            var satirlar = new[]
            {
                new MuavinSatiri { Sinif = MuavinSinifi.KurFarkiGideri, Borc = 100, Alacak = 30 },
                new MuavinSatiri { Sinif = MuavinSinifi.KurFarkiGeliri, Borc = 10, Alacak = 50 },
                new MuavinSatiri { Sinif = MuavinSinifi.FaizGideri, Borc = 200 },
                new MuavinSatiri { Sinif = MuavinSinifi.FaizKdv, Borc = 40 },
                new MuavinSatiri { Sinif = MuavinSinifi.Stopaj, Alacak = 25 },
                new MuavinSatiri { Sinif = MuavinSinifi.DamgaVergisi, Borc = 9 },
                new MuavinSatiri { Sinif = MuavinSinifi.Yok, Borc = 999_999 }
            };

            var t = OrtuluSermayeHesaplayici.Toplamlar(satirlar);

            Assert.Equal(70m, t.KurFarkiGideri);
            Assert.Equal(40m, t.KurFarkiGeliri);
            Assert.Equal(200m, t.FaizGideri);
            Assert.Equal(40m, t.FaizKdv);
            Assert.Equal(25m, t.Stopaj);
            Assert.Equal(9m, t.DamgaVergisi);
        }

        // ---------------- Sınıflandırma ----------------

        [Theory]
        [InlineData("656.01", "x", MuavinSinifi.KurFarkiGideri)]
        [InlineData("646.01", "x", MuavinSinifi.KurFarkiGeliri)]
        [InlineData("780.01", "Faiz tahakkuku", MuavinSinifi.FaizGideri)]
        [InlineData("661.01", "Kredi faizi", MuavinSinifi.FaizGideri)]
        [InlineData("770.01", "Faiz", MuavinSinifi.FaizGideri)]
        [InlineData("780.01", "Kredi sözleşmesi DAMGA vergisi", MuavinSinifi.DamgaVergisi)]
        [InlineData("780.01", "Sözleşme DV", MuavinSinifi.DamgaVergisi)]
        [InlineData("780.01", "DVZ faiz", MuavinSinifi.FaizGideri)] // "DV" ayrı kelime değil
        [InlineData("191.01", "Faiz faturası KDV", MuavinSinifi.FaizKdv)]
        [InlineData("360.01", "Muhtasar kâr payı stopajı", MuavinSinifi.Stopaj)]
        [InlineData("360.01", "Sorumlu KDV", MuavinSinifi.FaizKdv)]
        [InlineData("360.01", "Damga vergisi", MuavinSinifi.DamgaVergisi)]
        [InlineData("360.01", "SGK primi", MuavinSinifi.Yok)]
        [InlineData("770.99", "Kira", MuavinSinifi.FaizGideri)]
        [InlineData("120.01", "Faiz", MuavinSinifi.Yok)]
        public void Siniflandirma(string kod, string aciklama, MuavinSinifi beklenen)
            => Assert.Equal(beklenen, MuavinSiniflandirici.Sinifla(kod, aciklama));
    }
}
