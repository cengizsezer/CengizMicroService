namespace PkfRobot.Core;

/// <summary>Goruntuden OKUNAN tek satir. Neyin yazildigi degil, ekranda ne gorundugu.</summary>
/// <param name="Sira">Okuyucunun verdigi sira (1'den); yalnizca izleme icin.</param>
/// <param name="Aciklama">Aciklama kolonundaki metin; okunamadiysa bos.</param>
/// <param name="KarsiHesapKodu">Karsi Hesap Kodu kolonundaki metin; okunamadiysa bos.</param>
public record OkunanGridSatiri(int Sira, string Aciklama, string KarsiHesapKodu);

/// <summary>Beklenen ile okunanin ayrildigi tek satir.</summary>
/// <param name="SiraNo">Ekstredeki sira -- BEKLENEN satirin sira numarasi.</param>
public record SatirFarki(int SiraNo, string Beklenen, string Okunan);

/// <summary>
/// Grid dogrulamasinin sonucu.
///
/// <b>Karar vermiyor, rapor ediyor:</b> hicbir alani "aktarim yanlis" demiyor.
/// Kaydet'e kullanici basiyor ve bu katman ona bakacagi yeri soyluyor.
/// </summary>
/// <param name="Beklenen">Yazilmasi gereken satir sayisi.</param>
/// <param name="Okunan">Goruntude gorulen satir sayisi.</param>
/// <param name="Eslesen">Kodu beklenenle ayni cikan satir sayisi.</param>
/// <param name="Farklar">Kodu tutmayan satirlar.</param>
/// <param name="Kesildi">Okuyucu "tablo ekrana sigmadi" dedi mi?</param>
public record GridDogrulamaSonucu(
    int Beklenen,
    int Okunan,
    int Eslesen,
    IReadOnlyList<SatirFarki> Farklar,
    bool Kesildi)
{
    /// <summary>Karsilastirilan her satir tuttu ve eksik/fazla satir yok.</summary>
    public bool TamamTutuyor => Farklar.Count == 0 && Okunan == Beklenen && !Kesildi;

    /// <summary>Ekrana sigmadigi ya da okunamadigi icin hic karsilastirilamayan satir sayisi.</summary>
    public int Karsilastirilmayan => Math.Max(0, Beklenen - Okunan);
}

