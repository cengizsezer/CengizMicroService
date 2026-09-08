using PkfRobot.Ayarlar;
using PkfRobot.Core;

namespace PkfRobot.UnitTests.Core;

/// <summary>
/// Coklu banka dongusunun kurallari: hangi satir islenir, hangi sirayla, bir sey
/// ters gidince ne olur.
///
/// Isin kendisi (ORKA'yi surmek) disaridan veriliyor, bu yuzden dongu ORKA
/// olmadan sinanabiliyor.
/// </summary>
public class BankaKuyruguTests
{
    private const string Aday = Firmalar.PkfAdayKodu;
    private const string Smmm = Firmalar.SmmmKodu;

    private static BankaSatiri Satir(string banka, string kod, string dosya, bool secili = true)
    {
        var satir = new BankaSatiri { Banka = banka, Secili = secili, DosyaYolu = dosya };
        satir.HesapKoduYaz(Aday, kod);
        return satir;
    }

    // ---- secim ---------------------------------------------------------------

    [Fact]
    public void Isaretli_ve_dosyasi_olan_satirlar_sirayla_isleniyor()
    {
        var satirlar = new List<BankaSatiri>
        {
            Satir("Vakifbank", "102 1 1 VAK-01", @"C:\x\vakif.xlsx"),
            Satir("Ziraat", "102 1 1 ZIR-01", @"C:\x\ziraat.xlsx"),
            Satir("Akbank", "102 1 1 AKB-01", string.Empty),            // dosya yok
            Satir("Is Bankasi", "102 1 1 ISB-02", @"C:\x\is.xlsx", secili: false)
        };

        var isler = BankaKuyrugu.Isler(satirlar, Aday);

        Assert.Equal(new[] { "Vakifbank", "Ziraat" }, isler.Select(i => i.Banka));
        Assert.Equal(new[] { 1, 2 }, isler.Select(i => i.Sira));
        Assert.Equal("102 1 1 VAK-01", isler[0].HesapKodu);
        Assert.Equal(@"C:\x\ziraat.xlsx", isler[1].DosyaYolu);
    }

    [Fact]
    public void Hesap_kodu_secili_firmaya_gore_geliyor()
    {
        // Ayni banka satiri, iki firmada iki ayri hesap kodu tasiyor.
        var satir = Satir("Vakifbank", "102 1 1 VAK-01", @"C:\x\vakif.xlsx");
        satir.HesapKoduYaz(Smmm, "102 9 9 VAK-77");

        Assert.Equal("102 1 1 VAK-01", BankaKuyrugu.Isler(new[] { satir }, Aday)[0].HesapKodu);
        Assert.Equal("102 9 9 VAK-77", BankaKuyrugu.Isler(new[] { satir }, Smmm)[0].HesapKodu);
    }

