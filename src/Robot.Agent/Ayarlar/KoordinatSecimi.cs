namespace PkfRobot.Ayarlar;

/// <summary>
/// Bir koordinat secimi neden reddedildi?
///
/// Sebep <b>ayri bir alan</b> olarak tasiniyor: cumleyi degistirmek isteyen
/// biri log'u ve testi bozmasin, ve arayuz "hangi denetim tetiklendi"yi
/// kullaniciya cumleden ayirmadan gosterebilsin.
/// </summary>
public enum RedSebebi
{
    /// <summary>Ret yok; deger kaydedilebilir.</summary>
    Yok,

    /// <summary>ORKA calismiyor ya da ana penceresi bulunamadi.</summary>
    OrkaKapali,

    /// <summary>Tiklanan noktadaki pencerenin sureci okunamadi.</summary>
    PencereSureciOkunamadi,

    /// <summary>Tiklanan pencerenin sureci ORKA degil.</summary>
    BaskaUygulama,

    /// <summary>ORKA ana penceresinin olculeri alinamadi.</summary>
    PencereOlcusuOkunamadi,

    /// <summary>Nokta ORKA'nin bir penceresinde ama ana pencerenin disinda.</summary>
    AnaPencereDisinda
}

/// <summary>Bir koordinat secme denemesinin sonucu.</summary>
/// <param name="Kabul">Deger kaydedilebilir mi?</param>
/// <param name="OranX">Kabul edildiyse ORKA ANA penceresine goreli yatay oran.</param>
/// <param name="OranY">Kabul edildiyse ORKA ANA penceresine goreli dikey oran.</param>
/// <param name="Mesaj">Kullaniciya gosterilecek cumle; red sebebi ya da uyari.</param>
/// <param name="Uyari">Kabul edildi ama dikkat edilmesi gereken bir sey var.</param>
/// <param name="Sebep">Reddedildiyse hangi denetim tetiklendi.</param>
public record KoordinatSecimSonucu(
    bool Kabul,
    double OranX,
    double OranY,
    string? Mesaj = null,
    bool Uyari = false,
    RedSebebi Sebep = RedSebebi.Yok);

/// <summary>Tiklama aninda ekrandan okunan ham durum.</summary>
/// <param name="MutlakX">Tiklanan noktanin ekran koordinati.</param>
/// <param name="MutlakY">Tiklanan noktanin ekran koordinati.</param>
/// <param name="OrkaAnaPenceresi">
/// ORKA <b>ana</b> penceresinin olculeri; bulunamadiysa null. Oran her zaman
/// buna gore hesaplanir -- tiklanan alt pencereye gore degil.
/// </param>
/// <param name="TiklananPencereSureci">Tiklanan noktadaki pencerenin surec kimligi; okunamadiysa null.</param>
/// <param name="OrkaSurecleri">ORKA'ya ait surec kimlikleri.</param>
/// <param name="TamEkranMi">ORKA ana penceresi maximize durumda mi?</param>
/// <param name="TiklananPencereBasligi">Tiklanan ust seviye pencerenin basligi; ret mesajinda gosterilir.</param>
/// <param name="TiklananSurecAdi">Tiklanan pencerenin surec adi; ret mesajinda gosterilir.</param>
/// <param name="BeklenenSurecAdi">Beklenen surec adi; config'deki <c>Ajan.OrkaSurecAdi</c>.</param>
/// <param name="OrkaAnaPencereBasligi">ORKA ana penceresinin basligi; oranin kime goreli oldugunu yazmak icin.</param>
public record TiklamaOrtami(
    int MutlakX,
    int MutlakY,
    PencereOlcusu? OrkaAnaPenceresi,
    int? TiklananPencereSureci,
    IReadOnlyCollection<int> OrkaSurecleri,
    bool TamEkranMi,
    string? TiklananPencereBasligi = null,
    string? TiklananSurecAdi = null,
    string? BeklenenSurecAdi = null,
    string? OrkaAnaPencereBasligi = null)
{
    /// <summary>
    /// Tiklanan pencere ORKA'nin <b>herhangi</b> bir penceresi mi?
    ///
    /// Surec kimligiyle bakiliyor, tutamacla degil: ORKA'nin modal
    /// diyaloglari, "Firma Sifresini Giriniz." popup'i ve Veri Transferi
    /// ekrani ayri pencereler ama ayni surec. "Yalniz ana pencere" sarti
    /// olsaydi kalibrasyonun asil ihtiyac duyuldugu ekranlarda hicbir nokta
    /// secilemezdi.
    /// </summary>
    public bool TiklananOrkaMi
        => TiklananPencereSureci is { } pid && OrkaSurecleri.Contains(pid);

    /// <summary>"Veri Transferi" (OrkaWinIceberg.64, pid 4242) -- ret mesajinin tiklanan satiri.</summary>
    public string TiklananYazi
    {
        get
        {
            if (TiklananPencereSureci is not { } pid)
                return "okunamadi";

            var baslik = string.IsNullOrWhiteSpace(TiklananPencereBasligi)
                ? "(basliksiz)"
                : $"\"{TiklananPencereBasligi}\"";

            var surec = string.IsNullOrWhiteSpace(TiklananSurecAdi)
                ? "surec adi okunamadi"
                : TiklananSurecAdi;

            return $"{baslik} ({surec}, pid {pid})";
        }
    }

    /// <summary>Ret mesajinin "ne bekleniyordu" satiri.</summary>
    public string BeklenenYazi
    {
        get
        {
            var surec = string.IsNullOrWhiteSpace(BeklenenSurecAdi) ? "ORKA" : BeklenenSurecAdi;
            var pidler = OrkaSurecleri.Count == 0
                ? "acik surec yok"
                : $"pid {string.Join(", ", OrkaSurecleri)}";

            return $"ORKA surecine ({surec}, {pidler}) ait bir pencere -- " +
                   "ana pencere ya da onun modal/alt pencereleri.";
        }
    }
}

