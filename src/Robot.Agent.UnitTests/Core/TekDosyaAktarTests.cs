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
    public void Aktarim_koordinatlarinin_tek_kopyasi_bu_dosyada()
    {
        // Eskiden ayni oranlar orkaya-aktar.json'da da duruyordu ve bir test
        // ikisini zorla esit tutuyordu. Kopya silindi; kural artik "baska hicbir
        // gorev dosyasinda ayni tiklama noktasi olmayacak".
        //
        // 03-banka-transferi.json bilincli istisna: elle olcum/test dosyasi,
        // zincirin parcasi degil ve "Transfere Basla" oranini kendi icinde
        // tasiyor.
        var beklenen = new[] { "Sol panel", "Sablon satiri", "Transfere Basla", "Grid ilk satir" };
        foreach (var not in beklenen) TiklaBul(Gorevi(), not);   // dosyada TEK olmali

        var klasor = Path.GetDirectoryName(Yol(DosyaAdi))!;

        foreach (var dosya in Directory.GetFiles(klasor, "*.json"))
        {
            var ad = Path.GetFileName(dosya);
            if (ad.Equals(DosyaAdi, StringComparison.OrdinalIgnoreCase)) continue;
            if (ad.StartsWith("03-", StringComparison.OrdinalIgnoreCase)) continue;
            if (ad.StartsWith("04-", StringComparison.OrdinalIgnoreCase)) continue;

            // KoordinatKesfi HAM JSON okuyor, AltGorev acmiyor: sarmalayicilar
            // (acilis.json, ajan-aktar.json) govdeyi REFERANSLA iceriyor ve
            // burada dogru sekilde "kendi Tikla adimi yok" cikiyorlar.
            foreach (var kayit in KoordinatKesfi.KesfetDosya(dosya))
                foreach (var not in beklenen)
                    Assert.DoesNotContain(not, kayit.Aciklama, StringComparison.OrdinalIgnoreCase);
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

        // Acilis zincirinde onay bekleyen adim olmamali: orada duraklamak
        // kuyrugu daha ilk banka baslamadan durdururdu.
        var acilis = Gorev.Yukle(Yol("acilis.json"));
        Assert.DoesNotContain(acilis.Adimlar, a => a.OnayBekle);
    }

    // ---- gozetimsiz (ajan) kosu ---------------------------------------------

    [Fact]
    public void Gozetimsiz_kosuda_onay_bekleyen_adim_atlaniyor()
    {
        // Duraklatmayi Calistir sekmesi yapiyor; ajan yolunda DEVAM dugmesi yok.
        // Bayrak eskiden orada sessizce YOK SAYILIYORDU: robot, kullanici
        // kaydetmeden sekme kapatma tiklamasini yapardi -- ustelik koordinat
        // henuz ofiste olculmemis bir PLACEHOLDER.
        var adim = new Adim { Tip = "Tikla", OnayBekle = true, Not = "Sekme kapatma carpisi" };

        var not = AdimMotoru.GozetimsizAtlamaNotu(adim, gozetimsiz: true);

        Assert.NotNull(not);
        Assert.Contains("gozetimsiz", not, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sekme kapatma", not, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gozetimli_kosuda_onay_bekleyen_adim_atlanmiyor()
    {
        // Arayuz yolu: adim CALISIYOR, yalnizca DEVAM'a kadar bekliyor.
        var adim = new Adim { Tip = "Tikla", OnayBekle = true, Not = "Sekme kapatma carpisi" };

        Assert.Null(AdimMotoru.GozetimsizAtlamaNotu(adim, gozetimsiz: false));
    }

    [Fact]
    public void Gozetimsiz_kosu_onay_bayragi_olmayan_adimi_atlamiyor()
    {
        // Atlama YALNIZ bu bayraga bagli; ajanin geri kalan akisi degismedi.
        var adim = new Adim { Tip = "Tikla", Not = "Sol panel - Banka Ekstresi" };

        Assert.Null(AdimMotoru.GozetimsizAtlamaNotu(adim, gozetimsiz: true));
    }

    [Fact]
    public void Ajan_yolunda_atlanan_tek_adim_sekme_kapatma()
    {
        // Ajan govdeyi bastan sona kosuyor; atlanan adimin aktarimin KENDISINE
        // dokunmadigi burada sabitleniyor. Ikinci bir OnayBekle eklenirse
        // (ornegin grid oncesi bir kontrol) ajan onu da sessizce atlardi.
        var atlananlar = Gorevi().Adimlar
            .Where(a => AdimMotoru.GozetimsizAtlamaNotu(a, gozetimsiz: true) is not null)
            .ToList();

        var atlanan = Assert.Single(atlananlar);

        Assert.Equal("Tikla", atlanan.Tip);
        Assert.Contains("Sekme kapatma", atlanan.Not, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Tiklamalar(Gorevi()).Last().Not, atlanan.Not);
    }
}