    [Fact]
    public void Smmm_firmasinda_kod_bos_ise_eksik_sayiliyor()
    {
        // 0336 icin kodlar bilerek bos geliyor; kullanici doldurmadan
        // baslanirsa ORKA'da bos arama yapilirdi.
        var satir = Satir("Vakifbank", "102 1 1 VAK-01", @"C:\x\vakif.xlsx");

        var eksikler = BankaKuyrugu.Eksikler(new[] { satir }, Smmm, _ => true);

        Assert.Contains(eksikler, e => e.Contains("hesap kodu bos", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Isaretli_ama_dosyasiz_satir_sessizce_atlanmiyor()
    {
        var satirlar = new[] { Satir("Akbank", "102 1 1 AKB-01", string.Empty) };

        var eksikler = BankaKuyrugu.Eksikler(satirlar, Aday, _ => true);

        Assert.Contains(eksikler, e => e.Contains("ekstre dosyasi secilmemis"));
    }

    [Fact]
    public void Diskte_olmayan_dosya_baslamadan_yakalaniyor()
    {
        var satirlar = new[] { Satir("Ziraat", "102 1 1 ZIR-01", @"C:\yok\ziraat.xlsx") };

        var eksikler = BankaKuyrugu.Eksikler(satirlar, Aday, _ => false);

        Assert.Contains(eksikler, e => e.Contains("dosya bulunamadi", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Hicbir_banka_isaretli_degilse_eksik_bildiriliyor()
    {
        var satirlar = new[] { Satir("Ziraat", "102 1 1 ZIR-01", @"C:\x\z.xlsx", secili: false) };

        Assert.Contains(BankaKuyrugu.Eksikler(satirlar, Aday, _ => true),
                        e => e.Contains("Hicbir banka isaretli"));
    }

    [Fact]
    public void Her_sey_tamamsa_eksik_yok()
    {
        var satirlar = new[] { Satir("Vakifbank", "102 1 1 VAK-01", @"C:\x\vakif.xlsx") };

        Assert.Empty(BankaKuyrugu.Eksikler(satirlar, Aday, _ => true));
    }

    // ---- dongu ---------------------------------------------------------------

    private static List<BankaIsi> UcIs() => new()
    {
        new BankaIsi(1, "Vakifbank", "102 1 1 VAK-01", @"C:\x\vakif.xlsx"),
        new BankaIsi(2, "Ziraat", "102 1 1 ZIR-01", @"C:\x\ziraat.xlsx"),
        new BankaIsi(3, "Akbank", "102 1 1 AKB-01", @"C:\x\akbank.xlsx")
    };

    [Fact]
    public void Butun_isler_sirayla_calisiyor()
    {
        var calisan = new List<string>();
        var calistirici = new KuyrukCalistirici(new CalismaDenetimi(), i => calisan.Add(i.Banka));

        var ozet = calistirici.Calistir(UcIs());

        Assert.Equal(new[] { "Vakifbank", "Ziraat", "Akbank" }, calisan);
        Assert.Equal(3, ozet.Biten);
        Assert.Equal(0, ozet.Hatali);
        Assert.False(ozet.Durduruldu);
    }

    [Fact]
    public void Acilis_gorevi_bir_kez_calisiyor()
    {
        // Her banka icin tekrarlansaydi ORKA ikinci turda giris ekranini
        // gostermez ve zincir timeout'a duserdi.
        var acilis = 0;
        var calistirici = new KuyrukCalistirici(new CalismaDenetimi(), _ => { },
                                                acilis: () => acilis++);

        calistirici.Calistir(UcIs());

        Assert.Equal(1, acilis);
    }

    [Fact]
    public void Acilis_patlarsa_hicbir_banka_islenmiyor()
    {
        var calisan = 0;
        var calistirici = new KuyrukCalistirici(new CalismaDenetimi(), _ => calisan++,
            acilis: () => throw new InvalidOperationException("ORKA acilmadi"));

        var ozet = calistirici.Calistir(UcIs());

        Assert.Equal(0, calisan);
        Assert.Equal(3, ozet.Atlanan);
        Assert.All(ozet.Sonuclar, s => Assert.Equal(IsDurumu.Atlandi, s.Durum));
    }

    [Fact]
    public void Bir_banka_hata_verirse_kuyruk_duruyor()
    {
        // ORKA'nin hangi ekranda kaldigi bilinmiyor; sonraki bankaya devam etmek
        // "ne yaptigini kimsenin bilmedigi" bir aktarim olurdu.
        var calisan = new List<string>();
        var calistirici = new KuyrukCalistirici(new CalismaDenetimi(), i =>
        {
            calisan.Add(i.Banka);
            if (i.Banka == "Ziraat") throw new TimeoutException("Hesap Plani gelmedi");
        });

        var ozet = calistirici.Calistir(UcIs());

        Assert.Equal(new[] { "Vakifbank", "Ziraat" }, calisan);
        Assert.Equal(1, ozet.Biten);
        Assert.Equal(1, ozet.Hatali);
        Assert.Equal(1, ozet.Atlanan);
        Assert.Contains(ozet.Sonuclar, s => s.Durum == IsDurumu.Hata &&
                                            s.Mesaj!.Contains("Hesap Plani gelmedi"));
    }

    [Fact]
    public void Durdurulunca_kalan_bankalar_atlaniyor()
    {
        var denetim = new CalismaDenetimi();
        var calisan = new List<string>();

        var calistirici = new KuyrukCalistirici(denetim, i =>
        {
            calisan.Add(i.Banka);
            if (i.Banka == "Vakifbank") denetim.Durdur();
        });

        var ozet = calistirici.Calistir(UcIs());

        Assert.Equal(new[] { "Vakifbank" }, calisan);
        Assert.True(ozet.Durduruldu);
        Assert.Equal(1, ozet.Biten);
        Assert.Equal(2, ozet.Atlanan);
    }

    [Fact]
    public void Durum_degisimleri_ekrana_bildiriliyor()
    {
        var bildirimler = new List<(string Banka, IsDurumu Durum)>();

        var calistirici = new KuyrukCalistirici(new CalismaDenetimi(), _ => { },
            durumDegisti: (isim, durum, _) => bildirimler.Add((isim.Banka, durum)));

        calistirici.Calistir(UcIs().Take(1).ToList());

        Assert.Equal(new[]
        {
            ("Vakifbank", IsDurumu.Calisiyor),
            ("Vakifbank", IsDurumu.Bitti)
        }, bildirimler);
    }

    [Fact]
    public void Bos_kuyruk_patlamiyor()
    {
        var ozet = new KuyrukCalistirici(new CalismaDenetimi(), _ => { })
            .Calistir(Array.Empty<BankaIsi>());

        Assert.Empty(ozet.Sonuclar);
        Assert.Equal(0, ozet.Biten);
    }
}
