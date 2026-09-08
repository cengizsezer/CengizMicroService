using PkfRobot.Ayarlar;

namespace PkfRobot.Core;

/// <summary>Kuyrukta islenecek tek bir banka.</summary>
/// <param name="Sira">Kacinci is (1'den); log ve ilerleme metinleri icin.</param>
/// <param name="KarsiHesaplar">
/// ELLE girilen karsi hesap kodlari (ham metin, bos olabilir). Doluysa
/// <c>GridDoldur</c> sunucudan gelen liste yerine bunu kullaniyor -- sunucuya
/// baglanmadan grid adimini denemenin tek yolu.
/// </param>
public record BankaIsi(int Sira, string Banka, string HesapKodu, string DosyaYolu,
                       string KarsiHesaplar = "");

public enum IsDurumu
{
    Bekliyor,
    Calisiyor,
    Bitti,
    Hata,

    /// <summary>Siraya hic gelmedi: kuyruk once durduruldu ya da onceki is patladi.</summary>
    Atlandi
}

/// <summary>Kuyrugun sonu: hangi is ne oldu.</summary>
public record KuyrukOzeti(
    IReadOnlyList<(BankaIsi Is, IsDurumu Durum, string? Mesaj)> Sonuclar,
    bool Durduruldu)
{
    public int Biten => Sonuclar.Count(s => s.Durum == IsDurumu.Bitti);
    public int Hatali => Sonuclar.Count(s => s.Durum == IsDurumu.Hata);
    public int Atlanan => Sonuclar.Count(s => s.Durum == IsDurumu.Atlandi);
}

/// <summary>
/// Banka satirlarindan is listesi cikaran kurallar.
///
/// Secim kurali ekranda degil burada: "isaretli VE dosyasi secili" sarti tek bir
/// yerde dursun ki ekran ile calisan is ayrisamasin.
/// </summary>
public static class BankaKuyrugu
{
    /// <summary>Isaretli ve dosyasi secili satirlar, tablodaki SIRAYLA.</summary>
    public static List<BankaIsi> Isler(IEnumerable<BankaSatiri> satirlar, string firmaKodu)
    {
        var sonuc = new List<BankaIsi>();

        foreach (var satir in satirlar)
        {
            if (!satir.Secili) continue;
            if (string.IsNullOrWhiteSpace(satir.DosyaYolu)) continue;

            sonuc.Add(new BankaIsi(
                sonuc.Count + 1,
                satir.Banka,
                satir.HesapKodu(firmaKodu),
                satir.DosyaYolu,
                satir.KarsiHesaplar(firmaKodu)));
        }

        return sonuc;
    }

    /// <summary>
    /// START'i engelleyen eksikler. Bos liste = baslanabilir.
    ///
    /// Isaretli ama dosyasi olmayan satir <b>sessizce atlanmiyor</b>: kullanici o
    /// satiri bilerek isaretledi, atlamak "istedigim banka aktarilmamis" demek
    /// olurdu ve bu ancak is bittikten sonra fark edilirdi.
    /// </summary>
    public static List<string> Eksikler(IEnumerable<BankaSatiri> satirlar, string firmaKodu,
                                        Func<string, bool>? dosyaVarMi = null)
    {
        var varMi = dosyaVarMi ?? File.Exists;
        var eksikler = new List<string>();
        var isaretli = 0;

        foreach (var satir in satirlar)
        {
            if (!satir.Secili) continue;
            isaretli++;

            var ad = string.IsNullOrWhiteSpace(satir.Banka) ? "(adsiz satir)" : satir.Banka;

            if (string.IsNullOrWhiteSpace(satir.DosyaYolu))
                eksikler.Add($"{ad}: ekstre dosyasi secilmemis.");
            else if (!varMi(satir.DosyaYolu))
                eksikler.Add($"{ad}: dosya bulunamadi -> {satir.DosyaYolu}");

            if (string.IsNullOrWhiteSpace(satir.HesapKodu(firmaKodu)))
                eksikler.Add($"{ad}: {firmaKodu} firmasi icin hesap kodu bos.");
        }

        if (isaretli == 0) eksikler.Add("Hicbir banka isaretli degil.");

        return eksikler;
    }
}

