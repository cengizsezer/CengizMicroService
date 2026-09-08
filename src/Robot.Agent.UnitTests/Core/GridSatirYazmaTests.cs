using FlaUI.Core.WindowsAPI;
using PkfRobot.Config;
using PkfRobot.Core;

namespace PkfRobot.UnitTests.Core;

/// <summary>
/// GridDoldur once hicbir sey yazmiyordu: satir basina yalnizca kod + ENTER
/// gonderiliyordu ve imlec Karsi Hesap Kodu kolonunda DEGILDI (log'da
/// [TUS-TESHIS] satirlarinda TAB hic gorunmuyordu). Konumlanma (LEFT+TAB)
/// eklendi ama HER SATIRDA gonderildi: 06.09.2026 12:33 kosusunda uc satirin
/// ucunde de LEFT x12 + TAB x7 cikti, kodlar birbirinin uzerine yazildi ve
/// ekranda yalnizca sonuncusu ('800 01') kaldi -- o da 3. satirda.
///
/// Dogru dizi: konumlanma DONGUDEN ONCE bir kez, sonra her satir icin
/// yaz + ENTER + DOWN. Grid UIA'ya kapali, imlecin hangi kolonda oldugu
/// okunamiyor; gonderilen tus dizisi ORKA olmadan burada sabitleniyor.
/// </summary>
[Collection(KlavyeKoleksiyonu.Ad)]
public class GridSatirYazmaTests : IDisposable
{
    private readonly SahteKlavye _sahte = new();
    private readonly int _eskiBekleme = Klavye.VarsayilanBeklemeMs;

    public GridSatirYazmaTests()
    {
        Klavye.Surucu = _sahte;
        Klavye.VarsayilanBeklemeMs = 0;
    }

    public void Dispose()
    {
        Klavye.SurucuyuSifirla();
        Klavye.VarsayilanBeklemeMs = _eskiBekleme;
    }

    private sealed class SahteKlavye : IKlavyeSurucusu
    {
        public readonly List<string> Olaylar = new();

        public void TusaBas(VirtualKeyShort tus) => Olaylar.Add($"bas:{tus}");
        public void MetinYaz(string metin) => Olaylar.Add($"yaz:{metin}");
        public void BirlikteBas(IReadOnlyList<VirtualKeyShort> tuslar)
            => Olaylar.Add("birlikte:" + string.Join("+", tuslar));
        public void Bekle(int ms) { }
    }

    private static IReadOnlyList<GridSatiri> Satirlar(params string[] kodlar)
        => kodlar.Select((k, i) => new GridSatiri(i + 1, $"aciklama {i + 1}", k)).ToList();

    [Fact]
    public void Tek_satir_konumlan_yaz_onayla_alt_satira_gec()
    {
        AdimMotoru.GridiYaz(Satirlar("320 01 001"), solaGitAdet: 3, tabAdet: 2);

        Assert.Equal(new[]
        {
            "bas:LEFT", "bas:LEFT", "bas:LEFT",   // 1. kolona don
            "bas:TAB", "bas:TAB",                 // Karsi Hesap Kodu kolonu
            "yaz:320 01 001",
            "bas:ENTER",                          // hucreyi onayla
            "bas:DOWN"                            // sonraki satir
        }, _sahte.Olaylar);
    }

    [Fact]
    public void Konumlanma_YALNIZCA_BIR_KEZ_ve_DONGUDEN_ONCE()
    {
        // 06.09.2026 12:33 kosusunun kanitladigi hata: LEFT+TAB her satirda
        // tekrarlaninca imlec ilk satira geri donuyor ve son kod disindaki her
        // sey eziliyor. Konumlanma dongunun DISINDA kalmali.
        AdimMotoru.GridiYaz(Satirlar("100 01", "600 01", "800 01"),
                            solaGitAdet: 12, tabAdet: 7);

        Assert.Equal(12, _sahte.Olaylar.Count(o => o == "bas:LEFT"));
        Assert.Equal(7, _sahte.Olaylar.Count(o => o == "bas:TAB"));

        // ...ve hepsi ILK yazmadan once gonderilmis olmali.
        var ilkYazma = _sahte.Olaylar.FindIndex(o => o.StartsWith("yaz:", StringComparison.Ordinal));
        var sonKonumlanma = _sahte.Olaylar.FindLastIndex(o => o is "bas:LEFT" or "bas:TAB");
        Assert.True(sonKonumlanma < ilkYazma,
            "LEFT/TAB dongu icinde gonderiliyor: " + string.Join(" ", _sahte.Olaylar));

        // Satir gecisi yalnizca ENTER + DOWN ile oluyor.
        Assert.Equal(new[]
        {
            "bas:LEFT", "bas:LEFT", "bas:LEFT", "bas:LEFT", "bas:LEFT", "bas:LEFT",
            "bas:LEFT", "bas:LEFT", "bas:LEFT", "bas:LEFT", "bas:LEFT", "bas:LEFT",
            "bas:TAB", "bas:TAB", "bas:TAB", "bas:TAB", "bas:TAB", "bas:TAB", "bas:TAB",
            "yaz:100 01", "bas:ENTER", "bas:DOWN",
            "yaz:600 01", "bas:ENTER", "bas:DOWN",
            "yaz:800 01", "bas:ENTER", "bas:DOWN"
        }, _sahte.Olaylar);
    }

