using System.Reflection;
using PkfRobot.Config;
using PkfRobot.Core;

namespace PkfRobot.UnitTests.Core;

/// <summary>
/// Hiz carpani: yavas gunde "biraz daha bekle" demenin gorev dosyasina
/// dokunmadan yolu.
///
/// Iki kural sinaniyor: (1) yalniz Bekle adimlari olcekleniyor, (2) diskteki
/// gorev ve bellekteki ORIJINAL nesne degismiyor -- konsol modlari
/// (--gorev, --kalibre, --probe) ayni dosyalari carpansiz calistirmaya devam
/// etmeli.
/// </summary>
public class HizlandiriciTests
{
    private static Gorev OrnekGorev() => new()
    {
        Ad = "Ornek",
        Aciklama = "aciklama",
        Adimlar = new List<Adim>
        {
            new() { Tip = "Bekle", Sayi = 3000, Not = "veri transferi aciliyor" },
            new() { Tip = "Tikla", X = 0.4, Y = 0.25, CiftTik = true, Not = "Sablon satiri" },
            new() { Tip = "Tus", Deger = "ENTER", Adet = 2, Yuzde = 25 },
            new() { Tip = "BeklePencere", Deger = "ORKA_", TimeoutSn = 90 },
            new() { Tip = "bekle", Sayi = 500 }
        }
    };

    [Fact]
    public void Bekle_sureleri_carpanla_olcekleniyor()
    {
        var sonuc = Hizlandirici.Uygula(OrnekGorev(), 2.0);

        Assert.Equal(6000, sonuc.Adimlar[0].Sayi);
        Assert.Equal(1000, sonuc.Adimlar[4].Sayi);   // kucuk harfli "bekle" de sayilir
    }

    [Fact]
    public void Yavaslatma_da_hizlandirma_da_calisiyor()
    {
        Assert.Equal(1500, Hizlandirici.Uygula(OrnekGorev(), 0.5).Adimlar[0].Sayi);
        Assert.Equal(7500, Hizlandirici.Uygula(OrnekGorev(), 2.5).Adimlar[0].Sayi);
    }

    [Fact]
    public void Bekle_disindaki_adimlar_hicbir_alanini_kaybetmiyor()
    {
        // Adim'a yeni bir alan eklendiginde bu test kendiliginden yakalar:
        // kopyalamayi unutmak, ornegin CiftTik'i sessizce false yapardi.
        var orijinal = OrnekGorev();
        var sonuc = Hizlandirici.Uygula(orijinal, 3.0);

        var alanlar = typeof(Adim).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        for (var i = 0; i < orijinal.Adimlar.Count; i++)
        {
            if (Hizlandirici.BekleMi(orijinal.Adimlar[i])) continue;

            foreach (var alan in alanlar)
                Assert.Equal(alan.GetValue(orijinal.Adimlar[i]), alan.GetValue(sonuc.Adimlar[i]));
        }
    }

    [Fact]
    public void Bekle_adiminin_diger_alanlari_da_korunuyor()
    {
        var sonuc = Hizlandirici.Uygula(OrnekGorev(), 2.0);

        Assert.Equal("Bekle", sonuc.Adimlar[0].Tip);
        Assert.Equal("veri transferi aciliyor", sonuc.Adimlar[0].Not);
    }

    [Fact]
    public void Orijinal_gorev_degismiyor()
    {
        // Konsol modlari ayni nesneyi carpansiz calistirabilmeli.
        var orijinal = OrnekGorev();

        Hizlandirici.Uygula(orijinal, 4.0);

        Assert.Equal(3000, orijinal.Adimlar[0].Sayi);
        Assert.Equal(500, orijinal.Adimlar[4].Sayi);
    }

    [Fact]
    public void Carpan_bir_ise_gorev_oldugu_gibi_donuyor()
    {
        var orijinal = OrnekGorev();

        Assert.Same(orijinal, Hizlandirici.Uygula(orijinal, 1.0));
    }

    [Theory]
    [InlineData(0, Hizlandirici.EnAz)]
    [InlineData(-5, Hizlandirici.EnAz)]
    [InlineData(0.01, Hizlandirici.EnAz)]
    [InlineData(999, Hizlandirici.EnCok)]
    [InlineData(2.5, 2.5)]
    public void Carpan_gecerli_araliga_cekiliyor(double gelen, double beklenen)
    {
        // Sifir carpan butun beklemeleri yok eder ve robot ORKA'yi bekleyemez.
        Assert.Equal(beklenen, Hizlandirici.Kirp(gelen), 5);
    }

    [Fact]
    public void Gecersiz_sayi_bire_dusuyor()
    {
        Assert.Equal(1.0, Hizlandirici.Kirp(double.NaN), 5);
    }

    // ---- config: tuslar arasi bekleme ---------------------------------------
    // Carpan eskiden yalniz Bekle adimlarini olcekliyordu; tuslar arasi bosluga
    // (Zamanlama.TusBeklemeMs) hic dokunmuyordu. Modul gezinmesi tam orada
    // kirildi ve "carpani 1.5 yaptim, degismedi" gozlemi hatayi eledi sanildi.

    private static RobotConfig OrnekConfig() => new()
    {
        OrkaPath = @"C:\WinIceberg\OrkaWinIceberg.64.exe",
        Zamanlama = new ZamanlamaAyar
        {
            AdimBeklemeMs = 700,
            TusBeklemeMs = 150,
            PencereTimeoutSn = 40,
            OrkaAcilisTimeoutSn = 90
        }
    };

    [Fact]
    public void Tuslar_arasi_bekleme_carpanla_olcekleniyor()
    {
        var sonuc = Hizlandirici.Uygula(OrnekConfig(), 1.5);

        Assert.Equal(225, sonuc.Zamanlama.TusBeklemeMs);
    }

    [Fact]
    public void Config_carpani_orijinali_degistirmiyor()
    {
        // Ayni config nesnesi ayni anda calisan ajan isinde de kullanilabiliyor.
        var orijinal = OrnekConfig();

        Hizlandirici.Uygula(orijinal, 3.0);

        Assert.Equal(150, orijinal.Zamanlama.TusBeklemeMs);
    }

    [Fact]
    public void Config_carpani_bir_ise_nesne_oldugu_gibi_donuyor()
    {
        var orijinal = OrnekConfig();

        Assert.Same(orijinal, Hizlandirici.Uygula(orijinal, 1.0));
    }

    [Fact]
    public void Timeoutlar_ve_adim_beklemesi_carpandan_etkilenmiyor()
    {
        // Timeout "ne kadar bekleyeyim" degil "ne kadar sonra pes edeyim" sorusu;
        // formda ayri alanlar olarak duruyor ve iki yerden ayarlanmamali.
        var sonuc = Hizlandirici.Uygula(OrnekConfig(), 2.0);

        Assert.Equal(700, sonuc.Zamanlama.AdimBeklemeMs);
        Assert.Equal(40, sonuc.Zamanlama.PencereTimeoutSn);
        Assert.Equal(90, sonuc.Zamanlama.OrkaAcilisTimeoutSn);
        Assert.Equal(@"C:\WinIceberg\OrkaWinIceberg.64.exe", sonuc.OrkaPath);
    }

    [Fact]
    public void Yavaslatma_kadar_hizlandirma_da_tuslara_yansiyor()
    {
        Assert.Equal(75, Hizlandirici.Uygula(OrnekConfig(), 0.5).Zamanlama.TusBeklemeMs);
        Assert.Equal(1500, Hizlandirici.Uygula(OrnekConfig(), 10.0).Zamanlama.TusBeklemeMs);
    }
}
