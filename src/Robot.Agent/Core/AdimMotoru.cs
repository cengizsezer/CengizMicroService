using System.Diagnostics;
using System.Globalization;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using PkfRobot.Arayuz;
using PkfRobot.Config;

namespace PkfRobot.Core;

/// <summary>
/// Gorev JSON'undaki adimlari sirayla yurutur.
/// Yeni bir is eklemek = yeni bir JSON dosyasi. Kod degismez.
/// Bu ayrim, projenin "zamanla buyuyen bir platform" olmasinin sarti.
/// </summary>
public class AdimMotoru
{
    private readonly RobotConfig _cfg;
    private readonly AdimLogger _log;
    private readonly UIA3Automation _automation;
    private readonly PencereBekleyici _bekleyici;
    private readonly Dictionary<string, string> _degiskenler;
    private readonly GridDoldurVerisi? _gridVerisi;
    private readonly Action<Adim>? _adimBasladi;

    /// <summary>
    /// En son BILEREK one getirilen pencere (BeklePencere / Tikla / OrkaBaslat).
    ///
    /// Odak duzeltmenin hedefi bu: "ORKA'nin herhangi bir penceresi onde mi"
    /// sorusu yeterli degil, sirasi gelen adim HANGI pencereye yaziyorsa onun
    /// onde olmasi gerekiyor (bkz. <see cref="OdakKarari.Hedef"/>).
    /// </summary>
    private UstSeviyePencere? _sonOnPencere;

    public AdimMotoru(RobotConfig cfg, AdimLogger log, UIA3Automation automation,
                      Dictionary<string, string>? degiskenler = null,
                      GridDoldurVerisi? gridVerisi = null,
                      Action<Adim>? adimBasladi = null)
    {
        _cfg = cfg;
        _log = log;
        _automation = automation;
        _bekleyici = new PencereBekleyici(automation, log, cfg);
        _degiskenler = degiskenler ?? new Dictionary<string, string>();
        _gridVerisi = gridVerisi;
        _adimBasladi = adimBasladi;

        Klavye.VarsayilanBeklemeMs = cfg.Zamanlama.TusBeklemeMs;

        // GECICI TESHIS: her tus basisi ayri satir olarak log'a dussun.
        // Klavye statik ve loglayiciyi tanimiyor, bagi burada kuruyoruz.
        Klavye.Iz = satir => _log.Bilgi(satir);
    }

    public void Calistir(Gorev gorev)
    {
        _log.Bilgi($"Gorev: {gorev.Ad} ({gorev.Adimlar.Count} adim)");
        if (_cfg.DryRun)
            _log.Uyari("DRY RUN AKTIF - 'OnayGerekir' adimlari ATLANACAK, kayit yapilmayacak.");

        var cakisan = CakisanPencereBasliklari(_cfg);
        if (cakisan.Count > 0)
            _log.Uyari("AYAR CELISKISI: su basliklar hem BeklenmeyenPencereler (DURDUR) hem " +
                       "OtomatikKapatilacakPencereler (KAPAT VE DEVAM ET) listesinde: " +
                       string.Join(" | ", cakisan) + ". Kapatma adimdan ONCE calistigi icin " +
                       "pencere kapanir ve durdurma kurali hic tetiklenmez.");

        foreach (var adim in gorev.Adimlar)
        {
            try
            {
                OtomatikPencereKapat();
                _adimBasladi?.Invoke(adim);
                AdimiYurut(adim);
            }
            catch (Exception ex)
            {
                _log.Hata($"Adim basarisiz ({adim.Tip} / {Maskele(adim, adim.Deger)}): {ex.Message}");
                _log.EkranAl("HATA", zorla: true);
                _log.Bilgi("Ekrandaki pencereler: " +
                           string.Join(" | ", _bekleyici.TumPencereBasliklari().Take(20)));
                throw;
            }

            SurprizPencereKontrol();
            Thread.Sleep(_cfg.Zamanlama.AdimBeklemeMs);
        }

        _log.Bilgi("Gorev tamamlandi.");
    }

    private void AdimiYurut(Adim adim)
    {
        var deger = DegiskenleriCoz(adim.Deger);
        var etiket = string.IsNullOrWhiteSpace(adim.Not) ? deger : adim.Not;

        switch (adim.Tip.Trim().ToLowerInvariant())
        {
            case "orkabaslat":
                _log.Adim("OrkaBaslat", _cfg.OrkaPath);
                OrkaBaslat(TimeoutCoz(adim, _cfg.Zamanlama.OrkaAcilisTimeoutSn));
                break;

            case "beklepencere":
                _log.Adim("BeklePencere", etiket);
                var el = _bekleyici.Bekle(deger, TimeoutCoz(adim));
                _bekleyici.OneGetirVeBuyut(el);
                _sonOnPencere = el;
                break;

            case "dogrula":
                _log.Adim("Dogrula", etiket);
                if (!_bekleyici.VarMi(deger))
                    throw new InvalidOperationException(
                        $"Dogrulama basarisiz: '{deger}' iceren pencere yok. " +
                        "Robot beklenen ekranda degil, devam edilmiyor.");
                _log.Bilgi($"Dogrulandi: {deger}");
                break;

            case "yaz":
                _log.Adim("Yaz", Maskele(adim, deger));
                YazilacakDegeriDenetle("Yaz", adim, deger);
                OdakGuvence("Yaz");
                Klavye.Yaz(deger);
                break;

            case "temizleyaz":
                _log.Adim("TemizleYaz", Maskele(adim, deger));
                YazilacakDegeriDenetle("TemizleYaz", adim, deger);
                OdakGuvence("TemizleYaz");
                Klavye.TemizleVeYaz(deger);
                break;

            case "tus":
                var adet = AdetCoz(adim);
                _log.Adim("Tus", $"{deger} x{adet}");
                OdakGuvence("Tus");
                Klavye.Tus(deger, adet);
                break;

            case "kisayol":
                _log.Adim("Kisayol", deger);
                OdakGuvence("Kisayol");
                Klavye.Kisayol(deger);
                break;

            case "altpenceredogrula":
                AltPencereDogrula(adim, deger, etiket);
                break;

            case "tikla":
                Tikla(adim, deger);
                break;

            case "griddoldur":
                GridDoldur(adim);
                break;

            case "bekle":
                _log.Adim("Bekle", $"{adim.Sayi} ms");
                Thread.Sleep(adim.Sayi);
                break;

            case "ekrangoruntusu":
                _log.Adim("EkranGoruntusu", deger);
                _log.EkranAl(deger, zorla: true);
                return; // ekstra ekran goruntusu alma

            case "onaygerekir":
                if (_cfg.DryRun)
                {
                    _log.Adim("OnayGerekir", $"ATLANDI (DryRun) -> {etiket}");
                    return;
                }
                _log.Adim("OnayGerekir", $"UYGULANIYOR -> {etiket}");
                if (!string.IsNullOrWhiteSpace(deger))
                {
                    OdakGuvence("OnayGerekir");
                    Klavye.Kisayol(deger);
                }
                break;

            case "log":
                _log.Adim("Log", etiket);
                return;

            default:
                throw new ArgumentException($"Bilinmeyen adim tipi: '{adim.Tip}'");
        }

        if (_cfg.EkranGoruntusu.HerAdimda)
            _log.EkranAl(adim.Tip);
    }

