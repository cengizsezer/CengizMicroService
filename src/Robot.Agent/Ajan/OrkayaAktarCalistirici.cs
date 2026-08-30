using ClosedXML.Excel;
using PkfRobot.Config;
using PkfRobot.Core;
using System.Text.Json;

namespace PkfRobot.Ajan;

/// <summary>
/// Gercek ORKA aktarimi.
///
/// <b>Once dogrula, sonra yaz.</b> ORKA'nin gridi UI Automation'a kapali; robot
/// yazdigi degerin dogru satira gittigini ekrandan goremiyor. Bu yuzden butun
/// guvence yazmadan once aliniyor ve dogrulamalardan biri tutmazsa is <b>hic
/// baslamiyor</b>. Sayi uyusmazligi en tehlikeli durum: kodlar bir satir kayarsa
/// her kayit yanlis hesaba gider ve bunu kimse fark etmez.
///
/// <b>Kaydet'e basilmiyor.</b> Robot hucreleri dolduruyor, kullanici gozle
/// kontrol edip kendisi kaydediyor. Bu kural pazarlik konusu degil ve
/// <c>gorevler/orkaya-aktar.json</c> icinde Kaydet adimi yok.
/// </summary>
public sealed class OrkayaAktarCalistirici : IIsCalistirici
{
    public const string Tip = "OrkayaAktar";

    /// <summary>Basarisiz isin dosyalari incelenebilsin diye bu kadar duruyor.</summary>
    public static readonly TimeSpan BasarisizIsSaklama = TimeSpan.FromDays(7);

    private readonly RobotConfig _cfg;
    private readonly IIsDosyalari _dosyalar;
    private readonly IOrkaSurucusu _surucu;
    private readonly IOrkaDurumu _orka;
    private readonly IAjanLog _log;
    private readonly string _islerKlasoru;
    private readonly Func<DateTime> _simdi;

    public OrkayaAktarCalistirici(
        RobotConfig cfg,
        IIsDosyalari dosyalar,
        IOrkaSurucusu surucu,
        IOrkaDurumu orka,
        IAjanLog log,
        string islerKlasoru,
        Func<DateTime>? simdi = null)
    {
        _cfg = cfg;
        _dosyalar = dosyalar;
        _surucu = surucu;
        _orka = orka;
        _log = log;
        _islerKlasoru = islerKlasoru;
        _simdi = simdi ?? (() => DateTime.Now);
    }

    public bool Destekliyor(string isTipi) => string.Equals(isTipi, Tip, StringComparison.OrdinalIgnoreCase);

