using System.Text;
using PkfRobot.Ayarlar;

namespace PkfRobot.UnitTests.Arayuz;

/// <summary>
/// Ayarlarin diske yazilmasi, yedeklenmesi ve sifrelerin duz metin durmamasi.
///
/// Ayarlar <c>%AppData%</c> altinda duruyor: publish klasoru her yayinda
/// uzerine yaziliyor ve ofiste test edilmis ayarlar orada dursa her
/// guncellemede silinirdi.
/// </summary>
public class AyarDeposuTests : IDisposable
{
    private readonly string _klasor;

    public AyarDeposuTests()
    {
        _klasor = Path.Combine(Path.GetTempPath(), "pkfrobot-ayar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_klasor);
    }

    public void Dispose()
    {
        try { Directory.Delete(_klasor, recursive: true); } catch (IOException) { }
    }

    private static RobotAyarlari Ornek()
    {
        var ayarlar = new RobotAyarlari
        {
            OrkaExeYolu = @"C:\WinIceberg\OrkaWinIceberg.64.exe",
            LogKlasoru = @"C:\RobotLog",
            IsDosyalariKlasoru = @"C:\RobotGiris",
            FirmaKodu = "0001",
            KullaniciKodu = "pkf03",
            HerZamanUstte = true
        };

        ayarlar.KoordinatYaz("orkaya-aktar.json#0", "Sol panel - Banka Ekstresi", 0.081, 0.32);
        ayarlar.KoordinatYaz("orkaya-aktar.json#1", "Dosya sec dugmesi", 0.5, 0.12);
        return ayarlar;
    }

    [Fact]
    public void Ayarlar_kaydedilip_okunuyor()
    {
        var depo = new AyarDeposu(_klasor);
        depo.Yaz(Ornek());

        var okunan = depo.Oku();

        Assert.Equal("0001", okunan.FirmaKodu);
        Assert.Equal("pkf03", okunan.KullaniciKodu);
        Assert.True(okunan.HerZamanUstte);
        Assert.Equal(2, okunan.Koordinatlar.Count);
        Assert.Equal(0.081, okunan.Koordinat("orkaya-aktar.json#0")!.X, 3);
    }

    [Fact]
    public void Ayni_koordinat_iki_kez_birikmiyor()
    {
        var ayarlar = Ornek();
        ayarlar.KoordinatYaz("orkaya-aktar.json#0", "Sol panel - Banka Ekstresi", 0.2, 0.4);

        Assert.Equal(2, ayarlar.Koordinatlar.Count);
        Assert.Equal(0.2, ayarlar.Koordinat("orkaya-aktar.json#0")!.X, 3);
    }

    [Fact]
    public void Bozuk_ayar_dosyasi_arayuzu_kilitlemiyor()
    {
        // Bozuk bir JSON yuzunden ayarlarin duzeltilebilecegi tek ekrani
        // acilmaz yapmak, sorunu buyutmek olurdu.
        var depo = new AyarDeposu(_klasor);
        File.WriteAllText(depo.Dosya, "{ bu json degil");

        var okunan = depo.Oku();

        Assert.Empty(okunan.Koordinatlar);
        Assert.Equal(string.Empty, okunan.FirmaKodu);
    }

    [Fact]
    public void Ayar_dosyasinda_sifre_yok()
    {
        var depo = new AyarDeposu(_klasor);
        var sifreler = new SifreDeposu(_klasor);

        depo.Yaz(Ornek());
        sifreler.Yaz(new Sifreler { OrkaSifresi = "cok-gizli-sifre", FirmaSifresi = "pkf03-sifre" });

        var metin = File.ReadAllText(depo.Dosya);

        Assert.DoesNotContain("cok-gizli-sifre", metin);
        Assert.DoesNotContain("pkf03-sifre", metin);
    }

    [Fact]
    public void Sifreler_diskte_duz_metin_durmuyor()
    {
        var depo = new SifreDeposu(_klasor);
        depo.Yaz(new Sifreler { OrkaSifresi = "cok-gizli-sifre", FirmaSifresi = "ikinci-sifre" });

        var ham = File.ReadAllBytes(depo.Dosya);
        var metin = Encoding.UTF8.GetString(ham);

        Assert.DoesNotContain("cok-gizli-sifre", metin);
        Assert.DoesNotContain("ikinci-sifre", metin);

        // Ayni kullanicida geri okunabiliyor; baska makinede cozulemez (DPAPI).
        var okunan = depo.Oku();
        Assert.Equal("cok-gizli-sifre", okunan.OrkaSifresi);
        Assert.Equal("ikinci-sifre", okunan.FirmaSifresi);
    }

    [Fact]
    public void Bozuk_sifre_dosyasi_bos_donuyor()
    {
        var depo = new SifreDeposu(_klasor);
        File.WriteAllBytes(depo.Dosya, new byte[] { 1, 2, 3, 4, 5 });

        Assert.True(depo.Oku().BosMu);
    }

    // ---- yedek ----

    [Fact]
    public void Yedek_alinip_geri_yuklenince_ayarlar_ayni()
    {
        var depo = new AyarDeposu(_klasor);
        depo.Yaz(Ornek());

        var yedek = Path.Combine(_klasor, "yedek.json");
        depo.Yedekle(yedek);

        // Makine degisti: ayarlar sifirlandi.
        depo.Yaz(new RobotAyarlari());
        Assert.Empty(depo.Oku().Koordinatlar);

        var geri = depo.GeriYukle(yedek);

        Assert.Equal("0001", geri.FirmaKodu);
        Assert.Equal("pkf03", geri.KullaniciKodu);
        Assert.Equal(@"C:\WinIceberg\OrkaWinIceberg.64.exe", geri.OrkaExeYolu);
        Assert.Equal(2, geri.Koordinatlar.Count);
        Assert.Equal(0.081, geri.Koordinat("orkaya-aktar.json#0")!.X, 3);

        // Diske de yazildi: yeniden acilista kayip olmamali.
        Assert.Equal(2, depo.Oku().Koordinatlar.Count);
    }

    [Fact]
    public void Yedekte_sifre_yok()
    {
        // DPAPI ile sifrelenen deger zaten baska makinede cozulemez; yedege duz
        // metin koymak "makine degistirmek" icin sifreyi bir dosyaya dokmek olurdu.
        var depo = new AyarDeposu(_klasor);
        new SifreDeposu(_klasor).Yaz(new Sifreler { OrkaSifresi = "cok-gizli-sifre" });
        depo.Yaz(Ornek());

        var yedek = Path.Combine(_klasor, "yedek.json");
        depo.Yedekle(yedek);

        Assert.DoesNotContain("cok-gizli-sifre", File.ReadAllText(yedek));
    }

    [Fact]
    public void Elle_kopyalanmis_ayar_dosyasi_da_geri_yuklenebiliyor()
    {
        // Zarf degil ciplak ayarlar.json verilmis: reddetmek kullaniciyi dosyayi
        // elle tasimaya iterdi.
        var kaynak = new AyarDeposu(_klasor);
        kaynak.Yaz(Ornek());

        var kopya = Path.Combine(_klasor, "kopya-ayarlar.json");
        File.Copy(kaynak.Dosya, kopya);

        var hedefKlasor = Path.Combine(_klasor, "hedef");
        var geri = new AyarDeposu(hedefKlasor).GeriYukle(kopya);

        Assert.Equal("0001", geri.FirmaKodu);
        Assert.Equal(2, geri.Koordinatlar.Count);
    }

    [Fact]
    public void Olmayan_yedek_anlasilir_hata_veriyor()
    {
        var depo = new AyarDeposu(_klasor);

        Assert.Throws<FileNotFoundException>(
            () => depo.GeriYukle(Path.Combine(_klasor, "yok.json")));
    }
}

/// <summary>
/// Ayar tanimlari ve yol dogrulama. Arayuz bu listeden uretiliyor; listedeki
/// bir hata butun formda gorunur.
/// </summary>
public class AyarTanimlariTests : IDisposable
{
    private readonly string _klasor;

