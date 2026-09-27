namespace PkfRobot.Ayarlar;

/// <summary>ORKA'da acilan firma: kod + aciklama.</summary>
public record OrkaFirmasi(string Kod, string Ad)
{
    /// <summary>Acilir listede gorunen metin.</summary>
    public override string ToString() => string.IsNullOrWhiteSpace(Ad) ? Kod : $"{Ad} ({Kod})";
}

/// <summary>
/// Calistir sekmesindeki firma listesi.
///
/// <b>Artik SABIT DEGIL.</b> Liste once iki firmaya (0001, 0336) gomuluydu;
/// birden fazla firmayla calisilinca ucuncu firma icin kod degistirmek gerekiyordu.
/// Liste kullanicinin ayarlarina tasindi (<see cref="CalistirmaAyari.Firmalar"/>),
/// buradakiler yalnizca ILK ACILIS degerleri.
///
/// <b>Neden hala acilir liste, elle yazilan kutu degil:</b> ORKA giris zincirinde
/// F7'den sonra yazilan sey bu kod ve yanlis kod = yanlis firmaya aktarim. Kod bir
/// kez, bilerek, "Firma ekle" ekraninda yaziliyor; her calistirmada degil.
/// </summary>
public static class Firmalar
{
    public const string PkfAdayKodu = "0001";
    public const string SmmmKodu = "0336";

    /// <summary>Ilk acilistaki liste; kullanici ekleyip cikardikca ayarlarda degisir.</summary>
    public static IReadOnlyList<OrkaFirmasi> Hepsi { get; } = new[]
    {
        new OrkaFirmasi(PkfAdayKodu, "PKF Aday"),
        new OrkaFirmasi(SmmmKodu, "PKF Istanbul SMMM")
    };

    public static List<OrkaFirmasi> Varsayilanlar() => Hepsi.ToList();

    /// <summary>Varsayilan listede arar; ayarlardaki liste icin diger asiri yukleme.</summary>
    public static OrkaFirmasi Bul(string? kod) => Bul(Hepsi, kod) ?? Hepsi[0];

    /// <summary>Kullanicinin listesinde arar; liste bossa null.</summary>
    public static OrkaFirmasi? Bul(IEnumerable<OrkaFirmasi>? liste, string? kod)
    {
        if (liste is null) return null;

        var hepsi = liste as IReadOnlyList<OrkaFirmasi> ?? liste.ToList();

        return hepsi.FirstOrDefault(f => string.Equals(f.Kod, (kod ?? "").Trim(),
                                                       StringComparison.OrdinalIgnoreCase))
               ?? hepsi.FirstOrDefault();
    }
}

/// <summary>
/// Firma listesine ekleme/cikarma kurallari.
///
/// <b>Neden arayuzden ayri:</b> "ayni kod iki kere eklenirse hangi sifre gecerli"
/// ve "son firma da cikarilirsa ne olur" sorularinin cevabi WinForms olmadan test
/// edilebilmeli. Ikisi de sessizce yanlis calisabilecek turden: cift kod, sifre
/// kaydinin hangi firmaya ait oldugunu belirsiz yapar.
/// </summary>
public static class FirmaListesi
{
    /// <summary>Eklenebilir mi; eklenemezse KULLANICIYA gosterilecek sebep.</summary>
    public static string? EkleHatasi(IReadOnlyList<OrkaFirmasi> mevcut, string? kod)
    {
        var temiz = (kod ?? string.Empty).Trim();

        if (temiz.Length == 0)
            return "Firma kodu bos olamaz. ORKA'da F7'den sonra yazilan kod ne ise o.";

        if (mevcut.Any(f => string.Equals(f.Kod, temiz, StringComparison.OrdinalIgnoreCase)))
            return $"{temiz} zaten listede. Ayni kod iki kere olamaz: hangi sifrenin " +
                   "o firmaya ait oldugu belirsiz kalirdi.";

        return null;
    }

    /// <summary>Cikarilabilir mi; cikarilamazsa sebep.</summary>
    public static string? CikarHatasi(IReadOnlyList<OrkaFirmasi> mevcut, string? kod)
    {
        var temiz = (kod ?? string.Empty).Trim();

        if (mevcut.Count <= 1)
            return "Son firma cikarilamaz; liste bos kalirsa hicbir gorev calistirilamaz.";

        if (!mevcut.Any(f => string.Equals(f.Kod, temiz, StringComparison.OrdinalIgnoreCase)))
            return $"{temiz} listede degil.";

        return null;
    }
}

/// <summary>
/// Calistir sekmesindeki tek bir banka satiri: BIR banka, kendi hesap kodu,
/// kendi ekstre dosyasi.
///
/// <b>Neden dosya adindan banka tahmin edilmiyor:</b> dosyayi kullanici zaten
/// hangi bankaya aitse o satirda seciyor. Tahmin, yanlis bankanin ekstresini
/// dogru gorunen bir kodla ORKA'ya aktarma riski demek olurdu; kazanci ise bir
/// tiklama.
/// </summary>
public class BankaSatiri
{
    public string Banka { get; set; } = string.Empty;

