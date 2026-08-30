namespace PkfRobot.Core;

/// <summary>
/// Log'a duz yazilmamasi gereken alanlarin adlari.
///
/// Kural ad uzerinden yuruyor, tip uzerinden degil: gorev JSON'lari elle
/// yaziliyor ve yeni bir sir alani eklendiginde kimse maskeleme kodunu
/// guncellemeyi hatirlamiyor. Adinda bu sozcuklerden biri gecen her deger
/// kendiliginden maskeleniyor -- yeni bir sir eklerken yapilacak tek sey ona
/// dogru adi vermek.
/// </summary>
public static class Hassas
{
    /// <summary>
    /// <c>sifre</c> ORKA giris/firma sifreleri icin; digerleri ajan kimligi
    /// icin (<c>agent.dat</c>, ajan anahtari, ajan token'i).
    /// </summary>
    public static readonly string[] Sozcukler = { "sifre", "anahtar", "token", "agent" };

    public static bool Iceriyor(string? metin)
    {
        if (string.IsNullOrEmpty(metin)) return false;

        foreach (var s in Sozcukler)
        {
            if (metin.Contains(s, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
