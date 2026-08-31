namespace PkfRobot.Ayarlar;

/// <summary>Bir koordinat secme denemesinin sonucu.</summary>
/// <param name="Kabul">Deger kaydedilebilir mi?</param>
/// <param name="OranX">Kabul edildiyse pencereye goreli yatay oran.</param>
/// <param name="OranY">Kabul edildiyse pencereye goreli dikey oran.</param>
/// <param name="Mesaj">Kullaniciya gosterilecek cumle; red sebebi ya da uyari.</param>
/// <param name="Uyari">Kabul edildi ama dikkat edilmesi gereken bir sey var.</param>
public record KoordinatSecimSonucu(
    bool Kabul,
    double OranX,
    double OranY,
    string? Mesaj = null,
    bool Uyari = false);

/// <summary>Tiklama aninda ekrandan okunan ham durum.</summary>
/// <param name="MutlakX">Tiklanan noktanin ekran koordinati.</param>
/// <param name="MutlakY">Tiklanan noktanin ekran koordinati.</param>
/// <param name="OrkaPenceresi">ORKA ana penceresinin olculeri; ORKA yoksa null.</param>
/// <param name="TiklananPencereSureci">Tiklanan noktadaki pencerenin surec kimligi.</param>
/// <param name="OrkaSurecleri">ORKA'ya ait surec kimlikleri.</param>
/// <param name="TamEkranMi">ORKA penceresi maximize durumda mi?</param>
public record TiklamaOrtami(
    int MutlakX,
    int MutlakY,
    PencereOlcusu? OrkaPenceresi,
    int? TiklananPencereSureci,
    IReadOnlyCollection<int> OrkaSurecleri,
    bool TamEkranMi);

/// <summary>
/// "Kullanici nereye tikladi ve bu deger kaydedilebilir mi?" karari.
///
/// Karar ekrandan ayri tutuldu: kural burada, P/Invoke ve pencere yonetimi
/// <c>Arayuz</c> tarafinda. Arayuz test edilemez ama <b>kural edilebilir</b> ve
/// yanlis kabul edilen tek bir koordinat, robotun ORKA'da yanlis yere
/// tiklamasi demek.
/// </summary>
public static class KoordinatSecimi
{
    public static KoordinatSecimSonucu Degerlendir(TiklamaOrtami ortam)
    {
        if (ortam.OrkaPenceresi is not { } pencere)
            return new KoordinatSecimSonucu(false, 0, 0,
                "ORKA penceresi bulunamadi. ORKA'yi acip yeniden deneyin.");

        if (!pencere.Gecerli)
            return new KoordinatSecimSonucu(false, 0, 0,
                "ORKA penceresinin olculeri okunamadi; pencere simge durumunda olabilir. " +
                "ORKA'yi tam ekran yapip yeniden deneyin.");

        // Hedef pencere ORKA olmali. Baska bir pencerede olculen oran ORKA'ya
        // uygulandiginda bambaska bir noktaya dusuyor -- ve bunu kimse fark
        // etmiyor, cunku sayi makul gorunuyor.
        if (ortam.OrkaSurecleri.Count > 0 &&
            (ortam.TiklananPencereSureci is null ||
             !ortam.OrkaSurecleri.Contains(ortam.TiklananPencereSureci.Value)))
        {
            return new KoordinatSecimSonucu(false, 0, 0,
                "Tiklanan pencere ORKA degil. Koordinatlar ORKA penceresine goreli " +
                "olculuyor; baska bir pencereye tiklanan nokta kaydedilmez.");
        }

        var (oranX, oranY) = OranDonusturucu.Oran(ortam.MutlakX, ortam.MutlakY, pencere);

        // Surec ORKA'ya ait ama nokta ana pencerenin disinda: ORKA'nin ikinci bir
        // penceresine (diyalog) tiklanmis olabilir. Oran 0..1 disina cikar ve
        // Tikla adimi bu degeri zaten reddeder; burada durmak daha erken.
        if (!OranDonusturucu.OranIcerideMi(oranX, oranY))
            return new KoordinatSecimSonucu(false, oranX, oranY,
                "Tiklanan nokta ORKA ana penceresinin disinda kaldi " +
                $"(oran {OranDonusturucu.Yaz(oranX)} x {OranDonusturucu.Yaz(oranY)}). " +
                "ORKA'nin ana penceresi icine tiklayin.");

        if (!ortam.TamEkranMi)
            return new KoordinatSecimSonucu(true, oranX, oranY,
                "ORKA tam ekran degildi. Oran kaydedildi ama robot tiklamadan once " +
                "pencereyi buyutuyor; ORKA'yi tam ekran yapip yeniden olcun.",
                Uyari: true);

        return new KoordinatSecimSonucu(true, oranX, oranY);
    }
}
