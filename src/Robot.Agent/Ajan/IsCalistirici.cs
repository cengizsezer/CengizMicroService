namespace PkfRobot.Ajan;

/// <summary>Sunucudan gelen is paketi.</summary>
public class AjanIsPaketi
{
    public Guid IsId { get; set; }
    public string IsTipi { get; set; } = string.Empty;
    public int FirmaId { get; set; }

    /// <summary>Ise ozgu parametreler (JSON). Tipini calistirici bilir.</summary>
    public string Yuk { get; set; } = "{}";
}

/// <summary>Isin sonucu.</summary>
/// <param name="Basarili">Is beklendigi gibi bitti mi?</param>
/// <param name="HataMesaji">Basarisizsa sebep; kullaniciya oldugu gibi gosteriliyor.</param>
/// <param name="SonucOzetiJson">Basariliysa ozet (kac satir yazildi, ne kadar surdu).</param>
/// <param name="HataEkraniDosyaId">Hata ekraninin sunucuya yuklenmis goruntusu.</param>
public record IsSonucu(
    bool Basarili,
    string? HataMesaji = null,
    string? SonucOzetiJson = null,
    string? HataEkraniDosyaId = null)
{
    public static IsSonucu Basarildi(string? ozetJson = null) => new(true, null, ozetJson);
    public static IsSonucu Hata(string mesaj, string? ekranDosyaId = null) => new(false, mesaj, null, ekranDosyaId);
}

/// <summary>Ilerlemeyi sunucuya tasiyan agiz.</summary>
public interface IIsIlerleme
{
    Task BildirAsync(int yuzde, string mesaj, int? tamamlananAdim = null, CancellationToken ct = default);
}

/// <summary>
/// Bir is tipini calistiran birim.
///
/// <b>Neden arayuz:</b> baglanti katmani (token, yeniden baglanma, kalp atisi,
/// ilerleme bildirimi) is tipinden bagimsiz. C adiminda tek uygulama var ve
/// ORKA'ya hic dokunmuyor; D adiminda gercek ORKA akisi <b>yalniz bu arayuzun
/// arkasina</b> ekleniyor, baglanti katmani degismiyor.
/// </summary>
public interface IIsCalistirici
{
    bool Destekliyor(string isTipi);

    Task<IsSonucu> CalistirAsync(AjanIsPaketi paket, IIsIlerleme ilerleme, CancellationToken ct);
}

/// <summary>
/// C adiminin sahte isi: ORKA'ya dokunmadan, on adim boyunca birer saniye
/// bekleyip ilerleme bildirir.
///
/// Amaci is akisinin ucdan uca calistigini ORKA'nin karmasikligi devreye
/// girmeden kanitlamak. Gercek aktarim D adiminda.
/// </summary>
public sealed class SahteIsCalistirici : IIsCalistirici
{
    public const string Tip = "SahteAktarim";
    public const int AdimSayisi = 10;

    private readonly IAjanLog _log;
    private readonly Func<TimeSpan, CancellationToken, Task> _bekle;

    public SahteIsCalistirici(IAjanLog log, Func<TimeSpan, CancellationToken, Task>? bekle = null)
    {
        _log = log;
        _bekle = bekle ?? ((sure, ct) => Task.Delay(sure, ct));
    }

    public bool Destekliyor(string isTipi) => string.Equals(isTipi, Tip, StringComparison.OrdinalIgnoreCase);

    public async Task<IsSonucu> CalistirAsync(AjanIsPaketi paket, IIsIlerleme ilerleme, CancellationToken ct)
    {
        _log.Bilgi($"Sahte is basladi: {paket.IsId} (firma {paket.FirmaId}).");

        for (var adim = 1; adim <= AdimSayisi; adim++)
        {
            ct.ThrowIfCancellationRequested();

            await _bekle(TimeSpan.FromSeconds(1), ct);

            var yuzde = adim * 100 / AdimSayisi;
            await ilerleme.BildirAsync(yuzde, $"Sahte adim {adim}/{AdimSayisi}", adim, ct);
        }

        var ozet = $"{{\"Adim\":{AdimSayisi},\"Sahte\":true,\"KaydetBasilmadi\":true}}";
        _log.Bilgi($"Sahte is bitti: {paket.IsId}.");

        return IsSonucu.Basarildi(ozet);
    }
}