    public async Task<IsSonucu> CalistirAsync(AjanIsPaketi paket, IIsIlerleme ilerleme, CancellationToken ct)
    {
        var basladi = _simdi();
        var klasor = Path.Combine(_islerKlasoru, paket.IsId.ToString("N"));
        EskiIsKlasorleriniTemizle();

        try
        {
            var yuk = YukuCoz(paket.Yuk);

            // --- 1) Dosyalar --------------------------------------------------
            await ilerleme.BildirAsync(2, "Is paketi indiriliyor", 0, ct);
            var ekstreYolu = await _dosyalar.EkstreIndirAsync(paket.IsId, klasor, ct);
            var kodListesi = await _dosyalar.KodListesiIndirAsync(paket.IsId, ct);

            // --- 2) On dogrulamalar -------------------------------------------
            Dogrula(yuk, kodListesi, ekstreYolu);
            _log.Bilgi($"Dogrulamalar gecti: {yuk.SatirSayisi} satir, firma {yuk.FirmaKodu}, " +
                       $"hesap {yuk.BankaHesabiOrkaKodu}.");

            _log.Bilgi(_orka.CalisiyorMu()
                ? "ORKA zaten acik; giris zinciri gerekiyorsa gorev kendisi yurutecek."
                : "ORKA kapali; gorev once baslatacak.");

            // --- 3) ORKA akisi ------------------------------------------------
            var grid = new GridDoldurVerisi(kodListesi.Satirlar
                .Select(s => new GridSatiri(s.SiraNo, s.Aciklama, s.KarsiHesapKodu))
                .ToList());

            grid.SatirYazildi = (yazilan, toplam) =>
            {
                // %50-95 arasi grid dolduruluyor; her satirda degil, onda birde bildir.
                if (yazilan % 10 != 0 && yazilan != toplam) return;

                var yuzde = 50 + (int)(45L * yazilan / Math.Max(1, toplam));
                _ = ilerleme.BildirAsync(yuzde, $"Karsi hesap kodlari yaziliyor ({yazilan}/{toplam})",
                                         yazilan, CancellationToken.None);
            };

            var istek = new OrkaAktarimIstegi(GorevYolu(), new Dictionary<string, string>
            {
                ["firmaKodu"] = yuk.FirmaKodu,
                ["hesapKodu"] = yuk.BankaHesabiOrkaKodu,
                ["dosyaYolu"] = ekstreYolu,
                ["sifre"] = _cfg.Giris.Sifre,
                ["firmaSifre"] = _cfg.Giris.FirmaSifresi,
                ["donem"] = _simdi().ToString("yyyyMM")
            });

            await _surucu.CalistirAsync(istek, grid, adim =>
            {
                if (adim.Yuzde is { } y)
                    _ = ilerleme.BildirAsync(y, adim.Not.Length > 0 ? adim.Not : adim.Tip, null,
                                             CancellationToken.None);
            }, ct);

            // --- 4) Sonuc -----------------------------------------------------
            var sure = (int)(_simdi() - basladi).TotalSeconds;
            var ozet = JsonSerializer.Serialize(new
            {
                YazilanSatir = grid.YazilanSatir,
                ToplamSatir = grid.Satirlar.Count,
                SureSaniye = sure,
                KaydetBasilmadi = true
            });

            await ilerleme.BildirAsync(100,
                $"Tamamlandi — {grid.YazilanSatir} satir yazildi, Kaydet'e basilmadi",
                grid.YazilanSatir, ct);

            _log.Bilgi($"Aktarim tamamlandi: {grid.YazilanSatir}/{grid.Satirlar.Count} satir, {sure} sn. " +
                       "KAYDET'E BASILMADI.");

            KlasoruSil(klasor);
            return IsSonucu.Basarildi(ozet);
        }
        catch (OperationCanceledException)
        {
            throw;   // ust katman iptal/kapanma mesajini kendisi yaziyor
        }
        catch (IsDogrulamaHatasi ex)
        {
            // Dogrulama hatasinda ORKA'ya hic dokunulmadi: ekran goruntusu de yok.
            _log.Hata($"Is dogrulamasi basarisiz: {ex.Message}");
            return IsSonucu.Hata(ex.Message);
        }
        catch (Exception ex)
        {
            var dosyaId = await HataEkraniniYukleAsync(ct);
            var mesaj = $"{ex.Message} " +
                        "ORKA'da yarim kalmis giris olabilir; KAYDETMEDEN ekrani kapatin.";

            _log.Hata($"Aktarim basarisiz: {ex.Message}");
            return IsSonucu.Hata(mesaj, dosyaId);
        }
    }

    // ---- dogrulamalar -------------------------------------------------------

