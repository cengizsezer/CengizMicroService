using System.Globalization;

namespace PkfRobot.Core;

/// <summary>
/// Sureleri INSAN OKUYACAK sekilde yazar.
///
/// <b>Neden ayri:</b> ayni sure uc yerde gorunuyor (log satiri, formdaki canli
/// sayac, gorev sonu ozeti) ve ucunun ayni okunmasi gerekiyor -- "45.1 sn" ile
/// "00:00:45.1234567" yan yana durunca hangi adimin zaman yedigi gozle
/// karsilastirilamaz.
///
/// <b>Neden InvariantCulture:</b> makine Turkce ve ondalik ayraci virgul;
/// "3,2 sn" log'da tek basina sorun degil ama ayni satirdaki virgulle ayrilmis
/// adim listesinde ("[26] X 45,1 sn, [14] Y 4,5 sn") nerede yeni adimin
/// basladigi okunmaz hale geliyor.
/// </summary>
public static class SureBicimi
{
    /// <summary>Bir dakikanin altinda "3.2 sn", ustunde "4 dk 12 sn".</summary>
    public static string Kisa(TimeSpan sure)
    {
        if (sure < TimeSpan.Zero) sure = TimeSpan.Zero;

        return sure.TotalSeconds < 60
            ? string.Format(CultureInfo.InvariantCulture, "{0:0.0} sn", sure.TotalSeconds)
            : string.Format(CultureInfo.InvariantCulture, "{0} dk {1} sn",
                            (int)sure.TotalMinutes, sure.Seconds);
    }
}

/// <summary>Biten bir adimin olcumu: kacinci adim, ne tipte, ne kadar surdu.</summary>
/// <param name="No">Gorev icindeki sira (1'den baslar).</param>
/// <param name="Tip">Adim tipi (<c>BeklePencere</c>, <c>Bekle</c>...).</param>
public record AdimOlcumu(int No, string Tip, TimeSpan Sure);

/// <summary>
/// Calismakta olan adimin formda gosterilecek hali.
///
/// <b>Neden (adim, toplam) ikilisi yetmiyordu:</b> "adim 12/38" satiri hangi
/// adimda beklenildigini soylemiyor; 45 saniye duran bir
/// <c>AltPencereDogrula</c> ile 4 saniyelik bir <c>Bekle</c> ekranda birebir
/// ayni goruniyordu.
/// </summary>
/// <param name="Etiket">"BeklePencere 'Hesap Plan'" gibi tek satirlik tanim.</param>
public record AdimIlerlemesi(int No, int Toplam, string Etiket)
{
    /// <summary>Formda ve log'da kullanilan tek satirlik hali.</summary>
    public override string ToString() => $"adim {No}/{Toplam} -- {Etiket}";
}

/// <summary>
/// Gorev bitince "hangi adim zaman yedi" sorusunu tek satirda cevaplar.
///
/// <b>Neden en yavaslar, toplam degil:</b> toplam sure bir seyin yavas
/// oldugunu soyluyor ama NEYIN yavas oldugunu soylemiyor. Beklemeyi kisaltmak
/// icin once o adimi bulmak gerekiyor ve 38 adimlik bir log'da bunu gozle
/// aramak, her denemede bastan yapilan bir is.
/// </summary>
public static class AdimSureOzeti
{
    public const int VarsayilanAdet = 5;

    /// <summary>En uzun suren adimlar, uzundan kisaya.</summary>
    public static IReadOnlyList<AdimOlcumu> EnYavaslar(IEnumerable<AdimOlcumu> olcumler,
                                                       int adet = VarsayilanAdet)
        => olcumler.OrderByDescending(o => o.Sure)
                   .ThenBy(o => o.No)
                   .Take(Math.Max(0, adet))
                   .ToList();

    /// <summary>
    /// "En yavas adimlar: [26] AltPencereDogrula 45.1 sn, [14] Bekle 4.5 sn"
    /// Hic olcum yoksa BOS metin: cagiran taraf bos satiri log'a yazmiyor.
    /// </summary>
    public static string Ozet(IEnumerable<AdimOlcumu> olcumler, int adet = VarsayilanAdet)
    {
        var enYavaslar = EnYavaslar(olcumler, adet);
        if (enYavaslar.Count == 0) return string.Empty;

        return "En yavas adimlar: " +
               string.Join(", ", enYavaslar.Select(o => $"[{o.No}] {o.Tip} {SureBicimi.Kisa(o.Sure)}"));
    }
}

/// <summary>
/// Calistir sekmesindeki tek satirlik ilerleme metni.
///
/// <b>Neden formdan ayri:</b> "2/4 banka bitti" ile "adim 12/38" arasindaki
/// bicim kararlari WinForms olmadan sinanabilmeli; ozellikle adim bilgisi
/// YOKKEN (kuyruk baslarken, gorevler arasinda) satirin bos ya da yarim
/// kalmamasi.
/// </summary>
public static class IlerlemeMetni
{
    public static string Yaz(int bitenBanka, int toplamBanka, string? adimMetni,
                             TimeSpan adimSuresi, TimeSpan toplamSure)
    {
        var parcalar = new List<string> { $"{bitenBanka}/{toplamBanka} banka bitti" };

        if (!string.IsNullOrWhiteSpace(adimMetni))
        {
            parcalar.Add(adimMetni.Trim());
            parcalar.Add(SureBicimi.Kisa(adimSuresi));
        }

        parcalar.Add($"toplam {SureBicimi.Kisa(toplamSure)}");

        return string.Join(" · ", parcalar);
    }
}