/// <summary>
/// Is listesini sirayla yuruten dongu.
///
/// Isin kendisi disaridan veriliyor (<c>isiCalistir</c>): boylece dongu mantigi
/// -- sira, duraklama, durdurma, hata sonrasi ne olacagi -- ORKA olmadan
/// sinanabiliyor. Gercek calistirmada o delegate gorev JSON'unu adim motoruyla
/// yuruten koddur.
/// </summary>
public sealed class KuyrukCalistirici
{
    private readonly CalismaDenetimi _denetim;
    private readonly Action<BankaIsi> _isiCalistir;
    private readonly Action<BankaIsi, IsDurumu, string?>? _durumDegisti;
    private readonly Action<string>? _log;
    private readonly Action? _acilis;

    /// <param name="acilis">
    /// Kuyruk basinda BIR KEZ calisan hazirlik (ORKA'yi ac, firmaya gir).
    /// Her banka icin tekrarlanmaz: ORKA zaten acik firmayla giris ekranini
    /// bir daha gostermiyor ve giris adimlari ikinci turda timeout'a duserdi.
    /// </param>
    public KuyrukCalistirici(
        CalismaDenetimi denetim,
        Action<BankaIsi> isiCalistir,
        Action? acilis = null,
        Action<BankaIsi, IsDurumu, string?>? durumDegisti = null,
        Action<string>? log = null)
    {
        _denetim = denetim;
        _isiCalistir = isiCalistir;
        _acilis = acilis;
        _durumDegisti = durumDegisti;
        _log = log;
    }

    public KuyrukOzeti Calistir(IReadOnlyList<BankaIsi> isler)
    {
        var sonuclar = new List<(BankaIsi, IsDurumu, string?)>();
        var durduruldu = false;
        var kalanlarAtlansin = false;
        string? atlamaSebebi = null;

        try
        {
            _denetim.AdimOncesiBekle();
            _acilis?.Invoke();
        }
        catch (CalismaDurduruldu)
        {
            durduruldu = true;
            kalanlarAtlansin = true;
            atlamaSebebi = "Acilis oncesi durduruldu.";
            _log?.Invoke("Kuyruk baslamadan durduruldu.");
        }
        catch (Exception ex)
        {
            kalanlarAtlansin = true;
            atlamaSebebi = $"Acilis gorevi basarisiz: {ex.Message}";
            _log?.Invoke($"Acilis gorevi basarisiz, hicbir banka islenmedi: {ex.Message}");
        }

        foreach (var isim in isler)
        {
            if (kalanlarAtlansin)
            {
                Bildir(sonuclar, isim, IsDurumu.Atlandi, atlamaSebebi);
                continue;
            }

            try
            {
                _denetim.AdimOncesiBekle();

                Bildir(sonuclar, isim, IsDurumu.Calisiyor, null, kaydet: false);
                _log?.Invoke($"[{isim.Sira}/{isler.Count}] {isim.Banka} basliyor: {isim.DosyaYolu}");

                _isiCalistir(isim);

                Bildir(sonuclar, isim, IsDurumu.Bitti, null);
                _log?.Invoke($"[{isim.Sira}/{isler.Count}] {isim.Banka} bitti.");
            }
            catch (CalismaDurduruldu)
            {
                durduruldu = true;
                kalanlarAtlansin = true;
                atlamaSebebi = "Kullanici durdurdu.";

                Bildir(sonuclar, isim, IsDurumu.Atlandi, atlamaSebebi);
                _log?.Invoke($"DURDURULDU: {isim.Banka} yarida kaldi, kalan bankalar islenmedi.");
            }
            catch (Exception ex)
            {
                // Hatadan sonra DEVAM EDILMIYOR. Gorev yarida kaldiginda ORKA'nin
                // hangi ekranda kaldigi bilinmiyor; bir sonraki banka o ekrandan
                // baslarsa ne yaptigini kimse bilemez. Kalanlar atlaniyor,
                // kullanici ekrani gorup karar veriyor.
                kalanlarAtlansin = true;
                atlamaSebebi = $"Onceki banka ({isim.Banka}) hata verdi.";

                Bildir(sonuclar, isim, IsDurumu.Hata, ex.Message);
                _log?.Invoke($"HATA: {isim.Banka} -> {ex.Message}. Kuyruk durduruldu; " +
                             "ORKA'nin hangi ekranda kaldigini kontrol edin.");
            }
        }

        return new KuyrukOzeti(sonuclar, durduruldu);
    }

    private void Bildir(List<(BankaIsi, IsDurumu, string?)> sonuclar, BankaIsi isim,
                        IsDurumu durum, string? mesaj, bool kaydet = true)
    {
        if (kaydet) sonuclar.Add((isim, durum, mesaj));
        _durumDegisti?.Invoke(isim, durum, mesaj);
    }
}
