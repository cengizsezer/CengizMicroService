using PkfRobot.Arayuz;
using PkfRobot.Ayarlar;
using PkfRobot.Core;

namespace PkfRobot.UnitTests.Core;

/// <summary>
/// Odak duzeltmesi iki kere kirildi, ikisi de burada sabitleniyor.
///
/// <b>1. Surec duzeyi yetmiyordu.</b> "Odaktaki elemanin process'i ORKA mi" sorusu
/// ORKA'nin butun pencerelerine ayni cevabi veriyor.
///
/// <b>2. Pencere kimligi TEK BASINA da yetmiyor.</b> Gorev dosyalari ORKA'nin actigi
/// her modali beklemiyor: F7'den sonra BeklePencere yok, dogrudan TemizleYaz geliyor.
/// O an hedef hala giris ekranini gosteriyor ve yalniz tutamaca bakan bir kural odagi
/// "Firma Listesi" diyalogundan CALIYORDU -- firma kodu hicbir yere yazilmiyordu.
///
/// Kural: <b>odak ORKA'nin herhangi bir penceresindeyse dokunma; ORKA DISINDA ise
/// duzelt.</b> Modal olup olmadigi yalnizca log ayrimi icin okunuyor.
/// </summary>
public class OdakKarariTests
{
    private static int _sayac = 0x10;

    private static UstSeviyePencere P(string baslik, int pid = 4242)
        => new(new IntPtr(_sayac++), pid, baslik,
               new PencereOlcusu(0, 0, 1920, 1080), Gorunur: true, Sahip: IntPtr.Zero);

    // ---- hedef secimi --------------------------------------------------------

    [Fact]
    public void Hedef_son_one_getirilen_penceredir()
    {
        // Ofisteki asil senaryo: bir adim "Transfer Edilecek Excel" diyalogunu one
        // getirdi, siradaki Yaz adimi dosya adini ORAYA yazmali. "Her zaman ana
        // ekran" deseydik dosya adi ORKA ana penceresine giderdi.
        var diyalog = P("Transfer Edilecek Excel");
        var anaEkran = P("ORKA_0001_2026");

        var hedef = OdakKarari.Hedef(diyalog, sonPencereYasiyor: true, anaEkran);

        Assert.Same(diyalog, hedef);
    }

    [Fact]
    public void Son_pencere_kapandiysa_ORKA_on_penceresine_dusuluyor()
    {
        // Diyalog kapandi ya da sekme degisti: elde kalan tek makul hedef
        // config'deki ana ekran/giris/sube sirasi.
        var kapanan = P("Transfer Edilecek Excel");
        var anaEkran = P("ORKA_0001_2026");

        var hedef = OdakKarari.Hedef(kapanan, sonPencereYasiyor: false, anaEkran);

        Assert.Same(anaEkran, hedef);
    }

    [Fact]
    public void Henuz_hicbir_pencere_one_getirilmediyse_ORKA_on_penceresi_kullaniliyor()
    {
        var anaEkran = P("ORKA_0001_2026");

        Assert.Same(anaEkran, OdakKarari.Hedef(null, sonPencereYasiyor: false, anaEkran));
    }

    [Fact]
    public void Hicbir_aday_yoksa_hedef_yok()
    {
        // Cagiran bu durumda "ORKA penceresi bulunamadi" uyarisi yaziyor; sessizce
        // tus gondermek, tuslarin nereye gittigini bilinmez yapardi.
        Assert.Null(OdakKarari.Hedef(null, sonPencereYasiyor: false, null));
    }

    // ---- odak karari ---------------------------------------------------------

    [Fact]
    public void Hedef_zaten_ondeyse_dokunulmuyor()
    {
        var hedef = P("ORKA_0001_2026");

        var karar = OdakKarari.Karar(hedef, hedef.Tutamac, onPlanOrkada: true, hedefEtkin: true);

        Assert.Equal(OdakEylemi.HedefZatenOnde, karar);
    }

    [Fact]
    public void Hedefin_ustunde_modal_varken_odak_calinmiyor()
    {
        // F7 firma listesi, firma sifresi popup'i, hesap plani... Modal acikken
        // sahibi DEVRE DISI kalir; hedefEtkin=false tam olarak bunu soyluyor.
        var hedef = P("ORKA_0001_2026");
        var f7Diyalogu = P("Firma Listesi");

        var karar = OdakKarari.Karar(hedef, f7Diyalogu.Tutamac,
                                     onPlanOrkada: true, hedefEtkin: false);

        Assert.Equal(OdakEylemi.OrkaModaliAcik, karar);
    }

