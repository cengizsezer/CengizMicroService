using FlaUI.Core.WindowsAPI;
using PkfRobot.Ajan;
using PkfRobot.Config;
using PkfRobot.Core;

namespace PkfRobot.UnitTests.Core;

/// <summary>
/// 08.09.2026 kosusu: robot 0001 yerine <b>1187</b> firmasina girdi
/// (ORKA_1187_2026 "Pkf Risk Yonetimi..."). Kok sebep F7 firma arama kutusunun
/// OTOMATIK TAMAMLAMA alani olmasi: icinde onceki deger duruyor, <c>TemizleYaz</c>
/// onu CTRL+A ile secmeye calisiyor ama <b>ORKA'da CTRL kombinasyonlari
/// calismiyor</b>. Secim olmayinca yeni kod eskisinin UZERINE degil YANINA
/// yaziliyor ve tamamlama baska bir firmayi buluyor.
///
/// Hata SESSIZ: sonraki adimlarin hepsi "bir firma acildi" diyor. Bu yuzden
/// hem tus sirasi hem gorev dosyasindaki secim burada sabitleniyor.
/// </summary>
[Collection(KlavyeKoleksiyonu.Ad)]
public class FirmaKoduYazmaTests : IDisposable
{
    private readonly SahteKlavye _sahte = new();
    private readonly int _eskiBekleme = Klavye.VarsayilanBeklemeMs;

    public FirmaKoduYazmaTests()
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

    // ---- gorev dosyalari -----------------------------------------------------

