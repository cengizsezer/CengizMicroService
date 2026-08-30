using FlaUI.UIA3;
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
/// kendisi <c>gorevler/orkaya-aktar.json</c> icinde.
/// </summary>
public sealed class FlaUiOrkaSurucusu : IOrkaSurucusu
{
    private readonly RobotConfig _cfg;
    private readonly IAjanLog _log;

    public FlaUiOrkaSurucusu(RobotConfig cfg, IAjanLog log)
    {
        _cfg = cfg;
        _log = log;
    }

    public string? SonEkranGoruntusuYolu { get; private set; }

    public Task CalistirAsync(OrkaAktarimIstegi istek, GridDoldurVerisi grid,
                              Action<Adim> adimBasladi, CancellationToken ct)
    {
        // UI otomasyonu bastan sona senkron; is zaten kendi gorev parcaciginda
        // calisiyor, burada Task.Run ile ikinci bir parcacik acmanin kazanci yok.
        var gorev = Gorev.Yukle(istek.GorevYolu);

        using var automation = new UIA3Automation();
        using var adimLog = new AdimLogger(_cfg.LogKlasoru, gorev.Ad, _cfg.EkranGoruntusu.HerAdimda);

        SonEkranGoruntusuYolu = adimLog.Klasor;
        _log.Bilgi($"ORKA akisi basliyor: {gorev.Ad} ({gorev.Adimlar.Count} adim). Log: {adimLog.Klasor}");

        var motor = new AdimMotoru(_cfg, adimLog, automation, istek.Degiskenler, grid, adim =>
        {
            ct.ThrowIfCancellationRequested();
            adimBasladi(adim);
        });

        motor.Calistir(gorev);
        return Task.CompletedTask;
    }
}
