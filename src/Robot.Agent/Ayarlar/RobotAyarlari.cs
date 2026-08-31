namespace PkfRobot.Ayarlar;

/// <summary>Kalibre edilmis tek bir koordinat.</summary>
public class KoordinatAyari
{
    /// <summary><see cref="KoordinatKesfi.Anahtar"/> ile uretilir.</summary>
    public string Anahtar { get; set; } = string.Empty;

    /// <summary>
    /// Olcum sirasindaki adim aciklamasi. Yalniz gostermek icin degil:
    /// gorev dosyasi degistiginde kaydin hala ayni noktayi mi gosterdigini
    /// anlamanin tek yolu (bkz. <see cref="KalibrasyonUygulama"/>).
    /// </summary>
    public string Not { get; set; } = string.Empty;

    public double X { get; set; }
    public double Y { get; set; }

    /// <summary>Ne zaman olculdu; "bu koordinat ne kadar eski" sorusu icin.</summary>
    public DateTime Olculdu { get; set; } = DateTime.Now;
}

/// <summary>
/// Makinede kalan ayarlar. <b>Sifreler burada degil</b> -- onlar
/// <see cref="SifreDeposu"/> icinde DPAPI ile sifreli duruyor, bu dosya duz
/// metin JSON.
/// </summary>
public class RobotAyarlari
{
    // ---- yollar ----
    public string OrkaExeYolu { get; set; } = string.Empty;
    public string IsDosyalariKlasoru { get; set; } = string.Empty;
    public string LogKlasoru { get; set; } = string.Empty;

    // ---- ORKA giris ----
    /// <summary>ORKA giris zincirinde F7 sonrasi girilen firma kodu (or. "0001").</summary>
    public string FirmaKodu { get; set; } = string.Empty;

    /// <summary>ORKA kullanici kodu (or. "pkf03").</summary>
    public string KullaniciKodu { get; set; } = string.Empty;

    // ---- arayuz ----
    public bool HerZamanUstte { get; set; }

    /// <summary>Kapatma dugmesi tepsiye indirsin mi? Kapali ise uygulamayi kapatir.</summary>
    public bool KapatinceTepsiyeIn { get; set; } = true;

    /// <summary>Uygulama acilinca ajan baglantisi kendiliginden baslasin mi?</summary>
    public bool AcilistaBaglan { get; set; } = true;

    // ---- kalibrasyon ----
    public List<KoordinatAyari> Koordinatlar { get; set; } = new();

    public KoordinatAyari? Koordinat(string anahtar)
        => Koordinatlar.FirstOrDefault(k =>
               string.Equals(k.Anahtar, anahtar, StringComparison.OrdinalIgnoreCase));

    /// <summary>Ayni anahtar iki kez birikmesin: varsa uzerine yazilir.</summary>
    public void KoordinatYaz(string anahtar, string not, double x, double y)
    {
        var mevcut = Koordinat(anahtar);
        if (mevcut is null)
        {
            Koordinatlar.Add(new KoordinatAyari { Anahtar = anahtar, Not = not, X = x, Y = y });
            return;
        }

        mevcut.Not = not;
        mevcut.X = x;
        mevcut.Y = y;
        mevcut.Olculdu = DateTime.Now;
    }

    public bool KoordinatSil(string anahtar)
    {
        var mevcut = Koordinat(anahtar);
        return mevcut is not null && Koordinatlar.Remove(mevcut);
    }
}
