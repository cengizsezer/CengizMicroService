using PkfRobot.Ayarlar;

namespace PkfRobot.UnitTests.Arayuz;

/// <summary>
/// Kalibrasyonun sayisal cekirdegi.
///
/// Arayuz test edilemez ama <b>kural</b> edilebilir: yanlis hesaplanan tek bir
/// oran, robotun ORKA'da bambaska bir yere tiklamasi demek ve bunu kimse fark
/// etmiyor -- sayi makul gorunuyor.
/// </summary>
public class OranDonusturucuTests
{
    // Pencere ekranin sol ustunde degil: sol/ust degerleri hesaba katilmazsa
    // testler 0,0 konumunda yanlislikla gecerdi.
    private static readonly PencereOlcusu Pencere = new(Sol: 100, Ust: 50, Genislik: 1920, Yukseklik: 1080);

    [Fact]
    public void Mutlak_koordinat_pencereye_goreli_orana_cevriliyor()
    {
        var (x, y) = OranDonusturucu.Oran(100 + 240, 50 + 324, Pencere);

        Assert.Equal(0.125, x, 5);
        Assert.Equal(0.300, y, 5);
    }

    [Theory]
    // Ayni nokta, farkli cozunurluk: oran degismemeli. Piksel kullanilsaydi
    // makine degistiginde koordinat kayardi -- oranin varlik sebebi bu.
    [InlineData(1920, 1080)]
    [InlineData(1366, 768)]
    [InlineData(2560, 1440)]
    public void Oran_pencere_boyundan_bagimsiz(int genislik, int yukseklik)
    {
        var pencere = new PencereOlcusu(0, 0, genislik, yukseklik);
        var mutlakX = (int)Math.Round(genislik * 0.125);
        var mutlakY = (int)Math.Round(yukseklik * 0.300);

        var (x, y) = OranDonusturucu.Oran(mutlakX, mutlakY, pencere);

        // Tolerans piksel yuvarlamasindan: 1366 genislikte 0.125 orani 170.75.
        // pikseldir. Assert.Equal'in ondalik hassasiyeti bankaci yuvarlamasi
        // kullandigi icin burada acik tolerans tercih edildi.
        Assert.True(Math.Abs(x - 0.125) < 0.002, $"X sapmasi cok buyuk: {x}");
        Assert.True(Math.Abs(y - 0.300) < 0.002, $"Y sapmasi cok buyuk: {y}");
    }

    [Fact]
    public void Oran_mutlaga_cevrilince_ayni_noktaya_donuyor()
    {
        // Secici ile Tikla adimi ayni hesabi ters yonde yapiyor; ikisi
        // ayrilirsa kullanicinin sectigi yer ile robotun tikladigi yer sessizce
        // farklilasir.
        var (oranX, oranY) = OranDonusturucu.Oran(837, 461, Pencere);
        var (x, y) = OranDonusturucu.Mutlak(oranX, oranY, Pencere);

        Assert.Equal(837, x);
        Assert.Equal(461, y);
    }

    [Fact]
    public void Olcusuz_pencere_reddediliyor()
    {
        // Simge durumuna kucultulmus pencere: bolme sifira bolme olurdu.
        var kucultulmus = new PencereOlcusu(0, 0, 0, 0);

        Assert.Throws<ArgumentException>(() => OranDonusturucu.Oran(10, 10, kucultulmus));
        Assert.Throws<ArgumentException>(() => OranDonusturucu.Mutlak(0.5, 0.5, kucultulmus));
    }

    [Fact]
    public void Oran_her_zaman_nokta_ile_yaziliyor()
    {
        // Turkce locale virgul basar ve JSON'a yapistirilan deger bozulur.
        var onceki = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");

        try
        {
            Assert.Equal("0.125", OranDonusturucu.Yaz(0.125));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = onceki;
        }
    }
}

/// <summary>
/// "Kullanicinin tikladigi nokta kaydedilebilir mi?" karari.
/// </summary>
public class KoordinatSecimiTests
{
    private const int OrkaSureci = 4242;
    private static readonly PencereOlcusu Orka = new(0, 0, 1920, 1080);

