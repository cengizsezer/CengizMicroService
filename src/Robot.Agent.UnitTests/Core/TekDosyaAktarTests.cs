using PkfRobot.Arayuz;
using PkfRobot.Ayarlar;
using PkfRobot.Config;
using PkfRobot.Core;

namespace PkfRobot.UnitTests.Core;

/// <summary>
/// Dongu govdesi: <c>gorevler/tek-dosya-aktar.json</c>.
///
/// Bu dosya her banka icin BASTAN calisiyor. Icine giris/firma/modul adimlari
/// kacarsa ikinci turda ORKA giris ekranini gostermez ve zincir timeout'a duser;
/// asagidaki testler o siniri dosyanin kendisi uzerinden sabitliyor.
/// </summary>
public class TekDosyaAktarTests
{
    private const string DosyaAdi = "tek-dosya-aktar.json";

    private static string Yol(string ad) => Path.Combine(AppContext.BaseDirectory, "gorevler", ad);

    private static Gorev Gorevi()
    {
        var yol = Yol(DosyaAdi);
        Assert.True(File.Exists(yol), $"Gorev dosyasi publish ciktisinda yok: {yol}");
        return Gorev.Yukle(yol);
    }

    private static List<Adim> Tiklamalar(Gorev gorev)
        => gorev.Adimlar.Where(a => a.Tip.Equals("Tikla", StringComparison.OrdinalIgnoreCase)).ToList();

    private static Adim TiklaBul(Gorev gorev, string notParcasi)
        => Assert.Single(Tiklamalar(gorev),
                         a => a.Not.Contains(notParcasi, StringComparison.OrdinalIgnoreCase));

    // ---- dongu govdesi sinirlari --------------------------------------------