    /// <summary>
    /// "Dogru EKRANDA miyiz" sorusunu ALT KONTROLLERIN SINIF ADINDAN cevaplar.
    ///
    /// <b>Neden ayri bir adim:</b> ORKA'da modul/sekme gecisi ust seviye pencere
    /// ACMIYOR ve ana pencerenin basligi da degismiyor. Baslik bakan
    /// <c>Dogrula</c> bu yuzden "TRANSFERLER acildi mi" sorusunu cevaplayamiyor:
    /// robot modul ekranindan cikamadigi halde dogrulamayi GECIYOR ve fare
    /// adimlarina devam edip yanlis ekranda tikliyor. Alt pencere agaci
    /// (EnumChildWindows + GetClassName) UIA'ya kapali ORKA'da okunabilen tek
    /// yapisal sinyal.
    ///
    /// <b>Deger BOS ise KESIF modu:</b> ekrandaki sinif adlarini log'a doker ve
    /// DURDURMAZ. Beklenen sinif adi henuz bilinmiyor -- ofiste bir kez bu modda
    /// calistirilip, iki ekranin dokumu karsilastirilarak doldurulacak
    /// (<c>--probe</c> ayni dokumu tek atista veriyor). Bos deger "her zaman
    /// patlayan bir adim" birakmamak icin sessiz: yanlis bir sinif adi yazmak
    /// gorevi her calistirmada durdururdu.
    ///
    /// <b>Deger DOLU ise:</b> gorunur bir alt kontrolun sinif adi (ya da metni)
    /// aranir, bulunamazsa ekran goruntusu alinip gorev DURUR. Tekrar deneme
    /// YOK -- genel tekrar mekanizmasi bilerek kurulmadi.
    /// </summary>
    private void AltPencereDogrula(Adim adim, string deger, string etiket)
    {
        if (string.IsNullOrWhiteSpace(deger))
        {
            _log.Adim("AltPencereDogrula", $"KESIF MODU {etiket}");
            _log.Bilgi("[ALT-PENCERE] Beklenen sinif adi verilmedi, yalnizca dokum aliniyor:");
            foreach (var satir in _bekleyici.AltPencereOzeti())
                _log.Bilgi($"[ALT-PENCERE]   {satir}");
            foreach (var satir in _bekleyici.AltPencereMetinOzeti())
                _log.Bilgi($"[ALT-PENCERE]   metin: {satir}");
            _log.EkranAl("alt-pencere-kesif", zorla: true);
            return;
        }

        _log.Adim("AltPencereDogrula", etiket);

        var bulunan = _bekleyici.AltPencereBekle(deger, TimeoutCoz(adim));
        if (bulunan != null)
        {
            _log.Bilgi($"Alt pencere dogrulandi: '{bulunan.SinifAdi}' " +
                       $"(baslik='{bulunan.Baslik}', derinlik={bulunan.Derinlik}).");
            return;
        }

        // Ekranda NE oldugu hata mesajinda dursun: ofiste "neyi aradi, ne vardi"
        // sorusu tek satirda cevaplansin -- BeklePencere zaman asimindaki kalip.
        var ozet = _bekleyici.AltPencereOzeti();
        var ekrandakiler = ozet.Count == 0
            ? "(hic alt kontrol okunamadi)"
            : string.Join(" | ", ozet);

        _log.Uyari($"[ALT-PENCERE] '{deger}' bulunamadi. Ekranda olanlar: {ekrandakiler}");

        // Aranan sey bir SINIF ADI degil METIN olabilir (popup dogrulamalari
        // boyle: "Transfer Islemi Tamamland"). Sinif sayimlari o durumda hicbir
        // sey soylemiyor; ekrandaki metinler ayrica dokuluyor.
        foreach (var satir in _bekleyici.AltPencereMetinOzeti())
            _log.Bilgi($"[ALT-PENCERE]   metin: {satir}");
        _log.EkranAl("alt-pencere-dogrulama-hatasi", zorla: true);

        throw new InvalidOperationException(
            $"Alt pencere dogrulamasi basarisiz: '{deger}' sinif adina sahip gorunur " +
            $"kontrol yok. Robot beklenen ekranda DEGIL, devam edilmiyor. " +
            $"Ekranda olanlar: {ekrandakiler}");
    }

