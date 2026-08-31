using PkfRobot.Ajan;

namespace PkfRobot.Arayuz;

/// <summary>Ekranda gosterilen tek bir is satiri.</summary>
public record IsGecmisiKaydi(
    Guid IsId,
    string IsTipi,
    DateTime Basladi,
    DateTime? Bitti = null,
    bool? Basarili = null,
    string? Mesaj = null)
{
    public TimeSpan Sure => (Bitti ?? DateTime.Now) - Basladi;

    public string SonucMetni => Basarili switch
    {
        null => "calisiyor",
        true => "basarili",
        _ => "basarisiz"
    };
}

/// <summary>
/// Calisan isi ve son islerin ozetini tutan defter.
///
/// <b>Neden ayri bir sinif:</b> ajan servisine "son bes isi sakla" diye bir alan
/// eklemek, baglanti katmanina arayuz isi yuklemek olurdu. Burasi onu disaridan
/// izliyor: <see cref="IzlenenCalistirici"/> her isi sarmalayip buraya
/// bildiriyor, <see cref="AjanServisi"/> degismiyor.
/// </summary>
public sealed class IsIzleyici
{
    /// <summary>Ekranda gosterilen is sayisi; gorev metnindeki "son bes is".</summary>
    public const int GecmisSiniri = 5;

    private readonly object _kilit = new();
    private readonly List<IsGecmisiKaydi> _gecmis = new();

    private IsGecmisiKaydi? _calisan;
    private int _yuzde;
    private string _ilerlemeMesaji = string.Empty;

    /// <summary>Herhangi bir sey degisti; ekran kendini tazelesin.</summary>
    public event Action? Degisti;

    public IsGecmisiKaydi? Calisan { get { lock (_kilit) return _calisan; } }

    public int Yuzde { get { lock (_kilit) return _yuzde; } }

    public string IlerlemeMesaji { get { lock (_kilit) return _ilerlemeMesaji; } }

    /// <summary>En yeni en ustte.</summary>
    public IReadOnlyList<IsGecmisiKaydi> SonIsler
    {
        get { lock (_kilit) return _gecmis.ToList(); }
    }

    public void Basladi(Guid isId, string isTipi)
    {
        lock (_kilit)
        {
            _calisan = new IsGecmisiKaydi(isId, isTipi, DateTime.Now);
            _yuzde = 0;
            _ilerlemeMesaji = "Baslatildi";
        }

        Degisti?.Invoke();
    }

    public void Ilerledi(int yuzde, string mesaj)
    {
        lock (_kilit)
        {
            _yuzde = Math.Clamp(yuzde, 0, 100);
            _ilerlemeMesaji = mesaj;
        }

        Degisti?.Invoke();
    }

    public void Bitti(Guid isId, bool basarili, string? mesaj)
    {
        lock (_kilit)
        {
            var kayit = _calisan is { } c && c.IsId == isId
                ? c with { Bitti = DateTime.Now, Basarili = basarili, Mesaj = mesaj }
                : new IsGecmisiKaydi(isId, "?", DateTime.Now, DateTime.Now, basarili, mesaj);

            _gecmis.Insert(0, kayit);
            if (_gecmis.Count > GecmisSiniri) _gecmis.RemoveRange(GecmisSiniri, _gecmis.Count - GecmisSiniri);

            _calisan = null;
            _yuzde = 0;
            _ilerlemeMesaji = string.Empty;
        }

        Degisti?.Invoke();
    }
}

/// <summary>
/// Bir <see cref="IIsCalistirici"/>'yi sarmalayip basla/ilerle/bitti anlarini
/// <see cref="IsIzleyici"/>'ye bildiren katman.
///
/// Sarmalama, arayuzun ise <b>karismadan</b> onu gormesini sagliyor: is yine
/// asil calistiricida yuruyor, ilerleme yine sunucuya gidiyor. Bu katman
/// cikarilirsa ajan aynen calismaya devam eder.
/// </summary>
public sealed class IzlenenCalistirici : IIsCalistirici
{
    private readonly IIsCalistirici _ic;
    private readonly IsIzleyici _izleyici;

