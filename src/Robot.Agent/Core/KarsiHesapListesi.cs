namespace PkfRobot.Core;

/// <summary>
/// Elle girilen karsi hesap kodu listesini metinden okur.
///
/// <b>Neden var:</b> <c>GridDoldur</c> adimi -- zincirin ORKA'ya gercekten VERI
/// yazan tek parcasi -- yalnizca sunucudan gelen kod listesiyle calisiyordu.
/// Elle calistirmada liste bos geldigi icin adim uyari yazip ATLANIYOR ve en
/// kritik parca hic denenmiyordu. Bu sinif, Calistir sekmesine yazilan listeyi
/// ayni bicime cevirerek elle testi mumkun kiliyor.
///
/// Ayirici hem virgul hem satir sonu: 10 kodu tek satira sigdirmak zor, ama
/// kisa listeyi tek satira yazmak da dogal. Ikisini de kabul etmek kullaniciya
/// "hangisiydi" diye dusundurmuyor.
///
/// Ekrana ya da diske dokunmuyor: girdi metin, cikti kod listesi -- projedeki
/// <see cref="OdakKarari"/> kalibinin aynisi, ORKA olmadan test edilebiliyor.
/// </summary>
public static class KarsiHesapListesi
{
    private static readonly char[] Ayiricilar = { ',', ';', '\n', '\r' };

    /// <summary>
    /// Metni kod listesine cevirir. Bos girdi bos liste doner (cagiran taraf
    /// "elle liste yok" diye yorumluyor, hata degil).
    ///
    /// Kodlarin ICINDEKI bosluklar korunuyor: ORKA hesap kodlari "320 01 001"
    /// gibi bosluklu yaziliyor ve bosluklari temizlemek kodu bozardi. Yalniz
    /// bastaki/sondaki bosluk ve bos parcalar atiliyor.
    /// </summary>
    public static IReadOnlyList<string> Coz(string? metin)
    {
        if (string.IsNullOrWhiteSpace(metin)) return Array.Empty<string>();

        return metin.Split(Ayiricilar, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => p.Length > 0)
                    .ToList();
    }

    /// <summary>
    /// Kod listesini <see cref="GridSatiri"/> dizisine cevirir.
    ///
    /// Sira numarasi 1'den basliyor ve aciklama "elle girilen liste" olarak
    /// isaretleniyor: log'da bu satirlarin sunucudan gelmedigi gorunsun --
    /// ORKA'ya yazilan bir degerin nereden geldigi her zaman okunabilmeli.
    /// </summary>
    public static IReadOnlyList<GridSatiri> Satirlar(string? metin)
        => Coz(metin).Select((kod, i) => new GridSatiri(i + 1, "elle girilen liste", kod)).ToList();
}