    /// <summary>Isaretli degilse bu satir atlanir.</summary>
    public bool Secili { get; set; }

    /// <summary>Bu bankanin ekstre dosyasi. Bos ise satir islenmez.</summary>
    public string DosyaYolu { get; set; } = string.Empty;

    /// <summary>
    /// Firma kodu -> ORKA hesap kodu. Hesap plani firmadan firmaya degistigi icin
    /// tek bir alan degil, firma basina ayri deger tutuluyor: 0001'in
    /// "102 1 1 VAK-01" kodu 0336'da baska bir hesabi gosterebilir.
    /// </summary>
    public Dictionary<string, string> HesapKodlari { get; set; } = new();

    public string HesapKodu(string firmaKodu)
        => HesapKodlari.TryGetValue(firmaKodu ?? "", out var kod) ? kod : string.Empty;

    public void HesapKoduYaz(string firmaKodu, string? kod)
    {
        if (string.IsNullOrWhiteSpace(firmaKodu)) return;
        HesapKodlari[firmaKodu] = (kod ?? string.Empty).Trim();
    }

    /// <summary>
    /// Firma kodu -> ELLE girilen karsi hesap kodu listesi (ham metin).
    ///
    /// <b>Ne ise yariyor:</b> <c>GridDoldur</c> zincirin ORKA'ya gercekten veri
    /// yazan tek adimi ama listesi sunucudan geliyor; elle calistirmada liste
    /// bos oldugu icin adim uyari yazip ATLANIYOR ve hic denenmiyordu. Bu alan
    /// doluysa GridDoldur bu listeyi kullaniyor -- sunucuya baglanmadan test.
    ///
    /// <b>Neden firma basina:</b> hesap plani firmadan firmaya degisiyor
    /// (<see cref="HesapKodlari"/> ile ayni gerekce). Tek alan olsaydi 0001 icin
    /// girilen kodlar firma 0336 secilince sessizce onun grid'ine yazilirdi.
    ///
    /// <b>Neden HAM metin:</b> kullanicinin yazdigi bicim (satir sonlari, sira)
    /// aynen geri gelsin; ayristirma <see cref="PkfRobot.Core.KarsiHesapListesi"/>
    /// isi ve okuma aninda yapiliyor.
    /// </summary>
    public Dictionary<string, string> KarsiHesapKodlari { get; set; } = new();

    public string KarsiHesaplar(string firmaKodu)
        => KarsiHesapKodlari.TryGetValue(firmaKodu ?? "", out var metin) ? metin : string.Empty;

    public void KarsiHesaplarYaz(string firmaKodu, string? metin)
    {
        if (string.IsNullOrWhiteSpace(firmaKodu)) return;
        KarsiHesapKodlari[firmaKodu] = metin ?? string.Empty;
    }
}

/// <summary>
/// Calistir sekmesinin diskte kalan hali: secili firma, zamanlama, hiz carpani,
/// gorev dosyalari ve banka satirlari.
///
/// <b>Neden appsettings.json degil:</b> appsettings publish ile her guncellemede
/// uzerine yaziliyor. Kullanicinin sectigi dosya yollari ve duzelttigi hesap
/// kodlari orada dursa her yayinda silinirdi -- ayarlar.json ve ajan anahtariyla
/// ayni gerekce (bkz. <see cref="AyarDeposu"/>).
/// </summary>
public class CalistirmaAyari
{
    /// <summary>Secili firma; ORKA giris zincirine {firmaKodu} olarak gider.</summary>
    public string FirmaKodu { get; set; } = PkfRobot.Ayarlar.Firmalar.PkfAdayKodu;

    /// <summary>
    /// Calisilan firmalar: kod + aciklama. Kullanici Calistir sekmesinden ekleyip
    /// cikarabiliyor.
    ///
    /// <b>Sifreler BURADA DEGIL.</b> ayarlar.json duz metin ve yedeklenebiliyor;
    /// sifreler DPAPI ile sifreli <c>sifreler.dat</c> icinde, ayni firma koduyla
    /// eslenerek duruyor (bkz. <see cref="Sifreler"/>).
    /// </summary>
    public List<OrkaFirmasi> Firmalar { get; set; } = new();