    [Fact]
    public void Giris_firma_ve_modul_adimlari_icermiyor()
    {
        var gorev = Gorevi();

        Assert.DoesNotContain(gorev.Adimlar, a => a.Tip.Equals("OrkaBaslat", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(gorev.Adimlar, a => a.Deger.Contains("Orka SQL", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(gorev.Adimlar, a => a.Deger.Contains("F7", StringComparison.OrdinalIgnoreCase));

        // Giris zincirinin degiskenleri de gecmemeli.
        foreach (var degisken in new[] { "{firmaKodu}", "{sifre}", "{firmaSifre}" })
            Assert.DoesNotContain(gorev.Adimlar, a => a.Deger.Contains(degisken, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Yalniz_dosya_yolu_ve_hesap_kodu_degiskenlerini_kullaniyor()
    {
        var gorev = Gorevi();

        Assert.Contains(gorev.Adimlar, a => a.Deger.Contains("{dosyaYolu}"));
        Assert.Contains(gorev.Adimlar, a => a.Deger.Contains("{hesapKodu}"));
    }

    [Fact]
    public void Kaydet_adimi_yok()
    {
        // Kural pazarlik konusu degil: kullanici gozle kontrol edip kendisi kaydediyor.
        var gorev = Gorevi();

        Assert.DoesNotContain(gorev.Adimlar, a => a.Tip.Equals("OnayGerekir", StringComparison.OrdinalIgnoreCase));

        var tusaBasanlar = gorev.Adimlar.Where(a =>
            !a.Tip.Equals("Log", StringComparison.OrdinalIgnoreCase) &&
            !a.Tip.Equals("EkranGoruntusu", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(tusaBasanlar, a =>
            a.Deger.Contains("ALT+K", StringComparison.OrdinalIgnoreCase) ||
            a.Deger.Contains("Kaydet", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Grid_doldurma_adimi_var()
    {
        Assert.Contains(Gorevi().Adimlar, a => a.Tip.Equals("GridDoldur", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Sablon_satiri_cift_tikla_aciliyor()
    {
        var gorev = Gorevi();

        var ciftTiklananlar = Tiklamalar(gorev).Where(a => a.CiftTik).ToList();

        Assert.Single(ciftTiklananlar);
        Assert.Contains("Sablon satiri", ciftTiklananlar[0].Not, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Koordinatlar_orkaya_aktar_ile_ayni()
    {
        // Dosya oradan kopyalandi; bir koordinat sessizce ayrisirsa dongu ile tek
        // seferlik aktarim farkli yerlere tiklardi.
        var yeni = Gorevi();
        var kaynak = Gorev.Yukle(Yol("orkaya-aktar.json"));

        foreach (var not in new[] { "Sol panel", "Sablon satiri", "Transfere Basla", "Grid ilk satir" })
        {
            var a = TiklaBul(yeni, not);
            var b = TiklaBul(kaynak, not);

            Assert.Equal(b.X, a.X, 5);
            Assert.Equal(b.Y, a.Y, 5);
        }
    }

    // ---- sekme kapatma -------------------------------------------------------

    [Fact]
    public void Son_tiklama_sekme_kapatma_ve_onay_bekliyor()
    {
        var gorev = Gorevi();
        var sekme = Tiklamalar(gorev).Last();

        Assert.Equal(0.95, sekme.X, 5);
        Assert.Equal(0.06, sekme.Y, 5);
        Assert.Contains("Sekme kapatma", sekme.Not, StringComparison.OrdinalIgnoreCase);

        // KAYDET'e kullanici basiyor: bu tiklama DEVAM'dan once calisirsa
        // kullanici kaydedemeden ekran degisir.
        Assert.True(sekme.OnayBekle, "Sekme kapatma adimi onay beklemiyor.");
    }

    [Fact]
    public void Onay_bekleyen_tek_adim_var()
    {
        var gorev = Gorevi();

        var onayBekleyenler = gorev.Adimlar.Where(a => a.OnayBekle).ToList();

        Assert.Single(onayBekleyenler);
    }

    [Fact]
    public void Sekme_kapatma_kalibrasyon_panelinde_gorunuyor()
    {
        // Koordinat kodda degil gorev dosyasinda durdugu icin Kalibrasyon sekmesi
        // onu kendiliginden listeliyor; ofiste olculecek nokta bu.
        var kayitlar = KoordinatKesfi.KesfetDosya(Yol(DosyaAdi));

        Assert.Contains(kayitlar, k => k.Aciklama.Contains("Sekme kapatma", StringComparison.OrdinalIgnoreCase));
        Assert.Equal($"{DosyaAdi}#{kayitlar.Count - 1}", kayitlar[^1].Anahtar);
    }

    [Fact]
    public void Calistir_sekmesinin_varsayilan_banka_gorevi_bu_dosya()
    {
        Assert.Equal(DosyaAdi, new CalistirmaAyari().BankaGorevi);
    }

    // ---- onay duraklatmasi ---------------------------------------------------

    [Fact]
    public void Onay_bekleyen_adim_akisi_duraklatiyor()
    {
        var denetim = new CalismaDenetimi();
        var satirlar = new List<string>();

        GorevKosucusu.OnayGerekiyorsaDuraklat(
            new Adim { Tip = "Tikla", OnayBekle = true, Not = "Sekme kapatma" }, denetim, satirlar.Add);

        Assert.Equal(CalismaHali.Duraklatildi, denetim.Hal);
        Assert.Contains(satirlar, s => s.Contains("KAYDET", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Onay_bayragi_olmayan_adim_duraklatmiyor()
    {
        var denetim = new CalismaDenetimi();

        GorevKosucusu.OnayGerekiyorsaDuraklat(new Adim { Tip = "Tikla" }, denetim, _ => { });

        Assert.Equal(CalismaHali.Calisiyor, denetim.Hal);
    }

    [Fact]
    public void Durdurulmus_akis_onay_yuzunden_yeniden_duraklatilmiyor()
    {
        // DURDUR'dan sonra duraklatmak, kesme istegini yutup gorevi askida birakirdi.
        var denetim = new CalismaDenetimi();
        denetim.Durdur();

        GorevKosucusu.OnayGerekiyorsaDuraklat(
            new Adim { Tip = "Tikla", OnayBekle = true }, denetim, _ => { });

        Assert.Equal(CalismaHali.Durduruldu, denetim.Hal);
    }

    [Fact]
    public void Onay_bekleme_varsayilan_olarak_kapali()
    {
        // Mevcut butun adimlarin davranisi degismemeli.
        Assert.False(new Adim().OnayBekle);

        var eskiGorev = Gorev.Yukle(Yol("orkaya-aktar.json"));
        Assert.DoesNotContain(eskiGorev.Adimlar, a => a.OnayBekle);
    }
}