    /// <summary>
    /// Klavyeye dokunmadan once HEDEF PENCERE on planda mi diye bakar, degilse one getirir.
    ///
    /// Ofis testinde robot ORKA yerine cmd penceresine yazdi ve SIFRE cmd'ye gitti;
    /// bu metot onu engelliyor. Her gorevin basina elle BeklePencere koymak da
    /// gerekmiyor artik.
    ///
    /// <b>Odak ORKA'nin HERHANGI bir penceresindeyse dokunulmuyor.</b> Gorev
    /// dosyalari ORKA'nin actigi her modali beklemiyor: F7'den sonra BeklePencere
    /// yok, dogrudan TemizleYaz geliyor. O an <see cref="_sonOnPencere"/> hala giris
    /// ekranini gosteriyor ve yalniz pencere kimligine bakan bir kural odagi "Firma
    /// Listesi" diyalogundan CALIYORDU -- firma kodu hicbir yere yazilmiyordu.
    /// Karar tablosu <see cref="OdakKarari.Karar"/>'da.
    ///
    /// <b>Duzeltme yine de gerekli:</b> odak ORKA DISINDA ise (cmd, Excel, Gezgin)
    /// one getiriliyor. Ofis testinde robot ORKA yerine cmd penceresine yazdi ve
    /// SIFRE cmd'ye gitti; bu metodun varlik sebebi o.
    ///
    /// <b>Hedef "ana ekran" degil, son one getirilen pencere.</b> Bir adim bilerek
    /// dosya secim diyalogunu one getirdiyse siradaki Yaz oraya yazmali; hedef
    /// <see cref="_sonOnPencere"/> uzerinden tasiniyor.
    ///
    /// Tikla adiminda cagrilmiyor - orada zaten hedef pencere one getiriliyor,
    /// ustune ana pencereyi one almak popup'i arkada birakirdi.
    ///
    /// <b>Hicbir dali tus gonderimini DURDURMAZ.</b> Metot ya odagi duzeltir ya da
    /// yalnizca log yazip doner; cagiran her durumda klavyeye devam eder. Ofiste
    /// "etkin=False oldugu icin yazamadi" diye okundugu icin yaziliyor: bir adimin
    /// atlanmasi isteniyorsa o karar burada degil, adimin kendisinde verilmeli.
    /// </summary>
    private void OdakGuvence(string adimTipi)
    {
        if (!_cfg.OtomatikOneGetir) return;

        try
        {
            var sonYasiyor = OrkaPenceresi.PencereMi(_sonOnPencere?.Tutamac ?? IntPtr.Zero);
            var hedef = OdakKarari.Hedef(_sonOnPencere, sonYasiyor, _bekleyici.OrkaOnPenceresi());

            var onPlandaki = OrkaPenceresi.OnPencere();
            var onPlanOrkada = _bekleyici.OnPlanOrkadaMi(onPlandaki);
            var hedefEtkin = OrkaPenceresi.EtkinMi(hedef?.Tutamac ?? IntPtr.Zero);

            var karar = OdakKarari.Karar(hedef, onPlandaki, onPlanOrkada, hedefEtkin);
            OdakTeshisYaz(adimTipi, hedef, onPlandaki, onPlanOrkada, hedefEtkin, karar);

            switch (karar)
            {
                case OdakEylemi.HedefZatenOnde:
                    // Hedef on planda: gonderim icin gereken tek sart bu, hedefEtkin
                    // BILEREK yok sayiliyor (bkz. OdakKarari.Karar). Yine de log'a
                    // yaziliyor: 06.09.2026 22:42 kosusunda tam bu durum vardi ve
                    // "etkin=False" satiri, adimin neden durdugu sanilmasina yol acti.
                    if (!hedefEtkin)
                        _log.Bilgi($"{adimTipi}: hedef '{Baslik(hedef)}' ON PLANDA ama " +
                                   "IsWindowEnabled=false. Delphi modallarinda bu deger " +
                                   "yaniltici olabiliyor; tuslar YINE DE gonderiliyor.");
                    return;

                case OdakEylemi.OrkaModaliAcik:
                    // Hedefin ustunde modal var: odak dogru yerde, dokunmak yaziyi
                    // yanlis pencereye gonderirdi. Log'a yaziliyor cunku bu satir
                    // "kutuya neden yazamadi" sorusunun cevabinin yarisi.
                    _log.Bilgi($"{adimTipi}: '{Baslik(hedef)}' uzerinde modal diyalog acik, " +
                               "odak diyalogda birakiliyor.");
                    return;

                case OdakEylemi.OrkaninBaskaPenceresi:
                    _log.Bilgi($"{adimTipi}: odak ORKA'nin baska bir penceresinde " +
                               $"('{Win32Baslik(onPlandaki)}'), hedef '{Baslik(hedef)}' degil. " +
                               "Odak CALINMIYOR - beklenmeyen bir ORKA diyalogu acik olabilir.");
                    return;

                case OdakEylemi.HedefYok:
                    _log.Uyari(onPlanOrkada
                        ? $"{adimTipi}: hedef ORKA penceresi secilemedi; odak ORKA surecinde " +
                          "ama HANGI pencerede oldugu dogrulanamiyor."
                        : $"{adimTipi}: ORKA penceresi bulunamadi, odak duzeltilemedi. " +
                          "Tuslar baska bir pencereye gidebilir.");
                    return;

                case OdakEylemi.OneGetir:
                    _log.Uyari($"{adimTipi}: odak ORKA DISINDA " +
                               $"('{Win32Baslik(onPlandaki)}') -> '{Baslik(hedef)}' one getiriliyor.");
                    _bekleyici.OneGetir(hedef!);

                    // One getirme artik dogrulaniyor; bu bekleme onun ustune duran kucuk
                    // bir yerlesme payi (pencere cizimi, sekme degisimi). Tusun kendisinden
                    // onceki bekleme ayrica Klavye tarafinda duruyor.
                    Thread.Sleep(_cfg.Zamanlama.TusBeklemeMs);
                    return;
            }
        }
        catch (Exception ex)
        {
            // Odak kontrolu adimi patlatmasin; uyarip devam et.
            _log.Uyari($"{adimTipi}: odak kontrolu yapilamadi: {ex.Message}");
        }
    }

    // ================ GECICI TESHIS ================
    // Neden: ofiste 16. adimda (F7 firma listesi diyalogu ACIKKEN) karar yine
    // OneGetir dalina dustu; ayni calistirmada 40. adimin hata dokumu diyalogu
    // GORUYOR. Yani "on plandaki pencere ORKA surecinde" testi ile pencere
    // taramasi ayni gercekligi gormuyor. Hangi girdinin yanlis oldugunu ancak
    // kararin TUM girdileri log'a dusunce ayirt edebiliyoruz.
    //
    // Bu blok DAVRANISI DEGISTIRMIYOR - kararin kendisi yukarida verildi,
    // burada yalnizca yaziliyor. Teshis bitince silinecek.
    private void OdakTeshisYaz(
        string adimTipi,
        UstSeviyePencere? hedef,
        IntPtr onPlandaki,
        bool onPlanOrkada,
        bool hedefEtkin,
        OdakEylemi karar)
    {
        try
        {
            var hedefTutamac = hedef?.Tutamac ?? IntPtr.Zero;

            // Hedefin basligi IKI kere yaziliyor: tarama anindaki (onbellek) ve
            // SIMDIKI (Win32). Ikisi ayrilirsa tutamac baska bir pencereye
            // gecmis demektir ve karar zaten yanlis pencere hakkinda veriliyor.
            _log.Bilgi(
                $"[ODAK-TESHIS] {adimTipi}: karar={karar} | " +
                $"onPlan hwnd=0x{onPlandaki.ToInt64():X} " +
                $"baslik='{Win32Baslik(onPlandaki)}' " +
                $"pid={OrkaPenceresi.PencereSureci(onPlandaki)} " +
                $"etkin={OrkaPenceresi.EtkinMi(onPlandaki)} " +
                $"orkaSurecinde={onPlanOrkada} | " +
                $"hedef hwnd=0x{hedefTutamac.ToInt64():X} " +
                $"baslik(onbellek)='{Baslik(hedef)}' " +
                $"baslik(simdi)='{Win32Baslik(hedefTutamac)}' " +
                $"pid={OrkaPenceresi.PencereSureci(hedefTutamac)} " +
                $"yasiyor={OrkaPenceresi.PencereMi(hedefTutamac)} " +
                $"etkin={hedefEtkin} | " +
                $"orkaPidleri=[{string.Join(",", _bekleyici.OrkaProcessIdleri())}]");
        }
        catch (Exception ex)
        {
            // Teshis satiri gorevi durdurmasin.
            _log.Uyari($"[ODAK-TESHIS] {adimTipi}: teshis yazilamadi: {ex.Message}");
        }
    }
    // ================ GECICI TESHIS SONU ================

