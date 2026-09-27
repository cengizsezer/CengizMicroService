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
        Assert.Equal(1, sonuc.EslesmeyenOkunan);

        var metin = string.Join("\n", GridDogrulama.Satirlar(sonuc));
        Assert.Contains("eslesmedi", metin);
        Assert.Contains("FAZLA satir", metin);
    }

    // ---- kaydirma: aciklamaya gore eslestirme -------------------------------

    /// <summary>
    /// 08.09.2026 kosusunun aynisi: GridDoldur bittiginde grid ASAGI kaydirilmis
    /// kaliyor, goruntudeki ilk satir listenin ilk satiri degil. Sira bazli
    /// karsilastirma "0/48 eslesti, 27 satir tutmuyor" diyordu -- oysa okunan
    /// kodlarin HEPSI dogruydu.
    /// </summary>
    [Fact]
    public void Kaydirilmis_grid_aciklamaya_gore_dogru_eslesiyor()
    {
        var beklenen = new[]
        {
            new GridSatiri(1, "POS HESABINA VIRMAN",   "102 1 1 04"),
            new GridSatiri(2, "KOMISYON KESINTISI",    "770 01"),
            new GridSatiri(3, "EFT GIDEN HAVALE",      "320 A01"),
            new GridSatiri(4, "MASRAF KESINTISI",      "770 02"),
            new GridSatiri(5, "KREDI KARTI TAHSILATI", "360 02 002")
        };

        // Ekranda gorunen ilk satir 5. satir; 1-4 yukarida kalmis.
        var okunan = new[]
        {
            new OkunanGridSatiri(1, "KREDI KARTI TAHSILATI", "360 02 002"),
            new OkunanGridSatiri(2, "POS HESABINA VIRMAN",   "102 1 1 04")
        };

        var sonuc = GridDogrulama.Karsilastir(beklenen, okunan, kesildi: true);

        Assert.Equal(EslestirmeYolu.Aciklama, sonuc.Yol);
        Assert.Empty(sonuc.Farklar);
        Assert.Equal(2, sonuc.Eslesen);
        Assert.Equal(0, sonuc.EslesmeyenOkunan);
        Assert.Equal(3, sonuc.Karsilastirilmayan);

        // Sahte uyari YOK: tutmayan satir olmadigi soyleniyor, eksik kalanlar
        // ayri satirda "DOGRULANMADI" olarak.
        var metin = string.Join("\n", GridDogrulama.Satirlar(sonuc));
        Assert.DoesNotContain("DOGRULAMA UYARISI", metin);
        Assert.Contains("tutmayan satir yok", metin);
        Assert.Contains("3 satir goruntude yoktu", metin);
    }

    [Fact]
    public void Kaydirilmis_gridde_GERCEK_hata_yine_yakalaniyor()
    {
        // Kaymayi tolere etmek, yanlis kodu tolere etmek DEGIL.
        var beklenen = new[]
        {
            new GridSatiri(1, "POS HESABINA VIRMAN", "102 1 1 04"),
            new GridSatiri(2, "KOMISYON KESINTISI",  "770 01"),
            new GridSatiri(3, "EFT GIDEN HAVALE",    "320 A01")
        };

        var okunan = new[]
        {
            new OkunanGridSatiri(1, "EFT GIDEN HAVALE",    "320 A01"),
            new OkunanGridSatiri(2, "POS HESABINA VIRMAN", "102 1 1 09")   // yanlis
        };

        var sonuc = GridDogrulama.Karsilastir(beklenen, okunan);

        var fark = Assert.Single(sonuc.Farklar);
        Assert.Equal(1, fark.SiraNo);                 // EKSTREDEKI sira, ekrandaki degil
        Assert.Equal("102 1 1 04", fark.Beklenen);
        Assert.Equal("102 1 1 09", fark.Okunan);
        Assert.Contains("DOGRULAMA UYARISI", string.Join("\n", GridDogrulama.Satirlar(sonuc)));
    }

    [Fact]
    public void Ayni_aciklamali_satirlar_ekrandaki_siraya_gore_paylastiriliyor()
    {
        // Ekstrelerde ayni aciklama tekrar edebiliyor. Aciklama ayirt etmiyorsa
        // davranis eski sira bazli eslestirmenin aynisi olmali.
        var beklenen = new[]
        {
            new GridSatiri(1, "HAVALE", "320 A01"),
            new GridSatiri(2, "HAVALE", "320 A02"),
            new GridSatiri(3, "HAVALE", "320 A03")
        };

        var okunan = new[]
        {
            new OkunanGridSatiri(1, "HAVALE", "320 A01"),
            new OkunanGridSatiri(2, "HAVALE", "320 A02"),
            new OkunanGridSatiri(3, "HAVALE", "320 A03")
        };

        Assert.True(GridDogrulama.Karsilastir(beklenen, okunan).TamamTutuyor);
    }

    [Fact]
    public void Ekranda_kesilmis_aciklama_on_ekle_eslesiyor()
    {
        // Aciklama kolonu dar; uzun metin ekranda kesiliyor.
        var beklenen = new[] { new GridSatiri(1, "KREDI KARTI TAHSILATI 15/08", "360 02 002") };
        var okunan = new[] { new OkunanGridSatiri(1, "KREDI KARTI TAH", "360 02 002") };

        Assert.True(GridDogrulama.Karsilastir(beklenen, okunan).TamamTutuyor);
    }

    [Fact]
    public void Belirsiz_on_ek_eslestirilmiyor()
    {
        // Iki aday varsa hangisi oldugu BILINMIYOR demektir; yanlis satiri
        // "dogrulanmis" saymaktansa dogrulamamak yeglenir.
        var beklenen = new[]
        {
            new GridSatiri(1, "KREDI KARTI TAHSILATI 15/08", "360 02 002"),
            new GridSatiri(2, "KREDI KARTI TAHSILATI 16/08", "360 02 003")
        };

        var okunan = new[] { new OkunanGridSatiri(1, "KREDI KARTI TAH", "360 02 002") };

        var sonuc = GridDogrulama.Karsilastir(beklenen, okunan);

        Assert.Equal(0, sonuc.Eslesen);
        Assert.Empty(sonuc.Farklar);
        Assert.Equal(1, sonuc.EslesmeyenOkunan);
    }

    [Fact]
    public void Bosluk_noktalama_ve_Turkce_harf_farki_eslesmeyi_bozmuyor()
    {
        // Aciklama bir ESLESTIRME ANAHTARI, dogrulanan deger degil: burada
        // tolerans dogru karar. Kodda tolerans YOK.
        var beklenen = new[] { new GridSatiri(1, "Ödeme - İstanbul Şubesi", "320 A01") };
        var okunan = new[] { new OkunanGridSatiri(1, "ODEME  ISTANBUL SUBESI", "320 A01") };

        Assert.True(GridDogrulama.Karsilastir(beklenen, okunan).TamamTutuyor);
    }

    [Fact]
    public void Sayilar_tutsa_bile_satirlar_eslesmiyorsa_TamamTutuyor_degil()
    {
        // Tuzak: okunan 1 = beklenen 1 ve fark listesi bos, ama satirlar
        // BIRBIRINI tutmuyor. Yalniz sayilara bakan bir kontrol bunu "tamam"
        // gosterirdi.
        var beklenen = new[] { new GridSatiri(1, "POS HESABINA VIRMAN", "102 1 1 04") };
        var okunan = new[] { new OkunanGridSatiri(1, "BILINMEYEN SATIR", "999 99") };

        var sonuc = GridDogrulama.Karsilastir(beklenen, okunan);

        Assert.Equal(1, sonuc.Beklenen);
        Assert.Equal(1, sonuc.Okunan);
        Assert.Empty(sonuc.Farklar);
        Assert.False(sonuc.TamamTutuyor);
        Assert.Equal(1, sonuc.EslesmeyenOkunan);
        Assert.Equal(1, sonuc.Karsilastirilmayan);
    }

    // ---- yedek yol: aciklama okunamadiginda ---------------------------------

    [Fact]
    public void Aciklama_okunamazsa_siraya_dusuluyor_ve_bu_SOYLENIYOR()
    {
        // Model aciklama kolonunu hic okuyamamis. Sessizce siraya dusmek,
        // dogrulamanin en pahali kirilma sekli olurdu: grid kaydirilmissa bu
        // olcut her satiri "tutmuyor" gosterir.
        var beklenen = new[]
        {
            new GridSatiri(1, "POS HESABINA VIRMAN", "102 1 1 04"),
            new GridSatiri(2, "KOMISYON KESINTISI",  "770 01")
        };

        var okunan = new[]
        {
            new OkunanGridSatiri(1, "", "102 1 1 04"),
            new OkunanGridSatiri(2, "", "770 01")
        };

        var sonuc = GridDogrulama.Karsilastir(beklenen, okunan);

        Assert.Equal(EslestirmeYolu.Sira, sonuc.Yol);
        Assert.True(sonuc.TamamTutuyor);
    }

    [Fact]
    public void Sira_yedegine_dusuldugu_raporda_yaziyor()
    {
        var beklenen = new[] { new GridSatiri(1, "POS HESABINA VIRMAN", "102 1 1 04") };
        var okunan = new[] { new OkunanGridSatiri(1, "", "102 1 1 09") };

        var metin = string.Join("\n", GridDogrulama.Satirlar(GridDogrulama.Karsilastir(beklenen, okunan)));

        Assert.Contains("EKRANDAKI SIRAYA gore", metin);
        Assert.Contains("kaydirilmis", metin);
    }

    [Fact]
    public void Aciklamalarin_biri_okunduysa_yedege_dusulmuyor()
    {
        // Tek bir satirin aciklamasi okunamadi diye butun olcutu degistirmek,
        // kaydirma korumasini bir OCR hatasiyla kaybetmek olurdu.
        var beklenen = new[]
        {
            new GridSatiri(1, "POS HESABINA VIRMAN", "102 1 1 04"),
            new GridSatiri(2, "KOMISYON KESINTISI",  "770 01")
        };

        var okunan = new[]
        {
            new OkunanGridSatiri(1, "KOMISYON KESINTISI", "770 01"),
            new OkunanGridSatiri(2, "", "102 1 1 04")
        };

        var sonuc = GridDogrulama.Karsilastir(beklenen, okunan);

        Assert.Equal(EslestirmeYolu.Aciklama, sonuc.Yol);
        Assert.Equal(1, sonuc.Eslesen);
        Assert.Equal(1, sonuc.EslesmeyenOkunan);
    }

    [Fact]
    public void Aciklama_anahtari_bos_metinleri_ayni_saymiyor()
    {
        Assert.Equal(string.Empty, GridDogrulama.AciklamaAnahtari(null));
        Assert.Equal(string.Empty, GridDogrulama.AciklamaAnahtari("   -  "));
        Assert.Equal("ODEMEISTANBUL", GridDogrulama.AciklamaAnahtari("Ödeme İstanbul"));
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