    /// <summary>
    /// Kuyruk basinda BIR KEZ calisan gorev (ORKA'yi acar, firmaya girer,
    /// TRANSFERLER'e gecer). Bos ise atlanir.
    ///
    /// <b>Varsayilan neden acilis.json:</b> onceki varsayilan
    /// 01-orka-ac-firma-sec.json modul ekraninda BITIYORDU; modul gezinmesi
    /// 02-modul-ve-sekme.json'da duruyor ve tek dosyalik bu alan onu hic
    /// calistirmiyordu. acilis.json ikisini <c>AltGorev</c> ile birlestiriyor
    /// (adim kopyasi yok, bkz. <see cref="PkfRobot.Config.Gorev"/>).
    /// Eski deger diskteki ayarlar.json'da kalmis olabilir; goc
    /// <c>CalistirPaneli.GorevListesiniDoldur</c> icinde.
    /// </summary>
    public string AcilisGorevi { get; set; } = "acilis.json";

    /// <summary>Eski varsayilan; yalnizca goc kontrolu icin duruyor.</summary>
    public const string EskiAcilisGorevi = "01-orka-ac-firma-sec.json";

    /// <summary>Her banka satiri icin ayri ayri calisan gorev.</summary>
    public string BankaGorevi { get; set; } = "tek-dosya-aktar.json";

    // ---- Zamanlama: varsayilanlar appsettings.json'daki CALISAN degerler ----
    // Sifirdan baslamiyoruz; ofiste calisan ayar bu.
    public int AdimBeklemeMs { get; set; } = 700;
    public int TusBeklemeMs { get; set; } = 150;
    public int PencereTimeoutSn { get; set; } = 40;
    public int OrkaAcilisTimeoutSn { get; set; } = 90;

    /// <summary>
    /// GridDoldur'daki tuslar arasi bekleme; <see cref="TusBeklemeMs"/> yerine
    /// yalnizca o adimda kullaniliyor (bkz.
    /// <see cref="PkfRobot.Config.GridAyar.GridTusBeklemeMs"/>).
    ///
    /// Ayri alan olmasinin sebebi olcum: 43 satirlik bir ekstrede satir basina
    /// 5 tus dusuyor ve 150 ms'lik ortak degerle yalnizca bekleme 48 saniye
    /// ediyordu. Grid'de beklenecek bir pencere/modul yok, o yuzden varsayilan
    /// da daha kisa.
    /// </summary>
    public int GridTusBeklemeMs { get; set; } = 80;

    /// <summary>
    /// Gorev JSON'undaki <c>Bekle</c> adimlarinin <c>Sayi</c> degerini olcekler.
    /// 1.0 = dosyadaki degerler, 2.0 = iki kati bekleme (yavas gun), 0.5 = yarisi.
    /// JSON dosyalari DEGISMIYOR; olcekleme calisma aninda, bellekteki kopyada.
    /// </summary>
    public double HizCarpani { get; set; } = 1.0;

    public List<BankaSatiri> Bankalar { get; set; } = new();

    /// <summary>
    /// Ekran goruntusunden okunan baslangic kodlari. <b>Kullanici dogrulayacak:</b>
    /// yanlis hesap kodu, ekstrenin yanlis hesaba aktarilmasi demek. 0336 (SMMM)
    /// icin bilerek bos -- oradaki hesap plani bilinmiyor, kullanici doldursun.
    /// </summary>
    public static List<BankaSatiri> VarsayilanBankalar() => new()
    {
        Satir("Vakifbank", "102 1 1 VAK-01"),
        Satir("Ziraat", "102 1 1 ZIR-01"),
        Satir("Akbank", "102 1 1 AKB-01"),
        Satir("Is Bankasi", "102 1 1 ISB-02")
    };

    private static BankaSatiri Satir(string banka, string pkfAdayKodu) => new()
    {
        Banka = banka,
        Secili = false,
        HesapKodlari = new Dictionary<string, string>
        {
            [PkfRobot.Ayarlar.Firmalar.PkfAdayKodu] = pkfAdayKodu,
            [PkfRobot.Ayarlar.Firmalar.SmmmKodu] = string.Empty
        }
    };

    /// <summary>Ilk acilista tablo bos gelmesin; kayitli satirlar varsa dokunulmaz.</summary>
    public CalistirmaAyari VarsayilanlariTamamla()
    {
        if (Bankalar.Count == 0) Bankalar = VarsayilanBankalar();

        // Firma listesi bos ise (ilk acilis ya da liste alanini tanimayan eski
        // ayarlar.json) ofiste kullanilan iki firmayla baslasin.
        if (Firmalar.Count == 0) Firmalar = PkfRobot.Ayarlar.Firmalar.Varsayilanlar();

        if (string.IsNullOrWhiteSpace(FirmaKodu)) FirmaKodu = PkfRobot.Ayarlar.Firmalar.PkfAdayKodu;

        // Secili firma listeden cikarilmis olabilir; ilk firmaya donulsun ki
        // gorev hic var olmayan bir koda aktarim denemesin.
        if (PkfRobot.Ayarlar.Firmalar.Bul(Firmalar, FirmaKodu) is { } secili) FirmaKodu = secili.Kod;
        if (HizCarpani <= 0) HizCarpani = 1.0;

        return this;
    }
}
