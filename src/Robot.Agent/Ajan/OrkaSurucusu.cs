using FlaUI.UIA3;
using PkfRobot.Ayarlar;
using PkfRobot.Config;
using PkfRobot.Core;

namespace PkfRobot.Ajan;

/// <summary>ORKA'yi surmek icin gereken her sey.</summary>
/// <param name="GorevYolu">Calistirilacak gorev JSON'u.</param>
/// <param name="Degiskenler">{firmaKodu}, {dosyaYolu}, {hesapKodu}… gorevdeki yer tutucular.</param>
public record OrkaAktarimIstegi(string GorevYolu, Dictionary<string, string> Degiskenler);

/// <summary>
/// ORKA'yi surekli surme isi. Arayuz olmasinin sebebi test: ev makinesinde ORKA
/// yok ve UI otomasyonu sinanamiyor, ama aktarim isinin <b>dogrulama ve sonuc</b>
/// kurallari sinanabilmeli.
/// </summary>
public interface IOrkaSurucusu
{
    /// <summary>
    /// Gorevi yurutur. Basarisizlikta istisna atar; o anin ekran goruntusu
    /// <see cref="SonEkranGoruntusuYolu"/>'nda kalir.
    /// </summary>
    Task CalistirAsync(OrkaAktarimIstegi istek, GridDoldurVerisi grid,
                       Action<Adim> adimBasladi, CancellationToken ct);

    /// <summary>Son calistirmanin log klasoru; hata ekrani orada.</summary>
    string? SonEkranGoruntusuYolu { get; }
}

/// <summary>
/// Gercek surucu: mevcut JSON adim motorunu calistirir.
///
/// Adim motoru ve gorev JSON'u <b>degismedi</b> — bu sinif yalnizca motoru
/// kuruyor, degiskenleri veriyor ve grid verisini bagliyor. Aktarim akisinin
/// kendisi <c>gorevler/ajan-aktar.json</c> icinde (acilis + govde; ikisi de
/// Calistir sekmesinin kostugu dosyalar).
///
/// <b>Zamanlama artik formdan geliyor.</b> Bu yol once yalnizca
/// appsettings.json'daki <c>Zamanlama</c> ile kosuyor ve hiz carpanini hic
/// kullanmiyordu: Calistir sekmesinden "yavaslat" demek sunucudan gelen ise
/// hicbir sey yapmiyordu. Degerler her iste yeniden
/// <see cref="ZamanlamaCozumu.Diskten"/> ile okunuyor (ajan gunlerce acik
/// kaliyor, arada degistirilen deger gorulmeli) ve log'un basinda ne ile
/// kosuldugu yaziliyor.
/// </summary>
public sealed class FlaUiOrkaSurucusu : IOrkaSurucusu
{
    private readonly RobotConfig _cfg;
    private readonly IAjanLog _log;
    private readonly Func<CalistirmaAyari?> _zamanlamaOku;

    /// <param name="zamanlamaOku">
    /// Calistir sekmesi ayarlarini veren agiz; null ise diskten okunuyor.
    /// Disaridan verilebilmesi test icin: ev makinesinde %AppData% altindaki
    /// gercek ayarlar.json'a bagli bir davranis sinanamaz.
    /// </param>
    public FlaUiOrkaSurucusu(RobotConfig cfg, IAjanLog log,
                             Func<CalistirmaAyari?>? zamanlamaOku = null)
    {
        _cfg = cfg;
        _log = log;
        _zamanlamaOku = zamanlamaOku ?? (() => ZamanlamaCozumu.Diskten());
    }

    public string? SonEkranGoruntusuYolu { get; private set; }

    public Task CalistirAsync(OrkaAktarimIstegi istek, GridDoldurVerisi grid,
                              Action<Adim> adimBasladi, CancellationToken ct)
    {
        // UI otomasyonu bastan sona senkron; is zaten kendi gorev parcaciginda
        // calisiyor, burada Task.Run ile ikinci bir parcacik acmanin kazanci yok.
        var zamanlama = ZamanlamaCozumu.Coz(_cfg, _zamanlamaOku());
        var cfg = zamanlama.Config;

        // Carpan gorevin Bekle adimlarina da uygulaniyor: elle calistirmada oyle
        // ve iki yolun ayni gorevi ayni surelerle kosmasi gerekiyor.
        var gorev = Hizlandirici.Uygula(Gorev.Yukle(istek.GorevYolu), zamanlama.HizCarpani);

        using var automation = new UIA3Automation();
        using var adimLog = new AdimLogger(cfg.LogKlasoru, gorev.Ad, cfg.EkranGoruntusu.HerAdimda);

        SonEkranGoruntusuYolu = adimLog.Klasor;
        _log.Bilgi($"ORKA akisi basliyor: {gorev.Ad} ({gorev.Adimlar.Count} adim). Log: {adimLog.Klasor}");
        _log.Bilgi($"Zamanlama: {zamanlama.Ozet}");
        adimLog.Bilgi($"Zamanlama: {zamanlama.Ozet}");

        // gozetimsiz: bu yolu sunucudan gelen is tetikliyor ve basinda kimse
        // yok. 'OnayBekle' tasiyan adimlar (sekme kapatma) burada calismiyor;
        // duraklatacak DEVAM dugmesi olmadigi icin tek dogru davranis atlamak.
        var motor = new AdimMotoru(cfg, adimLog, automation, istek.Degiskenler, grid, adim =>
        {
            ct.ThrowIfCancellationRequested();
            adimBasladi(adim);
        }, gozetimsiz: true);

        try
        {
            motor.Calistir(gorev);
        }
        finally
        {
            // Ozet HATA durumunda da yaziliyor: yarim kalan bir kosunun hangi
            // adimda zaman yedigi, basarili kosununki kadar ise yariyor.
            var ozet = AdimSureOzeti.Ozet(motor.Olcumler);
            if (ozet.Length > 0)
                _log.Bilgi($"{gorev.Ad} · toplam {SureBicimi.Kisa(motor.GecenSure)} · {ozet}");
        }

        return Task.CompletedTask;
    }
}
