using System.Text;
using PkfRobot.Ajan;
using PkfRobot.Core;

namespace PkfRobot.UnitTests.Ajan;

/// <summary>
/// Kimligin diskte nasil durdugu ve anahtarin log'a dusmedigi.
///
/// Testler gecici bir klasorde calisiyor; gercek <c>%AppData%\PkfRobot</c>
/// dosyasina dokunulmuyor.
/// </summary>
public class AjanKimlikDeposuTests : IDisposable
{
    private readonly string _klasor;

    public AjanKimlikDeposuTests()
    {
        _klasor = Path.Combine(Path.GetTempPath(), "pkfrobot-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_klasor);
    }

    public void Dispose()
    {
        try { Directory.Delete(_klasor, recursive: true); } catch { }
    }

    [Fact]
    public void Anahtar_yazilip_geri_okunuyor()
    {
        var depo = new AjanKimlikDeposu(_klasor);

        depo.AnahtarYaz("pkfr_bir_anahtar_0123456789");

        Assert.True(depo.AnahtarVarMi);
        Assert.Equal("pkfr_bir_anahtar_0123456789", depo.AnahtarOku());
    }

    [Fact]
    public void Anahtar_diske_duz_metin_yazilmiyor()
    {
        // Ofisteki makine fiziksel olarak erisilebilir bir yerde: dosyayi acan
        // biri anahtari okuyamamali.
        const string anahtar = "pkfr_gizli_anahtar_0123456789";
        var depo = new AjanKimlikDeposu(_klasor);

        depo.AnahtarYaz(anahtar);

        var bayt = File.ReadAllBytes(depo.AnahtarDosyasi);
        var metin = Encoding.UTF8.GetString(bayt);

        Assert.DoesNotContain(anahtar, metin, StringComparison.Ordinal);
        Assert.DoesNotContain("pkfr_", metin, StringComparison.Ordinal);
        Assert.NotEqual(Encoding.UTF8.GetBytes(anahtar), bayt);
    }

    [Fact]
    public void Anahtar_silinince_yok_sayiliyor()
    {
        var depo = new AjanKimlikDeposu(_klasor);
        depo.AnahtarYaz("pkfr_bir_anahtar_0123456789");

        depo.AnahtarSil();

        Assert.False(depo.AnahtarVarMi);
        Assert.Null(depo.AnahtarOku());
    }

    [Fact]
    public void Bozuk_anahtar_dosyasi_null_donuyor()
    {
        // Baska makineden kopyalanmis ya da bozulmus dosya patlamasin; cagiran
        // taraf yeniden sorsun.
        var depo = new AjanKimlikDeposu(_klasor);
        File.WriteAllBytes(depo.AnahtarDosyasi, new byte[] { 1, 2, 3, 4, 5 });

        Assert.Null(depo.AnahtarOku());
    }

    [Fact]
    public void MakineId_iki_calistirmada_ayni_kaliyor()
    {
        // Her acilista yeni kimlik uretilseydi sunucudaki listede ayni makineden
        // hayalet kayitlar birikirdi.
        var ilk = new AjanKimlikDeposu(_klasor).MakineId();
        var ikinci = new AjanKimlikDeposu(_klasor).MakineId();

        Assert.Equal(ilk, ikinci);
        Assert.StartsWith(Environment.MachineName + "-", ilk);
        Assert.True(File.Exists(Path.Combine(_klasor, AjanKimlikDeposu.MakineDosyaAdi)));
    }

    [Fact]
    public void Farkli_klasorlerde_MakineId_farkli()
    {
        var digerKlasor = Path.Combine(Path.GetTempPath(), "pkfrobot-test-" + Guid.NewGuid().ToString("N"));

        try
        {
            var a = new AjanKimlikDeposu(_klasor).MakineId();
            var b = new AjanKimlikDeposu(digerKlasor).MakineId();

            Assert.NotEqual(a, b);
        }
        finally
        {
            try { Directory.Delete(digerKlasor, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Bos_anahtar_kabul_edilmiyor()
    {
        var depo = new AjanKimlikDeposu(_klasor);

        Assert.Throws<ArgumentException>(() => depo.AnahtarYaz("   "));
    }
}

/// <summary>Log'a sir dusmemesi.</summary>
public class AjanLogMaskesiTests
{
    [Fact]
    public void Ajan_anahtari_maskeleniyor()
    {
        var metin = "Token alinamadi, anahtar: pkfr_AbC123_deneme-anahtari";

        var sonuc = AjanLogMaskesi.Maskele(metin);

        Assert.DoesNotContain("AbC123", sonuc);
        Assert.Contains("pkfr_***", sonuc);
    }

    [Fact]
    public void Jwt_maskeleniyor()
    {
        var metin = "Baglanti basligi: eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJhamFuLTcifQ.imzaBolumu";

        var sonuc = AjanLogMaskesi.Maskele(metin);

        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", sonuc);
        Assert.Contains("***token***", sonuc);
    }

    [Fact]
    public void Sira_disi_metne_dokunulmuyor()
    {
        const string metin = "Hub'a baglanildi: BANKA-PC (BANKA-PC-abc123), surum 1.0.0.";

        Assert.Equal(metin, AjanLogMaskesi.Maskele(metin));
    }

    [Fact]
    public void Bos_metin_patlamiyor()
    {
        Assert.Equal(string.Empty, AjanLogMaskesi.Maskele(null));
        Assert.Equal(string.Empty, AjanLogMaskesi.Maskele(""));
    }
}

/// <summary>Gorev adimlarindaki maskeleme kurali.</summary>
public class HassasTests
{
    [Theory]
    [InlineData("sifre")]
    [InlineData("firmaSifre")]
    [InlineData("{firmaSifre}")]
    [InlineData("Ajan anahtari")]
    [InlineData("agent.dat")]
    [InlineData("Bearer token")]
    [InlineData("SIFRE")]
    public void Hassas_sozcuk_gecen_alan_maskeleniyor(string metin)
        => Assert.True(Hassas.Iceriyor(metin));

    [Theory]
    [InlineData("firmaKodu")]
    [InlineData("Veri Transferi")]
    [InlineData("")]
    [InlineData(null)]
    public void Sira_disi_alan_maskelenmiyor(string? metin)
        => Assert.False(Hassas.Iceriyor(metin));
}

/// <summary>Gunluk log dosyasi ve eskilerin temizlenmesi.</summary>
public class AjanDosyaLogTests : IDisposable
{
    private readonly string _klasor;

    public AjanDosyaLogTests()
    {
        _klasor = Path.Combine(Path.GetTempPath(), "pkfrobot-log-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_klasor, recursive: true); } catch { }
    }

    [Fact]
    public void Gunluk_dosyaya_yaziyor_ve_anahtari_maskeliyor()
    {
        using (var log = new AjanDosyaLog(_klasor))
        {
            log.Bilgi("Hub'a baglanildi.");
            log.Hata("Anahtar reddedildi: pkfr_gizli_anahtar_0123456789");
        }

        var dosya = Path.Combine(_klasor, $"ajan-{DateTime.Now:yyyy-MM-dd}.log");
        var icerik = File.ReadAllText(dosya);

        Assert.Contains("Hub'a baglanildi.", icerik);
        Assert.DoesNotContain("gizli_anahtar", icerik);
        Assert.Contains("pkfr_***", icerik);
    }

    [Fact]
    public void Saklama_suresini_asan_dosyalar_siliniyor()
    {
        Directory.CreateDirectory(_klasor);

        var eski = Path.Combine(_klasor, "ajan-2020-01-01.log");
        File.WriteAllText(eski, "eski kayit");
        File.SetLastWriteTime(eski, DateTime.Now.AddDays(-30));

        var yakin = Path.Combine(_klasor, "ajan-2020-01-02.log");
        File.WriteAllText(yakin, "yakin kayit");
        File.SetLastWriteTime(yakin, DateTime.Now.AddDays(-3));

        using var log = new AjanDosyaLog(_klasor, saklamaGun: 14);

        Assert.False(File.Exists(eski));
        Assert.True(File.Exists(yakin));
    }
}

/// <summary>Geri cekilme aralilari.</summary>
public class GeriCekilmeTests
{
    [Fact]
    public void Araliklar_5_10_30_60_diye_ilerliyor_ve_60_ta_kaliyor()
    {
        var g = new GeriCekilme();

        Assert.Equal(TimeSpan.FromSeconds(5), g.Sonraki());
        Assert.Equal(TimeSpan.FromSeconds(10), g.Sonraki());
        Assert.Equal(TimeSpan.FromSeconds(30), g.Sonraki());
        Assert.Equal(TimeSpan.FromSeconds(60), g.Sonraki());
        Assert.Equal(TimeSpan.FromSeconds(60), g.Sonraki());
        Assert.Equal(TimeSpan.FromSeconds(60), g.Sonraki());
    }

    [Fact]
    public void Sifirlaninca_bastan_basliyor()
    {
        var g = new GeriCekilme();
        g.Sonraki();
        g.Sonraki();

        g.Sifirla();

        Assert.Equal(TimeSpan.FromSeconds(5), g.Sonraki());
    }
}

/// <summary>Hub adresinin semasi ve surum okuma.</summary>
public class HubAdresiTests
{
    [Theory]
    [InlineData("wss://www.dijitalmasraf.com/agenthub", "https://www.dijitalmasraf.com/agenthub")]
    [InlineData("ws://localhost:5004/agenthub", "http://localhost:5004/agenthub")]
    [InlineData("https://www.dijitalmasraf.com/agenthub", "https://www.dijitalmasraf.com/agenthub")]
    [InlineData("http://localhost:5004/agenthub", "http://localhost:5004/agenthub")]
    public void Ws_semasi_http_ye_cevriliyor(string girdi, string beklenen)
    {
        // Ayar dosyasina wss:// yazmak dogal; SignalR once HTTP ile negotiate
        // yaptigi icin sema cevriliyor.
        Assert.Equal(beklenen, SignalRHubBaglantisi.HttpAdresi(girdi));
    }

    [Fact]
    public void Surum_derlemeden_okunuyor()
    {
        var surum = SurumBilgisi.Oku();

        Assert.Matches(@"^\d+\.\d+\.\d+$", surum);
        Assert.NotEqual("0.0.0", surum);
    }
}
