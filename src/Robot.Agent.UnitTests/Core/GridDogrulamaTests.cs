using PkfRobot.Core;

namespace PkfRobot.UnitTests.Core;

/// <summary>
/// Grid dogrulamasinin KARAR veren yani: goruntuden okunan satirlarla yazilmasi
/// beklenen kod listesinin karsilastirilmasi.
///
/// <b>Neden burada test edilebiliyor:</b> karsilastirma saf -- ekrana, aga ve
/// modele dokunmuyor. Modele sorulan tek sey "ekranda ne yaziyor"; "dogru mu"
/// karari bu fonksiyonda, string esitligiyle veriliyor.
/// </summary>
public class GridDogrulamaTests
{
    private static GridSatiri B(int no, string kod) => new(no, "ORNEK ODEME", kod);
    private static OkunanGridSatiri O(int no, string kod) => new(no, "ORNEK ODEME", kod);

    [Fact]
    public void Hepsi_tutuyorsa_tek_satir_ozet()
    {
        var sonuc = GridDogrulama.Karsilastir(
            new[] { B(1, "320 A01"), B(2, "770 01"), B(3, "102 1 1 01") },
            new[] { O(1, "320 A01"), O(2, "770 01"), O(3, "102 1 1 01") });

        Assert.True(sonuc.TamamTutuyor);
        Assert.Equal(3, sonuc.Eslesen);
        Assert.Empty(sonuc.Farklar);

        var satirlar = GridDogrulama.Satirlar(sonuc);
        Assert.Single(satirlar);
        Assert.Equal("Dogrulama: 3/3 satir eslesti.", satirlar[0]);
    }

    [Fact]
    public void Uyusmayan_satir_sira_no_beklenen_ve_okunanla_raporlaniyor()
    {
        var sonuc = GridDogrulama.Karsilastir(
            new[] { B(1, "320 A01"), B(2, "770 01"), B(3, "600 05") },
            new[] { O(1, "320 A01"), O(2, "770 02"), O(3, "600 05") });

        Assert.False(sonuc.TamamTutuyor);
        Assert.Equal(2, sonuc.Eslesen);

        var fark = Assert.Single(sonuc.Farklar);
        Assert.Equal(2, fark.SiraNo);
        Assert.Equal("770 01", fark.Beklenen);
        Assert.Equal("770 02", fark.Okunan);

        var metin = string.Join("\n", GridDogrulama.Satirlar(sonuc));
        Assert.Contains("DOGRULAMA UYARISI", metin);
        Assert.Contains("satir 2: beklenen '770 01', okunan '770 02'", metin);
    }

    [Fact]
    public void Sira_no_degil_SIRA_esliyor()
    {
        // Okuyucunun verdigi sira numarasi yanlis olabilir; ekrandaki SIRA kesin.
        // Kodlar sirayla tuttugu icin bu bir uyusmazlik DEGIL.
        var sonuc = GridDogrulama.Karsilastir(
            new[] { B(7, "320 A01"), B(8, "770 01") },
            new[] { O(99, "320 A01"), O(1, "770 01") });

        Assert.True(sonuc.TamamTutuyor);
    }

    [Fact]
    public void Fazla_bosluk_uyusmazlik_sayilmiyor()
    {
        // ORKA kodlari bosluklu ("320 01 001"); goruntude iki bosluk tek
        // gorunebilir. Bu gercek bir fark degil.
        var sonuc = GridDogrulama.Karsilastir(
            new[] { B(1, "320 01 001") },
            new[] { O(1, "  320   01  001 ") });

        Assert.True(sonuc.TamamTutuyor);
    }

    [Fact]
    public void Rakam_farki_uyusmazlik()
    {
        // Boslukta tolerans var, rakamda YOK: katmanin varlik sebebi bu.
        var sonuc = GridDogrulama.Karsilastir(
            new[] { B(1, "320 01 001") },
            new[] { O(1, "320 01 002") });

        Assert.False(sonuc.TamamTutuyor);
        Assert.Single(sonuc.Farklar);
    }

