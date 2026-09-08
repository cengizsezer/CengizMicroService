using PkfRobot.Arayuz;
using PkfRobot.Ayarlar;
using PkfRobot.Core;

namespace PkfRobot.UnitTests.Core;

/// <summary>
/// Uctan uca deneme "Firma Sifresini Giriniz." popup'inda 40 sn timeout ile
/// kirildi: popup ekranda duruyordu ama arama onu goremedi, hata mesajindaki
/// "Ekrandakiler (14)" listesinde de yoktu. Sebep, taramanin UIA'nin
/// <c>GetDesktop().FindAllChildren()</c> cagrisina dayanmasiydi -- ORKA'nin
/// pencereleri gizli bir kabuk pencere tarafindan SAHIPLENILDIGI icin (owner != 0)
/// UIA onlari masaustunun cocugu saymiyor.
///
/// Ekran test edilemez ama <b>eleme kurali</b> edilebilir: asagidaki testler
/// pencere listesini elle kurup "hangisi bulunur, hangisi elenir" sorusunu
/// sabitliyor. Sahiplik ve surec ELEME OLCUTU DEGIL; gorunurluk ve baslik.
/// </summary>
public class PencereBekleyiciTests
{
    private const int OrkaPid = 4242;

    private static int _sayac = 0x10;

    private static UstSeviyePencere P(
        string baslik,
        bool gorunur = true,
        int pid = OrkaPid,
        int sahip = 0,
        int genislik = 1920,
        int yukseklik = 1080)
        => new(new IntPtr(_sayac++),
               pid,
               baslik,
               new PencereOlcusu(0, 0, genislik, yukseklik),
               gorunur,
               new IntPtr(sahip));

    /// <summary>Ofisteki ekranin kucuk bir kopyasi: sahipli ORKA pencereleri + digerleri.</summary>
    private static List<UstSeviyePencere> Ekran() => new()
    {
        P("Program Manager", pid: 900),
        P("ORKA_0001_2026", sahip: 0x4001),
        P("Firma Sifresini Giriniz.", sahip: 0x4001, genislik: 420, yukseklik: 180),
        P("Belgeler - Dosya Gezgini", pid: 1500),
        P("", pid: 1200),
    };

    [Fact]
    public void Sahipli_popup_bulunuyor()
    {
        // Kirilmanin kendisi: owner != 0 diye elenirse popup bir daha bulunamaz.
        var bulunan = PencereBekleyici.Esles(Ekran(), "Giriniz");

        Assert.NotNull(bulunan);
        Assert.Equal("Firma Sifresini Giriniz.", bulunan!.Baslik);
        Assert.NotEqual(IntPtr.Zero, bulunan.Sahip);
    }

    [Fact]
    public void Baska_surece_ait_pencere_de_bulunuyor()
    {
        // Pid filtresi OLMAMALI: Excel dosya secim diyalogu ORKA surecine ait
        // olmayabilir ve "Transfer Edilecek Excel" penceresi kacardi.
        var ekran = Ekran();
        ekran.Add(P("Transfer Edilecek Excel Dosyasini Seciniz", pid: 7777));

        var bulunan = PencereBekleyici.Esles(ekran, "Transfer Edilecek Excel");

        Assert.NotNull(bulunan);
        Assert.Equal(7777, bulunan!.Pid);
    }

    [Fact]
    public void Gorunmeyen_pencere_bulunmuyor()
    {
        var ekran = new List<UstSeviyePencere> { P("Hesap Plani", gorunur: false) };

        Assert.Null(PencereBekleyici.Esles(ekran, "Hesap Plan"));
    }

    [Fact]
    public void Baslik_parcasi_buyuk_kucuk_harf_duyarsiz_araniyor()
    {
        Assert.NotNull(PencereBekleyici.Esles(Ekran(), "firma sifresini"));
    }

    [Fact]
    public void Coklu_aday_sirayla_deneniyor()
    {
        // "A|B" ayraci korunmali: ORKA surumden surume baslik degistiriyor.
        var ekran = new List<UstSeviyePencere> { P("Transfer Islemleri") };

        var bulunan = PencereBekleyici.Esles(ekran, "Veri Transferi|Transfer Islemleri");

        Assert.NotNull(bulunan);
        Assert.Equal("Transfer Islemleri", bulunan!.Baslik);
    }

    [Fact]
    public void Ilk_aday_varsa_ikinciye_gecilmiyor()
    {
        var ekran = new List<UstSeviyePencere> { P("Transfer Islemleri"), P("Veri Transferi") };

        var bulunan = PencereBekleyici.Esles(ekran, "Veri Transferi|Transfer Islemleri");

        Assert.Equal("Veri Transferi", bulunan!.Baslik);
    }

    [Fact]
    public void Eslesme_yoksa_null_donuyor()
    {
        Assert.Null(PencereBekleyici.Esles(Ekran(), "Boyle Bir Pencere Yok"));
    }

    [Fact]
    public void Ekrandakiler_listesi_aramanin_bakdigi_kumeden_geliyor()
    {
        // Timeout mesajindaki liste ile aramanin taradigi kume ayrisirsa hata
        // ayiklayan kisi yanilir: ofiste tam bu oldu, popup listede yoktu.
        var basliklar = PencereBekleyici.Basliklar(Ekran());

        Assert.Contains("Firma Sifresini Giriniz.", basliklar);
        Assert.Contains("ORKA_0001_2026", basliklar);
        Assert.DoesNotContain("", basliklar);           // basliksiz pencere
    }

    [Fact]
    public void Ekrandakiler_listesinde_gorunmeyen_pencere_yok()
    {
        var ekran = new List<UstSeviyePencere> { P("Gizli Kabuk", gorunur: false), P("ORKA_0001_2026") };

        Assert.Equal(new[] { "ORKA_0001_2026" }, PencereBekleyici.Basliklar(ekran));
    }

    [Fact]
    public void Aday_ayraci_bos_parcalari_atiyor()
    {
        Assert.Equal(new[] { "A", "B" }, PencereBekleyici.Adaylar("A||B"));
        Assert.Equal(new[] { "A", "B" }, PencereBekleyici.Adaylar(" A | B "));
        Assert.Empty(PencereBekleyici.Adaylar(""));
    }

    [Fact]
    public void Tarama_pencereleri_filtresiz_donduruyor()
    {
        // Gercek tarama: EnumWindows patlamamali ve dondurdugu her kaydin
        // tutamaci gecerli olmali. Ekranda hangi pencerelerin oldugu makineye
        // gore degistigi icin sayiya bakilmiyor.
        var hepsi = OrkaPenceresi.TumUstSeviyePencereler();

        Assert.All(hepsi, p => Assert.NotEqual(IntPtr.Zero, p.Tutamac));
    }
}
