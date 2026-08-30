namespace PkfRobot.Core;

/// <summary>Grid'e yazilacak tek satir.</summary>
/// <param name="SiraNo">Ekstredeki sira; log ve hata mesajlarinda gecer.</param>
/// <param name="Aciklama">Satirin aciklamasi; hangi satirda durdugumuzu anlatmak icin.</param>
/// <param name="KarsiHesapKodu">Grid'e yazilan deger.</param>
public record GridSatiri(int SiraNo, string Aciklama, string KarsiHesapKodu);

/// <summary>
/// <c>GridDoldur</c> adiminin girdisi.
///
/// Gorev JSON'u <b>ne yazilacagini</b> icermiyor, yalnizca "burada grid doldurulur"
/// diyor; satirlar sunucudan indirilen kod listesinden geliyor. Boylece is akisi
/// JSON'da kalirken veri koda gomulmuyor.
/// </summary>
public class GridDoldurVerisi
{
    public GridDoldurVerisi(IReadOnlyList<GridSatiri> satirlar) => Satirlar = satirlar;

    public IReadOnlyList<GridSatiri> Satirlar { get; }

    /// <summary>Kac satir yazildi; adim sonunda dolar.</summary>
    public int YazilanSatir { get; set; }

    /// <summary>
    /// Her satir yazildiktan sonra cagrilir (satirNo, toplam). Ilerleme bildirimi
    /// buradan gidiyor; motor sunucuyu tanimiyor.
    /// </summary>
    public Action<int, int>? SatirYazildi { get; set; }
}
