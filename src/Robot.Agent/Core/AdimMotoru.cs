using System.Diagnostics;
using System.Globalization;
using FlaUI.Core.Input;
using FlaUI.UIA3;
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
    }

    public void Calistir(Gorev gorev)
    {
        _log.Bilgi($"Gorev: {gorev.Ad} ({gorev.Adimlar.Count} adim)");
        if (_cfg.DryRun)
            _log.Uyari("DRY RUN AKTIF - 'OnayGerekir' adimlari ATLANACAK, kayit yapilmayacak.");

        foreach (var adim in gorev.Adimlar)
        {
            try
            {
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
                OdakGuvence("Yaz");
                Klavye.Yaz(deger);
                break;

            case "temizleyaz":
                _log.Adim("TemizleYaz", Maskele(adim, deger));
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
    /// Klavyeye dokunmadan once ORKA on planda mi diye bakar, degilse one getirir.
    ///
    /// Ofis testinde robot ORKA yerine cmd penceresine yazdi ve SIFRE cmd'ye gitti;
    /// bu metot onu engelliyor. Her gorevin basina elle BeklePencere koymak da
    /// gerekmiyor artik.
    ///
    /// Kontrol BASLIK degil PROCESS uzerinden: ORKA'nin kendi acdigi Excel dosya
    /// secim diyalogu da ORKA sayilir, ondan odagi calip ana pencereye donmeyiz.
    ///
    /// Tikla adiminda cagrilmiyor - orada zaten hedef pencere one getiriliyor,
    /// ustune ana pencereyi one almak popup'i arkada birakirdi.
    /// </summary>
    private void OdakGuvence(string adimTipi)
    {
        if (!_cfg.OtomatikOneGetir) return;

        try
        {
            if (_bekleyici.OdakOrkadaMi()) return;

            var hedef = _bekleyici.OrkaOnPenceresi();
            if (hedef == null)
            {
                _log.Uyari($"{adimTipi}: ORKA penceresi bulunamadi, odak duzeltilemedi. " +
                           "Tuslar baska bir pencereye gidebilir.");
                return;
            }

            _log.Uyari($"{adimTipi}: odak ORKA'da degildi -> '{hedef.Name}' one getiriliyor.");
            _bekleyici.OneGetir(hedef);
            Thread.Sleep(_cfg.Zamanlama.TusBeklemeMs);
        }
        catch (Exception ex)
        {
            // Odak kontrolu adimi patlatmasin; uyarip devam et.
            _log.Uyari($"{adimTipi}: odak kontrolu yapilamadi: {ex.Message}");
        }
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
        var etiket = string.IsNullOrWhiteSpace(adim.Not)
            ? $"oran {oran}"
            : $"{adim.Not} (oran {oran})";
        _log.Adim("Tikla", etiket);

        // Hedef pencereyi one getirip buyutuyoruz: oranin anlamli olmasi buna bagli.
        // Bu ayni zamanda OdakGuvence'in yaptigi isi de kapsiyor.
        var pencere = _bekleyici.Bekle(baslik, TimeoutCoz(adim));
        _bekleyici.OneGetirVeBuyut(pencere);
        Thread.Sleep(_cfg.Zamanlama.TusBeklemeMs); // buyutme sonrasi olculer otursun

        var r = pencere.BoundingRectangle;
        if (r.Width <= 0 || r.Height <= 0)
            throw new InvalidOperationException(
                $"'{baslik}' penceresinin olculeri okunamadi (G={r.Width}, Y={r.Height}). " +
                "Pencere simge durumunda kucultulmus olabilir.");

        var mutlakX = (int)Math.Round(r.X + r.Width * adim.X);
        var mutlakY = (int)Math.Round(r.Y + r.Height * adim.Y);

        _log.Bilgi($"Pencere '{pencere.Name}': sol={r.X} ust={r.Y} genislik={r.Width} " +
                   $"yukseklik={r.Height} -> tiklanan nokta: ({mutlakX}, {mutlakY})");

        Mouse.MoveTo(mutlakX, mutlakY);
        Thread.Sleep(_cfg.Zamanlama.TusBeklemeMs);
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
        if (_gridVerisi is null || _gridVerisi.Satirlar.Count == 0)
            throw new InvalidOperationException(
                "GridDoldur adimi icin veri verilmedi. Bu adim yalnizca ORKA aktarim " +
                "isinde, kod listesiyle birlikte calisir.");

        var satirlar = _gridVerisi.Satirlar;
        _log.Adim("GridDoldur", $"{satirlar.Count} satir");

        // Ilk hucreye konumlanmak gorev JSON'unun isi (Tikla); burada yalnizca
        // odagin ORKA'da oldugundan emin olunuyor.
        OdakGuvence("GridDoldur");
        _log.EkranAl("griddoldur-oncesi", zorla: true);

        for (var i = 0; i < satirlar.Count; i++)
        {
            var satir = satirlar[i];

            if (string.IsNullOrWhiteSpace(satir.KarsiHesapKodu))
                throw new InvalidOperationException(
                    $"Satir {satir.SiraNo} icin karsi hesap kodu bos ({satir.Aciklama}). " +
                    $"{_gridVerisi.YazilanSatir} satir yazildi, devam edilmiyor.");

            OdakGuvence("GridDoldur");
            Klavye.TemizleVeYaz(satir.KarsiHesapKodu);

            // ENTER hucreyi onaylayip bir alt satira geciyor: ORKA gridinde
            // gezinmenin klavyeyle calisan tek yolu.
            Klavye.Tus("ENTER", 1);

            _gridVerisi.YazilanSatir = i + 1;
            _gridVerisi.SatirYazildi?.Invoke(i + 1, satirlar.Count);

            Thread.Sleep(_cfg.Zamanlama.TusBeklemeMs);
        }

        _log.EkranAl("griddoldur-sonrasi", zorla: true);
        _log.Bilgi($"GridDoldur bitti: {_gridVerisi.YazilanSatir}/{satirlar.Count} satir yazildi. " +
                   "KAYDET'E BASILMADI.");
    }

    /// <summary>{firmaKodu}, {hesapKodu}, {dosyaYolu} gibi degiskenleri yerine koyar.</summary>
    private string DegiskenleriCoz(string metin)
    {
        if (string.IsNullOrEmpty(metin)) return metin;

        var sonuc = metin;
        foreach (var (anahtar, deger) in _degiskenler)
            sonuc = sonuc.Replace("{" + anahtar + "}", deger);
        return sonuc;
    }

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