    private static string Baslik(UstSeviyePencere? pencere) => pencere?.Baslik ?? "(yok)";

    /// <summary>On plandaki pencerenin basligi; log'da "odak nerede" satiri icin.</summary>
    private static string Win32Baslik(IntPtr tutamac)
    {
        var baslik = PkfRobot.Arayuz.OrkaPenceresi.PencereBasligi(tutamac);
        return string.IsNullOrWhiteSpace(baslik) ? "(basliksiz)" : baslik;
    }

    /// <summary>
    /// Pencereye GORELI oranla fare tiklamasi.
    ///
    /// Neden gerekli: Veri Transferi ekraninda sol panel, grid satirlari ve
    /// "Transfere Basla" butonu klavyeyle erisilemiyor (Ctrl+F yok, F6 yok,
    /// Tab gecmiyor, yazarak arama yok). Bu kontroller UIA'ya da kapali oldugu
    /// icin tiklanacak eleman bulunamiyor; geriye tek yol koordinat kaliyor.
    ///
    /// Piksel yerine ORAN kullaniliyor: ekran cozunurlugu ya da pencere boyu
    /// degisince piksel kayar, oran kaymaz. Sart: pencere TAM EKRAN olmali,
    /// bu yuzden tiklamadan once One Getir + Buyut yapiliyor.
    ///
    /// Adimda <c>CiftTik</c> true ise tek tik yerine CIFT tik gonderilir:
    /// Banka Ekstreleri grid'indeki sablon satirina girmek icin ORKA cift tik
    /// istiyor, tek tik satiri yalnizca secili yapiyor.
    /// </summary>
    private void Tikla(Adim adim, string deger)
    {
        if (adim.X < 0 || adim.X > 1 || adim.Y < 0 || adim.Y > 1)
            throw new ArgumentOutOfRangeException(nameof(adim),
                $"Tikla adiminda X/Y pencereye goreli ORAN olmali (0.0 - 1.0), piksel degil. " +
                $"Gelen: X={adim.X}, Y={adim.Y}");

        // Hedef pencere: adimda baslik verilmisse o, verilmemisse config'deki ana ekran.
        var baslik = string.IsNullOrWhiteSpace(deger) ? _cfg.Pencereler.AnaEkran : deger;
        if (string.IsNullOrWhiteSpace(baslik))
            throw new InvalidOperationException(
                "Tikla adimi icin hedef pencere yok: adimda 'Deger' bos ve " +
                "config'de Pencereler.AnaEkran tanimsiz.");

        // Oranlar JSON'a yapistirilabilsin diye nokta ile yazilir (Turkce locale virgul basar).
        var oran = $"{adim.X.ToString("0.###", CultureInfo.InvariantCulture)} x " +
                   $"{adim.Y.ToString("0.###", CultureInfo.InvariantCulture)}";
        // Tik turu de log'a yaziliyor: "satira girmedi" diye bakan kisi tek tik mi
        // cift tik mi gittigini log'dan gormeli.
        var tikTuru = adim.CiftTik ? "cift tik" : "tek tik";
        var etiket = string.IsNullOrWhiteSpace(adim.Not)
            ? $"oran {oran}, {tikTuru}"
            : $"{adim.Not} (oran {oran}, {tikTuru})";
        _log.Adim("Tikla", etiket);

        // Hedef pencereyi one getirip buyutuyoruz: oranin anlamli olmasi buna bagli.
        // Bu ayni zamanda OdakGuvence'in yaptigi isi de kapsiyor.
        var pencere = _bekleyici.Bekle(baslik, TimeoutCoz(adim));
        _bekleyici.OneGetirVeBuyut(pencere);
        _sonOnPencere = pencere;
        Thread.Sleep(_cfg.Zamanlama.TusBeklemeMs); // buyutme sonrasi olculer otursun

        // Olcu UIA'nin BoundingRectangle'indan degil, tek olcu kaynagindan
        // (OrkaPenceresi.OlcuAl -> Win32 GetWindowRect) aliniyor. Iki kaynak ayni
        // pencere icin farkli dikdortgen dondurebiliyor; kalibrasyon paneli Win32
        // ile olcup robot UIA ile tikladiginda kaymayi kimse fark etmiyordu.
        var hwnd = pencere.Tutamac;
        var r = OrkaPenceresi.OlcuAl(hwnd);
        if (!r.Gecerli)
            throw new InvalidOperationException(
                $"'{baslik}' penceresinin olculeri okunamadi (hwnd=0x{hwnd.ToInt64():X}, " +
                $"G={r.Genislik}, Y={r.Yukseklik}). Pencere simge durumunda kucultulmus olabilir.");

        // Sol/Ust NEGATIF olabilir (ikinci monitor, DWM kenarlik payi); deger
        // kirpilmadan isaretli aritmetikle tasiniyor.
        var mutlakX = (int)Math.Round(r.Sol + r.Genislik * adim.X);
        var mutlakY = (int)Math.Round(r.Ust + r.Yukseklik * adim.Y);

        _log.Bilgi($"Pencere '{pencere.Baslik}': sol={r.Sol} ust={r.Ust} genislik={r.Genislik} " +
                   $"yukseklik={r.Yukseklik} -> tiklanan nokta: ({mutlakX}, {mutlakY})");

        Mouse.MoveTo(mutlakX, mutlakY);
        Thread.Sleep(_cfg.Zamanlama.TusBeklemeMs);

        // Cift tik FlaUI'nin hazir metoduyla gonderiliyor. Iki ayri Mouse.Click
        // arasina kendi beklememizi koysaydik aradaki sure Windows'un
        // GetDoubleClickTime() esigini asabilir ve ORKA iki ayri TEK tik gorurdu.
        if (adim.CiftTik)
            Mouse.DoubleClick(MouseButton.Left);
        else
            Mouse.Click(MouseButton.Left);

        Thread.Sleep(_cfg.Zamanlama.TusBeklemeMs);
    }

