using FlaUI.Core.WindowsAPI;
using PkfRobot.Core;

namespace PkfRobot.UnitTests.Core;

/// <summary>
/// Iki kirilma birden burada sabitleniyor:
///
/// 1. <b>Tuslar birakilmiyordu.</b> Gonderim FlaUI'nin <c>Keyboard.Press</c>
///    metoduyla yapiliyordu; o YALNIZ key-down gonderir (FlaUI 4.0.0,
///    <c>PressVirtualKeyCode -> SendInput isKeyDown:true</c>). Tus isletim sistemi
///    acisindan basili kaliyor, sonraki basislar hedefe auto-repeat olarak gidiyor.
///
/// 2. <b>Ilk basistan once bekleme yoktu.</b> Bekleme her basistan SONRA
///    konuyordu; adimin ilk tusu, penceresinin daha yeni tamamlanan aktivasyonunun
///    ustune biniyordu -- modul ekraninda ilk SAG'in yutulmasinin sekli tam buydu.
///
/// SendInput'un ekranda ne yaptigi dogrulanamaz; <b>surucuye ne soylendigi</b>
/// dogrulanabilir. Key-up garantisi arayuzun kendisinde: tek gonderim metodu
/// <see cref="IKlavyeSurucusu.TusaBas"/> ve sozlesmesi "bas ve BIRAK".
/// </summary>
[Collection(KlavyeKoleksiyonu.Ad)]
public class KlavyeTests : IDisposable
{
    private readonly SahteKlavye _sahte = new();
    private readonly int _eskiBekleme = Klavye.VarsayilanBeklemeMs;

    public KlavyeTests()
    {
        Klavye.Surucu = _sahte;
        Klavye.VarsayilanBeklemeMs = 150;
    }

    public void Dispose()
    {
        Klavye.SurucuyuSifirla();
        Klavye.VarsayilanBeklemeMs = _eskiBekleme;
    }

    /// <summary>Gonderilen her seyi sirasiyla kaydeden sahte klavye.</summary>
    private sealed class SahteKlavye : IKlavyeSurucusu
    {
        public readonly List<string> Olaylar = new();

        public void TusaBas(VirtualKeyShort tus) => Olaylar.Add($"bas:{tus}");
        public void MetinYaz(string metin) => Olaylar.Add($"yaz:{metin}");
        public void BirlikteBas(IReadOnlyList<VirtualKeyShort> tuslar)
            => Olaylar.Add("birlikte:" + string.Join("+", tuslar));
        public void Bekle(int ms) => Olaylar.Add($"bekle:{ms}");
    }

    [Fact]
    public void Ilk_basistan_ONCE_de_bekleniyor()
    {
        Klavye.Tus("RIGHT", 1);

        Assert.Equal("bekle:150", _sahte.Olaylar[0]);
        Assert.Equal("bas:RIGHT", _sahte.Olaylar[1]);
    }

    [Fact]
    public void Adet_kadar_basiyor_ve_her_basisin_arasinda_bekliyor()
    {
        // Modul gezinmesinin kendisi: SAG x3.
        Klavye.Tus("RIGHT", 3);

        Assert.Equal(new[]
        {
            "bekle:150",
            "bas:RIGHT", "bekle:150",
            "bas:RIGHT", "bekle:150",
            "bas:RIGHT", "bekle:150"
        }, _sahte.Olaylar);
    }

    [Fact]
    public void Tus_gonderimi_tek_yoldan_gidiyor_ve_o_yol_birakmayi_iceriyor()
    {
        // Sozlesme testi: Klavye'nin tus gondermek icin kullanabildigi tek metot
        // TusaBas ve o metot "bas ve BIRAK" olarak tanimli. Yalniz key-down
        // gonderen bir yol eklenirse bu beklenti bozulur.
        Klavye.Tus("DOWN", 2);

        Assert.Equal(2, _sahte.Olaylar.Count(o => o == "bas:DOWN"));
        Assert.DoesNotContain(_sahte.Olaylar, o => o.StartsWith("birlikte:"));
    }

    [Fact]
    public void Adima_ozel_bekleme_hem_oncesinde_hem_arasinda_gecerli()
    {
        Klavye.Tus("ENTER", 1, beklemeMs: 40);

        Assert.Equal(new[] { "bekle:40", "bas:ENTER", "bekle:40" }, _sahte.Olaylar);
    }

    [Fact]
    public void Kisayolda_tek_tus_da_TusaBas_uzerinden_gidiyor()
    {
        // Eskiden bu yol dogrudan key-down gonderiyordu ve ESC hic birakilmiyordu.
        Klavye.Kisayol("ESC");

        // VirtualKeyShort.ESC ile ESCAPE ayni deger; enum adi ESCAPE olarak yaziliyor.
        Assert.Equal(new[] { "bekle:150", "bas:ESCAPE", "bekle:150" }, _sahte.Olaylar);
    }

