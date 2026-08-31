namespace PkfRobot.Ajan;

/// <summary>
/// Ajan surumu derleme surumunden okunur, elle yazilmaz.
///
/// Sunucu asgari surum kontrolu yapiyor. Surum bir sabit olsaydi, csproj'daki
/// surumu artirip koddakini unutmak sessiz bir uyumsuzluk yaratirdi: ajan
/// guncellenmis gorunur, sunucuya eski surumu bildirirdi.
/// </summary>
public static class SurumBilgisi
{
    public static string Oku()
    {
        var s = typeof(SurumBilgisi).Assembly.GetName().Version;
        return s is null ? "0.0.0" : $"{s.Major}.{s.Minor}.{s.Build}";
    }
}

/// <summary>
/// Ajanin hub'a bagli kalmasindan sorumlu dongu.
///
/// Bu tur <b>yalniz baglanti</b>: bagla, kendini tanit, kalp atisi gonder, ORKA
/// durumunu bildir. Is alma ve calistirma C adiminda; JSON adim motoruna burada
/// dokunulmuyor.
///
/// <b>Yeniden baglanma neden elde yazildi:</b> SignalR'in
/// <c>WithAutomaticReconnect</c>'i elindeki token'i aynen tekrar kullaniyor.
/// Token 8 saatte bayatladigi icin, gece kopan bir baglanti sabaha kadar
/// susmadan basarisiz denemeler yapardi. Buradaki dongu once token tazeligine
/// bakiyor, gerekirse yeniliyor, sonra bagliyor.
/// </summary>
public sealed class AjanServisi : IAsyncDisposable
{
    private readonly IHubFabrikasi _fabrika;
    private readonly AjanTokenSaglayici _tokenSaglayici;
    private readonly IOrkaDurumu _orka;
    private readonly AjanKimlik _kimlik;
    private readonly string _hubAdresi;
    private readonly TimeSpan _kalpAtisiAraligi;
    private readonly IAjanLog _log;
    private readonly Func<TimeSpan, CancellationToken, Task> _bekle;
    private readonly IReadOnlyList<IIsCalistirici> _calistiricilar;

    private IHubBaglantisi? _baglanti;
    private bool? _sonBildirilenOrka;

    // Ayni anda tek is: robot tek ORKA penceresiyle calisiyor. Sunucu da ayni
    // kurali uyguluyor; buradaki, sunucunun yanildigi durumda son savunma.
    private readonly object _isKilidi = new();
    private Guid _calisanIsId;
    private CancellationTokenSource? _isIptali;
    private Task _isGorevi = Task.CompletedTask;

    // Is neden durduruluyor: kullanicinin iptali mi, ajanin kapanmasi mi?
    // Ikisi de ayni iptal jetonunu tetikliyor ama kullaniciya soylenecek sey
    // farkli.
    private string? _isDurdurmaSebebi;

    public AjanServisi(
        IHubFabrikasi fabrika,
        AjanTokenSaglayici tokenSaglayici,
        IOrkaDurumu orka,
        AjanKimlik kimlik,
        string hubAdresi,
        TimeSpan kalpAtisiAraligi,
        IAjanLog log,
        Func<TimeSpan, CancellationToken, Task>? bekle = null,
        IEnumerable<IIsCalistirici>? calistiricilar = null)
    {
        _fabrika = fabrika;
        _tokenSaglayici = tokenSaglayici;
        _orka = orka;
        _kimlik = kimlik;
        _hubAdresi = hubAdresi;
        _kalpAtisiAraligi = kalpAtisiAraligi;
        _log = log;
        _bekle = bekle ?? ((sure, ct) => Task.Delay(sure, ct));
        _calistiricilar = (calistiricilar ?? Array.Empty<IIsCalistirici>()).ToList();
    }

    /// <summary>Su an calisan isin kimligi; is yoksa <see cref="Guid.Empty"/>.</summary>
    public Guid CalisanIsId
    {
        get { lock (_isKilidi) return _calisanIsId; }
    }

    /// <summary>
    /// Sunucu kaydi reddettiyse (eski surum) true. Bu bir ag sorunu degil:
    /// guncelleme gerekiyor, yeniden denemenin anlami yok.
    /// </summary>
    public bool KayitKaliciReddedildi { get; private set; }

    /// <summary>Su an bagli miyiz?</summary>
    public bool Bagli => _baglanti?.Bagli == true;