    public IzlenenCalistirici(IIsCalistirici ic, IsIzleyici izleyici)
    {
        _ic = ic;
        _izleyici = izleyici;
    }

    public bool Destekliyor(string isTipi) => _ic.Destekliyor(isTipi);

    public async Task<IsSonucu> CalistirAsync(AjanIsPaketi paket, IIsIlerleme ilerleme, CancellationToken ct)
    {
        _izleyici.Basladi(paket.IsId, paket.IsTipi);

        try
        {
            var sonuc = await _ic.CalistirAsync(paket, new IzlenenIlerleme(ilerleme, _izleyici), ct);
            _izleyici.Bitti(paket.IsId, sonuc.Basarili, sonuc.HataMesaji);
            return sonuc;
        }
        catch (OperationCanceledException)
        {
            _izleyici.Bitti(paket.IsId, false, "Is yarida kesildi.");
            throw;
        }
        catch (Exception ex)
        {
            _izleyici.Bitti(paket.IsId, false, ex.Message);
            throw;
        }
    }

    /// <summary>Ilerlemeyi once sunucuya, sonra ekrana tasiyan agiz.</summary>
    private sealed class IzlenenIlerleme : IIsIlerleme
    {
        private readonly IIsIlerleme _ic;
        private readonly IsIzleyici _izleyici;

        public IzlenenIlerleme(IIsIlerleme ic, IsIzleyici izleyici)
        {
            _ic = ic;
            _izleyici = izleyici;
        }

        public async Task BildirAsync(int yuzde, string mesaj, int? tamamlananAdim = null,
                                      CancellationToken ct = default)
        {
            _izleyici.Ilerledi(yuzde, mesaj);
            await _ic.BildirAsync(yuzde, mesaj, tamamlananAdim, ct);
        }
    }
}

/// <summary>
/// Log satirlarini hem asil aga hem ekrana veren agiz.
///
/// Ekranda log penceresi olmasi log dosyasini gereksiz kilmiyor: dosya ofiste
/// sonradan bakilan yer, pencere ise o anda ne oldugunu goren yer.
/// </summary>
public sealed class CiftYonluLog : IAjanLog
{
    /// <summary>Ekranda tutulan satir sayisi; pencere sonsuz buyumesin.</summary>
    public const int SatirSiniri = 500;

    private readonly object _kilit = new();
    private readonly LinkedList<string> _satirlar = new();

    /// <summary>
    /// Asil log agzi. Ajan baslayana kadar <c>null</c>: dosyayi ajan kendi
    /// aciyor ve ayni gunluk dosyayi ikinci bir yazicinin acmasi hataya yol
    /// acardi. Ajan basladiginda kendi dosya log'unu buraya takiyor.
    /// </summary>
    public IAjanLog? Ic { get; set; }

    public event Action<string>? SatirGeldi;

    public IReadOnlyList<string> Satirlar
    {
        get { lock (_kilit) return _satirlar.ToList(); }
    }

    public void Bilgi(string mesaj) => Yaz("BILGI", mesaj, l => l.Bilgi(mesaj));
    public void Uyari(string mesaj) => Yaz("UYARI", mesaj, l => l.Uyari(mesaj));
    public void Hata(string mesaj) => Yaz("HATA ", mesaj, l => l.Hata(mesaj));

    private void Yaz(string seviye, string mesaj, Action<IAjanLog> icYaz)
    {
        if (Ic is { } ic) icYaz(ic);

        // Maske ic log'da da uygulaniyor ama ekrana giden kopya oradan gecmiyor;
        // sir ekranda da gorunmemeli.
        var satir = $"{DateTime.Now:HH:mm:ss} [{seviye}] {AjanLogMaskesi.Maskele(mesaj)}";

        lock (_kilit)
        {
            _satirlar.AddLast(satir);
            while (_satirlar.Count > SatirSiniri) _satirlar.RemoveFirst();
        }

        SatirGeldi?.Invoke(satir);
    }
}