    [Fact]
    public void Kombinasyon_birlikte_basiliyor()
    {
        Klavye.Kisayol("CTRL+A");

        Assert.Equal(new[] { "bekle:150", "birlikte:CONTROL+KEY_A", "bekle:150" }, _sahte.Olaylar);
    }

    [Fact]
    public void TemizleVeYaz_once_seciyor_sonra_yaziyor()
    {
        Klavye.TemizleVeYaz("0001");

        Assert.Equal(new[]
        {
            "bekle:100", "birlikte:CONTROL+KEY_A", "bekle:100",
            "yaz:0001", "bekle:150"
        }, _sahte.Olaylar);
    }

    // ---- CTRL'suz temizleme (yanlis firma hatasi) ---------------------------

    [Fact]
    public void SecVeSil_END_SHIFT_HOME_DELETE_sirasiyla_temizliyor()
    {
        // Sira onemli: END olmadan SHIFT+HOME yalniz imlecin SOLUNU secer ve
        // imlec ortadaysa metnin sagi kutuda kalir.
        Klavye.SecVeSilVeYaz("0001");

        Assert.Equal(new[]
        {
            "bekle:100", "bas:END",    "bekle:100",
            "bekle:100", "birlikte:SHIFT+HOME", "bekle:100",
            "bekle:100", "bas:DELETE", "bekle:100",
            "yaz:0001", "bekle:150"
        }, _sahte.Olaylar);
    }

    [Fact]
    public void SecVeSil_hicbir_CTRL_tusu_gondermiyor()
    {
        // Hatanin kok sebebi buydu: ORKA'da CTRL kombinasyonlari calismiyor,
        // kutu temizlenmiyor ve yeni kod eskisinin yanina yaziliyordu.
        Klavye.SecVeSilVeYaz("0001");

        Assert.DoesNotContain(_sahte.Olaylar, o => o.Contains("CONTROL", StringComparison.Ordinal));
    }

    [Fact]
    public void SecVeSil_metni_temizlemeden_SONRA_yaziyor()
    {
        // Yazma once gitseydi temizleme yeni degeri silerdi.
        Klavye.SecVeSilVeYaz("0001");

        var silme = _sahte.Olaylar.FindIndex(o => o == "bas:DELETE");
        var yazma = _sahte.Olaylar.FindIndex(o => o == "yaz:0001");

        Assert.True(silme >= 0 && yazma > silme);
    }

    [Fact]
    public void Dagitici_yola_gore_dogru_temizlemeyi_seciyor()
    {
        Klavye.TemizleVeYaz("0001", TemizlemeYolu.SecVeSil);
        Assert.Contains(_sahte.Olaylar, o => o == "bas:END");
        Assert.DoesNotContain(_sahte.Olaylar, o => o.Contains("CONTROL", StringComparison.Ordinal));

        _sahte.Olaylar.Clear();

        Klavye.TemizleVeYaz(@"C:\RobotGiris\ekstre.xlsx", TemizlemeYolu.CtrlA);
        Assert.Contains(_sahte.Olaylar, o => o == "birlikte:CONTROL+KEY_A");
        Assert.DoesNotContain(_sahte.Olaylar, o => o == "bas:END");
    }

    [Fact]
    public void Mevcut_TemizleVeYaz_degismedi()
    {
        // Dosya secim diyalogunda calisan yol; CTRL'suz yolu eklerken bozulmadigi
        // burada sabitleniyor.
        Klavye.TemizleVeYaz(@"C:\RobotGiris\ekstre.xlsx");

        Assert.Equal(new[]
        {
            "bekle:100", "birlikte:CONTROL+KEY_A", "bekle:100",
            @"yaz:C:\RobotGiris\ekstre.xlsx", "bekle:150"
        }, _sahte.Olaylar);
    }

    // ---- 'Temizleme' alaninin cozumu ----------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Temizleme_alani_bos_ise_varsayilan_CtrlA(string? deger)
    {
        // Mevcut butun TemizleYaz adimlarinin davranisi degismemeli.
        Assert.Equal(TemizlemeYolu.CtrlA, Klavye.TemizlemeCoz(deger));
    }

    [Theory]
    [InlineData("SecVeSil")]
    [InlineData("secvesil")]
    [InlineData("  SECVESIL  ")]
    public void Temizleme_alani_SecVeSil_okunuyor(string deger)
    {
        Assert.Equal(TemizlemeYolu.SecVeSil, Klavye.TemizlemeCoz(deger));
    }

    [Fact]
    public void Taninmayan_Temizleme_degeri_HATA_veriyor()
    {
        // Sessizce CTRL+A'ya dusmek, bir yazim hatasinin ayni sessiz kirilmayi
        // (yanlis firmaya girme) geri getirmesi demekti.
        var hata = Assert.Throws<ArgumentException>(() => Klavye.TemizlemeCoz("sec-ve-sil"));

        Assert.Contains("SecVeSil", hata.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Yaz_metni_tek_parca_gonderiyor()
    {
        Klavye.Yaz("pkf03");

        Assert.Equal(new[] { "yaz:pkf03", "bekle:150" }, _sahte.Olaylar);
    }
}
