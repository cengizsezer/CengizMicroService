using Anthropic;
using Anthropic.Models.Messages;
using PkfRobot.Config;
using PkfRobot.Core;

namespace PkfRobot.Ajan;

/// <summary>
/// Ekran goruntusundeki grid'i Anthropic API'siyle OKUYAN uygulama.
///
/// <b>Modele karar verdirilmiyor.</b> Gonderilen yonerge (bkz.
/// <see cref="GridOkuma.Yonerge"/>) yalnizca "ekranda yazani aktar" diyor;
/// "bu kod dogru mu" sorusu hic sorulmuyor. Dogru kodun ne oldugunu robot zaten
/// biliyor ve karsilastirmayi <see cref="GridDogrulama"/> string esitligiyle
/// yapiyor. Muhasebe karari bu katmanda yok.
///
/// <b>Hicbir kosulda istisna atmiyor.</b> Ag hatasi, kota, zaman asimi, bozuk
/// yanit -- hepsi loglanip bos okuma olarak doner. Dogrulama bir ON ELEME ve
/// robotu durdurmamali; Kaydet'e zaten kullanici basiyor.
///
/// <b>Neden dusuk effort:</b> istenen is okuma, akil yurutme degil. Dusunmeyi
/// tumden kapatmak yerine effort dusuruluyor -- kapatmanin bilinen yan
/// etkileri var, dusuk effort ayni tasarrufu yan etkisiz veriyor.
/// </summary>
public sealed class AnthropicGridOkuyucu : IGridOkuyucu
{
    /// <summary>
    /// Yanit bir JSON listesi; buyuk gridde bile birkac bin jetonu gecmiyor.
    /// Yine de bol tutuluyor: kapaga carpan yanit yarim JSON demek ve yarim
    /// JSON cozulemeyip dogrulamayi tumden kaybettirirdi.
    /// </summary>
    private const int EnFazlaJeton = 16000;

    private readonly GoruntuDogrulamaAyar _ayar;
    private readonly string _anahtar;
    private readonly IAjanLog _log;

    public AnthropicGridOkuyucu(GoruntuDogrulamaAyar ayar, string anahtar, IAjanLog log)
    {
        _ayar = ayar;
        _anahtar = anahtar;
        _log = log;
    }

    public async Task<GridOkumasi> OkuAsync(string goruntuYolu, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(goruntuYolu))
            {
                _log.Uyari($"Grid dogrulamasi atlandi: ekran goruntusu yok ({goruntuYolu}).");
                return GridOkumasi.Bos;
            }

            var bayt = await File.ReadAllBytesAsync(goruntuYolu, ct);
            var base64 = Convert.ToBase64String(bayt);

            var istemci = new AnthropicClient
            {
                ApiKey = _anahtar,
                Timeout = TimeSpan.FromSeconds(Math.Max(5, _ayar.ZamanAsimiSaniye))
            };

            // Zaman asimi hem istemcide hem burada: istemci ayari HTTP turunu
            // sinirliyor, jeton robotun beklemesini. ORKA ekranda bekliyor.
            using var sinir = CancellationTokenSource.CreateLinkedTokenSource(ct);
            sinir.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _ayar.ZamanAsimiSaniye)));

            var istek = new MessageCreateParams
            {
                Model = _ayar.Model,
                MaxTokens = EnFazlaJeton,
                OutputConfig = new OutputConfig { Effort = Effort.Low },
                Messages =
                [
                    new()
                    {
                        Role = Role.User,
                        Content = new List<ContentBlockParam>
                        {
                            new ImageBlockParam
                            {
                                Source = new Base64ImageSource
                                {
                                    Data = base64,
                                    MediaType = MediaType.ImagePng
                                }
                            },
                            new TextBlockParam { Text = GridOkuma.Yonerge }
                        }
                    }
                ]
            };

            var yanit = await istemci.Messages.Create(istek, sinir.Token);

            var metin = string.Concat(
                yanit.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));

            var okuma = GridOkuma.Coz(metin);

            _log.Bilgi($"Grid okundu: {okuma.Satirlar.Count} satir" +
                       (okuma.Kesildi ? " (tablo ekrana sigmamis)" : string.Empty) +
                       $". Jeton: {yanit.Usage.InputTokens} giris / {yanit.Usage.OutputTokens} cikis.");

            if (okuma.Satirlar.Count == 0)
            {
                // Cozulemeyen yanit sessizce gecmesin: ofiste "neden dogrulama
                // yok" sorusunun cevabi log'da olmali.
                _log.Uyari("Grid dogrulamasi atlandi: yanittan satir okunamadi. " +
                           $"Yanitin basi: {Kisalt(metin)}");
            }

            return okuma;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Isin kendisi iptal edildi; ust katman mesaji zaten yaziyor.
            throw;
        }
        catch (OperationCanceledException)
        {
            _log.Uyari($"Grid dogrulamasi atlandi: okuma {_ayar.ZamanAsimiSaniye} sn icinde bitmedi.");
            return GridOkumasi.Bos;
        }
        catch (Exception ex)
        {
            _log.Uyari($"Grid dogrulamasi atlandi: {ex.GetType().Name} - {ex.Message}");
            return GridOkumasi.Bos;
        }
    }

    private static string Kisalt(string? metin)
    {
        if (string.IsNullOrWhiteSpace(metin)) return "(bos)";
        var tek = metin.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return tek.Length <= 200 ? tek : tek[..200] + "…";
    }
}
