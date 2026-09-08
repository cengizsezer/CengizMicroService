using PkfRobot.Core;

namespace PkfRobot.UnitTests.Core;

/// <summary>
/// Liste ile grid'in satir sayisi tutmadiginda ne olur?
///
/// Uc durumun UCU DE adimi durdurmuyor: yarim kalan bir grid gozle
/// tamamlanabilir ama robotun ortada durup ekrani bilinmeyen halde birakmasi
/// sonraki adimlari da bozardi (KAYDET'e zaten kullanici basiyor). Fark log'da:
/// sessizce eksik yazmak "neden 2 satir bos kalmis" sorusunu cevapsiz birakirdi.
/// </summary>
public class GridHizalamaTests
{
    private static IReadOnlyList<GridSatiri> Liste(params string[] kodlar)
        => kodlar.Select((k, i) => new GridSatiri(i + 1, "test", k)).ToList();

    private sealed class Kayit
    {
        public List<string> Uyarilar { get; } = new();
        public List<string> Bilgiler { get; } = new();
    }

    private static (IReadOnlyList<GridSatiri> Sonuc, Kayit Log) Hizala(
        IReadOnlyList<GridSatiri> satirlar, int? hedef)
    {
        var kayit = new Kayit();
        var sonuc = AdimMotoru.Hizala(satirlar, hedef, kayit.Uyarilar.Add, kayit.Bilgiler.Add);
        return (sonuc, kayit);
    }

    [Fact]
    public void Sayi_bilinmiyorsa_liste_bitene_kadar_yaziliyor()
    {
        // ORKA gridi UIA'ya kapali; satir sayisi cogu zaman bilinmiyor.
        var (sonuc, log) = Hizala(Liste("A", "B", "C"), hedef: null);

        Assert.Equal(3, sonuc.Count);
        Assert.Empty(log.Uyarilar);
        Assert.Contains(log.Bilgiler, b => b.Contains("bilinmiyor"));
    }

    [Fact]
    public void Liste_EKSIK_ise_kalan_satirlar_bos_kaliyor_ve_uyari_dusuyor()
    {
        var (sonuc, log) = Hizala(Liste("A", "B"), hedef: 5);

        Assert.Equal(2, sonuc.Count);
        Assert.Contains(log.Uyarilar, u => u.Contains("BOS"));
    }

    [Fact]
    public void Liste_FAZLA_ise_fazlasi_yok_sayiliyor_ve_uyari_dusuyor()
    {
        var (sonuc, log) = Hizala(Liste("A", "B", "C", "D"), hedef: 2);

        Assert.Equal(new[] { "A", "B" }, sonuc.Select(s => s.KarsiHesapKodu));
        Assert.Contains(log.Uyarilar, u => u.Contains("YOK SAYILIYOR"));
    }

    [Fact]
    public void Sayilar_tutuyorsa_uyari_yok()
    {
        var (sonuc, log) = Hizala(Liste("A", "B"), hedef: 2);

        Assert.Equal(2, sonuc.Count);
        Assert.Empty(log.Uyarilar);
    }

    [Fact]
    public void Hedef_sifir_ya_da_negatifse_bilinmiyor_sayiliyor()
    {
        // Ekstre okunamadiginda 0 donebilir; bunu "grid bos" diye yorumlayip
        // hicbir sey yazmamak, elle testi yine imkansiz kilardi.
        var (sonuc, _) = Hizala(Liste("A", "B"), hedef: 0);

        Assert.Equal(2, sonuc.Count);
    }
}
