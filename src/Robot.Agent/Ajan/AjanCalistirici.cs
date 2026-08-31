using System.Runtime.Versioning;
using PkfRobot.Config;

namespace PkfRobot.Ajan;

/// <summary>
/// Ajan calistirilirken disaridan takilabilen kancalar.
///
/// <b>Neden var:</b> arayuz ayni ajani calistirmak istiyor ama log satirlarini
/// ekranda gostermek, isleri izlemek ve anahtari konsol yerine bir pencereden
/// sormak zorunda. Bunun icin ikinci bir kurulum yazilsaydi iki nesne grafigi
/// olur ve biri degistiginde digeri sessizce eskirdi. Hepsi bos birakilirsa
/// davranis konsol modundaki ile birebir ayni.
/// </summary>
public sealed class AjanKancalari
{
    /// <summary>Log agzini sarmalar (ornegin ekrana da yazan bir kopya).</summary>
    public Func<IAjanLog, IAjanLog>? LogSarmala { get; init; }

    /// <summary>Her is calistiriciyi sarmalar (ornegin ilerlemeyi izlemek icin).</summary>
    public Func<IIsCalistirici, IIsCalistirici>? IsSarmala { get; init; }

    /// <summary>Anahtar yoksa nereden sorulacak. Parametre: ayar klasoru.</summary>
    public Func<string, string?>? AnahtarSor { get; init; }

    /// <summary>Servis kurulduktan sonra cagrilir; arayuz durumu buradan okuyor.</summary>
    public Action<AjanServisi>? ServisHazir { get; init; }
}

/// <summary>
/// <c>--ajan</c> modunun giris noktasi: anahtari kurar, servisi ayaga kaldirir,
/// Ctrl+C'ye (arayuzde "Durdur" dugmesine) kadar bagli tutar.
///
/// Baglanti ORKA'dan bagimsiz: ajan ORKA kapaliyken de bagli kalir ve gorev
/// calistirmadan bekler. ORKA yalnizca <c>OrkayaAktar</c> isi geldiginde
/// devreye giriyor.
/// </summary>
[SupportedOSPlatform("windows")]
public static class AjanCalistirici
{
    public static async Task<int> CalistirAsync(RobotConfig cfg, bool anahtariSifirla, CancellationToken ct,
                                                AjanKancalari? kancalar = null)
    {
        var kok = AjanKimlikDeposu.VarsayilanKlasor;
        using var dosyaLog = new AjanDosyaLog(Path.Combine(kok, "logs"), cfg.Ajan.LogSaklamaGun);
        IAjanLog log = kancalar?.LogSarmala?.Invoke(dosyaLog) ?? dosyaLog;

        var depo = new AjanKimlikDeposu(kok);

        if (anahtariSifirla && depo.AnahtarVarMi)
        {
            depo.AnahtarSil();
            log.Bilgi("Kayitli ajan anahtari silindi, yenisi sorulacak.");
        }

        var anahtar = depo.AnahtarOku();
        if (anahtar is null)
        {
            anahtar = kancalar?.AnahtarSor is { } sor ? sor(kok) : AnahtariSor(kok);
            if (anahtar is null)
            {
                log.Hata("Ajan anahtari girilmedi. Baglanti kurulamaz.");
                return 2;
            }

            depo.AnahtarYaz(anahtar);
            log.Bilgi($"Ajan anahtari kaydedildi: {depo.AnahtarDosyasi} (DPAPI ile sifreli).");
        }

        var kimlik = AjanKimlik.Olustur(depo.MakineId());

        log.Bilgi($"PkfRobot ajan modu. Surum {kimlik.AjanSurumu}.");
        log.Bilgi($"Makine  : {kimlik.MakineAdi} ({kimlik.MakineId})");
        log.Bilgi($"Hub     : {cfg.Ajan.HubAdresi}");
        log.Bilgi($"Token   : {cfg.Ajan.TokenUcu}");
        log.Bilgi($"Log     : {dosyaLog.Klasor}");
        log.Bilgi("Durdurmak icin Ctrl+C.");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        var tokenSaglayici = new AjanTokenSaglayici(
            http,
            cfg.Ajan.TokenUcu,
            () => anahtar!,
            TimeSpan.FromMinutes(cfg.Ajan.TokenYenilemeEsigiDakika),
            log);

        // Is tipleri: her tip icin bir calistirici. Sunucu tanimadigi bir tip
        // gonderirse ajan onu reddedip sebebini bildiriyor.
        var dosyalar = new IsDosyalari(
            http,
            tokenSaglayici.TokenAlAsync,
            cfg.Ajan.IsUcuKoku,
            cfg.Ajan.DosyaYuklemeUcu,
            log);

        var calistiricilar = new List<IIsCalistirici>
        {
            new SahteIsCalistirici(log),
            new OrkayaAktarCalistirici(
                cfg,
                dosyalar,
                new FlaUiOrkaSurucusu(cfg, log),
                new OrkaSureci(cfg.Ajan.OrkaSurecAdi),
                log,
                Path.Combine(kok, "isler"))
        };

        if (kancalar?.IsSarmala is { } sarmala)
            calistiricilar = calistiricilar.Select(sarmala).ToList();

        await using var servis = new AjanServisi(
            new SignalRHubFabrikasi(),
            tokenSaglayici,
            new OrkaSureci(cfg.Ajan.OrkaSurecAdi),
            kimlik,
            cfg.Ajan.HubAdresi,
            TimeSpan.FromSeconds(cfg.Ajan.KalpAtisiSaniye),
            log,
            calistiricilar: calistiricilar);

        kancalar?.ServisHazir?.Invoke(servis);

        await servis.CalistirAsync(ct);

        // Anahtar gecersiz ya da surum eski: durum kodu 0 olmasin ki gorev
        // zamanlayici / baslangic kisayolu "bitti, sorun yok" sanmasin.
        return servis.KayitKaliciReddedildi ? 3 : 0;
    }