    /// <summary>
    /// Sunucuya en son ne zaman canlilik bildirildi.
    ///
    /// Yalniz <b>okunan</b> bir damga; baglanti mantigina karismiyor. Arayuz
    /// "bagli" yazmakla yetinemez: kopmus ama henuz fark edilmemis bir
    /// baglantida da "bagli" gorunurdu. Son atisin uzerinden gecen sure, o
    /// durumu ekranda goren tek isaret.
    /// </summary>
    public DateTime? SonKalpAtisi { get; private set; }

    /// <summary>Token al, bagla, kendini tanit. Kabul edilirse true.</summary>
    public async Task<bool> BaglanVeKaydolAsync(CancellationToken ct = default)
    {
        var token = await _tokenSaglayici.TokenAlAsync(ct);

        await KapatAsync();
        _baglanti = _fabrika.Olustur(_hubAdresi, token);

        // Dinleyiciler baglanti kurulmadan once: sunucu, kayit kabul edilir
        // edilmez bekleyen isi gonderiyor.
        _baglanti.IsGeldiginde(IsGeldiAsync);
        _baglanti.IsIptalEdildiginde(IsIptalGeldiAsync);

        await _baglanti.BaslatAsync(ct);

        var orkaSuAn = _orka.CalisiyorMu();
        var sonuc = await _baglanti.KaydolAsync(Istek(orkaSuAn), ct);

        if (!sonuc.Kabul)
        {
            // Surum reddi: mesaji goster ve birak. Sunucu asgari surumu de
            // gonderiyor, kullanicinin ne yapacagi belli olsun.
            KayitKaliciReddedildi = true;
            _log.Hata($"Sunucu kaydi reddetti: {sonuc.Mesaj}");
            _log.Hata($"Sunucu surumu {sonuc.SunucuSurumu}, asgari ajan surumu " +
                      $"{sonuc.AsgariAjanSurumu}, bu ajan {SurumBilgisi.Oku()}.");
            _log.Hata("PkfRobot guncellenmeden baglanti kurulamaz.");

            await KapatAsync();
            return false;
        }

        _sonBildirilenOrka = orkaSuAn;
        SonKalpAtisi = DateTime.Now;
        _log.Bilgi($"Hub'a baglanildi: {_kimlik.MakineAdi} ({_kimlik.MakineId}), " +
                   $"surum {_kimlik.AjanSurumu}, ORKA: {(orkaSuAn ? "acik" : "kapali")}.");
        return true;
    }

    /// <summary>
    /// Bir kalp atisi turu: canliligi bildir, token tazeligini gozet, ORKA
    /// durumu degistiyse haber ver.
    /// </summary>
    public async Task NabizAsync(CancellationToken ct = default)
    {
        if (_baglanti is null)
            throw new InvalidOperationException("Baglanti kurulmadan nabiz atilamaz.");

        await _baglanti.KalpAtisiAsync(ct);
        SonKalpAtisi = DateTime.Now;

        // Token'i bayatlamadan tazele: yenileme, baglanti koptugu ana denk
        // gelmesin diye kopusu beklemiyor.
        if (!_tokenSaglayici.TokenTaze)
        {
            try
            {
                await _tokenSaglayici.TokenAlAsync(ct);
            }
            catch (AjanAnahtariGecersizException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Gecici sorun: acik baglanti calismaya devam ediyor, bir sonraki
                // turda yine denenir.
                _log.Uyari($"Token tazelenemedi, sonraki turda tekrar denenecek: {ex.Message}");
            }
        }

        var orkaSuAn = _orka.CalisiyorMu();
        if (orkaSuAn == _sonBildirilenOrka) return;

        // Sunucuda ayri bir "durum bildir" cagrisi yok; kayit paketinin kendisi
        // ORKA alanini tasiyor. Ayni baglantidan ikinci kez Kaydol cagirmak
        // sunucu tarafinda "bilgi tazeleme" sayiliyor, dusurme degil.
        var sonuc = await _baglanti.KaydolAsync(Istek(orkaSuAn), ct);
        if (!sonuc.Kabul)
        {
            _log.Uyari($"ORKA durumu bildirilemedi: {sonuc.Mesaj}");
            return;
        }

        _sonBildirilenOrka = orkaSuAn;
        _log.Bilgi($"ORKA durumu degisti: {(orkaSuAn ? "acildi" : "kapandi")}. Sunucuya bildirildi.");
    }