/// <summary>
/// Goruntuden okunan grid ile yazilmasi beklenen kod listesini karsilastirir.
///
/// <b>Neden burada karar yok:</b> modele "bu grid dogru mu" diye sorulmuyor --
/// muhasebe karari onun isi degil. Robot ne yazmasi gerektigini zaten biliyor;
/// modelden istenen tek sey ekranda YAZAN'i okumak. Karar bu saf fonksiyonda,
/// string karsilastirmasiyla veriliyor ve sonucu yalnizca log'a dusuyor.
///
/// <b>Neden siraya gore eslesiyor, SiraNo'ya gore degil:</b> ORKA gridinde
/// gorunen sira numarasi kolonuna guvenilemez (okuyucu onu yanlis okuyabilir ya
/// da kolon hic olmayabilir), ama satirlarin ekrandaki SIRASI kesin -- robot da
/// zaten ustten asagi yaziyor. Bu yuzden okunan[i] ile beklenen[i]
/// karsilastiriliyor; SiraNo rapora BEKLENEN satirdan aliniyor.
///
/// Ekrana ya da aga dokunmuyor: girdi iki liste, cikti bir rapor.
/// </summary>
public static class GridDogrulama
{
    /// <summary>
    /// Kodlari karsilastirmadan once sadelestirir.
    ///
    /// <b>Yalniz bosluk:</b> ORKA hesap kodlari "320 01 001" gibi bosluklu
    /// yaziliyor ve goruntuden okurken iki boslugun tek gorunmesi (ya da tersi)
    /// gercek bir uyusmazlik degil. Rakam, tire, harf DOKUNULMUYOR -- kodun
    /// kendisinde tolerans, bu katmanin varlik sebebini yok ederdi.
    /// </summary>
    public static string Normalize(string? kod)
    {
        if (string.IsNullOrWhiteSpace(kod)) return string.Empty;

        var parcalar = kod.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parcalar);
    }

    /// <summary>
    /// Beklenen kod listesiyle okunan satirlari karsilastirir.
    ///
    /// Eksik okunan satirlar fark sayilmiyor, <see cref="GridDogrulamaSonucu.Karsilastirilmayan"/>
    /// olarak ayri raporlaniyor: "ekrana sigmadi" ile "yanlis kod yazildi" ayni
    /// sey degil ve ikisini tek sayida toplamak uyariyi okunmaz yapardi.
    /// </summary>
    public static GridDogrulamaSonucu Karsilastir(
        IReadOnlyList<GridSatiri> beklenen,
        IReadOnlyList<OkunanGridSatiri> okunan,
        bool kesildi = false)
    {
        var farklar = new List<SatirFarki>();
        var eslesen = 0;

        var karsilastirilacak = Math.Min(beklenen.Count, okunan.Count);

        for (var i = 0; i < karsilastirilacak; i++)
        {
            var b = Normalize(beklenen[i].KarsiHesapKodu);
            var o = Normalize(okunan[i].KarsiHesapKodu);

            if (string.Equals(b, o, StringComparison.OrdinalIgnoreCase))
            {
                eslesen++;
                continue;
            }

            farklar.Add(new SatirFarki(
                beklenen[i].SiraNo,
                beklenen[i].KarsiHesapKodu,
                // Bos okuma "okunamadi" demek; log'da tirnak icinde bos gorunmesin.
                string.IsNullOrWhiteSpace(okunan[i].KarsiHesapKodu)
                    ? "(okunamadi)"
                    : okunan[i].KarsiHesapKodu));
        }

        return new GridDogrulamaSonucu(beklenen.Count, okunan.Count, eslesen, farklar, kesildi);
    }

    /// <summary>
    /// Sonucun log'a dusen hali. Tek satir ozet + varsa satir satir farklar.
    ///
    /// Mesajlarin hepsi "kontrol et" diyor, "durduruldu" demiyor: bu katman
    /// robotu durdurmuyor (bkz. <see cref="GridDogrulamaSonucu"/>).
    /// </summary>
    public static IReadOnlyList<string> Satirlar(GridDogrulamaSonucu sonuc)
    {
        var satirlar = new List<string>();

        if (sonuc.TamamTutuyor)
        {
            satirlar.Add($"Dogrulama: {sonuc.Eslesen}/{sonuc.Beklenen} satir eslesti.");
            return satirlar;
        }

        satirlar.Add($"DOGRULAMA UYARISI: {sonuc.Eslesen}/{sonuc.Beklenen} satir eslesti, " +
                     $"{sonuc.Farklar.Count} satir tutmuyor. KAYDET'e basmadan gozle kontrol edin.");

        foreach (var fark in sonuc.Farklar)
            satirlar.Add($"  satir {fark.SiraNo}: beklenen '{fark.Beklenen}', okunan '{fark.Okunan}'");

        if (sonuc.Kesildi || sonuc.Karsilastirilmayan > 0)
        {
            satirlar.Add($"  {sonuc.Karsilastirilmayan} satir goruntude yoktu " +
                         "(tablo ekrana sigmamis olabilir); o satirlar DOGRULANMADI, " +
                         "asagi kaydirip elle bakin.");
        }

        if (sonuc.Okunan > sonuc.Beklenen)
        {
            satirlar.Add($"  goruntude beklenenden {sonuc.Okunan - sonuc.Beklenen} FAZLA satir okundu; " +
                         "grid'de onceki bir aktarimdan kalan satir olabilir.");
        }

        return satirlar;
    }
}