    private static TiklamaOrtami Ortam(
        int x = 240, int y = 324,
        PencereOlcusu? pencere = null,
        bool orkaVar = true,
        int? tiklananSurec = OrkaSureci,
        bool tamEkran = true,
        int[]? orkaSurecleri = null,
        string tiklananBaslik = "ORKA_0001_2026",
        string tiklananSurecAdi = "OrkaWinIceberg.64")
        => new(x, y, orkaVar ? pencere ?? Orka : null, tiklananSurec,
               orkaSurecleri ?? new[] { OrkaSureci }, tamEkran,
               tiklananBaslik, tiklananSurecAdi,
               BeklenenSurecAdi: "OrkaWinIceberg.64",
               OrkaAnaPencereBasligi: "ORKA_0001_2026");

    [Fact]
    public void Orka_penceresine_tiklama_kabul_ediliyor()
    {
        var sonuc = KoordinatSecimi.Degerlendir(Ortam());

        Assert.True(sonuc.Kabul);
        Assert.Equal(0.125, sonuc.OranX, 3);
        Assert.Equal(0.300, sonuc.OranY, 3);
        Assert.False(sonuc.Uyari);
        Assert.Equal(RedSebebi.Yok, sonuc.Sebep);
    }

    [Fact]
    public void Orkanin_alt_penceresine_tiklama_kabul_ediliyor()
    {
        // Veri Transferi ekrani, dialoglar ve firma sifresi popup'i ORKA'nin
        // AYRI pencereleri ama AYNI sureci. Kalibrasyona asil bu ekranlarda
        // ihtiyac var; "yalniz ana pencere" sarti onlari olculemez yapardi.
        var sonuc = KoordinatSecimi.Degerlendir(
            Ortam(tiklananBaslik: "Veri Transferi"));

        Assert.True(sonuc.Kabul);
        Assert.Equal(RedSebebi.Yok, sonuc.Sebep);
    }

    [Fact]
    public void Alt_pencereye_tiklansa_da_oran_ana_pencereye_gore_hesaplaniyor()
    {
        // Kritik ayrim: yetki denetimi TIKLANAN pencereye, oran ANA pencereye
        // bakar. AdimMotoru.Tikla da tiklamayi ana pencereye (Pencereler.
        // AnaEkran) oranla uyguluyor; alt pencereye gore olculen bir oran
        // calisma aninda bambaska bir noktaya duserdi.
        var ana = new PencereOlcusu(Sol: 100, Ust: 50, Genislik: 1000, Yukseklik: 800);

        // Nokta ana pencerenin ortasi; ustundeki modal diyalog kucuk ve kaymis
        // olsun -- sonuca girmemeli, cunku hesaba yalniz ana pencere giriyor.
        var sonuc = KoordinatSecimi.Degerlendir(
            Ortam(x: 600, y: 450, pencere: ana, tiklananBaslik: "Firma Sifresini Giriniz."));

        Assert.True(sonuc.Kabul);
        Assert.Equal(0.5, sonuc.OranX, 3);
        Assert.Equal(0.5, sonuc.OranY, 3);
    }

    [Fact]
    public void Orka_disi_pencereye_tiklama_reddediliyor()
    {
        // Baska bir pencerede olculen oran ORKA'ya uygulandiginda bambaska bir
        // noktaya duser. Sayi makul gorundugu icin de kimse fark etmez.
        var sonuc = KoordinatSecimi.Degerlendir(
            Ortam(tiklananSurec: 9999, tiklananBaslik: "Adsiz - Paint", tiklananSurecAdi: "mspaint"));

        Assert.False(sonuc.Kabul);
        Assert.Equal(RedSebebi.BaskaUygulama, sonuc.Sebep);
    }

    [Fact]
    public void Ret_mesaji_hangi_pencereye_tiklandigini_ve_beklenen_yaziyor()
    {
        var sonuc = KoordinatSecimi.Degerlendir(
            Ortam(tiklananSurec: 9999, tiklananBaslik: "Adsiz - Paint", tiklananSurecAdi: "mspaint"));

        // Kullanicinin ekranda gormesi gereken uc sey: hangi denetim tetiklendi,
        // hangi pencereye tiklandi, ne bekleniyordu.
        Assert.Contains("pencere sureci ORKA degil", sonuc.Mesaj);
        Assert.Contains("Adsiz - Paint", sonuc.Mesaj);
        Assert.Contains("mspaint", sonuc.Mesaj);
        Assert.Contains("9999", sonuc.Mesaj);
        Assert.Contains("OrkaWinIceberg.64", sonuc.Mesaj);
    }

