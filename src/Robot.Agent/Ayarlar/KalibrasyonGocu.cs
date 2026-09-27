namespace PkfRobot.Ayarlar;

/// <summary>
/// Silinen gorev dosyasina bakan kalibrasyon anahtarlarini yenisine tasir.
///
/// <b>Neden gerekli:</b> olcumler gorev dosyasinda degil
/// <c>%AppData%\PkfRobot\ayarlar.json</c> icinde duruyor ve anahtari
/// "dosya adi + kacinci Tikla adimi" (<c>orkaya-aktar.json#0</c>). Ajan ve
/// kuyruk tek dosyada birlesince <c>orkaya-aktar.json</c> silindi; kayitlar
/// oldugu gibi kalsaydi <see cref="KalibrasyonUygulama"/> her acilista
/// "AdimYok" satiri uretirdi ve ofiste olculmus degerler bosa giderdi.
///
/// <b>Neden birebir tasinabiliyor:</b> iki dosyanin ilk dort <c>Tikla</c>
/// adimi ayni sirada ve ayni oranlardaydi (bir test bunu esit tutuyordu:
/// TekDosyaAktarTests.Koordinatlar_orkaya_aktar_ile_ayni). Yani
/// <c>orkaya-aktar.json#N</c> ile <c>tek-dosya-aktar.json#N</c> ayni noktayi
/// gosteriyor. Yeni anahtarda kayit ZATEN varsa eski kayit siliniyor: yeni
/// dosyanin kendi olcumu daha gunceldir.
///
/// Bir kez calisan bir goc; ikinci acilista tasinacak kayit kalmaz.
/// <see cref="PkfRobot.Arayuz.CalistirPaneli"/> icindeki acilis gorevi gocuyle
/// ayni kalip.
/// </summary>
public static class KalibrasyonGocu
{
    /// <summary>08.09.2026'da silinen ajan gorevi.</summary>
    public const string EskiDosya = "orkaya-aktar.json";

    /// <summary>Aktarimin tek kopyasi; ajan da bunu kosuyor.</summary>
    public const string YeniDosya = "tek-dosya-aktar.json";

    /// <summary>
    /// Ayarlari YERINDE duzeltir.
    /// </summary>
    /// <returns>
    /// Log'a yazilacak satirlar; bos liste "tasinacak bir sey yoktu" demek.
    /// Cagiran taraf bu listeye bakip ayarlari diske yazip yazmayacagina karar
    /// veriyor -- her acilista dosyaya dokunmak, "neyi ne zaman degistirdim"
    /// sorusunu cevapsiz birakirdi.
    /// </returns>
    public static IReadOnlyList<string> Uygula(RobotAyarlari ayarlar)
    {
        var satirlar = new List<string>();

        var eskiler = ayarlar.Koordinatlar
            .Where(k => EskiDosya.Equals(KalibrasyonUygulama.DosyaAdi(k.Anahtar),
                                         StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var kayit in eskiler)
        {
            var sira = KalibrasyonUygulama.Sira(kayit.Anahtar);
            if (sira < 0)
            {
                // Anahtar zaten okunamiyor; tasinacak bir hedefi de yok.
                ayarlar.Koordinatlar.Remove(kayit);
                satirlar.Add($"Kalibrasyon: taninmayan anahtar '{kayit.Anahtar}' silindi.");
                continue;
            }

            var yeniAnahtar = KoordinatKesfi.Anahtar(YeniDosya, sira);

            if (ayarlar.Koordinat(yeniAnahtar) is not null)
            {
                ayarlar.Koordinatlar.Remove(kayit);
                satirlar.Add($"Kalibrasyon: '{kayit.Anahtar}' silindi, '{yeniAnahtar}' zaten olculmustu.");
                continue;
            }

            kayit.Anahtar = yeniAnahtar;
            satirlar.Add($"Kalibrasyon: '{EskiDosya}#{sira}' -> '{yeniAnahtar}' olarak tasindi " +
                         $"({kayit.Not}). Eski dosya silindi, olcum korundu.");
        }

        return satirlar;
    }
}