    [Fact]
    public void Ekrana_sigmayan_satirlar_fark_degil_dogrulanmamis_sayiliyor()
    {
        // "Ekrana sigmadi" ile "yanlis kod yazildi" ayni sey degil; ikisini tek
        // sayida toplamak uyariyi okunmaz yapardi.
        var sonuc = GridDogrulama.Karsilastir(
            new[] { B(1, "320 A01"), B(2, "770 01"), B(3, "600 05") },
            new[] { O(1, "320 A01"), O(2, "770 01") },
            kesildi: true);

        Assert.Empty(sonuc.Farklar);
        Assert.Equal(2, sonuc.Eslesen);
        Assert.Equal(1, sonuc.Karsilastirilmayan);
        Assert.False(sonuc.TamamTutuyor);

        var metin = string.Join("\n", GridDogrulama.Satirlar(sonuc));
        Assert.Contains("1 satir goruntude yoktu", metin);
        Assert.Contains("DOGRULANMADI", metin);
    }

    [Fact]
    public void Goruntude_fazla_satir_varsa_uyariliyor()
    {
        var sonuc = GridDogrulama.Karsilastir(
            new[] { B(1, "320 A01") },
            new[] { O(1, "320 A01"), O(2, "999 99") });

        Assert.Empty(sonuc.Farklar);
        Assert.False(sonuc.TamamTutuyor);
        Assert.Contains("FAZLA satir okundu", string.Join("\n", GridDogrulama.Satirlar(sonuc)));
    }

    [Fact]
    public void Okunamayan_kod_tahmin_degil_bos_geliyor_ve_fark_sayiliyor()
    {
        // Yonerge "emin degilsen tahmin etme, bos birak" diyor. Bos okuma
        // sessizce gecmemeli: uydurulmus bir kod uyariyi susturur, bos okuma
        // kullaniciyi o satira bakmaya gonderir.
        var sonuc = GridDogrulama.Karsilastir(
            new[] { B(1, "320 A01") },
            new[] { O(1, "") });

        var fark = Assert.Single(sonuc.Farklar);
        Assert.Equal("(okunamadi)", fark.Okunan);
    }
}

/// <summary>Modelin yanitindan satirlarin cikarilmasi. Ag yok, saf metin isi.</summary>
public class GridOkumaTests
{
    [Fact]
    public void Duz_json_cozuluyor()
    {
        var okuma = GridOkuma.Coz(
            """{"kesildi": false, "satirlar": [{"sira":1,"aciklama":"ODEME","kod":"320 A01"}]}""");

        var satir = Assert.Single(okuma.Satirlar);
        Assert.Equal(1, satir.Sira);
        Assert.Equal("ODEME", satir.Aciklama);
        Assert.Equal("320 A01", satir.KarsiHesapKodu);
        Assert.False(okuma.Kesildi);
    }

    [Fact]
    public void Kod_cercevesi_ve_cevresindeki_metin_temizleniyor()
    {
        // Yonerge yalniz JSON istiyor; uyulmadi diye dogrulamayi tumden
        // kaybetmek gereksiz.
        var okuma = GridOkuma.Coz(
            "Iste okudugum satirlar:\n```json\n{\"satirlar\":[{\"sira\":1,\"kod\":\"770 01\"}]}\n```\n");

        Assert.Equal("770 01", Assert.Single(okuma.Satirlar).KarsiHesapKodu);
    }

    [Fact]
    public void Kesildi_bayragi_okunuyor()
    {
        var okuma = GridOkuma.Coz("""{"kesildi": true, "satirlar": [{"sira":1,"kod":"320"}]}""");
        Assert.True(okuma.Kesildi);
    }

    [Fact]
    public void Eksik_alanlar_bos_geliyor()
    {
        var okuma = GridOkuma.Coz("""{"satirlar":[{"sira":1},{"kod":"770 01"}]}""");

        Assert.Equal(2, okuma.Satirlar.Count);
        Assert.Equal(string.Empty, okuma.Satirlar[0].KarsiHesapKodu);
        Assert.Equal(string.Empty, okuma.Satirlar[0].Aciklama);

        // Sira okunamayinca kendi sayacimiz kullaniliyor.
        Assert.Equal(2, okuma.Satirlar[1].Sira);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("hicbir sey okuyamadim")]
    [InlineData("{bozuk json")]
    [InlineData("""{"satirlar": "liste degil"}""")]
    public void Cozulemeyen_yanit_bos_okuma(string? yanit)
    {
        // Bos okuma "dogrulanamadi" demek, hata degil: katman robotu durdurmuyor.
        Assert.Empty(GridOkuma.Coz(yanit).Satirlar);
    }
}
