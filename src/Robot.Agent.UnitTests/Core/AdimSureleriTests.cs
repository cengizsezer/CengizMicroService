using PkfRobot.Config;
using PkfRobot.Core;

namespace PkfRobot.UnitTests.Core;

/// <summary>
/// Formda "2/4 banka bitti · adim 12/38" vardi ama hangi adimda ne kadar
/// beklendigi gorunmuyordu: 45 saniye duran bir <c>AltPencereDogrula</c> ile
/// 4 saniyelik bir <c>Bekle</c> ekranda birebir ayni goruniyordu ve hangi
/// beklemenin kisaltilacagi bilinemiyordu.
///
/// Burada sabitlenen: sure bicimi, adim etiketi (hassas degerler MASKELI),
/// en yavas adim ozeti ve tek satirlik ilerleme metni.
/// </summary>
public class AdimSureleriTests
{
    [Fact]
    public void Bir_dakikanin_altinda_saniye_ondalikli()
    {
        Assert.Equal("3.2 sn", SureBicimi.Kisa(TimeSpan.FromSeconds(3.24)));
        Assert.Equal("45.1 sn", SureBicimi.Kisa(TimeSpan.FromSeconds(45.14)));
        Assert.Equal("0.0 sn", SureBicimi.Kisa(TimeSpan.Zero));
    }

    [Fact]
    public void Bir_dakikanin_ustunde_dakika_saniye()
    {
        Assert.Equal("1 dk 5 sn", SureBicimi.Kisa(TimeSpan.FromSeconds(65)));
        Assert.Equal("12 dk 30 sn", SureBicimi.Kisa(TimeSpan.FromSeconds(750)));
    }

    [Fact]
    public void Ondalik_ayraci_makine_dilinden_bagimsiz()
    {
        // Makine Turkce; virgul kullanilsaydi virgulle ayrilmis adim listesinde
        // ("[26] X 45,1 sn, [14] Y 4,5 sn") yeni adimin nerede basladigi okunmazdi.
        var eski = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");

        try { Assert.Equal("3.2 sn", SureBicimi.Kisa(TimeSpan.FromSeconds(3.2))); }
        finally { Thread.CurrentThread.CurrentCulture = eski; }
    }

    // ---- adim etiketi -------------------------------------------------------

    [Fact]
    public void Etiket_notu_varsa_notu_kullaniyor()
    {
        var adim = new Adim { Tip = "BeklePencere", Deger = "Hesap Plan", Not = "Hesap Plani" };

        Assert.Equal("BeklePencere 'Hesap Plani'", AdimMotoru.AdimEtiketi(adim));
    }

    [Fact]
    public void Etiket_not_yoksa_degeri_kullaniyor()
    {
        var adim = new Adim { Tip = "BeklePencere", Deger = "Hesap Plan" };

        Assert.Equal("BeklePencere 'Hesap Plan'", AdimMotoru.AdimEtiketi(adim));
    }

    [Fact]
    public void Etiket_bekle_adiminda_sureyi_gosteriyor()
    {
        Assert.Equal("Bekle '3000 ms'", AdimMotoru.AdimEtiketi(new Adim { Tip = "Bekle", Sayi = 3000 }));
    }

    [Fact]
    public void Etiket_hassas_degeri_MASKELIYOR()
    {
        // Ilerleme satiri ekranda duruyor ve ekran goruntusu aliniyor; sifre
        // oraya dusmemeli.
        var adim = new Adim { Tip = "Yaz", Deger = "{firmaSifre}" };

        Assert.Equal("Yaz '***'", AdimMotoru.AdimEtiketi(adim));
    }

    [Fact]
    public void Etiket_degersiz_adimda_yalniz_tip()
    {
        Assert.Equal("GridDoldur", AdimMotoru.AdimEtiketi(new Adim { Tip = "GridDoldur" }));
    }

    // ---- en yavas adimlar ---------------------------------------------------

    private static AdimOlcumu Olcum(int no, string tip, double sn)
        => new(no, tip, TimeSpan.FromSeconds(sn));

    [Fact]
    public void En_yavas_bes_adim_uzundan_kisaya()
    {
        var olcumler = new[]
        {
            Olcum(14, "Bekle", 4.5), Olcum(26, "AltPencereDogrula", 45.1),
            Olcum(2, "Tus", 0.4), Olcum(7, "BeklePencere", 12.0),
            Olcum(9, "Tikla", 1.2), Olcum(30, "Yaz", 0.9)
        };

        var enYavaslar = AdimSureOzeti.EnYavaslar(olcumler);

        Assert.Equal(new[] { 26, 7, 14, 9, 30 }, enYavaslar.Select(o => o.No));
    }

    [Fact]
    public void Ozet_satiri_adim_numarasi_tip_ve_sure_iceriyor()
    {
        var ozet = AdimSureOzeti.Ozet(new[]
        {
            Olcum(26, "AltPencereDogrula", 45.1), Olcum(14, "Bekle", 4.5)
        });

        Assert.Equal("En yavas adimlar: [26] AltPencereDogrula 45.1 sn, [14] Bekle 4.5 sn", ozet);
    }

    [Fact]
    public void Olcum_yoksa_ozet_BOS()
    {
        // Bos bir "En yavas adimlar:" satiri log'a yazilmasin.
        Assert.Equal(string.Empty, AdimSureOzeti.Ozet(Array.Empty<AdimOlcumu>()));
    }

    [Fact]
    public void Bes_adimdan_az_varsa_hepsi_yaziliyor()
    {
        var ozet = AdimSureOzeti.Ozet(new[] { Olcum(1, "Tus", 1.0) });

        Assert.Equal("En yavas adimlar: [1] Tus 1.0 sn", ozet);
    }

    // ---- ilerleme metni -----------------------------------------------------

    [Fact]
    public void Ilerleme_metni_adim_ve_sureleri_gosteriyor()
    {
        var metin = IlerlemeMetni.Yaz(2, 4, "adim 12/38 -- BeklePencere 'Hesap Plan'",
                                      TimeSpan.FromSeconds(3.2), TimeSpan.FromSeconds(252));

        Assert.Equal("2/4 banka bitti · adim 12/38 -- BeklePencere 'Hesap Plan' · 3.2 sn · " +
                     "toplam 4 dk 12 sn", metin);
    }

    [Fact]
    public void Adim_bilgisi_yokken_satir_yarim_kalmiyor()
    {
        // Kuyruk baslarken ve gorevler arasinda adim bilgisi yok.
        var metin = IlerlemeMetni.Yaz(0, 4, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));

        Assert.Equal("0/4 banka bitti · toplam 2.0 sn", metin);
    }

    [Fact]
    public void Adim_ilerlemesi_tek_satira_donuyor()
    {
        Assert.Equal("adim 12/38 -- BeklePencere 'Hesap Plan'",
                     new AdimIlerlemesi(12, 38, "BeklePencere 'Hesap Plan'").ToString());
    }
}
