using PkfRobot.Ayarlar;
using PkfRobot.Core;

namespace PkfRobot.UnitTests.Core;

/// <summary>
/// Elle girilen karsi hesap listesi: <c>GridDoldur</c> zincirin ORKA'ya gercekten
/// veri yazan tek adimi ama listesi sunucudan geliyordu; elle calistirmada liste
/// bos oldugu icin adim uyari yazip ATLANIYOR ve hic denenmiyordu.
/// </summary>
public class KarsiHesapListesiTests
{
    [Fact]
    public void Virgulle_ayrilmis_liste_okunuyor()
    {
        var kodlar = KarsiHesapListesi.Coz("320 01 001, 320 01 002, 329 01 001");

        Assert.Equal(new[] { "320 01 001", "320 01 002", "329 01 001" }, kodlar);
    }

    [Fact]
    public void Satir_sonuyla_ayrilmis_liste_okunuyor()
    {
        // 10 kod tek satira sigmiyor; cok satirli giris asil kullanim.
        var kodlar = KarsiHesapListesi.Coz("320 01 001\r\n320 01 002\n329 01 001");

        Assert.Equal(new[] { "320 01 001", "320 01 002", "329 01 001" }, kodlar);
    }

    [Fact]
    public void Kodun_ICINDEKI_bosluklar_korunuyor()
    {
        // ORKA hesap kodlari bosluklu yaziliyor; bosluklari temizlemek kodu bozardi.
        Assert.Equal("320 01 001", Assert.Single(KarsiHesapListesi.Coz("   320 01 001   ")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",,\n , \r\n")]
    public void Bos_girdi_bos_liste(string? metin)
    {
        // Bos = "elle liste yok" demek; hata degil, sunucudan gelen liste kullanilir.
        Assert.Empty(KarsiHesapListesi.Coz(metin));
    }

    [Fact]
    public void Satirlar_sira_numarasi_veriyor_ve_kaynagi_isaretliyor()
    {
        var satirlar = KarsiHesapListesi.Satirlar("A, B");

        Assert.Equal(new[] { 1, 2 }, satirlar.Select(s => s.SiraNo));
        Assert.Equal(new[] { "A", "B" }, satirlar.Select(s => s.KarsiHesapKodu));

        // ORKA'ya yazilan bir degerin nereden geldigi log'dan okunabilmeli.
        Assert.All(satirlar, s => Assert.Contains("elle", s.Aciklama));
    }

    // ---- ayar deposu ---------------------------------------------------------

    [Fact]
    public void Liste_FIRMA_BASINA_tutuluyor()
    {
        // Hesap plani firmadan firmaya degisiyor: 0001 icin girilen kodlar
        // firma 0336 secilince sessizce onun grid'ine yazilmamali.
        var satir = new BankaSatiri();

        satir.KarsiHesaplarYaz("0001", "320 01 001");
        satir.KarsiHesaplarYaz("0336", "320 99 001");

        Assert.Equal("320 01 001", satir.KarsiHesaplar("0001"));
        Assert.Equal("320 99 001", satir.KarsiHesaplar("0336"));
        Assert.Equal(string.Empty, satir.KarsiHesaplar("9999"));
    }

    [Fact]
    public void Ham_metin_oldugu_gibi_saklaniyor()
    {
        // Kullanicinin yazdigi bicim (satir sonlari, sira) aynen geri gelmeli.
        var satir = new BankaSatiri();
        const string metin = "320 01 001\r\n320 01 002";

        satir.KarsiHesaplarYaz("0001", metin);

        Assert.Equal(metin, satir.KarsiHesaplar("0001"));
    }

    [Fact]
    public void Kuyruk_isi_elle_listeyi_tasiyor()
    {
        var satir = new BankaSatiri { Banka = "Vakifbank", Secili = true, DosyaYolu = @"c:\x.xlsx" };
        satir.KarsiHesaplarYaz("0001", "320 01 001, 320 01 002");

        var isler = BankaKuyrugu.Isler(new[] { satir }, "0001");

        Assert.Equal("320 01 001, 320 01 002", Assert.Single(isler).KarsiHesaplar);
    }

    [Fact]
    public void Elle_liste_girilmemisse_is_bos_geliyor()
    {
        // Eski davranis korunuyor: alan bos ise sunucudan gelen liste kullanilir.
        var satir = new BankaSatiri { Banka = "Ziraat", Secili = true, DosyaYolu = @"c:\y.xlsx" };

        Assert.Equal(string.Empty, Assert.Single(BankaKuyrugu.Isler(new[] { satir }, "0001")).KarsiHesaplar);
    }
}
