namespace PkfRobot.Core;

/// <summary>Kullanici DURDUR dedigi icin akis kesildi. Hata degil, karar.</summary>
public sealed class CalismaDurduruldu : Exception
{
    public CalismaDurduruldu() : base("Calisma kullanici tarafindan durduruldu.") { }
}

public enum CalismaHali
{
    Calisiyor,
    Duraklatildi,
    Durduruldu
}

/// <summary>
/// START / DURDUR / DEVAM dugmelerinin akistaki karsiligi.
///
/// <b>Neden adim motoruna dokunmuyor:</b> <see cref="AdimMotoru"/> her adimdan
/// once <c>adimBasladi</c> geri cagirmasini yapiyor. Denetim oraya takiliyor:
/// DURAKLAT o cagride bloke oluyor, DEVAM birakiyor, DURDUR
/// <see cref="CalismaDurduruldu"/> atiyor. Motorun kendisi durdurulabilirligi
/// bilmiyor ve bilmesi de gerekmiyor.
///
/// <b>Neden adim ORTASINDA degil ARASINDA duruyor:</b> yarim kalmis bir adim
/// ORKA'yi bilinmeyen bir ekranda birakir -- yazilmakta olan bir hesap kodunun
/// yarisi gitmis olabilir. Calisan adim bitiyor, sonrakine gecilmiyor; DEVAM
/// kaldigi adimdan suruyor.
/// </summary>
public sealed class CalismaDenetimi
{
    private readonly ManualResetEventSlim _devam = new(initialState: true);
    private volatile bool _duraklatildi;
    private volatile bool _durduruldu;

    /// <summary>Hal degisti; ekran dugmeleri kendini tazelesin.</summary>
    public event Action? Degisti;

    public CalismaHali Hal => _durduruldu ? CalismaHali.Durduruldu
                            : _duraklatildi ? CalismaHali.Duraklatildi
                            : CalismaHali.Calisiyor;

    public bool Duraklatildi => _duraklatildi;
    public bool Durduruldu => _durduruldu;

    /// <summary>Yeni bir kuyruk baslarken onceki turun kalintisi kalmasin.</summary>
    public void Sifirla()
    {
        _durduruldu = false;
        _duraklatildi = false;
        _devam.Set();
        Degisti?.Invoke();
    }

    public void Duraklat()
    {
        if (_durduruldu || _duraklatildi) return;

        _duraklatildi = true;
        _devam.Reset();
        Degisti?.Invoke();
    }

    public void Devam()
    {
        if (_durduruldu || !_duraklatildi) return;

        _duraklatildi = false;
        _devam.Set();
        Degisti?.Invoke();
    }

    /// <summary>
    /// Durdurur. Duraklatilmis bir akis da uyandirilir: bekleyen adim
    /// <see cref="CalismaDurduruldu"/> ile cikar, yoksa DURDUR sonrasi
    /// "duraklatilmis ama olmus" bir gorev parcacigi kalirdi.
    /// </summary>
    public void Durdur()
    {
        _durduruldu = true;
        _duraklatildi = false;
        _devam.Set();
        Degisti?.Invoke();
    }

    /// <summary>
    /// Her adimin basinda cagrilir. Duraklatilmissa DEVAM'a kadar bloke olur;
    /// durdurulmussa <see cref="CalismaDurduruldu"/> atar.
    /// </summary>
    public void AdimOncesiBekle()
    {
        DurdurulduysaAt();

        if (!_devam.IsSet) _devam.Wait();

        // Duraklamadan cikis DEVAM ile de DURDUR ile de olabilir; ikinci kontrol
        // olmadan durdurulmus bir akis bir adim daha calistirirdi.
        DurdurulduysaAt();
    }

    private void DurdurulduysaAt()
    {
        if (_durduruldu) throw new CalismaDurduruldu();
    }
}
