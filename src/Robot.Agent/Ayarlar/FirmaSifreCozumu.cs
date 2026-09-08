namespace PkfRobot.Ayarlar;

/// <summary>Bir sifrenin NEREDEN geldigi. Log satiri bunu yaziyor.</summary>
public enum SifreKaynagi
{
    /// <summary>Hicbir yerde yok; is baslamamali.</summary>
    Yok,

    /// <summary>Firma bazli DPAPI kaydi (sifreler.dat). Asil kaynak.</summary>
    FirmaKaydi,

    /// <summary>Tek alanlik eski DPAPI kaydi. Yedek.</summary>
    EskiTekAlan,

    /// <summary>appsettings.json &gt; Giris. Geriye donuk uyumluluk yedegi.</summary>
    Appsettings
}

/// <summary>Cozulen tek bir sifre ve nereden geldigi.</summary>
public sealed record SifreDegeri(string Deger, SifreKaynagi Kaynak)
{
    public static readonly SifreDegeri Yok = new(string.Empty, SifreKaynagi.Yok);

    public bool Var => Kaynak != SifreKaynagi.Yok;

    /// <summary>Log'a yazilabilir kaynak adi. <b>Sifrenin kendisi asla yazilmiyor.</b></summary>
    public string KaynakAdi => Kaynak switch
    {
        SifreKaynagi.FirmaKaydi  => "firma bazli kayit (sifreler.dat)",
        SifreKaynagi.EskiTekAlan => "tek alanlik eski kayit (sifreler.dat) - YEDEK",
        SifreKaynagi.Appsettings => "appsettings.json > Giris - YEDEK",
        _                        => "yok"
    };
}

/// <summary>Bir firmanin iki sifresi, kaynaklariyla birlikte.</summary>
public sealed record FirmaSifreCozumu(string FirmaKodu, SifreDegeri Orka, SifreDegeri Firma)
{
    public bool Eksik => !Orka.Var || !Firma.Var;

    /// <summary>
    /// Is baslamadan once log'a ve hataya giden mesaj.
    ///
    /// Ilk cumle bilerek sabit ve eylemi soyluyor: ofiste bu satiri okuyan kisi
    /// nereye gidecegini bilmeli. Hangi sifrenin eksik oldugu parantezde.
    /// </summary>
    public string EksikMesaji =>
        $"{FirmaKodu} firmasi icin sifre kaydi yok, PkfRobot arayuzunde Calistir " +
        $"sekmesinden girin. (Eksik: {string.Join(", ", EksikOlanlar)})";

    public IReadOnlyList<string> EksikOlanlar
    {
        get
        {
            var eksik = new List<string>();
            if (!Orka.Var) eksik.Add("ORKA giris sifresi");
            if (!Firma.Var) eksik.Add("firma sifresi");
            return eksik;
        }
    }

    /// <summary>
    /// "Hangi sifre nereden geldi" satirlari.
    ///
    /// <b>Neden log'a yaziliyor:</b> appsettings'teki tek alan yedek olarak
    /// duruyor ve sessizce devreye girerse -- ornegin firma bazli kayit
    /// yanlis koda yazildiysa -- robot BASKA bir firmanin sifresiyle ORKA'ya
    /// girmeyi dener. Yanlis sifre ORKA'yi kilitliyor; hangi kaynagin
    /// kullanildigi gorunmeli.
    /// </summary>
    public IReadOnlyList<string> LogSatirlari => new[]
    {
        $"Firma {FirmaKodu} ORKA giris sifresi: {Orka.KaynakAdi}.",
        $"Firma {FirmaKodu} firma sifresi: {Firma.KaynakAdi}."
    };
}

/// <summary>
/// "Bu firma icin hangi sifre kullanilacak" karari -- TEK YERDE.
///
/// <b>Neden ayri bir sinif:</b> ayni soruyu uc yol soruyor (Calistir sekmesi,
/// ajan modu, konsol gorevi) ve daha once her biri kendi cevabini veriyordu:
/// arayuz firma bazli depoyu okurken ajan <c>appsettings.Giris</c>'teki TEK
/// alani okuyordu. Birden fazla firmayla calisirken ajan her firmaya ayni
/// sifreyi denerdi. Karar ekrana ve DPAPI'ye dokunmadigi icin ORKA olmadan
/// test edilebiliyor -- <see cref="PkfRobot.Core.OdakKarari"/> kalibinin aynisi.
///
/// <b>Oncelik:</b> firma bazli kayit &gt; tek alanlik eski kayit &gt; appsettings.
/// Sonuncusu geriye donuk uyumluluk icin duruyor; tek firmayla calisan eski
/// kurulumlar bozulmasin diye.
/// </summary>
public static class FirmaSifreCozucu
{
    public static FirmaSifreCozumu Coz(Sifreler? depo, string? firmaKodu,
                                       string? appsettingsOrka, string? appsettingsFirma)
    {
        var kod = (firmaKodu ?? string.Empty).Trim();

        return new FirmaSifreCozumu(
            kod.Length > 0 ? kod : "(kodsuz)",
            Sec(depo?.FirmaBazliOrkaSifresi(kod), depo?.OrkaSifresi, appsettingsOrka),
            Sec(depo?.FirmaBazliFirmaSifresi(kod), depo?.FirmaSifresi, appsettingsFirma));
    }

    private static SifreDegeri Sec(string? firmaBazli, string? eskiTekAlan, string? appsettings)
    {
        if (!string.IsNullOrEmpty(firmaBazli))
            return new SifreDegeri(firmaBazli, SifreKaynagi.FirmaKaydi);

        if (!string.IsNullOrEmpty(eskiTekAlan))
            return new SifreDegeri(eskiTekAlan, SifreKaynagi.EskiTekAlan);

        if (!string.IsNullOrEmpty(appsettings))
            return new SifreDegeri(appsettings, SifreKaynagi.Appsettings);

        return SifreDegeri.Yok;
    }
}