    [Fact]
    public void ORKA_nin_baska_penceresi_ondeyken_de_odak_calinmiyor()
    {
        // Modelsiz (modal olmayan) ORKA diyaloglari buraya duser: hedef etkin
        // kaldigi icin modallik okunamaz, ama odak yine de ORKA'da -- calmak
        // yaziyi yanlis pencereye gonderirdi.
        var hedef = P("ORKA_0001_2026", pid: 4242);
        var baskaOrkaPenceresi = P("Yazici baglantisi", pid: 4242);

        var karar = OdakKarari.Karar(hedef, baskaOrkaPenceresi.Tutamac,
                                     onPlanOrkada: true, hedefEtkin: true);

        Assert.Equal(OdakEylemi.OrkaninBaskaPenceresi, karar);
    }

    [Fact]
    public void Odak_ORKA_DISINDAYKEN_one_getiriliyor()
    {
        // Odak duzeltmenin varlik sebebi: ofis testinde sifre cmd penceresine gitti.
        var hedef = P("ORKA_0001_2026");

        var karar = OdakKarari.Karar(hedef, new IntPtr(0x7777),
                                     onPlanOrkada: false, hedefEtkin: true);

        Assert.Equal(OdakEylemi.OneGetir, karar);
    }

    [Fact]
    public void Hedef_yoksa_ya_da_tutamaci_yoksa_karar_verilemiyor()
    {
        var tutamacsiz = new UstSeviyePencere(IntPtr.Zero, 4242, "ORKA_0001_2026",
                                              new PencereOlcusu(0, 0, 800, 600), true, IntPtr.Zero);

        Assert.Equal(OdakEylemi.HedefYok,
            OdakKarari.Karar(null, new IntPtr(0x7777), onPlanOrkada: false, hedefEtkin: false));
        Assert.Equal(OdakEylemi.HedefYok,
            OdakKarari.Karar(tutamacsiz, new IntPtr(0x7777), onPlanOrkada: false, hedefEtkin: true));
    }

    // ---- uctan uca: iki senaryo bir arada ------------------------------------
    // Asagidaki ikisi ayni anda dogru olmali; birini duzeltirken digeri bozuldugu
    // icin bu bolum var.

    [Fact]
    public void F7_senaryosu_odak_diyalogda_kaliyor()
    {
        // 01-orka-ac-firma-sec.json: 14. adim F7, 16. adim TemizleYaz. Arada
        // BeklePencere YOK, yani son one getirilen pencere hala GIRIS EKRANI.
        var girisEkrani = P("Orka SQL");
        var f7Diyalogu = P("Firma Listesi");

        var hedef = OdakKarari.Hedef(girisEkrani, sonPencereYasiyor: true, P("ORKA_0001_2026"));

        // Hedef "yanlis" secilir -- bu kacinilmaz, cunku diyalogu bekleyen adim yok.
        Assert.Same(girisEkrani, hedef);

        // Karar yine de dogru olmali: odak ORKA'da, dokunma.
        var karar = OdakKarari.Karar(hedef, f7Diyalogu.Tutamac,
                                     onPlanOrkada: true, hedefEtkin: false);

        Assert.Equal(OdakEylemi.OrkaModaliAcik, karar);
    }

    [Fact]
    public void Dosya_secim_senaryosu_bozulmuyor()
    {
        // orkaya-aktar.json: BeklePencere "Transfer Edilecek Excel" diyalogu one
        // getirdi, siradaki TemizleYaz dosya yolunu ORAYA yazmali.
        var diyalog = P("Transfer Edilecek Excel");

        var hedef = OdakKarari.Hedef(diyalog, sonPencereYasiyor: true, P("ORKA_0001_2026"));

        // Diyalog onde: dokunulmuyor, yazi diyaloga gidiyor.
        Assert.Equal(OdakEylemi.HedefZatenOnde,
            OdakKarari.Karar(hedef, diyalog.Tutamac, onPlanOrkada: true, hedefEtkin: true));

        // Arada baska bir uygulama odagi caldiysa diyalog geri getiriliyor --
        // ana pencere degil, DIYALOG.
        Assert.Equal(OdakEylemi.OneGetir,
            OdakKarari.Karar(hedef, new IntPtr(0x7777), onPlanOrkada: false, hedefEtkin: true));
    }
}
