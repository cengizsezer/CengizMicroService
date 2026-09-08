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
    public GridDoldurVerisi(IReadOnlyList<GridSatiri> satirlar,
                            int? hedefSatirSayisi = null,
                            string kaynak = "sunucudan gelen liste")
    {
        Satirlar = satirlar;
        HedefSatirSayisi = hedefSatirSayisi;
        Kaynak = kaynak;
    }

    public IReadOnlyList<GridSatiri> Satirlar { get; }

    /// <summary>
    /// Grid'de KAC satir doldurulmasi gerektigi; bilinmiyorsa null.
    ///
    /// <b>Neden nullable:</b> ORKA gridi UIA'ya kapali, satir sayisi ekrandan
    /// OKUNAMIYOR. Sunucudan gelen iste sayi is paketinde geliyor ve duzeltilmis
    /// ekstreyle capraz dogrulaniyor; elle calistirmada ise ekstre dosyasindan
    /// okunabiliyorsa dolduruluyor, okunamiyorsa null kaliyor.
    ///
    /// Doluysa <c>GridDoldur</c> liste ile karsilastirip eksik/fazlayi UYARI
    /// olarak yaziyor -- adimi durdurmuyor: yarim kalan bir grid'i gozle
    /// tamamlamak, robotun ortada durup ekrani bilinmeyen halde birakmasindan
    /// iyidir (KAYDET'e zaten kullanici basiyor).
    /// </summary>
    public int? HedefSatirSayisi { get; }

    /// <summary>Listenin nereden geldigi; log'da gorunsun diye.</summary>
    public string Kaynak { get; }

    /// <summary>Kac satir yazildi; adim sonunda dolar.</summary>
    public int YazilanSatir { get; set; }

    /// <summary>
    /// Grid doldurulduktan HEMEN SONRA alinan ekran goruntusunun yolu; adim
    /// sonunda dolar, goruntu alinamadiysa null kalir.
    ///
    /// <b>Neden motor bunu yaziyor ama okumuyor:</b> goruntuyu dogrulama
    /// katmani tuketiyor ve o katman aga cikiyor. Motor aga dokunmuyor --
    /// ilerleme bildiriminin <see cref="SatirYazildi"/> ile disari verilmesiyle
    /// ayni gerekce. Motor yalnizca "goruntu su dosyada" diyor; onunla ne
    /// yapilacagi cagiran tarafin isi.
    /// </summary>
    public string? GridGoruntusuYolu { get; set; }

    /// <summary>
    /// Her satir yazildiktan sonra cagrilir (satirNo, toplam). Ilerleme bildirimi
    /// buradan gidiyor; motor sunucuyu tanimiyor.
    /// </summary>
    public Action<int, int>? SatirYazildi { get; set; }
}