/// <summary>
/// "Kullanici nereye tikladi ve bu deger kaydedilebilir mi?" karari.
///
/// Karar ekrandan ayri tutuldu: kural burada, P/Invoke ve pencere yonetimi
/// <c>Arayuz</c> tarafinda. Arayuz test edilemez ama <b>kural edilebilir</b> ve
/// yanlis kabul edilen tek bir koordinat, robotun ORKA'da yanlis yere
/// tiklamasi demek.
///
/// <b>Iki ayri pencere var, karistirilmamali:</b> tiklanan pencere (ORKA'nin
/// herhangi bir penceresi olabilir) yalniz <i>yetki</i> denetimi icin; oran her
/// zaman ORKA <b>ana</b> penceresine gore hesaplanir, cunku
/// <c>AdimMotoru.Tikla</c> de tiklamayi ana pencereye (config'deki
/// <c>Pencereler.AnaEkran</c>) oranla uyguluyor. Alt pencereye gore olculen bir
/// oran calisma aninda bambaska bir noktaya duserdi.
/// </summary>
public static class KoordinatSecimi
{
    public static KoordinatSecimSonucu Degerlendir(TiklamaOrtami ortam)
    {
        // 1. ORKA acik mi? Surec listesi bossa kiyaslanacak bir sey yok.
        if (ortam.OrkaSurecleri.Count == 0)
            return Ret(ortam, RedSebebi.OrkaKapali,
                "ORKA calismiyor (calisan surec bulunamadi). ORKA'yi acip kalibre " +
                "edilecek ekrana gidin, sonra Sec'e basin.");

        if (ortam.OrkaAnaPenceresi is not { } ana)
            return Ret(ortam, RedSebebi.OrkaKapali,
                "ORKA calisiyor ama ANA penceresi bulunamadi. Oran ana pencereye goreli " +
                "olculuyor; ana pencere simge durumunda ya da gizli olabilir.");

        // 2. Tiklanan noktadaki pencerenin sureci okunabildi mi? "Bilmiyorum"
        //    durumunda kabul etmek, yanlis koordinatin sessizce kaydedilmesine
        //    acik kapi birakirdi.
        if (ortam.TiklananPencereSureci is null)
            return Ret(ortam, RedSebebi.PencereSureciOkunamadi,
                "Tiklanan noktadaki pencerenin sureci okunamadi. Nokta hicbir pencerenin " +
                "uzerinde olmayabilir (masaustu) ya da pencere daha yuksek yetkiyle " +
                "calisiyor olabilir; ORKA yonetici modundaysa PkfRobot'u da yonetici " +
                "olarak calistirin.");

        // 3. Surec ORKA'ya mi ait? Alt pencereler de ORKA sayilir
        //    (bkz. TiklamaOrtami.TiklananOrkaMi); baska bir uygulamada olculen
        //    oran ORKA'ya uygulandiginda bambaska bir noktaya duser -- ve sayi
        //    makul gorundugu icin kimse fark etmez.
        if (!ortam.TiklananOrkaMi)
            return Ret(ortam, RedSebebi.BaskaUygulama,
                "Tiklanan pencerenin sureci ORKA degil.");

        // 4. Oranin paydasi: ana pencerenin olcusu.
        if (!ana.Gecerli)
            return Ret(ortam, RedSebebi.PencereOlcusuOkunamadi,
                $"ORKA ana penceresinin olculeri alinamadi (G={ana.Genislik}, Y={ana.Yukseklik}). " +
                "Pencere simge durumuna kucultulmus olabilir; ORKA'yi ekranda gorunur hale " +
                "getirip yeniden deneyin.");

        var (oranX, oranY) = OranDonusturucu.Oran(ortam.MutlakX, ortam.MutlakY, ana);

        // 5. ORKA'nin bir penceresi ama nokta ana pencerenin disinda: oran 0..1
        //    disina cikar ve AdimMotoru.Tikla bu degeri zaten reddeder. Burada
        //    durmak hem daha erken hem de sebebin soylenebildigi yer.
        if (!OranDonusturucu.OranIcerideMi(oranX, oranY))
            return Ret(ortam, RedSebebi.AnaPencereDisinda,
                $"Nokta ORKA ana penceresinin disinda kaldi (oran " +
                $"{OranDonusturucu.Yaz(oranX)} x {OranDonusturucu.Yaz(oranY)}, 0 ile 1 " +
                "arasinda olmaliydi). Alt pencere ana pencerenin disina tasmis olabilir; " +
                "ana pencerenin uzerine tasiyip yeniden olcun.");

        // Tam ekran RET SEBEBI DEGIL: oran pencerenin o anki olcusunden
        // hesaplaniyor, maximize olmamasi matematigi bozmuyor. Risk su: pencere
        // sonradan yeniden boyutlandirilirsa ORKA'nin ic yerlesimi orantili
        // buyumedigi icin oran kayar. Bu bir uyari konusu.
        if (!ortam.TamEkranMi)
            return new KoordinatSecimSonucu(true, oranX, oranY,
                "ORKA tam ekran degildi. Oran, pencerenin su anki olcusune gore kaydedildi " +
                "ve bu haliyle dogru. Ancak pencere yeniden boyutlandirilirsa oran kayar; " +
                "ORKA'yi tam ekran yapip yeniden olcmek en guvenlisi.",
                Uyari: true);

        return new KoordinatSecimSonucu(true, oranX, oranY);
    }

