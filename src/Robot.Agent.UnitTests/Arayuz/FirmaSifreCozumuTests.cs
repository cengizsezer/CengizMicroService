using PkfRobot.Ayarlar;

namespace PkfRobot.UnitTests.Arayuz;

/// <summary>
/// Sifreler ARTIK FIRMA BAZLI.
///
/// Once hem ORKA giris sifresi hem firma sifresi <c>appsettings.Giris</c> icinde
/// TEK alandi. Arayuzdeki Calistir sekmesi firma bazli depoyu okumaya baslamisti
/// ama ajan yolu okumuyordu: sunucudan hangi firma gelirse gelsin ayni sifre
/// deneniyordu. Birden fazla firmayla calisirken bu, ikinci firmaya birincinin
/// sifresiyle girmeye calismak demek -- ve yanlis sifre ORKA'yi kilitliyor.
///
/// Karar tek yerde (<see cref="FirmaSifreCozucu"/>) toplandi; uc yol da (Calistir
/// sekmesi, ajan, konsol) onu cagiriyor. Oncelik: <b>firma bazli kayit &gt; tek
/// alanlik eski kayit &gt; appsettings</b>.
/// </summary>
public class FirmaSifreCozumuTests
{
    private const string Aday = Firmalar.PkfAdayKodu;   // 0001
    private const string Smmm = Firmalar.SmmmKodu;      // 0336

    private static Sifreler Depo(params (string Kod, string Orka, string Firma)[] kayitlar)
    {
        var s = new Sifreler();
        foreach (var (kod, orka, firma) in kayitlar)
        {
            s.OrkaSifresiYaz(kod, orka);
            s.FirmaSifresiYaz(kod, firma);
        }
        return s;
    }

    // ---- firma bazli cozum --------------------------------------------------

    [Fact]
    public void Her_firma_KENDI_sifresini_aliyor()
    {
        // Asil senaryo: iki firma, dort ayri sifre.
        var depo = Depo((Aday, "aday-orka", "aday-firma"),
                        (Smmm, "smmm-orka", "smmm-firma"));

        var aday = FirmaSifreCozucu.Coz(depo, Aday, "yedek-orka", "yedek-firma");
        var smmm = FirmaSifreCozucu.Coz(depo, Smmm, "yedek-orka", "yedek-firma");

        Assert.Equal("aday-orka", aday.Orka.Deger);
        Assert.Equal("aday-firma", aday.Firma.Deger);
        Assert.Equal("smmm-orka", smmm.Orka.Deger);
        Assert.Equal("smmm-firma", smmm.Firma.Deger);
    }

    [Fact]
    public void Firma_bazli_kayit_appsettings_yedegini_EZIYOR()
    {
        var depo = Depo((Aday, "depodaki", "depodaki-firma"));

        var cozum = FirmaSifreCozucu.Coz(depo, Aday, "appsettings", "appsettings-firma");

        Assert.Equal("depodaki", cozum.Orka.Deger);
        Assert.Equal(SifreKaynagi.FirmaKaydi, cozum.Orka.Kaynak);
    }

    [Fact]
    public void Firma_kodu_buyuk_kucuk_harf_farkindan_kacmiyor()
    {
        // Kod elle de girilebiliyor; "0001a" ile "0001A" ayni firma.
        var depo = Depo(("0001a", "orka", "firma"));

        Assert.Equal("orka", FirmaSifreCozucu.Coz(depo, "0001A", null, null).Orka.Deger);
    }

    // ---- yedekler (geriye donuk uyumluluk) ----------------------------------

    [Fact]
    public void Firma_kaydi_yoksa_tek_alanlik_eski_kayda_dusuluyor()
    {
        // Tek firmayla calisan eski kurulum: sifreler.dat'ta yalnizca tek alan var.
        var depo = new Sifreler { OrkaSifresi = "eski-orka", FirmaSifresi = "eski-firma" };

        var cozum = FirmaSifreCozucu.Coz(depo, Smmm, "appsettings", "appsettings-firma");

        Assert.Equal("eski-orka", cozum.Orka.Deger);
        Assert.Equal(SifreKaynagi.EskiTekAlan, cozum.Orka.Kaynak);
        Assert.Equal(SifreKaynagi.EskiTekAlan, cozum.Firma.Kaynak);
    }