    public AyarTanimlariTests()
    {
        _klasor = Path.Combine(Path.GetTempPath(), "pkfrobot-tanim-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_klasor);
    }

    public void Dispose()
    {
        try { Directory.Delete(_klasor, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Her_tanim_ayar_nesnesine_yazip_okuyabiliyor()
    {
        // Oku/Yaz ciftinin ayni alani gostermesi arayuzun tek varsayimi;
        // bir tanimda kopyala-yapistir hatasi olsa ayar sessizce kaybolurdu.
        var ayarlar = new RobotAyarlari();

        foreach (var tanim in AyarTanimlari.Tumu)
        {
            tanim.Yaz(ayarlar, "deneme-" + tanim.Anahtar);
            Assert.Equal("deneme-" + tanim.Anahtar, tanim.Oku(ayarlar));
        }
    }

    [Fact]
    public void Bos_yol_ayarlari_varsayilanla_dolduruluyor()
    {
        var ayarlar = AyarTanimlari.VarsayilanlariTamamla(new RobotAyarlari());

        Assert.False(string.IsNullOrWhiteSpace(ayarlar.OrkaExeYolu));
        Assert.False(string.IsNullOrWhiteSpace(ayarlar.LogKlasoru));
        Assert.False(string.IsNullOrWhiteSpace(ayarlar.IsDosyalariKlasoru));
    }

    [Fact]
    public void Girilmis_deger_varsayilanla_ezilmiyor()
    {
        var ayarlar = AyarTanimlari.VarsayilanlariTamamla(
            new RobotAyarlari { OrkaExeYolu = @"D:\Ozel\Orka.exe" });

        Assert.Equal(@"D:\Ozel\Orka.exe", ayarlar.OrkaExeYolu);
    }

    [Fact]
    public void Olmayan_exe_yolu_uyari_veriyor()
    {
        var tanim = AyarTanimlari.Bul(AyarTanimlari.OrkaExeYolu)!;

        var sorun = YolDogrulama.Sorun(tanim, Path.Combine(_klasor, "yok.exe"));

        Assert.NotNull(sorun);
        Assert.Contains("Dosya bulunamadi", sorun);
        Assert.True(YolDogrulama.Engelleyici(tanim, Path.Combine(_klasor, "yok.exe")));
    }

    [Fact]
    public void Var_olan_yol_uyari_vermiyor()
    {
        var exe = Path.Combine(_klasor, "orka.exe");
        File.WriteAllText(exe, string.Empty);

        Assert.Null(YolDogrulama.Sorun(AyarTanimlari.Bul(AyarTanimlari.OrkaExeYolu)!, exe));
        Assert.Null(YolDogrulama.Sorun(AyarTanimlari.Bul(AyarTanimlari.LogKlasoru)!, _klasor));
    }

    [Fact]
    public void Olmayan_klasor_uyarisi_engelleyici_degil()
    {
        // Klasor kaydedince aciliyor; kullaniciyi durdurmanin anlami yok.
        var tanim = AyarTanimlari.Bul(AyarTanimlari.LogKlasoru)!;
        var yok = Path.Combine(_klasor, "henuz-yok");

        Assert.NotNull(YolDogrulama.Sorun(tanim, yok));
        Assert.False(YolDogrulama.Engelleyici(tanim, yok));
    }

    [Fact]
    public void Metin_ayarlari_yol_dogrulamasina_girmiyor()
    {
        Assert.Null(YolDogrulama.Sorun(AyarTanimlari.Bul(AyarTanimlari.FirmaKodu)!, "0001"));
    }
}
