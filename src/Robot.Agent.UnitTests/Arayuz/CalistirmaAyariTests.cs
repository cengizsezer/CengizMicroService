using PkfRobot.Ayarlar;

namespace PkfRobot.UnitTests.Arayuz;

/// <summary>
/// Calistir sekmesinin kaliciligi: secilen dosya yollari, duzeltilen hesap
/// kodlari ve zamanlama uygulama kapaninca kaybolmamali.
///
/// <b>Neden onemli:</b> publish klasoru her yayinda uzerine yaziliyor. Bu
/// degerler orada dursa ofiste her guncellemede yeniden girilirdi -- ayarlarin
/// %AppData%'da durmasinin gerekcesi bu.
/// </summary>
public class CalistirmaAyariTests : IDisposable
{
    private readonly string _klasor;

    public CalistirmaAyariTests()
    {
        _klasor = Path.Combine(Path.GetTempPath(), "pkfrobot-calistir-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_klasor);
    }

    public void Dispose()
    {
        try { Directory.Delete(_klasor, recursive: true); } catch (IOException) { }
    }

    // ---- varsayilanlar -------------------------------------------------------

    [Fact]
    public void Ilk_acilista_dort_banka_geliyor()
    {
        var ayarlar = AyarTanimlari.VarsayilanlariTamamla(new RobotAyarlari());

        Assert.Equal(new[] { "Vakifbank", "Ziraat", "Akbank", "Is Bankasi" },
                     ayarlar.Calistirma.Bankalar.Select(b => b.Banka));
    }

    [Fact]
    public void Varsayilan_kodlar_0001_icin_dolu_0336_icin_bos()
    {
        // 0336'nin hesap plani bilinmiyor; tahmin kod yazmak yanlis hesaba
        // aktarim riski olurdu.
        var bankalar = CalistirmaAyari.VarsayilanBankalar();

        Assert.Equal("102 1 1 VAK-01", bankalar[0].HesapKodu(Firmalar.PkfAdayKodu));
        Assert.Equal(string.Empty, bankalar[0].HesapKodu(Firmalar.SmmmKodu));
    }

    [Fact]
    public void Varsayilan_zamanlama_appsettings_degerleri()
    {
        // Sifirdan baslamiyoruz: ofiste calisan ayar bu.
        var ayar = new CalistirmaAyari();

        Assert.Equal(700, ayar.AdimBeklemeMs);
        Assert.Equal(150, ayar.TusBeklemeMs);
        Assert.Equal(40, ayar.PencereTimeoutSn);
        Assert.Equal(90, ayar.OrkaAcilisTimeoutSn);
        Assert.Equal(1.0, ayar.HizCarpani, 3);
    }

    [Fact]
    public void Kayitli_satirlar_varsayilanlarla_ezilmiyor()
    {
        var ayarlar = new RobotAyarlari();
        ayarlar.Calistirma.Bankalar.Add(new BankaSatiri { Banka = "Yalniz bu" });

        AyarTanimlari.VarsayilanlariTamamla(ayarlar);

        Assert.Equal(new[] { "Yalniz bu" }, ayarlar.Calistirma.Bankalar.Select(b => b.Banka));
    }

    // ---- kalicilik -----------------------------------------------------------

    [Fact]
    public void Dosya_yollari_hesap_kodlari_ve_sira_diskte_kaliyor()
    {
        var depo = new AyarDeposu(_klasor);
        var ayarlar = AyarTanimlari.VarsayilanlariTamamla(new RobotAyarlari());

        // Kullanicinin yaptigi: sirayi degistir, dosya sec, kod duzelt.
        var ziraat = ayarlar.Calistirma.Bankalar.First(b => b.Banka == "Ziraat");
        ayarlar.Calistirma.Bankalar.Remove(ziraat);
        ayarlar.Calistirma.Bankalar.Insert(0, ziraat);

        ziraat.Secili = true;
        ziraat.DosyaYolu = @"C:\RobotGiris\ziraat-temmuz.xlsx";
        ziraat.HesapKoduYaz(Firmalar.SmmmKodu, "102 9 9 ZIR-77");

        ayarlar.Calistirma.FirmaKodu = Firmalar.SmmmKodu;
        ayarlar.Calistirma.HizCarpani = 1.5;
        ayarlar.Calistirma.PencereTimeoutSn = 75;
        ayarlar.Calistirma.BankaGorevi = "03-banka-transferi.json";

        depo.Yaz(ayarlar);

        var geri = AyarTanimlari.VarsayilanlariTamamla(new AyarDeposu(_klasor).Oku());
        var ilk = geri.Calistirma.Bankalar[0];

        Assert.Equal("Ziraat", ilk.Banka);
        Assert.True(ilk.Secili);
        Assert.Equal(@"C:\RobotGiris\ziraat-temmuz.xlsx", ilk.DosyaYolu);
        Assert.Equal("102 1 1 ZIR-01", ilk.HesapKodu(Firmalar.PkfAdayKodu));
        Assert.Equal("102 9 9 ZIR-77", ilk.HesapKodu(Firmalar.SmmmKodu));

        Assert.Equal(Firmalar.SmmmKodu, geri.Calistirma.FirmaKodu);
        Assert.Equal(1.5, geri.Calistirma.HizCarpani, 3);
        Assert.Equal(75, geri.Calistirma.PencereTimeoutSn);
        Assert.Equal("03-banka-transferi.json", geri.Calistirma.BankaGorevi);
    }

    [Fact]
    public void Calistirma_bolumu_olmayan_eski_ayar_dosyasi_aciliyor()
    {
        // Ofisteki makinede ayarlar.json bu bolumu tasimiyor; yeni surum onu
        // acamazsa kullanici butun ayarlarini kaybederdi.
        File.WriteAllText(Path.Combine(_klasor, AyarDeposu.DosyaAdi),
            """{ "OrkaExeYolu": "C:\\WinIceberg\\OrkaWinIceberg.64.exe", "HerZamanUstte": true }""");

        var ayarlar = AyarTanimlari.VarsayilanlariTamamla(new AyarDeposu(_klasor).Oku());

        Assert.True(ayarlar.HerZamanUstte);
        Assert.Equal(4, ayarlar.Calistirma.Bankalar.Count);
        Assert.Equal(700, ayarlar.Calistirma.AdimBeklemeMs);
    }

    [Fact]
    public void Yedekte_banka_satirlari_da_geliyor()
    {
        var depo = new AyarDeposu(_klasor);
        var ayarlar = AyarTanimlari.VarsayilanlariTamamla(new RobotAyarlari());
        ayarlar.Calistirma.Bankalar[0].DosyaYolu = @"C:\RobotGiris\vakif.xlsx";
        depo.Yaz(ayarlar);

        var yedek = Path.Combine(_klasor, "yedek.json");
        depo.Yedekle(yedek);

        var geri = new AyarDeposu(Path.Combine(_klasor, "ikinci")).GeriYukle(yedek);

        Assert.Equal(@"C:\RobotGiris\vakif.xlsx", geri.Calistirma.Bankalar[0].DosyaYolu);
    }

    // ---- firma bazli sifre ---------------------------------------------------

    [Fact]
    public void Firma_bazli_sifre_ayri_ayri_saklaniyor()
    {
        var depo = new SifreDeposu(_klasor);
        var sifreler = new Sifreler { OrkaSifresi = "orka" };

        sifreler.FirmaSifresiYaz(Firmalar.PkfAdayKodu, "aday-sifre");
        sifreler.FirmaSifresiYaz(Firmalar.SmmmKodu, "smmm-sifre");
        depo.Yaz(sifreler);

        var geri = new SifreDeposu(_klasor).Oku();

        Assert.Equal("orka", geri.OrkaSifresi);
        Assert.Equal("aday-sifre", geri.FirmaSifresiAl(Firmalar.PkfAdayKodu));
        Assert.Equal("smmm-sifre", geri.FirmaSifresiAl(Firmalar.SmmmKodu));
    }

    [Fact]
    public void Firma_sifresi_yoksa_tek_alanlik_eski_degere_dusuluyor()
    {
        // Ajan modu ve konsol gorevleri firma secmiyor; onlarin sifresi hala
        // tek alanda duruyor ve kaybolmamali.
        var sifreler = new Sifreler { FirmaSifresi = "eski" };

        Assert.Equal("eski", sifreler.FirmaSifresiAl(Firmalar.SmmmKodu));
    }

    [Fact]
    public void Firma_kodu_taninmazsa_ilk_firmaya_dusuluyor()
    {
        Assert.Equal(Firmalar.PkfAdayKodu, Firmalar.Bul("9999").Kod);
        Assert.Equal(Firmalar.SmmmKodu, Firmalar.Bul(Firmalar.SmmmKodu).Kod);
    }
}