    [Fact]
    public void Hicbir_kayit_yoksa_appsettings_kullaniliyor()
    {
        // #4: appsettings.Giris geriye donuk uyumluluk icin duruyor.
        var cozum = FirmaSifreCozucu.Coz(new Sifreler(), Aday, "app-orka", "app-firma");

        Assert.Equal("app-orka", cozum.Orka.Deger);
        Assert.Equal("app-firma", cozum.Firma.Deger);
        Assert.Equal(SifreKaynagi.Appsettings, cozum.Orka.Kaynak);
        Assert.False(cozum.Eksik);
    }

    [Fact]
    public void Depo_hic_yoksa_appsettings_yine_de_calisiyor()
    {
        var cozum = FirmaSifreCozucu.Coz(null, Aday, "app-orka", "app-firma");

        Assert.Equal("app-orka", cozum.Orka.Deger);
        Assert.False(cozum.Eksik);
    }

    // ---- kaynak log'a yaziliyor ---------------------------------------------

    [Fact]
    public void Log_satiri_kaynagi_yaziyor_ama_SIFREYI_YAZMIYOR()
    {
        var depo = Depo((Aday, "gizli-orka", "gizli-firma"));

        var satirlar = FirmaSifreCozucu.Coz(depo, Aday, "app", "app").LogSatirlari;

        Assert.Contains(satirlar, s => s.Contains("firma bazli kayit"));
        Assert.All(satirlar, s => Assert.DoesNotContain("gizli", s));
    }

    [Fact]
    public void Yedege_dusuldugunde_log_bunu_YEDEK_diye_isaretliyor()
    {
        // Sessizce yedege dusmek tehlikeli: robot BASKA bir firmanin sifresiyle
        // ORKA'ya girmeyi dener ve yanlis sifre ORKA'yi kilitler.
        var cozum = FirmaSifreCozucu.Coz(new Sifreler(), Smmm, "app-orka", "app-firma");

        Assert.All(cozum.LogSatirlari, s => Assert.Contains("YEDEK", s));
    }

    // ---- eksik kayit: is BASLAMAMALI ----------------------------------------

    [Fact]
    public void Hicbir_sifre_yoksa_eksik_ve_mesaj_ne_yapilacagini_soyluyor()
    {
        var cozum = FirmaSifreCozucu.Coz(new Sifreler(), Aday, null, null);

        Assert.True(cozum.Eksik);
        Assert.Equal(
            "0001 firmasi icin sifre kaydi yok, PkfRobot arayuzunde Calistir sekmesinden girin.",
            cozum.EksikMesaji.Split(" (Eksik:")[0]);
        Assert.Contains("ORKA giris sifresi", cozum.EksikMesaji);
        Assert.Contains("firma sifresi", cozum.EksikMesaji);
    }

    [Fact]
    public void Tek_sifre_eksikse_de_eksik_sayiliyor()
    {
        // ORKA giris sifresi var ama firma sifresi yok: giris ekrani gecilir,
        // firma acilamaz. Yarim kalan oturum birakmaktansa hic baslamamali.
        var depo = new Sifreler();
        depo.OrkaSifresiYaz(Aday, "orka");

        var cozum = FirmaSifreCozucu.Coz(depo, Aday, null, null);

        Assert.True(cozum.Eksik);
        Assert.Equal(new[] { "firma sifresi" }, cozum.EksikOlanlar);
    }

    // ---- depo davranisi -----------------------------------------------------

    [Fact]
    public void Firma_cikarilinca_sifreleri_de_siliniyor()
    {
        // Artik calisilmayan bir firmanin sifresi diskte durmamali.
        var depo = Depo((Aday, "a", "b"), (Smmm, "c", "d"));

        depo.FirmaKayitlariniSil(Smmm);

        Assert.False(depo.FirmaKaydiVarMi(Smmm));
        Assert.True(depo.FirmaKaydiVarMi(Aday));
    }

    [Fact]
    public void Firma_bazli_okuma_yedege_DUSMUYOR()
    {
        // FirmaSifresiAl yedege duser, FirmaBazli* dusmez: cozucunun "kaynak"
        // ayrimini yapabilmesi buna bagli.
        var depo = new Sifreler { OrkaSifresi = "eski", FirmaSifresi = "eski-firma" };

        Assert.Equal("eski", depo.OrkaSifresiAl(Aday));
        Assert.Null(depo.FirmaBazliOrkaSifresi(Aday));
    }
}
