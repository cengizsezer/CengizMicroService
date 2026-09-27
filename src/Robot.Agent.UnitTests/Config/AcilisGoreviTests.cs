using PkfRobot.Config;

namespace PkfRobot.UnitTests.Config;

/// <summary>
/// Gercek gorev dosyalari uzerinde tek soru: acilis.json calistirildiginda
/// robot TRANSFERLER'e giriyor mu?
///
/// <b>Neden test:</b> eskiden acilis gorevi 01-orka-ac-firma-sec.json'du ve o
/// dosya modul ekraninda BITIYOR, modul gezinmesini (02) hic calistirmiyordu.
/// Dongu govdesi bu yuzden yanlis ekranda basliyordu. Bu bosluk dosyalarin
/// ICERIGINDE kapandi, kodda degil -- kirilirsa ancak boyle bir test yakalar.
/// </summary>
public class AcilisGoreviTests
{
    private static string GorevlerKlasoru()
    {
        // Test dll'i bin/Debug/netX altinda; depo kokune cikip gorevleri buluyoruz.
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

    [Fact]
    public void Acilis_hem_giris_hem_modul_gezinmesini_iceriyor()
    {
        var adimlar = Acilis();

        // Giris zinciri: ORKA baslatiliyor.
        Assert.Contains(adimlar, a => a.Tip.Equals("OrkaBaslat", StringComparison.OrdinalIgnoreCase));

        // Modul gezinmesi: SAG ve ASAGI oklari. 01 tek basina bunlari ICERMIYORDU.
        Assert.Contains(adimlar, a => a.Tip.Equals("Tus", StringComparison.OrdinalIgnoreCase) &&
                                      a.Deger.Equals("RIGHT", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(adimlar, a => a.Tip.Equals("Tus", StringComparison.OrdinalIgnoreCase) &&
                                      a.Deger.Equals("DOWN", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Acilis_Veri_Transferi_ekraninda_bitiyor()
    {
        // Dongu govdesi (tek-dosya-aktar.json) ilk adiminda TcxDBTreeList ariyor;
        // acilis o ekrani birakmazsa her tur ilk adimda durur.
        var son = Acilis().Last(a => a.Tip.Equals("AltPencereDogrula", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("TcxDBTreeList", son.Deger);
    }

    [Fact]
    public void Acilis_dosyasi_adim_KOPYALAMIYOR()
    {
        // Dosyanin kendisi yalniz referans tutmali: adimlar kopyalanirsa modul
        // gezinmesi iki yerde bakilir hale gelir (koordinat kopyalamanin
        // tek-dosya-aktar.json'da acdigi bakim yukunun aynisi).
        var ham = File.ReadAllText(Path.Combine(GorevlerKlasoru(), "acilis.json"));
        var dosya = System.Text.Json.JsonDocument.Parse(ham, new System.Text.Json.JsonDocumentOptions
        {
            CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        var tipler = dosya.RootElement.GetProperty("Adimlar").EnumerateArray()
                          .Select(a => a.GetProperty("Tip").GetString())
                          .ToList();

        Assert.All(tipler, t => Assert.Equal("AltGorev", t));
    }

    /// <summary>
    /// Ekstre aktaran IKI GIRIS NOKTASI da ayni popup'i beklemeli.
    ///
    /// tek-dosya-aktar.json elle/kuyruk yolu; ajan-aktar.json sunucudan gelen
    /// isin kostugu sarmalayici (acilis + ayni govde). Ajan yolunda basinda
    /// kimse olmadigi icin sessiz kirilma daha pahali: transfer bitmeden hesap
    /// planina gecilirse kodlar henuz dolmamis bir grid'e yazilir.
    ///
    /// <b>Artik ayni adimlari sinamis oluyorlar</b> ve olmasi gereken de bu:
    /// eskiden ikinci dosya (orkaya-aktar.json) govdenin AYRI bir kopyasiydi ve
    /// bu testler iki kopyayi zorla esit tutmaya calisiyordu. Kopya silindi;
    /// test simdi "sarmalayici govdeyi gercekten iceriyor mu" sorusunu da
    /// cevapliyor.
    /// </summary>
    public static TheoryData<string> AktarimGorevleri => new() { "tek-dosya-aktar.json", "ajan-aktar.json" };

    [Theory]
    [MemberData(nameof(AktarimGorevleri))]
    public void Transfer_bitis_popupi_TURKCE_HARFLERIYLE_okunuyor(string dosya)
    {
        // Deger 'Transfer İşlemi Tamamland' -- 'I' (U+0130) ve 's' (U+015F).
        // Dosya BOM'suz UTF-8; File.ReadAllText yanlis kod sayfasiyla okusaydi
        // eslesme sessizce tutmaz ve robot transfer bitmeden devam ederdi.
        var adimlar = Gorev.Yukle(Path.Combine(GorevlerKlasoru(), dosya)).Adimlar;

        var popup = adimlar.Single(a => a.Deger.Contains("Tamamland", StringComparison.Ordinal));

        Assert.Equal("Transfer İşlemi Tamamland", popup.Deger);
        Assert.Equal("AltPencereDogrula", popup.Tip);
        Assert.Equal(120, popup.TimeoutSn);
    }

    [Theory]
    [MemberData(nameof(AktarimGorevleri))]
    public void Transfer_bitisi_HESAP_SECIMINDEN_SONRA_ve_gridden_ONCE_bekleniyor(string dosya)
    {
        // ORKA'nin gercek sirasi (olculdu 06.09.2026): dosya yolu ENTER'i Hesap
        // Plani'ni HEMEN aciyor, transfer ARKA PLANDA suruyor, popup en sonda
        // cikiyor. Bekleme once Hesap Plani'ndan ONCE duruyordu ve 120 sn
        // timeout'a dusuyordu -- ekranda Hesap Plani vardi, popup yoktu.
        //
        // Grid'den ONCE olmasi ayri bir sart: popup acikken tiklarsak tiklama
        // popup'a gider.
        var adimlar = Gorev.Yukle(Path.Combine(GorevlerKlasoru(), dosya)).Adimlar;

        var hesapPlani = adimlar.FindIndex(a => a.Deger.Contains("Hesap Plan", StringComparison.Ordinal));
        var hesapSecildi = adimlar.FindIndex(a => a.Not.Contains("hesap secildi", StringComparison.Ordinal));
        var popup = adimlar.FindIndex(a => a.Deger.Contains("Tamamland", StringComparison.Ordinal));
        var grid = adimlar.FindIndex(a => a.Tip.Equals("GridDoldur", StringComparison.OrdinalIgnoreCase));

        Assert.True(hesapPlani >= 0 && hesapSecildi >= 0 && popup >= 0 && grid >= 0);
        Assert.True(hesapPlani < popup, $"{dosya}: Hesap Plani popup beklemesinden ONCE gelmeli.");
        Assert.True(hesapSecildi < popup, $"{dosya}: popup beklemesi hesap seciminden SONRA olmali.");
        Assert.True(popup < grid, $"{dosya}: popup beklemesi grid doldurmadan ONCE olmali.");
    }

    [Theory]
    [MemberData(nameof(AktarimGorevleri))]
    public void Popup_kapatildiktan_sonra_fazladan_ENTER_yok(string dosya)
    {
        // Yanlis teshise dayanan bir "Hesap Plani'ni acar" ENTER'i eklenmisti;
        // Hesap Plani zaten kendiliginden aciliyor. Fazladan ENTER, popup
        // kapandiktan sonra bilinmeyen bir pencereye giderdi.
        var adimlar = Gorev.Yukle(Path.Combine(GorevlerKlasoru(), dosya)).Adimlar;

        Assert.DoesNotContain(adimlar, a => a.Not.Contains("Hesap Plani'ni acar", StringComparison.Ordinal));
    }

    [Fact]
    public void Gorev_dosyalarinda_sabit_firma_kodu_kalmadi()
    {
        // "ORKA_0001_2026" sabiti 0001 disinda bir firma secilince (0336 SMMM)
        // her zaman patlardi; degisken kullaniliyor.
        //
        // ADIM DEGERLERINE bakiliyor, ham metne degil: dosyalardaki aciklama
        // notlari eski sabiti "boyle YAZILIYDU" diye anmaya devam ediyor ve
        // ham metin aramasi onlari da yakalardi.
        foreach (var dosya in Directory.GetFiles(GorevlerKlasoru(), "*.json"))
            foreach (var adim in Gorev.Yukle(dosya).Adimlar)
                Assert.DoesNotContain("ORKA_0001", adim.Deger);
    }
}