    /// <summary>
    /// Adima ozel timeout varsa onu, yoksa config'deki degeri kullanir.
    /// Tek bir yavas adim yuzunden tum gorevin timeout'unu buyutmek zorunda kalmayalim.
    /// </summary>
    private int TimeoutCoz(Adim adim, int? varsayilan = null)
    {
        if (adim.TimeoutSn is > 0)
        {
            _log.Bilgi($"Adima ozel timeout: {adim.TimeoutSn} sn");
            return adim.TimeoutSn.Value;
        }
        return varsayilan ?? _cfg.Zamanlama.PencereTimeoutSn;
    }

    private void OrkaBaslat(int acilisTimeoutSn)
    {
        if (!File.Exists(_cfg.OrkaPath))
            throw new FileNotFoundException($"ORKA bulunamadi: {_cfg.OrkaPath}");

        // Zaten acik mi?
        if (_bekleyici.VarMi(_cfg.Pencereler.AnaEkran) ||
            _bekleyici.VarMi(_cfg.Pencereler.GirisEkrani))
        {
            _log.Uyari("ORKA zaten acik gorunuyor. Yeniden baslatilmiyor.");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _cfg.OrkaPath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(_cfg.OrkaPath) ?? ""
        });

        var el = _bekleyici.Bekle(_cfg.Pencereler.GirisEkrani, acilisTimeoutSn);
        _bekleyici.OneGetirVeBuyut(el);
        _sonOnPencere = el;
    }

    // ================== OTOMATIK PENCERE KAPATMA ==================
    // BeklenmeyenPencereler ile ayni sey DEGIL:
    //   BeklenmeyenPencereler          -> adimdan SONRA bakilir, robot DURUR.
    //   OtomatikKapatilacakPencereler  -> adimdan ONCE bakilir, pencere KAPATILIR,
    //                                     gorev devam eder.
    // Ayrim niyet farki: birincisi "bir sey ters gitti, kor devam etme", ikincisi
    // "bu diyalog isle ilgisiz, yoldan cekilsin".

    /// <summary>
    /// Her adimdan ONCE: kapatilacak pencerelerden biri ekranda mi?
    ///
    /// <b>Neden adimdan once:</b> ORKA'nin yazici baglantisi diyalogu modul ekraninda
    /// rastgele cikiyor, odagi aliyor ve o anda gonderilen tuslari yutuyor. Adimdan
    /// sonra bakmak (SurprizPencereKontrol gibi) zaten kirilmis adimi kurtarmaz.
    ///
    /// Tarama <see cref="OrkaPenceresi.TumUstSeviyePencereler"/> ile: pencere aramanin
    /// geri kalaniyla ayni kaynak, ayni EnumWindows.
    /// </summary>
    private void OtomatikPencereKapat()
    {
        if (_cfg.OtomatikKapatilacakPencereler.Count == 0) return;

        List<UstSeviyePencere> pencereler;
        try
        {
            pencereler = OrkaPenceresi.TumUstSeviyePencereler();
        }
        catch (Exception ex)
        {
            _log.Uyari($"Otomatik kapatma: pencere listesi alinamadi: {ex.Message}");
            return;
        }

        foreach (var kural in _cfg.OtomatikKapatilacakPencereler)
        {
            if (string.IsNullOrWhiteSpace(kural.Baslik)) continue;

            var pencere = PencereBekleyici.Esles(pencereler, kural.Baslik);
            if (pencere is null) continue;

            Kapat(pencere, kural);
        }
    }

    /// <summary>
    /// Pencereyi one getirip once dugmeye, olmazsa ESC'e basar ve kapandigini dogrular.
    /// Kapanmasa bile gorev DURMAZ: bu liste "durdur" listesi degil.
    /// </summary>
    private void Kapat(UstSeviyePencere pencere, KapatilacakPencere kural)
    {
        _log.Uyari($"Otomatik kapatma: '{pencere.Baslik}' penceresi ekranda " +
                   $"(kural: '{kural.Baslik}').");
        _log.EkranAl("otomatik-kapatma-oncesi", zorla: true);

        try
        {
            _bekleyici.OneGetir(pencere);
            Thread.Sleep(_cfg.Zamanlama.TusBeklemeMs);

            if (DugmeyeBas(pencere, kural.Dugme) && KapandiMi(kural.Baslik))
            {
                _log.Bilgi($"Otomatik kapatma: '{pencere.Baslik}' -> '{kural.Dugme}' " +
                           "dugmesiyle kapandi, adima devam ediliyor.");
                return;
            }

            // Dugme yok, basilamadi ya da basildi da pencere duruyor: klavyeye dus.
            //
            // ESC KOR GONDERILMIYOR. Diyalog on plana gelmediyse tus ORKA'nin modul
            // ekranina duser; orada ESC bir seviye geri alir ve arkasindan gelen
            // "SAG x3 / ASAGI x1" gezinmesi bambaska bir yerde calisir. Gorunmeyen
            // bir kapatma denemesi, gorunur bir gezinme hatasina donusuyordu.
            if (!PencereBekleyici.OndeMi(pencere))
            {
                _log.Uyari($"Otomatik kapatma: '{pencere.Baslik}' on plana gelmedi, " +
                           "ESC GONDERILMEDI (tus ORKA ana ekranina duser, gezinmeyi bozardi). " +
                           "Pencere ekranda kalmis olabilir.");
                return;
            }

            if (!OrkaPenceresi.YanitVeriyorMu(pencere.Tutamac, _cfg.Zamanlama.TusBeklemeMs))
                _log.Uyari($"Otomatik kapatma: '{pencere.Baslik}' on planda ama mesajlara " +
                           "yanit vermiyor; ESC yutulabilir.");

            Klavye.Tus("ESC", 1);

            if (KapandiMi(kural.Baslik))
            {
                _log.Bilgi($"Otomatik kapatma: '{pencere.Baslik}' -> ESC ile kapandi, " +
                           "adima devam ediliyor.");
                return;
            }

            _log.Uyari($"Otomatik kapatma: '{pencere.Baslik}' KAPANMADI. Adima yine de " +
                       "devam ediliyor; adim bu pencere yuzunden basarisiz olabilir.");
        }
        catch (Exception ex)
        {
            // Kapatma denemesi gorevi dusurmesin.
            _log.Uyari($"Otomatik kapatma: '{pencere.Baslik}' kapatilamadi: {ex.Message}");
        }
    }

    /// <summary>
    /// Pencere icindeki dugmeye UIA ile basar. Dugme bulunamazsa false doner ve
    /// cagiran ESC'e duser -- ORKA'nin diyaloglari UIA'ya kapali olabiliyor.
    /// </summary>
    private bool DugmeyeBas(UstSeviyePencere pencere, string dugme)
    {
        if (string.IsNullOrWhiteSpace(dugme)) return false;

        try
        {
            var el = _automation.FromHandle(pencere.Tutamac);
            var buton = el?.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                           .FirstOrDefault(b => ButonAdi(b).Contains(dugme, StringComparison.OrdinalIgnoreCase));

            if (buton is null)
            {
                _log.Uyari($"Otomatik kapatma: '{dugme}' dugmesi bulunamadi " +
                           "(diyalog UIA'ya kapali olabilir), ESC denenecek.");
                return false;
            }

            buton.AsButton().Invoke();
            Thread.Sleep(_cfg.Zamanlama.TusBeklemeMs);
            return true;
        }
        catch (Exception ex)
        {
            _log.Uyari($"Otomatik kapatma: '{dugme}' dugmesine basilamadi ({ex.Message}), " +
                       "ESC denenecek.");
            return false;
        }
    }

    /// <summary>Basligi 'parca' iceren pencere ekrandan kalkti mi? En fazla ~1,5 sn bakar.</summary>
    private bool KapandiMi(string baslikParcasi)
    {
        for (var i = 0; i < 5; i++)
        {
            Thread.Sleep(300);
            if (PencereBekleyici.Esles(OrkaPenceresi.TumUstSeviyePencereler(), baslikParcasi) is null)
                return true;
        }

        return false;
    }

    /// <summary>Dugmenin yazisi; okunamazsa bos dizge.</summary>
    private static string ButonAdi(AutomationElement e)
    {
        try { return e.Name ?? ""; } catch { return ""; }
    }

    /// <summary>
    /// Iki listede birden gecen basliklar. Bos degilse ayarlar celisiyor: ayni pencere
    /// hem "durdur" hem "kapat ve devam et" diye isaretlenmis.
    ///
    /// Celiskide KAPATMA kazanir -- adimdan once calistigi icin pencere zaten kapanmis
    /// olur ve durdurma kurali hic tetiklenmez. Sessiz kalmamasi icin gorev basinda
    /// uyariliyor; karar kullanicinin.
    /// </summary>
    public static List<string> CakisanPencereBasliklari(RobotConfig cfg)
    {
        var sonuc = new List<string>();

        foreach (var kural in cfg.OtomatikKapatilacakPencereler)
        {
            if (string.IsNullOrWhiteSpace(kural.Baslik)) continue;

            foreach (var durdur in cfg.BeklenmeyenPencereler)
            {
                if (string.IsNullOrWhiteSpace(durdur)) continue;

                // Iki yonlu bakiliyor: "Yazici" kurali "Yazici baglantisi" durdurma
                // kaydini da yakalamali, tersi de.
                if (durdur.Contains(kural.Baslik, StringComparison.OrdinalIgnoreCase) ||
                    kural.Baslik.Contains(durdur, StringComparison.OrdinalIgnoreCase))
                    sonuc.Add(kural.Baslik);
            }
        }

        return sonuc.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Delphi uygulamalarinda beklenmeyen uyari penceresi cok cikar.
    /// Her adimdan sonra kontrol et; ciktiginda kor devam etme.
    /// </summary>
    private void SurprizPencereKontrol()
    {
        if (_cfg.BeklenmeyenPencereler.Count == 0) return;

        var bulunan = _bekleyici.BeklenmeyenPencereVarMi(_cfg.BeklenmeyenPencereler);
        if (bulunan != null)
        {
            _log.Uyari($"BEKLENMEYEN PENCERE: '{bulunan}'");
            _log.EkranAl("beklenmeyen-pencere", zorla: true);
            throw new InvalidOperationException(
                $"Beklenmeyen pencere acildi: '{bulunan}'. " +
                "Robot durduruldu. Ekran goruntusune bak.");
        }
    }

    /// <summary>
    /// Tus adedini once degiskenden okumayi dener, yoksa JSON'daki Adet'i kullanir.
    /// Boylece "kac kere sag ok" degeri komut satirindan verilebilir:
    ///   --degisken modulSagOk=7
    /// </summary>
    private int AdetCoz(Adim adim)
    {
        if (!string.IsNullOrWhiteSpace(adim.AdetDegisken) &&
            _degiskenler.TryGetValue(adim.AdetDegisken, out var ham) &&
            int.TryParse(ham, out var sayi))
        {
            _log.Bilgi($"Adet degiskenden alindi: {adim.AdetDegisken}={sayi}");
            return sayi;
        }
        return adim.Adet;
    }

    /// <summary>
    /// ORKA gridine karsi hesap kodlarini yazar.
    ///
    /// <b>Neden korlemesine yaziliyor:</b> ORKA'nin gridi (TcxGridSite) UI
    /// Automation'a kapali tek bir blok -- satir/hucre okunamiyor (bkz. OKUBENI).
    /// Yazilan degerin dogru satira gittigini robot ekrandan DOGRULAYAMIYOR.
    /// Bu yuzden guvence yazmadan once aliniyor: satir sayisi sunucuda, indirilen
    /// dosyada ve kod listesinde ayni olmadan is hic baslamiyor
    /// (bkz. OrkayaAktarCalistirici).
    ///
    /// <b>Kaydet'e basilmiyor.</b> Bu adim yalnizca hucrelere yaziyor; kaydetme
    /// kullanicinin isi ve oyle kalacak.
    /// </summary>
    private void GridDoldur(Adim adim)
    {
        // Veri yoksa adim ATLANIYOR, zincir durmuyor.
        //
        // Sunucudan gelen iste bu durum zaten olusamaz: OrkayaAktarCalistirici.Dogrula
        // bos kod listesini is baslamadan reddediyor. Geriye yalnizca elle "--gorev"
        // calistirmasi kaliyor -- oradaki amac akisin geri kalanini (pencereler,
        // koordinatlar, cift tik) denemek ve veri yok diye burada patlamak
        // denemenin kendisini imkansiz kiliyordu.
        if (_gridVerisi is null || _gridVerisi.Satirlar.Count == 0)
        {
            _log.Uyari("GridDoldur: karsi hesap listesi bos, adim ATLANDI. " +
                       "Sunucudan gelen iste bu olusamaz (bos liste is baslamadan reddediliyor); " +
                       "elle calistirmada Calistir sekmesindeki 'Karsi hesap kodlari' " +
                       "alanini doldurunca bu adim da denenebilir.");
            return;
        }

        // Listeyi grid'in satir sayisiyla hizala. Sayi bilinmiyorsa liste
        // oldugu gibi yaziliyor (bkz. GridDoldurVerisi.HedefSatirSayisi).
        var satirlar = HizalanmisSatirlar(_gridVerisi);

        var solaGit = adim.SolaGitAdet ?? _cfg.Grid.SolaGitAdet;
        var tabAdet = adim.TabAdet ?? _cfg.Grid.TabAdet;

        _log.Adim("GridDoldur", $"{satirlar.Count} satir ({_gridVerisi.Kaynak}) " +
                                $"[SOL x{solaGit} -> TAB x{tabAdet}]");

        // Grid'e konumlanmak gorev JSON'unun isi (Tikla); burada yalnizca odagin
        // ORKA'da oldugundan emin olunuyor.
        OdakGuvence("GridDoldur");
        _log.EkranAl("griddoldur-oncesi", zorla: true);

        GridiYaz(satirlar, solaGit, tabAdet,
            satirBasliyor: (no, toplam) =>
            {
                // Her satir ayri satirda log'a dusuyor: "kac satir yazildi ve NEREDE
                // durdu" sorusu, yarim kalan bir grid'de gozle tamamlamanin ilk
                // adimi -- adim sonundaki tek ozet satiri bunu soylemiyordu.
                _log.Bilgi($"[GRID] satir {no}/{toplam} -> '{satirlar[no - 1].KarsiHesapKodu}' yaziliyor");

                OdakGuvence("GridDoldur");
            },
            satirYazildi: (no, toplam) =>
            {
                _gridVerisi.YazilanSatir = no;
                _gridVerisi.SatirYazildi?.Invoke(no, toplam);

                Thread.Sleep(_cfg.Zamanlama.TusBeklemeMs);
            });

        // Yol saklaniyor: dogrulama katmani bu goruntuyu okuyacak (bkz.
        // GridDoldurVerisi.GridGoruntusuYolu). Motor okumayi kendisi yapmiyor.
        _gridVerisi.GridGoruntusuYolu = _log.EkranAl("griddoldur-sonrasi", zorla: true);

        _log.Bilgi($"GridDoldur bitti: {_gridVerisi.YazilanSatir}/{satirlar.Count} satir yazildi. " +
                   "KAYDET'E BASILMADI.");
    }

    /// <summary>
    /// Grid'in TAMAMINI doldurur: BIR KEZ konumlan, sonra her satir icin yaz.
    ///
    /// <b>Neden konumlanma donguden ONCE:</b> LEFT+TAB dizisi once her satirda
    /// tekrarlaniyordu; her tekrar imleci ilk satira geri goturdugu icin kodlar
    /// birbirinin uzerine yazildi. 06.09.2026 12:33 kosusunda uc satirin ucunde
    /// de LEFT x12 + TAB x7 gonderildi ve ekranda yalnizca son kod ('800 01')
    /// kaldi, o da 3. satira dustu. Konumlanma bir kereye inince imlec ilk
    /// satirin Karsi Hesap Kodu hucresinde basliyor, alt satira gecisi satir
    /// sonundaki ENTER+DOWN suruyor.
    ///
    /// Yalniz <see cref="Klavye"/> uzerinden calisiyor, ekrani sorgulamiyor:
    /// gonderilen tus dizisi ORKA olmadan test edilebiliyor. Ekrana bakan isler
    /// (odak guvencesi, log, ilerleme bildirimi) disaridan geri cagirmayla
    /// veriliyor.
    /// </summary>
    /// <param name="satirBasliyor">(satirNo, toplam) -- satir yazilmadan hemen once.</param>
    /// <param name="satirYazildi">(satirNo, toplam) -- satir yazildiktan hemen sonra.</param>
    public static void GridiYaz(IReadOnlyList<GridSatiri> satirlar,
                                int solaGitAdet, int tabAdet,
                                Action<int, int>? satirBasliyor = null,
                                Action<int, int>? satirYazildi = null)
    {
        // BIR KEZ: imlec ilk satirin Karsi Hesap Kodu hucresine getiriliyor.
        GrideKonumlan(solaGitAdet, tabAdet);

        for (var i = 0; i < satirlar.Count; i++)
        {
            var satir = satirlar[i];

            if (string.IsNullOrWhiteSpace(satir.KarsiHesapKodu))
                throw new InvalidOperationException(
                    $"Satir {satir.SiraNo} icin karsi hesap kodu bos ({satir.Aciklama}). " +
                    $"{i} satir yazildi, devam edilmiyor.");

            satirBasliyor?.Invoke(i + 1, satirlar.Count);
            SatiriYaz(satir.KarsiHesapKodu);
            satirYazildi?.Invoke(i + 1, satirlar.Count);
        }
    }

    /// <summary>
    /// Imleci grid'in ILK satirindaki Karsi Hesap Kodu hucresine getirir.
    /// Grid basina YALNIZCA BIR KEZ cagriliyor (bkz. <see cref="GridiYaz"/>).
    ///
    /// <b>Neden korlemesine sayiliyor:</b> grid UIA'ya kapali, imlecin HANGI
    /// kolonda oldugu okunamiyor; tek cozum bilinen bir noktaya donup oradan
    /// saymak (ofiste olculdu 06.09.2026).
    ///
    /// <b>SOL ok neden kolon sayisindan fazla:</b> ilk kolonda sol ok etkisiz
    /// kalir. Fazlasi zararsiz, eksigi hedefi kaydirir -- yani fazla basmak
    /// "hangi kolondaydik" sorusunu sormadan sabit bir baslangic veriyor.
    /// </summary>
    public static void GrideKonumlan(int solaGitAdet, int tabAdet)
    {
        if (solaGitAdet > 0) Klavye.Tus("LEFT", solaGitAdet);
        if (tabAdet > 0) Klavye.Tus("TAB", tabAdet);
    }

    /// <summary>
    /// Imlecin bulundugu hucreye TEK bir kodu yazar, onaylar ve alt satira gecer.
    /// Konumlanma bu metodun isi DEGIL (bkz. <see cref="GrideKonumlan"/>).
    ///
    /// <b>Ctrl+A neden yok:</b> TAB ile girilen hucrede mevcut metin zaten
    /// secili geliyor; ustelik grid hucresinde duzenleme kipi acik degilken
    /// Ctrl+A TUM SATIRLARI secebilir.
    /// </summary>
    public static void SatiriYaz(string kod)
    {
        Klavye.Yaz(kod);

        // ENTER hucreyi onayliyor; Karsi Hesap Adi kolonu ORKA tarafindan
        // kendiliginden doluyor.
        Klavye.Tus("ENTER", 1);

        // Satir atlamayi ENTER'a birakmak, ENTER'in imleci hangi kolonda
        // biraktigina bagli olurdu; ASAGI ok bagimsiz.
        Klavye.Tus("DOWN", 1);
    }

    /// <summary>
    /// Kod listesini grid'in satir sayisiyla hizalar ve farki LOG'A yazar.
    ///
    /// Uc durum var, ucu de adimi DURDURMUYOR -- yarim kalan bir grid gozle
    /// tamamlanabilir, ama robotun ortada durup ekrani bilinmeyen halde
    /// birakmasi sonraki adimlari da bozardi (KAYDET'e zaten kullanici basiyor):
    ///   - sayi bilinmiyor  -> liste bitene kadar yazilir, log'a not dusulur
    ///   - liste EKSIK      -> kalan satirlar bos kalir, uyari
    ///   - liste FAZLA      -> fazlasi yok sayilir, uyari
    ///
    /// Ekrana dokunmadigi icin ORKA olmadan test edilebiliyor.
    /// </summary>
    public static IReadOnlyList<GridSatiri> Hizala(
        IReadOnlyList<GridSatiri> satirlar, int? hedefSatirSayisi, Action<string> uyari,
        Action<string>? bilgi = null)
    {
        if (hedefSatirSayisi is not > 0)
        {
            bilgi?.Invoke($"GridDoldur: grid'in satir sayisi bilinmiyor, " +
                          $"listedeki {satirlar.Count} kod bitene kadar yaziliyor.");
            return satirlar;
        }

        var hedef = hedefSatirSayisi.Value;

        if (satirlar.Count < hedef)
        {
            uyari($"GridDoldur: listede {satirlar.Count} kod var ama grid'de {hedef} satir. " +
                  $"Son {hedef - satirlar.Count} satir BOS birakiliyor, adim durmuyor -- " +
                  "kalanini elle doldurup kaydedin.");
            return satirlar;
        }

        if (satirlar.Count > hedef)
        {
            uyari($"GridDoldur: listede {satirlar.Count} kod var ama grid'de {hedef} satir. " +
                  $"Fazladan {satirlar.Count - hedef} kod YOK SAYILIYOR -- " +
                  "liste ile ekstre ayni ise bu bir uyumsuzluk isareti, gozden gecirin.");
            return satirlar.Take(hedef).ToList();
        }

        return satirlar;
    }

    private IReadOnlyList<GridSatiri> HizalanmisSatirlar(GridDoldurVerisi veri)
        => Hizala(veri.Satirlar, veri.HedefSatirSayisi, _log.Uyari, _log.Bilgi);

    /// <summary>{firmaKodu}, {hesapKodu}, {dosyaYolu} gibi degiskenleri yerine koyar.</summary>
    private string DegiskenleriCoz(string metin)
    {
        if (string.IsNullOrEmpty(metin)) return metin;

        var sonuc = metin;
        foreach (var (anahtar, deger) in _degiskenler)
            sonuc = sonuc.Replace("{" + anahtar + "}", deger);
        return sonuc;
    }

    /// <summary>
    /// Yazilacak deger gercekten YAZILABILIR mi; degilse SEBEBI log'a duser.
    ///
    /// 06.09.2026 22:42 kosusunda firma sifresi popup'ina hicbir karakter gitmedi
    /// ve adimin log'daki tek izi "Yaz -> ***" satiriydi. <see cref="Maskele"/>
    /// ADIM TANIMINA bakiyor (Not/Deger hassas sozcuk iceriyor mu), COZULMUS degere
    /// degil; yani sunlarin ucu de log'da ayni gorunuyordu:
    ///   - deger cozuldu, dolu  -> yazilmali
    ///   - deger cozuldu, BOS   -> klavyeye hicbir sey gitmez (ayarda sifre bos)
    ///   - deger HIC cozulmedi  -> kutuya literal '{firmaSifre}' yazilir
    /// Son ikisi "robot yazmadi" olarak gorunup odak/pencere tarafinda aranmisti.
    ///
    /// Adimi DURDURMUYOR: bos deger gecerli bir istek olabilir (alani temizlemek).
    /// </summary>
    private void YazilacakDegeriDenetle(string adimTipi, Adim adim, string deger)
    {
        if (string.IsNullOrEmpty(deger))
        {
            _log.Uyari($"{adimTipi}: yazilacak deger BOS -- klavyeye hicbir karakter " +
                       "gonderilmeyecek. Adimin tanimli degeri: " +
                       $"'{Maskele(adim, adim.Deger)}'. Degisken/ayar dolduruldu mu?");
            return;
        }

        var cozulmemis = CozulmemisDegiskenler(deger);
        if (cozulmemis.Count > 0)
            _log.Uyari($"{adimTipi}: su degisken(ler) cozulemedi, METIN OLARAK yazilacak: " +
                       string.Join(", ", cozulmemis));
    }

    /// <summary>
    /// Degiskenler cozuldukten sonra metinde kalan '{ad}' kaliplari.
    ///
    /// Cozulmemis bir degisken sessiz kalirsa ORKA'nin kutusuna sifre yerine
    /// '{firmaSifre}' yazilir; ekranda da log'da da bu "yazilmadi" gibi gorunur.
    /// </summary>
    public static IReadOnlyList<string> CozulmemisDegiskenler(string? deger)
        => string.IsNullOrEmpty(deger)
            ? Array.Empty<string>()
            : System.Text.RegularExpressions.Regex
                .Matches(deger, @"\{[A-Za-z][A-Za-z0-9_]*\}")
                .Select(m => m.Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();

    private static string Maskele(Adim adim, string deger)
    {
        // Sifre gibi hassas alanlar log'a duz yazilmasin.
        // Sadece {sifre} aramak yetmiyordu: {firmaSifre} iceren adimlar log'a
        // duz yaziliyordu. Artik hassas sayilan bir sozcuk gecen her Deger
        // maskeleniyor -- listede "sifre"nin yaninda ajan anahtarini ve
        // token'i tarif eden sozcukler de var.
        return Hassas.Iceriyor(adim.Not) || Hassas.Iceriyor(adim.Deger)
            ? "***"
            : deger;
    }
}
