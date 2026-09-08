using FlaUI.Core.WindowsAPI;
using PkfRobot.Arayuz;
using PkfRobot.Ayarlar;
using PkfRobot.Core;

namespace PkfRobot.UnitTests.Core;

/// <summary>
/// 06.09.2026 22:42 kosusu: "Firma Sifresini Giriniz." popup'ina HICBIR karakter
/// gitmedi. Log'da adimin tek izi "Yaz -> ***" satiriydi, [ODAK-TESHIS] ise
/// <c>karar=HedefZatenOnde</c> ve <c>etkin=False</c> diyordu. "Devre disi pencereye
/// yazilamadi" diye okundu; oysa kullanici ayni anda kutuya ELLE yazabiliyordu.
///
/// Iki ayri sey burada sabitleniyor:
///
/// <b>1. Etkinlik gonderimi belirlemez.</b> Hedef on plandaysa karar ON PLAN
/// testinden cikar; IsWindowEnabled Delphi modallarinda yaniltici olabiliyor ve
/// yalnizca "odagi calalim mi" sorusunda anlamli.
///
/// <b>2. Gonderim artik sessiz degil.</b> Yaz'in iz satiri yoktu (Tus/Kisayol'da
/// vardi), bu yuzden "deger bostu" ile "gonderildi ama ORKA yuttu" ayirt edilemiyordu.
/// Maskeleme de ADIM TANIMINA baktigi icin bos deger ve cozulmemis degisken log'da
/// dolu degerle ayni gorunuyordu.
/// </summary>
[Collection(KlavyeKoleksiyonu.Ad)]
public class FirmaSifresiYazmaTests : IDisposable
{
    private readonly SahteKlavye _sahte = new();
    private readonly List<string> _iz = new();
    private readonly int _eskiBekleme = Klavye.VarsayilanBeklemeMs;
    private readonly Action<string>? _eskiIz = Klavye.Iz;

    public FirmaSifresiYazmaTests()
    {
        Klavye.Surucu = _sahte;
        Klavye.VarsayilanBeklemeMs = 0;
        Klavye.Iz = _iz.Add;
    }

    public void Dispose()
    {
        Klavye.SurucuyuSifirla();
        Klavye.VarsayilanBeklemeMs = _eskiBekleme;
        Klavye.Iz = _eskiIz;
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

    private static UstSeviyePencere Popup(nint tutamac = 0x2A1C)
        => new(new IntPtr(tutamac), 4242, "Firma Şifresini Giriniz.",
               new PencereOlcusu(0, 0, 400, 200), Gorunur: true, Sahip: IntPtr.Zero);

    // ---- 1. etkinlik gonderimi engellemesin ----------------------------------

    [Fact]
    public void Popup_onde_ve_DEVRE_DISI_gorunse_bile_karar_HedefZatenOnde()
    {
        // Kosunun tam girdileri: on plandaki pencere = hedef, etkin=False.
        var popup = Popup();

        Assert.Equal(OdakEylemi.HedefZatenOnde,
            OdakKarari.Karar(popup, popup.Tutamac, onPlanOrkada: true, hedefEtkin: false));
    }

    [Fact]
    public void Hedef_ondeyken_karar_etkinlikten_BAGIMSIZ()
    {
        // Etkinlik degeri kararin hicbir dalini degistirmemeli; "etkin=False gorursek
        // dokunmayalim" refleksi tam olarak bu senaryoyu kirardi.
        var popup = Popup();

        Assert.Equal(
            OdakKarari.Karar(popup, popup.Tutamac, onPlanOrkada: true, hedefEtkin: true),
            OdakKarari.Karar(popup, popup.Tutamac, onPlanOrkada: true, hedefEtkin: false));
    }

    [Fact]
    public void Etkinlik_YALNIZCA_hedef_arkadayken_anlam_tasiyor()
    {
        // Ayrimin hala yerinde durdugu: hedef on planda DEGILSE etkinlik "ustunde
        // modal var mi" sorusunu cevaplamaya devam ediyor.
        var hedef = Popup();
        var baskaPencere = new IntPtr(0x9001);

        Assert.Equal(OdakEylemi.OrkaModaliAcik,
            OdakKarari.Karar(hedef, baskaPencere, onPlanOrkada: true, hedefEtkin: false));
        Assert.Equal(OdakEylemi.OrkaninBaskaPenceresi,
            OdakKarari.Karar(hedef, baskaPencere, onPlanOrkada: true, hedefEtkin: true));
    }

    // ---- 2. gonderim artik log'da gorunuyor ----------------------------------

    [Fact]
    public void Yaz_gonderilen_karakter_sayisini_iz_satirina_dusuyor()
    {
        Klavye.Yaz("pkf03");

        Assert.Equal(new[] { "yaz:pkf03" }, _sahte.Olaylar);
        Assert.Contains(_iz, s => s.StartsWith("[TUS-TESHIS]") && s.Contains("5 karakter"));
        // Sifrenin KENDISI log'a dusmemeli.
        Assert.DoesNotContain(_iz, s => s.Contains("pkf03"));
    }

    [Fact]
    public void Bos_deger_gonderildiginde_iz_satiri_BOS_diyor()
    {
        // Bu kosunun en olasi sebebi: firmaSifre ayari bos, Klavye.Yaz("") hicbir
        // sey gondermiyor ve eski log'da bu "Yaz -> ***" olarak gorunuyordu.
        Klavye.Yaz("");

        Assert.Contains(_iz, s => s.Contains("[TUS-TESHIS]") && s.Contains("BOS"));
    }

    [Fact]
    public void TemizleYaz_da_ayni_iz_satirini_birakiyor()
    {
        Klavye.TemizleVeYaz("0001");

        Assert.Contains(_iz, s => s.Contains("4 karakter"));
    }

    // ---- 3. cozulmemis degisken sessiz kalmasin ------------------------------

    [Fact]
    public void Cozulmemis_degisken_yakalaniyor()
    {
        // Degisken sozlugunde firmaSifre yoksa DegiskenleriCoz metni oldugu gibi
        // birakir ve ORKA'nin kutusuna literal '{firmaSifre}' yazilir.
        Assert.Equal(new[] { "{firmaSifre}" },
                     AdimMotoru.CozulmemisDegiskenler("{firmaSifre}"));
    }

    [Fact]
    public void Cozulmus_deger_uyari_uretmiyor()
    {
        Assert.Empty(AdimMotoru.CozulmemisDegiskenler("pkf03"));
        Assert.Empty(AdimMotoru.CozulmemisDegiskenler(""));
        Assert.Empty(AdimMotoru.CozulmemisDegiskenler(null));
    }
}