    /// <summary>
    /// Ret cumlesi: <b>hangi denetim tetiklendi</b>, <b>hangi pencereye
    /// tiklandi</b>, <b>ne bekleniyordu</b>. Ucu de sart -- "kabul etmiyor"
    /// diyen kullanicinin elinde su an bunlarin hicbiri yok.
    /// </summary>
    private static KoordinatSecimSonucu Ret(TiklamaOrtami ortam, RedSebebi sebep, string aciklama)
        => new(false, 0, 0,
            "Koordinat kaydedilmedi." + Environment.NewLine + Environment.NewLine +
            $"Sebep ({SebepAdi(sebep)}): {aciklama}" + Environment.NewLine + Environment.NewLine +
            $"Tiklanan pencere: {ortam.TiklananYazi}" + Environment.NewLine +
            $"Beklenen: {ortam.BeklenenYazi}",
            Sebep: sebep);

    /// <summary>Log satirinda ve ret cumlesinde gorunen kisa sebep adi.</summary>
    public static string SebepAdi(RedSebebi sebep) => sebep switch
    {
        RedSebebi.Yok => "kabul",
        RedSebebi.OrkaKapali => "ORKA penceresi yok",
        RedSebebi.PencereSureciOkunamadi => "pencere sureci okunamadi",
        RedSebebi.BaskaUygulama => "pencere sureci ORKA degil",
        RedSebebi.PencereOlcusuOkunamadi => "pencere olcusu alinamadi",
        RedSebebi.AnaPencereDisinda => "nokta ana pencerenin disinda",
        _ => sebep.ToString()
    };

    /// <summary>
    /// Tek satirlik log kaydi: karar verilirken ekrandan ne okundugu.
    ///
    /// Ret mesaji kullanici icin; bu satir <b>bizim icin</b>. Ofiste "secici
    /// kabul etmiyor" denildiginde bakilacak ilk yer burasi olmali.
    /// </summary>
    public static string Gunluk(TiklamaOrtami ortam, KoordinatSecimSonucu sonuc)
    {
        var ana = ortam.OrkaAnaPenceresi is { } p
            ? $"sol={p.Sol} ust={p.Ust} genislik={p.Genislik} yukseklik={p.Yukseklik}"
            : "bulunamadi";

        var anaBaslik = string.IsNullOrWhiteSpace(ortam.OrkaAnaPencereBasligi)
            ? "?"
            : ortam.OrkaAnaPencereBasligi;

        var karar = sonuc.Kabul
            ? $"KABUL oran {OranDonusturucu.Yaz(sonuc.OranX)} x {OranDonusturucu.Yaz(sonuc.OranY)}" +
              (sonuc.Uyari ? " (uyarili)" : string.Empty)
            : $"RET [{SebepAdi(sonuc.Sebep)}]";

        return $"Koordinat secimi: {karar} | tiklanan nokta ({ortam.MutlakX}, {ortam.MutlakY}) " +
               $"| tiklanan pencere {ortam.TiklananYazi} " +
               $"| ORKA surecleri [{string.Join(", ", ortam.OrkaSurecleri)}] " +
               $"| ana pencere \"{anaBaslik}\" {ana} " +
               $"| tam ekran: {(ortam.TamEkranMi ? "evet" : "hayir")}";
    }
}
