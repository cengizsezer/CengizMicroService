namespace PkfRobot.Ajan;

/// <summary>
/// Yeniden baglanma aralilari: 5s, 10s, 30s, 60s, sonra 60s sabit.
///
/// Ustel ama tavanli: gece ag koparsa sabah baglantinin kurulmus olmasi
/// gerekiyor, dolayisiyla aralik sonsuza kadar buyuyemez. Ilk adimlarin kisa
/// olmasinin sebebi ise kopuslarin cogunun saniyelik olmasi -- bir dakika
/// beklemek gereksiz bir bosluk yaratirdi.
/// </summary>
public sealed class GeriCekilme
{
    private static readonly TimeSpan[] Adimlar =
    {
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60)
    };

    private int _sira;

    /// <summary>Sonraki bekleme suresi; son adima gelince orada kalir.</summary>
    public TimeSpan Sonraki()
    {
        var sure = Adimlar[Math.Min(_sira, Adimlar.Length - 1)];
        if (_sira < Adimlar.Length - 1) _sira++;
        return sure;
    }

    /// <summary>Baglanti kurulunca cagrilir: bir sonraki kopusta yine 5s'den baslasin.</summary>
    public void Sifirla() => _sira = 0;
}