    private static string GorevlerKlasoru()
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);

        while (dizin != null)
        {
            var aday = Path.Combine(dizin.FullName, "src", "Robot.Agent", "gorevler");
            if (Directory.Exists(aday)) return aday;
            dizin = dizin.Parent;
        }

        throw new DirectoryNotFoundException("gorevler klasoru bulunamadi.");
    }

    private static List<Adim> Acilis()
        => Gorev.Yukle(Path.Combine(GorevlerKlasoru(), "acilis.json")).Adimlar;

    private static Adim FirmaKoduAdimi(IEnumerable<Adim> adimlar)
        => Assert.Single(adimlar, a => a.Tip.Equals("TemizleYaz", StringComparison.OrdinalIgnoreCase) &&
                                       a.Deger.Contains("{firmaKodu}", StringComparison.Ordinal));

    [Fact]
    public void Firma_kodu_adimi_CTRLsuz_temizliyor()
    {
        var adim = FirmaKoduAdimi(Acilis());

        Assert.Equal(TemizlemeYolu.SecVeSil, Klavye.TemizlemeCoz(adim.Temizleme));
    }

    [Fact]
    public void Ajanin_kostugu_akista_da_CTRLsuz()
    {
        // Acilis hem kuyrukta hem ajan-aktar.json icinde kosuyor; duzeltmenin
        // iki yolda da gecerli oldugu buradan gorulur.
        var adim = FirmaKoduAdimi(Gorev.Yukle(
            Path.Combine(GorevlerKlasoru(), OrkayaAktarCalistirici.GorevDosyasi)).Adimlar);

        Assert.Equal(TemizlemeYolu.SecVeSil, Klavye.TemizlemeCoz(adim.Temizleme));
    }

    [Fact]
    public void Dosya_yolu_ve_hesap_kodu_adimlari_CTRL_A_ile_KALIYOR()
    {
        // Onlar Windows'un dosya secim diyalogunda ve orada CTRL+A calisiyor.
        // Duzeltmeyi butun TemizleYaz adimlarina yaymak, sinanmis yolu bozmak
        // olurdu; sinir burada duruyor.
        var govde = Gorev.Yukle(Path.Combine(GorevlerKlasoru(), "tek-dosya-aktar.json")).Adimlar;

        var digerleri = govde
            .Where(a => a.Tip.Equals("TemizleYaz", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(digerleri);
        Assert.All(digerleri, a => Assert.Equal(TemizlemeYolu.CtrlA, Klavye.TemizlemeCoz(a.Temizleme)));
    }

    [Fact]
    public void Grid_doldurma_adimina_dokunulmadi()
    {
        // Grid hucresinde END'in ne yaptigi OLCULMEDI; oraya yayilmasi ayri bir
        // karar. GridDoldur kendi tus dizisini kullaniyor ve Temizleme alani
        // tasimiyor.
        var govde = Gorev.Yukle(Path.Combine(GorevlerKlasoru(), "tek-dosya-aktar.json")).Adimlar;

        var grid = Assert.Single(govde, a => a.Tip.Equals("GridDoldur", StringComparison.OrdinalIgnoreCase));

        Assert.Null(grid.Temizleme);
    }

    // ---- gonderilen tuslar ---------------------------------------------------

    [Fact]
    public void Kutu_yazmadan_ONCE_gercekten_bosaltiliyor()
    {
        // Kosudaki akisin aynisi: kutuda "1187" duruyor, robot "0001" yazacak.
        var adim = FirmaKoduAdimi(Acilis());

        Klavye.TemizleVeYaz("0001", Klavye.TemizlemeCoz(adim.Temizleme));

        var secim = _sahte.Olaylar.FindIndex(o => o == "birlikte:SHIFT+HOME");
        var silme = _sahte.Olaylar.FindIndex(o => o == "bas:DELETE");
        var yazma = _sahte.Olaylar.FindIndex(o => o == "yaz:0001");

        Assert.True(secim >= 0, "Kutu secilmedi.");
        Assert.True(silme > secim, "Secim silinmedi.");
        Assert.True(yazma > silme, "Kod, kutu bosaltilmadan yazildi.");
    }

    [Fact]
    public void Firma_kodu_yazilirken_CALISMAYAN_kisayol_gonderilmiyor()
    {
        // Hatanin kendisi: gonderilen CTRL+A ORKA'ya hic ulasmiyordu ve robot
        // "temizledim" sanip uzerine yaziyordu.
        var adim = FirmaKoduAdimi(Acilis());

        Klavye.TemizleVeYaz("0001", Klavye.TemizlemeCoz(adim.Temizleme));

        Assert.DoesNotContain(_sahte.Olaylar, o => o.Contains("CONTROL", StringComparison.Ordinal));
        Assert.DoesNotContain(_sahte.Olaylar, o => o.Contains("ALT", StringComparison.Ordinal));
    }

    [Fact]
    public void Yanlis_firma_senaryosu_eski_yolla_geri_gelirdi()
    {
        // Karsilastirma testi: ayni adim CtrlA yoluyla kosulsaydi kutuya
        // gonderilen TEK sey CTRL+A olurdu -- ORKA'da calismayan kombinasyon.
        // Kutuyu bosaltan hicbir tus yok; "1187" yerinde kalir ve "0001"
        // yanina yazilir.
        Klavye.TemizleVeYaz("0001", TemizlemeYolu.CtrlA);

        Assert.Contains(_sahte.Olaylar, o => o == "birlikte:CONTROL+KEY_A");
        Assert.DoesNotContain(_sahte.Olaylar, o => o is "bas:DELETE" or "bas:END");
    }

    // ---- ikinci savunma ------------------------------------------------------

    [Fact]
    public void Firma_dogrulamasi_SECILI_firmanin_koduna_bakiyor()
    {
        // Temizleme yine tutmazsa hata sessiz kalmamali: baslik dogrulamasi
        // {firmaKodu} tasiyor, yani 1187 acilirsa gorev orada durur.
        // Sabit "ORKA_" yazsaydi 1187 de tutar ve robot devam ederdi.
        var dogrulamalar = Acilis()
            .Where(a => a.Tip.Equals("Dogrula", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(dogrulamalar);
        Assert.All(dogrulamalar, a => Assert.Contains("{firmaKodu}", a.Deger, StringComparison.Ordinal));
    }
}
