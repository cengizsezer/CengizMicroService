using System.Text.Json;
using PkfRobot.Ayarlar;

namespace PkfRobot.UnitTests.Arayuz;

/// <summary>
/// Koordinat listesi gorev JSON'larindan turetiliyor, elle yazilmiyor -- ve
/// kalibrasyon oradan geri gorev dosyalarina yaziliyor.
/// </summary>
public class KoordinatKesfiTests : IDisposable
{
    private readonly string _klasor;

    public KoordinatKesfiTests()
    {
        _klasor = Path.Combine(Path.GetTempPath(), "pkfrobot-kesif-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_klasor);
    }

    public void Dispose()
    {
        try { Directory.Delete(_klasor, recursive: true); } catch (IOException) { }
    }

    private string GorevYaz(string ad, string icerik)
    {
        var yol = Path.Combine(_klasor, ad);
        File.WriteAllText(yol, icerik);
        return yol;
    }

    private const string IkiTikla = """
    {
      "Ad": "Ornek Gorev",
      "// KULLANIM": [ "Bu satir bir JSON anahtari, gercek yorum degil." ],
      "Adimlar": [
        { "Tip": "Log", "Not": "basla" },
        { "Tip": "Tikla", "X": 0.125, "Y": 0.3, "Not": "Sol panel - Banka Ekstresi" },
        { "Tip": "Bekle", "Sayi": 1000 },
        { "Tip": "Tikla", "X": 0.5, "Y": 0.12, "Not": "Dosya sec dugmesi", "Deger": "Transfer" }
      ]
    }
    """;

    [Fact]
    public void Tikla_adimlari_gorev_dosyasindan_turetiliyor()
    {
        GorevYaz("ornek.json", IkiTikla);

        var kayitlar = KoordinatKesfi.Kesfet(_klasor);

        Assert.Equal(2, kayitlar.Count);

        Assert.Equal("ornek.json#0", kayitlar[0].Anahtar);
        Assert.Equal("Ornek Gorev", kayitlar[0].GorevAdi);
        Assert.Equal("Sol panel - Banka Ekstresi", kayitlar[0].Aciklama);
        Assert.Equal(0.125, kayitlar[0].X, 3);
        Assert.Equal(0.3, kayitlar[0].Y, 3);
        Assert.Equal(string.Empty, kayitlar[0].HedefPencere);

        Assert.Equal("ornek.json#1", kayitlar[1].Anahtar);
        Assert.Equal("Transfer", kayitlar[1].HedefPencere);
    }

    [Fact]
    public void Tikla_disindaki_adimlar_listeye_girmiyor()
    {
        GorevYaz("ornek.json", IkiTikla);

        var kayitlar = KoordinatKesfi.Kesfet(_klasor);

        Assert.All(kayitlar, k => Assert.NotEqual(0, k.X + k.Y));
        Assert.DoesNotContain(kayitlar, k => k.Aciklama == "basla");
    }

    [Fact]
    public void Bozuk_dosya_butun_listeyi_dusurmuyor()
    {
        // Arayuz acilmali: bozuk bir gorev dosyasi yuzunden kullanicinin
        // duzeltme yapabilecegi tek ekrani kapatmak dogru olmaz.
        GorevYaz("bozuk.json", "{ bu json degil");
        GorevYaz("ornek.json", IkiTikla);

        var kayitlar = KoordinatKesfi.Kesfet(_klasor);

        Assert.Equal(2, kayitlar.Count);
    }

    [Fact]
    public void Aciklamasiz_adim_icin_okunabilir_etiket_uretiliyor()
    {
        GorevYaz("adsiz.json", """
        { "Ad": "Adsiz", "Adimlar": [ { "Tip": "Tikla", "X": 0.1, "Y": 0.2 } ] }
        """);

        var kayit = Assert.Single(KoordinatKesfi.Kesfet(_klasor));

        Assert.Contains("adsiz.json", kayit.Etiket);
        Assert.Contains("aciklama yok", kayit.Etiket);
    }

    // ---- gorev dosyasina geri yazma ----

    [Fact]
    public void Kayitli_olcum_gorev_dosyasina_yaziliyor()
    {
        var yol = GorevYaz("ornek.json", IkiTikla);

        var rapor = KalibrasyonUygulama.Uygula(_klasor, new[]
        {
            new KoordinatAyari
            {
                Anahtar = "ornek.json#0",
                Not = "Sol panel - Banka Ekstresi",
                X = 0.081,
                Y = 0.32
            }
        });

        Assert.Equal(1, rapor.Uygulanan);
        Assert.False(rapor.SorunVar);

        var yeniden = KoordinatKesfi.KesfetDosya(yol);
        Assert.Equal(0.081, yeniden[0].X, 3);
        Assert.Equal(0.32, yeniden[0].Y, 3);

        // Diger adim ve JSON anahtar-yorumlari yerinde kalmali.
        Assert.Equal(0.5, yeniden[1].X, 3);
        Assert.Contains("// KULLANIM", File.ReadAllText(yol));
    }

    [Fact]
    public void Ayni_deger_yeniden_yazilmiyor()
    {
        // Her acilista butun gorev dosyalarinin tarihini degistirmek, "neyi ne
        // zaman elledim" sorusunu cevapsiz birakirdi.
        var yol = GorevYaz("ornek.json", IkiTikla);
        var oncekiIcerik = File.ReadAllText(yol);

        var rapor = KalibrasyonUygulama.Uygula(_klasor, new[]
        {
            new KoordinatAyari { Anahtar = "ornek.json#0", Not = "Sol panel - Banka Ekstresi", X = 0.125, Y = 0.3 }
        });

        Assert.Equal(0, rapor.Uygulanan);
        Assert.Equal(1, rapor.Ayni);
        Assert.Equal(oncekiIcerik, File.ReadAllText(yol));
    }

    [Fact]
    public void Adim_aciklamasi_degistiyse_olcum_uygulanmiyor()
    {
        // Gorev akisi degistiginde eski bir orani yeni bir adima sessizce
        // yazmak, robotun yanlis yere tiklamasinin en sinsi yolu.
        var yol = GorevYaz("ornek.json", IkiTikla);

        var rapor = KalibrasyonUygulama.Uygula(_klasor, new[]
        {
            new KoordinatAyari { Anahtar = "ornek.json#0", Not = "Eski adim adi", X = 0.081, Y = 0.32 }
        });

        Assert.Equal(0, rapor.Uygulanan);
        Assert.True(rapor.SorunVar);
        Assert.Equal(UygulamaDurumu.NotUyusmuyor, Assert.Single(rapor.Satirlar).Durum);

        Assert.Equal(0.125, KoordinatKesfi.KesfetDosya(yol)[0].X, 3);
    }

    [Fact]
    public void Olmayan_adim_rapor_ediliyor_hata_atmiyor()
    {
        GorevYaz("ornek.json", IkiTikla);

        var rapor = KalibrasyonUygulama.Uygula(_klasor, new[]
        {
            new KoordinatAyari { Anahtar = "ornek.json#7", Not = "yok", X = 0.1, Y = 0.1 },
            new KoordinatAyari { Anahtar = "silinmis.json#0", Not = "yok", X = 0.1, Y = 0.1 }
        });

        Assert.Equal(0, rapor.Uygulanan);
        Assert.All(rapor.Satirlar, s => Assert.Equal(UygulamaDurumu.AdimYok, s.Durum));
    }

    [Fact]
    public void Yazilan_oran_uc_haneye_yuvarlaniyor()
    {
        // Ham double yazmak dosyaya 0.30000000000000004 gibi degerler dusururdu.
        var yol = GorevYaz("ornek.json", IkiTikla);

        KalibrasyonUygulama.Uygula(_klasor, new[]
        {
            new KoordinatAyari
            {
                Anahtar = "ornek.json#0",
                Not = "Sol panel - Banka Ekstresi",
                X = 240 / 1913.0,   // 0.1254573...
                Y = 0.1 + 0.2       // 0.30000000000000004
            }
        });

        var metin = File.ReadAllText(yol);
        Assert.Contains("0.125", metin);
        Assert.DoesNotContain("0.30000000000000004", metin);

        // Dosya hala gecerli JSON.
        using var belge = JsonDocument.Parse(metin);
        Assert.Equal("Ornek Gorev", belge.RootElement.GetProperty("Ad").GetString());
    }

    [Fact]
    public void Araya_adim_eklenmesi_kalibrasyonu_bozmuyor()
    {
        // Anahtar adim indeksine degil Tikla SIRASINA bagli: goreve bir Bekle
        // eklemek kalibrasyonu dusurmemeli.
        var yol = GorevYaz("ornek.json", """
        {
          "Ad": "Ornek Gorev",
          "Adimlar": [
            { "Tip": "Log", "Not": "basla" },
            { "Tip": "EkranGoruntusu", "Deger": "yeni-adim" },
            { "Tip": "Bekle", "Sayi": 500 },
            { "Tip": "Tikla", "X": 0.125, "Y": 0.3, "Not": "Sol panel - Banka Ekstresi" }
          ]
        }
        """);

        var rapor = KalibrasyonUygulama.Uygula(_klasor, new[]
        {
            new KoordinatAyari { Anahtar = "ornek.json#0", Not = "Sol panel - Banka Ekstresi", X = 0.081, Y = 0.32 }
        });

        Assert.Equal(1, rapor.Uygulanan);
        Assert.Equal(0.081, KoordinatKesfi.KesfetDosya(yol)[0].X, 3);
    }
}