    /// <summary>
    /// Ilk kurulumda anahtari sorar. Girdi ekranda gorunmesin diye yildizla
    /// yazdiriliyor: bu ekranin goruntusu alinabilir ve omuz ustunden okunabilir.
    /// </summary>
    private static string? AnahtariSor(string kok)
    {
        Console.WriteLine();
        Console.WriteLine("=== PkfRobot ajan kurulumu ===");
        Console.WriteLine($"Bu makinede kayitli ajan anahtari yok ({Path.Combine(kok, AjanKimlikDeposu.AnahtarDosyaAdi)}).");
        Console.WriteLine("Anahtari DijitalMasraf > Yonetim > Ajanlar ekranindan alin (pkfr_ ile baslar).");
        Console.WriteLine("Anahtar ekranda gorunmeyecek, yapistirip Enter'a basin.");
        Console.Write("Ajan anahtari: ");

        var anahtar = GizliOku();
        Console.WriteLine();

        return string.IsNullOrWhiteSpace(anahtar) ? null : anahtar.Trim();
    }

    private static string GizliOku()
    {
        // Konsol yonlendirilmisse (servis, zamanlanmis gorev) tus tus okunamaz;
        // duz okumaya dusuluyor.
        if (Console.IsInputRedirected)
            return Console.ReadLine() ?? string.Empty;

        var girilen = new System.Text.StringBuilder();

        while (true)
        {
            var tus = Console.ReadKey(intercept: true);

            if (tus.Key == ConsoleKey.Enter) break;

            if (tus.Key == ConsoleKey.Backspace)
            {
                if (girilen.Length > 0)
                {
                    girilen.Length--;
                    Console.Write("\b \b");
                }
                continue;
            }

            if (char.IsControl(tus.KeyChar)) continue;

            girilen.Append(tus.KeyChar);
            Console.Write('*');
        }

        return girilen.ToString();
    }
}
