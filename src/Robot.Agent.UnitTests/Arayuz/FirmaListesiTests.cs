using PkfRobot.Ayarlar;

namespace PkfRobot.UnitTests.Arayuz;

/// <summary>
/// Firma listesi artik kullanicinin: ekleyip cikarabiliyor.
///
/// Once liste koda gomuluydu (0001 ve 0336) ve ucuncu bir firma icin kod
/// degistirmek gerekiyordu. Liste ayarlara tasindi; kurallar arayuzden ayri
/// durdugu icin WinForms olmadan test edilebiliyor.
///
/// Iki kural da sessizce yanlis calisabilecek turden: <b>cift kod</b>, hangi
/// sifrenin hangi firmaya ait oldugunu belirsiz yapar; <b>bos liste</b>, hicbir
/// gorevin calistirilamamasi demektir.
/// </summary>
public class FirmaListesiTests
{
    private static List<OrkaFirmasi> Liste(params string[] kodlar)
        => kodlar.Select(k => new OrkaFirmasi(k, "Firma " + k)).ToList();

    [Fact]
    public void Yeni_kod_eklenebiliyor()
    {
        Assert.Null(FirmaListesi.EkleHatasi(Liste("0001", "0336"), "0500"));
    }

    [Fact]
    public void Ayni_kod_iki_kere_eklenemiyor()
    {
        var hata = FirmaListesi.EkleHatasi(Liste("0001"), "0001");

        Assert.NotNull(hata);
        Assert.Contains("zaten listede", hata);
    }

    [Fact]
    public void Cift_kod_kontrolu_buyuk_kucuk_harf_ve_bosluk_farkina_takilmiyor()
    {
        Assert.NotNull(FirmaListesi.EkleHatasi(Liste("0001a"), " 0001A "));
    }

    [Fact]
    public void Bos_kod_eklenemiyor()
    {
        var hata = FirmaListesi.EkleHatasi(Liste("0001"), "   ");

        Assert.NotNull(hata);
        Assert.Contains("bos olamaz", hata);
    }

    [Fact]
    public void Son_firma_cikarilamiyor()
    {
        // Liste bos kalirsa secili firma da kalmaz ve START hicbir sey yapamaz.
        var hata = FirmaListesi.CikarHatasi(Liste("0001"), "0001");

        Assert.NotNull(hata);
        Assert.Contains("Son firma", hata);
    }

    [Fact]
    public void Listede_olmayan_firma_cikarilamiyor()
    {
        Assert.NotNull(FirmaListesi.CikarHatasi(Liste("0001", "0336"), "9999"));
    }

    [Fact]
    public void Iki_firmadan_biri_cikarilabiliyor()
    {
        Assert.Null(FirmaListesi.CikarHatasi(Liste("0001", "0336"), "0336"));
    }

    // ---- ayarlarla birlikte -------------------------------------------------

    [Fact]
    public void Ilk_acilista_liste_ofisteki_iki_firmayla_geliyor()
    {
        var ayar = new CalistirmaAyari().VarsayilanlariTamamla();

        Assert.Equal(new[] { Firmalar.PkfAdayKodu, Firmalar.SmmmKodu },
                     ayar.Firmalar.Select(f => f.Kod));
    }

    [Fact]
    public void Kullanicinin_listesi_varsa_varsayilanlar_geri_gelmiyor()
    {
        // Eski surumun ayarlar.json'u firma listesi ICERMIYOR; tamamlama yalnizca
        // BOS listeyi dolduruyor, kullanicinin listesine dokunmuyor.
        var ayar = new CalistirmaAyari { Firmalar = Liste("0500"), FirmaKodu = "0500" };

        ayar.VarsayilanlariTamamla();

        Assert.Equal(new[] { "0500" }, ayar.Firmalar.Select(f => f.Kod));
        Assert.Equal("0500", ayar.FirmaKodu);
    }

    [Fact]
    public void Secili_firma_listeden_cikarilmissa_ilk_firmaya_donuluyor()
    {
        // Aksi halde gorev ARTIK VAR OLMAYAN bir koda aktarim denerdi.
        var ayar = new CalistirmaAyari { Firmalar = Liste("0500", "0600"), FirmaKodu = "0336" };

        ayar.VarsayilanlariTamamla();

        Assert.Equal("0500", ayar.FirmaKodu);
    }

    [Fact]
    public void Firma_kullanicinin_listesinde_araniyor()
    {
        var liste = Liste("0500", "0600");

        Assert.Equal("0600", Firmalar.Bul(liste, "0600")!.Kod);
        Assert.Equal("0500", Firmalar.Bul(liste, "yok")!.Kod);
        Assert.Null(Firmalar.Bul(new List<OrkaFirmasi>(), "0500"));
    }
}