    [Fact]
    public void Satir_geri_cagirmalari_her_satirda_sirayla_tetikleniyor()
    {
        // [GRID] satir n/m logu ve ilerleme bildirimi bunlarin uzerinden gidiyor.
        var basliyor = new List<string>();
        var yazildi = new List<string>();

        AdimMotoru.GridiYaz(Satirlar("A", "B"), 12, 7,
            satirBasliyor: (no, toplam) => basliyor.Add($"{no}/{toplam}"),
            satirYazildi: (no, toplam) => yazildi.Add($"{no}/{toplam}"));

        Assert.Equal(new[] { "1/2", "2/2" }, basliyor);
        Assert.Equal(new[] { "1/2", "2/2" }, yazildi);
    }

    [Fact]
    public void Bos_kod_gorulunce_duruyor()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            AdimMotoru.GridiYaz(Satirlar("A", "  "), 12, 7));

        Assert.Contains("Satir 2", ex.Message);

        // Ilk satir yazilmis, ikincisi icin hicbir tus gitmemis olmali.
        Assert.Equal(1, _sahte.Olaylar.Count(o => o.StartsWith("yaz:", StringComparison.Ordinal)));
    }

    [Fact]
    public void CTRL_A_GONDERILMIYOR()
    {
        // TAB ile girilen hucrede metin zaten secili; grid hucresinde duzenleme
        // kipi acik degilken Ctrl+A TUM SATIRLARI secebilir.
        AdimMotoru.GridiYaz(Satirlar("320 01 001"), 12, 7);

        Assert.DoesNotContain(_sahte.Olaylar, o => o.StartsWith("birlikte:", StringComparison.Ordinal));
    }

    [Fact]
    public void Sayilar_ayardan_geliyor()
    {
        AdimMotoru.GrideKonumlan(solaGitAdet: 12, tabAdet: 7);

        Assert.Equal(12, _sahte.Olaylar.Count(o => o == "bas:LEFT"));
        Assert.Equal(7, _sahte.Olaylar.Count(o => o == "bas:TAB"));
    }

    [Fact]
    public void Sifir_verilirse_o_adim_hic_gonderilmiyor()
    {
        // Kolon duzeni degisip gezinme gereksizlesirse JSON'dan 0 yazilabilsin.
        AdimMotoru.GridiYaz(Satirlar("A"), solaGitAdet: 0, tabAdet: 0);

        Assert.Equal(new[] { "yaz:A", "bas:ENTER", "bas:DOWN" }, _sahte.Olaylar);
    }

    [Fact]
    public void Varsayilanlar_12_sol_7_tab()
    {
        // Ofiste olculen degerler; appsettings.json'dan degistirilebilir.
        var ayar = new GridAyar();

        Assert.Equal(12, ayar.SolaGitAdet);
        Assert.Equal(7, ayar.TabAdet);
    }

    [Fact]
    public void Adim_ayari_config_ayarini_eziyor()
    {
        // Kolon duzeni degisirse tek bir gorev dosyasindan duzeltilebilmeli.
        var adim = new Adim { Tip = "GridDoldur", SolaGitAdet = 4, TabAdet = 9 };
        var cfg = new GridAyar();

        Assert.Equal(4, adim.SolaGitAdet ?? cfg.SolaGitAdet);
        Assert.Equal(9, adim.TabAdet ?? cfg.TabAdet);

        var bos = new Adim { Tip = "GridDoldur" };
        Assert.Equal(12, bos.SolaGitAdet ?? cfg.SolaGitAdet);
        Assert.Equal(7, bos.TabAdet ?? cfg.TabAdet);
    }
}