    private static OrkayaAktarYuku YukuCoz(string yuk)
    {
        OrkayaAktarYuku? cozulen;
        try
        {
            cozulen = JsonSerializer.Deserialize<OrkayaAktarYuku>(
                yuk, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            throw new IsDogrulamaHatasi($"Is paketi okunamadi: {ex.Message}");
        }

        if (cozulen is null || cozulen.EkstreYuklemeId <= 0)
            throw new IsDogrulamaHatasi("Is paketi eksik: ekstre kimligi yok.");

        if (string.IsNullOrWhiteSpace(cozulen.FirmaKodu))
            throw new IsDogrulamaHatasi("Is paketinde ORKA firma kodu yok.");

        if (string.IsNullOrWhiteSpace(cozulen.BankaHesabiOrkaKodu))
            throw new IsDogrulamaHatasi("Is paketinde banka hesabinin ORKA kodu yok.");

        return cozulen;
    }

    /// <summary>
    /// Sirayla; biri tutmazsa <b>hic baslama</b>. En tehlikelisi sayi
    /// uyusmazligi: kodlar bir satir kayarsa her kayit yanlis hesaba gider.
    /// </summary>
    public static void Dogrula(OrkayaAktarYuku yuk, KodListesi liste, string ekstreYolu)
    {
        if (liste.Satirlar.Count == 0)
            throw new IsDogrulamaHatasi("Kod listesi bos; aktarilacak satir yok.");

        if (liste.Satirlar.Count != yuk.SatirSayisi)
            throw new IsDogrulamaHatasi(
                $"Satir sayisi uyusmuyor: is paketi {yuk.SatirSayisi}, kod listesi " +
                $"{liste.Satirlar.Count} satir. Aktarim baslatilmadi.");

        var ekstreSatirlari = EkstreSatirSayisi(ekstreYolu);
        if (ekstreSatirlari != yuk.SatirSayisi)
            throw new IsDogrulamaHatasi(
                $"Satir sayisi uyusmuyor: duzeltilmis ekstre {ekstreSatirlari}, is paketi " +
                $"{yuk.SatirSayisi} satir. Kodlar yanlis satirlara gidebilirdi, aktarim baslatilmadi.");

        foreach (var satir in liste.Satirlar)
        {
            if (string.IsNullOrWhiteSpace(satir.KarsiHesapKodu))
                throw new IsDogrulamaHatasi(
                    $"Satir {satir.SiraNo} icin karsi hesap kodu bos. Aktarim baslatilmadi.");

            if (string.IsNullOrWhiteSpace(satir.Aciklama))
                throw new IsDogrulamaHatasi(
                    $"Satir {satir.SiraNo} icin aciklama bos. Aktarim baslatilmadi.");
        }
    }

    /// <summary>
    /// Duzeltilmis ekstredeki VERI satiri sayisi (baslik haric).
    ///
    /// Dosyayi sunucu ClosedXML ile yaziyor; ayni kutuphaneyle okunuyor ki
    /// "kac satir var" sorusunun iki tarafta ayni yaniti olsun.
    /// </summary>
    public static int EkstreSatirSayisi(string yol)
    {
        try
        {
            using var kitap = new XLWorkbook(yol);
            var sayfa = kitap.Worksheets.First();
            var kullanilan = sayfa.LastRowUsed();

            // Ilk satir baslik; veri satiri yoksa 0.
            return kullanilan is null ? 0 : Math.Max(0, kullanilan.RowNumber() - 1);
        }
        catch (Exception ex)
        {
            throw new IsDogrulamaHatasi($"Duzeltilmis ekstre okunamadi: {ex.Message}");
        }
    }

    // ---- yardimcilar --------------------------------------------------------

    private string GorevYolu() => Path.Combine(AppContext.BaseDirectory, "gorevler", "orkaya-aktar.json");

    private async Task<string?> HataEkraniniYukleAsync(CancellationToken ct)
    {
        var klasor = _surucu.SonEkranGoruntusuYolu;
        if (klasor is null || !Directory.Exists(klasor)) return null;

        // AdimLogger hata aninda "HATA" adli goruntuyu zorla aliyor; en yenisi o.
        var goruntu = new DirectoryInfo(klasor).GetFiles("*.png")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();

        return goruntu is null ? null : await _dosyalar.HataEkraniYukleAsync(goruntu.FullName, ct);
    }

    private static void KlasoruSil(string klasor)
    {
        try { if (Directory.Exists(klasor)) Directory.Delete(klasor, recursive: true); }
        catch { /* temizlik bir yan is */ }
    }

    /// <summary>
    /// Basarisiz islerin klasorleri inceleme icin duruyor ama sonsuza kadar degil.
    /// </summary>
    private void EskiIsKlasorleriniTemizle()
    {
        try
        {
            if (!Directory.Exists(_islerKlasoru)) return;

            var esik = _simdi() - BasarisizIsSaklama;
            foreach (var klasor in Directory.GetDirectories(_islerKlasoru))
            {
                if (Directory.GetLastWriteTime(klasor) < esik)
                    Directory.Delete(klasor, recursive: true);
            }
        }
        catch
        {
            // Temizlik isi durdurmamali.
        }
    }
}