    /// <summary>
    /// Ana dongu: bagli kal. Kopusta geri cekilerek sonsuz dene -- gece ag
    /// koparsa sabah bagli olmali.
    /// </summary>
    public async Task CalistirAsync(CancellationToken ct)
    {
        var geriCekilme = new GeriCekilme();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (await BaglanVeKaydolAsync(ct))
                {
                    geriCekilme.Sifirla();

                    while (!ct.IsCancellationRequested && Bagli)
                    {
                        await _bekle(_kalpAtisiAraligi, ct);
                        if (ct.IsCancellationRequested) break;
                        await NabizAsync(ct);
                    }

                    if (!ct.IsCancellationRequested)
                        _log.Uyari("Hub baglantisi koptu.");
                }
                else if (KayitKaliciReddedildi)
                {
                    return;
                }
            }
            catch (AjanAnahtariGecersizException)
            {
                // Mesaj token saglayicida zaten yazildi. Sonsuz donguye girmenin
                // anlami yok: yeni anahtar gerekiyor.
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Uyari($"Baglanti hatasi: {ex.Message}");
            }
            finally
            {
                // Kapanis bildirimi baglanti HENUZ ayaktayken gonderiliyor;
                // KapatAsync'ten sonra gonderecek bir kanal kalmiyor.
                if (ct.IsCancellationRequested)
                    await CalisanIsiSonlandirAsync("Ajan kapatildi; is yarida kesildi.");

                await KapatAsync();
            }

            if (ct.IsCancellationRequested) break;

            var sure = geriCekilme.Sonraki();
            _log.Bilgi($"{sure.TotalSeconds:0} sn sonra yeniden baglanilacak.");

