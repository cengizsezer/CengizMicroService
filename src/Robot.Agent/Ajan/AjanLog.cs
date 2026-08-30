using System.Text;
using System.Text.RegularExpressions;

namespace PkfRobot.Ajan;

/// <summary>
/// Ajanin log agzi. Arayuz olmasinin tek sebebi test: baglanti mantigini
/// sinarken diske dosya acilmasin.
/// </summary>
public interface IAjanLog
{
    void Bilgi(string mesaj);
    void Uyari(string mesaj);
    void Hata(string mesaj);
}

/// <summary>
/// Log satirlarindaki sirlari son anda temizleyen agiz.
///
/// Ajan anahtari zaten hicbir yere yazilmiyor -- bu, "yazilmasin" kuralinin
/// koda yerlesmis hali. Ileride biri hata mesajina yanlislikla anahtari koyarsa
/// diske duz metin dusmesin diye burada bir kez daha eleniyor.
/// </summary>
public static class AjanLogMaskesi
{
    // pkfr_ ile baslayan ajan anahtari.
    private static readonly Regex Anahtar =
        new(@"pkfr_[A-Za-z0-9_\-]{6,}", RegexOptions.Compiled);

    // Uc parcali JWT (ajan token'i). Log'a token dusmesinin de anlami yok.
    private static readonly Regex Jwt =
        new(@"eyJ[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]{5,}", RegexOptions.Compiled);

    public static string Maskele(string? metin)
    {
        if (string.IsNullOrEmpty(metin)) return metin ?? string.Empty;

        var sonuc = Anahtar.Replace(metin, "pkfr_***");
        return Jwt.Replace(sonuc, "***token***");
    }
}

/// <summary>
/// Ajanin gunluk log dosyasi.
///
/// <b>Neden <see cref="PkfRobot.Core.AdimLogger"/> kullanilmadi:</b> o, bir
/// gorev calistirmasi icin klasor acip ekran goruntusu biriktiriyor -- omru
/// dakikalarla olculen bir is icin dogru. Ajan gunlerce ayakta duruyor; her
/// acilista yeni klasor acmak ve tek dosyayi sinirsiz buyutmek ayni sey degil.
/// Bu yuzden ayri, ama ayni bicimde yazan (saat + seviye + mesaj) kucuk bir
/// gunluk dosya. Yeni kutuphane eklenmedi.
/// </summary>
public sealed class AjanDosyaLog : IAjanLog, IDisposable
{
    private readonly string _klasor;
    private readonly int _saklamaGun;
    private readonly object _kilit = new();

    private StreamWriter? _yazici;
    private DateTime _dosyaGunu = DateTime.MinValue;

    public AjanDosyaLog(string klasor, int saklamaGun = 14)
    {
        _klasor = klasor;
        _saklamaGun = Math.Max(1, saklamaGun);
        Directory.CreateDirectory(_klasor);
        EskileriTemizle();
    }

    public string Klasor => _klasor;

    public void Bilgi(string mesaj) => Yaz("BILGI", mesaj);
    public void Uyari(string mesaj) => Yaz("UYARI", mesaj);
    public void Hata(string mesaj) => Yaz("HATA ", mesaj);

    private void Yaz(string seviye, string mesaj)
    {
        var temiz = AjanLogMaskesi.Maskele(mesaj);
        var satir = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{seviye}] {temiz}";

        lock (_kilit)
        {
            GunuKontrolEt();
            _yazici?.WriteLine(satir);
        }

        Console.WriteLine(satir);
    }

    /// <summary>Gun degistiyse yeni dosyaya gecer ve eskileri siler.</summary>
    private void GunuKontrolEt()
    {
        var bugun = DateTime.Now.Date;
        if (_yazici is not null && _dosyaGunu == bugun) return;

        _yazici?.Dispose();
        _dosyaGunu = bugun;
        _yazici = new StreamWriter(
            Path.Combine(_klasor, $"ajan-{bugun:yyyy-MM-dd}.log"), append: true, Encoding.UTF8)
        {
            AutoFlush = true
        };

        EskileriTemizle();
    }

    private void EskileriTemizle()
    {
        try
        {
            var esik = DateTime.Now.Date.AddDays(-_saklamaGun);
            foreach (var dosya in Directory.GetFiles(_klasor, "ajan-*.log"))
            {
                if (File.GetLastWriteTime(dosya).Date < esik)
                    File.Delete(dosya);
            }
        }
        catch
        {
            // Temizlik bir yan is; basarisiz olmasi ajani durdurmamali.
        }
    }

    public void Dispose()
    {
        lock (_kilit)
        {
            _yazici?.Dispose();
            _yazici = null;
        }
    }
}
