using System.Diagnostics;

namespace PkfRobot.Ajan;

/// <summary>ORKA su an ayakta mi?</summary>
public interface IOrkaDurumu
{
    bool CalisiyorMu();
}

/// <summary>
/// ORKA'yi surec listesinden okur.
///
/// Pencere basligina degil <b>surece</b> bakiliyor: ORKA acikken de kapali gibi
/// gorunebilecegi tek durum, pencere basliginin degismesi olurdu ve baslik
/// ORKA'nin surumune gore degisiyor (bkz. OKUBENI). Surec adi sabit.
///
/// ORKA'nin calismasi baglanti icin sart degil -- ajan ORKA kapaliyken de bagli
/// kalir, yalnizca durumu bildirir.
/// </summary>
public sealed class OrkaSureci : IOrkaDurumu
{
    private readonly string _surecAdi;

    public OrkaSureci(string surecAdi) => _surecAdi = surecAdi;

    public bool CalisiyorMu()
    {
        Process[] surecler;
        try
        {
            surecler = Process.GetProcessesByName(_surecAdi);
        }
        catch
        {
            // Surec listesi okunamiyorsa (yetki) "bilmiyorum" demek yerine
            // "calismiyor" demek yaniltici olurdu; ama arayuz bool donuyor ve
            // ORKA'nin kapali gorunmesi zararsiz. Ajan calismaya devam eder.
            return false;
        }

        try
        {
            return surecler.Length > 0;
        }
        finally
        {
            foreach (var s in surecler) s.Dispose();
        }
    }
}