    [Fact]
    public void Surec_okunamadiysa_da_reddediliyor()
    {
        // "Bilmiyorum" durumunda kabul etmek, yanlis koordinatin sessizce
        // kaydedilmesine acik kapi birakirdi.
        var sonuc = KoordinatSecimi.Degerlendir(Ortam(tiklananSurec: null));

        Assert.False(sonuc.Kabul);
        Assert.Equal(RedSebebi.PencereSureciOkunamadi, sonuc.Sebep);
        Assert.Contains("sureci okunamadi", sonuc.Mesaj);
    }

    [Fact]
    public void Orka_yoksa_reddediliyor()
    {
        var sonuc = KoordinatSecimi.Degerlendir(Ortam(orkaVar: false));

        Assert.False(sonuc.Kabul);
        Assert.Equal(RedSebebi.OrkaKapali, sonuc.Sebep);
        Assert.Contains("ANA penceresi bulunamadi", sonuc.Mesaj);
    }

    [Fact]
    public void Orka_sureci_hic_yoksa_reddediliyor()
    {
        var sonuc = KoordinatSecimi.Degerlendir(Ortam(orkaSurecleri: Array.Empty<int>()));

        Assert.False(sonuc.Kabul);
        Assert.Equal(RedSebebi.OrkaKapali, sonuc.Sebep);
        Assert.Contains("ORKA calismiyor", sonuc.Mesaj);
    }

    [Fact]
    public void Ana_pencerenin_disina_tiklama_reddediliyor()
    {
        // Surec ORKA'nin ama nokta ana pencerenin disinda: oran 0..1 disina
        // cikar ve AdimMotoru.Tikla bu degeri zaten reddederdi; burada durmak
        // hem daha erken hem sebebi soylenebilir olan yer.
        var sonuc = KoordinatSecimi.Degerlendir(Ortam(x: 2400, y: 324));

        Assert.False(sonuc.Kabul);
        Assert.Equal(RedSebebi.AnaPencereDisinda, sonuc.Sebep);
        Assert.Contains("disinda", sonuc.Mesaj);
    }

    [Fact]
    public void Tam_ekran_olmayan_orkada_uyari_veriliyor_ama_kaydediliyor()
    {
        // Oran pencerenin o anki olcusunden hesaplaniyor; maximize olmamasi
        // matematigi bozmuyor. Risk pencerenin sonradan yeniden
        // boyutlandirilmasi -- uyari konusu, ret konusu degil.
        var sonuc = KoordinatSecimi.Degerlendir(Ortam(tamEkran: false));

        Assert.True(sonuc.Kabul);
        Assert.True(sonuc.Uyari);
        Assert.Equal(RedSebebi.Yok, sonuc.Sebep);
        Assert.Equal(0.125, sonuc.OranX, 3);
        Assert.Contains("tam ekran", sonuc.Mesaj);
    }

    [Fact]
    public void Gunluk_satiri_karari_ve_pencereleri_yaziyor()
    {
        // Ofiste "secici kabul etmiyor" denildiginde bakilacak satir bu.
        var ortam = Ortam(tiklananSurec: 9999, tiklananBaslik: "Adsiz - Paint",
                          tiklananSurecAdi: "mspaint");
        var satir = KoordinatSecimi.Gunluk(ortam, KoordinatSecimi.Degerlendir(ortam));

        Assert.Contains("RET", satir);
        Assert.Contains("pencere sureci ORKA degil", satir);
        Assert.Contains("mspaint", satir);
        Assert.Contains("ORKA_0001_2026", satir);
    }

    [Fact]
    public void Olcusu_okunamayan_orka_penceresi_reddediliyor()
    {
        var sonuc = KoordinatSecimi.Degerlendir(Ortam(pencere: new PencereOlcusu(0, 0, 0, 0)));

        Assert.False(sonuc.Kabul);
        Assert.Equal(RedSebebi.PencereOlcusuOkunamadi, sonuc.Sebep);
        Assert.Contains("olculeri alinamadi", sonuc.Mesaj);
    }
}