            try
            {
                await _bekle(sure, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _log.Bilgi("Ajan durduruldu.");
    }

    // ---- is alma ve yurutme ------------------------------------------------

    /// <summary>
    /// Sunucudan is paketi geldi. Is arka planda yuruyor: bu geri cagri hub'in
    /// mesaj hattinda calisiyor, burada beklemek kalp atisini ve iptal
    /// bildirimini de bloklardi.
    /// </summary>
    private Task IsGeldiAsync(AjanIsPaketi paket)
    {
        var calistirici = _calistiricilar.FirstOrDefault(c => c.Destekliyor(paket.IsTipi));
        if (calistirici is null)
        {
            _log.Hata($"Bilinmeyen is tipi: '{paket.IsTipi}' ({paket.IsId}). " +
                      "Ajan guncel degil olabilir.");
            return BildirBittiAsync(paket.IsId, false,
                $"Ajan '{paket.IsTipi}' is tipini tanimiyor; PkfRobot guncellenmeli.", null);
        }

        lock (_isKilidi)
        {
            if (_calisanIsId != Guid.Empty)
            {
                _log.Uyari($"Zaten calisan is var ({_calisanIsId}); yeni is reddedildi: {paket.IsId}");
                return BildirBittiAsync(paket.IsId, false,
                    "Ajanda zaten calisan bir is var; ayni anda tek is yurutulebiliyor.", null);
            }

            _calisanIsId = paket.IsId;
            _isDurdurmaSebebi = null;
            _isIptali = new CancellationTokenSource();
            _isGorevi = IsiYurutAsync(paket, calistirici, _isIptali.Token);
        }

        return Task.CompletedTask;
    }

    private Task IsIptalGeldiAsync(Guid isId)
    {
        lock (_isKilidi)
        {
            if (_calisanIsId != isId)
            {
                _log.Bilgi($"Iptal bildirimi calisan ise ait degil, atlandi: {isId}");
                return Task.CompletedTask;
            }

            _log.Uyari($"Is iptal edildi: {isId}");
            _isDurdurmaSebebi = "Is iptal edildi. ORKA'da yarim kalmis giris olabilir, " +
                                "kaydetmeden kontrol edin.";
            _isIptali?.Cancel();
        }

        return Task.CompletedTask;
    }

    private async Task IsiYurutAsync(AjanIsPaketi paket, IIsCalistirici calistirici, CancellationToken ct)
    {
        IsSonucu sonuc;

        try
        {
            await BildirBasladiAsync(paket.IsId);
            sonuc = await calistirici.CalistirAsync(paket, new HubIlerleme(this, paket.IsId), ct);
        }
        catch (OperationCanceledException)
        {
            sonuc = IsSonucu.Hata(_isDurdurmaSebebi
                ?? "Is yarida kesildi. ORKA'da yarim kalmis giris olabilir, kaydetmeden kontrol edin.");
        }
        catch (Exception ex)
        {
            _log.Hata($"Is basarisiz ({paket.IsId}): {ex.Message}");
            sonuc = IsSonucu.Hata(ex.Message);
        }
        finally
        {
            lock (_isKilidi)
            {
                _calisanIsId = Guid.Empty;
                _isIptali?.Dispose();
                _isIptali = null;
            }
        }

        await BildirBittiAsync(paket.IsId, sonuc.Basarili, sonuc.HataMesaji,
                               sonuc.SonucOzetiJson, sonuc.HataEkraniDosyaId);
    }

    private async Task BildirBasladiAsync(Guid isId)
    {
        try { if (_baglanti is not null) await _baglanti.IsBasladiAsync(isId, CancellationToken.None); }
        catch (Exception ex) { _log.Uyari($"Is baslangici bildirilemedi ({isId}): {ex.Message}"); }
    }

    internal async Task BildirIlerlemeAsync(Guid isId, int yuzde, string mesaj, int? tamamlananAdim, CancellationToken ct)
    {
        // Bildirim gonderilemezse is durmuyor: baglanti kopmussa sunucu zaten
        // isi basarisiz sayacak, kopmamissa bir sonraki bildirim yakalar.
        try { if (_baglanti is not null) await _baglanti.IsIlerlemeAsync(isId, yuzde, mesaj, tamamlananAdim, ct); }
        catch (Exception ex) { _log.Uyari($"Ilerleme bildirilemedi ({isId}): {ex.Message}"); }
    }

    private async Task BildirBittiAsync(Guid isId, bool basarili, string? hata, string? ozet,
                                        string? hataEkraniDosyaId = null)
    {
        try
        {
            if (_baglanti is not null)
                await _baglanti.IsBittiAsync(isId, basarili, hata, ozet, hataEkraniDosyaId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _log.Uyari($"Is sonucu bildirilemedi ({isId}): {ex.Message}");
        }
    }

    /// <summary>
    /// Ajan kapanirken calisan is varsa sunucuya haber verir. Yoksa is, zaman
    /// asimina ugrayana kadar (varsayilan 15 dk) "calisiyor" gorunurdu.
    /// </summary>
    private async Task CalisanIsiSonlandirAsync(string sebep)
    {
        Guid isId;
        Task gorev;
        lock (_isKilidi)
        {
            isId = _calisanIsId;
            gorev = _isGorevi;
            _isDurdurmaSebebi = sebep;
            _isIptali?.Cancel();
        }

        if (isId == Guid.Empty) return;

        _log.Uyari($"Calisan is yarida kesiliyor: {isId} ({sebep})");

        // Once isin kendi bitis bildirimini gondermesi bekleniyor; iki kez
        // bildirmek sunucuda zararsiz ama log'u ve gecmisi bulandirir.
        var bittiMi = await Task.WhenAny(gorev, Task.Delay(TimeSpan.FromSeconds(3))) == gorev;
        if (bittiMi) return;

        // Calistirici iptali yutmus ya da takilmis: son soz sunucuya yine de
        // gitsin, is "calisiyor" kalmasin.
        _log.Uyari($"Is kendi bitisini bildirmedi, sonuc dogrudan gonderiliyor: {isId}");
        await BildirBittiAsync(isId, false, sebep, null);
    }

    /// <summary>Calistiriciya verilen ilerleme agzi.</summary>
    private sealed class HubIlerleme : IIsIlerleme
    {
        private readonly AjanServisi _servis;
        private readonly Guid _isId;

        public HubIlerleme(AjanServisi servis, Guid isId)
        {
            _servis = servis;
            _isId = isId;
        }

        public Task BildirAsync(int yuzde, string mesaj, int? tamamlananAdim = null, CancellationToken ct = default)
            => _servis.BildirIlerlemeAsync(_isId, yuzde, mesaj, tamamlananAdim, ct);
    }

    private AjanKaydiIstegi Istek(bool orkaCalisiyorMu) => new()
    {
        MakineId = _kimlik.MakineId,
        MakineAdi = _kimlik.MakineAdi,
        AjanSurumu = _kimlik.AjanSurumu,
        IsletimSistemi = _kimlik.IsletimSistemi,
        OrkaCalisiyorMu = orkaCalisiyorMu
    };

    private async Task KapatAsync()
    {
        if (_baglanti is null) return;

        try
        {
            await _baglanti.DisposeAsync();
        }
        catch
        {
            // Kapanirken cikan hata onemli degil; zaten birakiyoruz.
        }

        _baglanti = null;
        _sonBildirilenOrka = null;
    }

    public async ValueTask DisposeAsync() => await KapatAsync();
}

/// <summary>Ajanin sunucuya bildirdigi kimlik bilgileri.</summary>
public sealed class AjanKimlik
{
    public required string MakineId { get; init; }
    public required string MakineAdi { get; init; }
    public required string AjanSurumu { get; init; }
    public string? IsletimSistemi { get; init; }

    public static AjanKimlik Olustur(string makineId) => new()
    {
        MakineId = makineId,
        MakineAdi = Environment.MachineName,
        AjanSurumu = SurumBilgisi.Oku(),
        IsletimSistemi = Environment.OSVersion.VersionString
    };
}
