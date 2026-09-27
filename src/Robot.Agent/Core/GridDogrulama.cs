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
/// <summary>Okunan satirin beklenen satirla nasil eslestirildigi.</summary>
public enum EslestirmeYolu
{
    /// <summary>Aciklama metnine gore. Varsayilan; kaydirmadan etkilenmiyor.</summary>
    Aciklama,

    /// <summary>
    /// Ekrandaki siraya gore (okunan[i] ~ beklenen[i]). Yalniz aciklama
    /// okunamadiginda: grid kaydirilmissa yaniltir.
    /// </summary>
    Sira
}

/// <param name="Beklenen">Yazilmasi gereken satir sayisi.</param>
/// <param name="Okunan">Goruntude gorulen satir sayisi.</param>
/// <param name="Eslesen">Kodu beklenenle ayni cikan satir sayisi.</param>
/// <param name="Farklar">Kodu tutmayan satirlar.</param>
/// <param name="Kesildi">Okuyucu "tablo ekrana sigmadi" dedi mi?</param>
/// <param name="Yol">Satirlar hangi olcute gore eslestirildi.</param>
/// <param name="EslesmeyenOkunan">
/// Goruntude gorulup beklenen listede karsiligi bulunamayan satir sayisi.
/// <b>Ayri sayiliyor:</b> "yanlis kod yazildi" degil, "bu satirin ne oldugunu
/// bilemedik" demek -- grid'de onceki aktarimdan kalan satir da olabilir,
/// aciklamasi okunamamis bir satir da.
/// </param>
public record GridDogrulamaSonucu(
    int Beklenen,
    int Okunan,
    int Eslesen,
    IReadOnlyList<SatirFarki> Farklar,
    bool Kesildi,
    EslestirmeYolu Yol = EslestirmeYolu.Aciklama,
    int EslesmeyenOkunan = 0)
{
    /// <summary>
    /// Karsilastirilan her satir tuttu ve eksik/fazla satir yok.
    ///
    /// <b><see cref="EslesmeyenOkunan"/> da sifir olmali:</b> aciklamaya gore
    /// eslestirmede "okunan 5 = beklenen 5" olup satirlarin BIRBIRINI TUTMAMASI
    /// mumkun (bir okunan eslesmez, bir beklenen aciktir). Yalniz sayilara
    /// bakmak o durumu "tamam" gosterirdi.
    /// </summary>
    public bool TamamTutuyor => Farklar.Count == 0 && Okunan == Beklenen &&
                                EslesmeyenOkunan == 0 && !Kesildi;

    /// <summary>
    /// Goruntude karsiligi bulunamadigi icin hic karsilastirilamayan BEKLENEN
    /// satir sayisi (ekrana sigmayanlar buraya duser).
    /// </summary>
    public int Karsilastirilmayan => Math.Max(0, Beklenen - (Eslesen + Farklar.Count));
}

/// <summary>
/// Goruntuden okunan grid ile yazilmasi beklenen kod listesini karsilastirir.
///
/// <b>Neden burada karar yok:</b> modele "bu grid dogru mu" diye sorulmuyor --
/// muhasebe karari onun isi degil. Robot ne yazmasi gerektigini zaten biliyor;
/// modelden istenen tek sey ekranda YAZAN'i okumak. Karar bu saf fonksiyonda,
/// string karsilastirmasiyla veriliyor ve sonucu yalnizca log'a dusuyor.
///
/// <b>Neden ACIKLAMAYA gore eslesiyor, siraya gore degil:</b> once okunan[i] ile
/// beklenen[i] karsilastiriliyordu -- "satirlarin ekrandaki sirasi kesin" diye.
/// Degilmis. 08.09.2026 kosusu: GridDoldur bittiginde grid ASAGI KAYDIRILMIS
/// durumda kaliyor (robot son satira kadar ASAGI ok gonderiyor) ve goruntude
/// gorunen ilk satir listenin ilk satiri DEGIL. Log:
///
///   "Grid okundu: 27 satir (tablo ekrana sigmamis)"
///   "0/48 satir eslesti, 27 satir tutmuyor"
///
/// ...oysa okunan kodlarin hepsi DOGRUYDU, yalnizca 5 satir kaymislardi
/// (beklenen 1. satir '102 1 1 04' okunan listede 6. sirada). Sira bazli
/// karsilastirma bu yuzden 27 sahte uyari uretti: "yanlis kod yazildi" diyen bir
/// rapor, aslinda dogru yazilmis bir grid icin. Yanlis alarm, uyarinin kendisini
/// degersizlestirir -- bir sonraki gercek kayma da ayni gorunurdu.
///
/// Aciklama iki tarafta da var: sunucudan gelen kod listesinde
/// (<see cref="GridSatiri.Aciklama"/>) ve modelden istenen okumada
/// (<see cref="OkunanGridSatiri.Aciklama"/>). Kaydirma aciklamayi degistirmiyor.
///
/// <b>Sira yine de yedek:</b> aciklama iki tarafta da okunamiyorsa eski davranisa
/// dusuluyor ve rapor bunu SOYLUYOR (<see cref="EslestirmeYolu"/>) -- sessizce
/// yaniltici bir olcute dusmek, dogrulamanin en pahali kirilma sekli olurdu.
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
    /// Aciklamalari karsilastirmak icin sadelestirir.
    ///
    /// <b>Kodlardan daha serbest, bilerek:</b> aciklama bir ESLESTIRME ANAHTARI,
    /// dogrulanan deger degil. Yanlis eslesmenin bedeli bir satirin
    /// karsilastirilmamasi; fazla katiligin bedeli butun satirlarin
    /// eslesmemesi. Bu yuzden bosluk, noktalama ve Turkce harf farklari
    /// atiliyor: ekran goruntusunden okunan "ODEME - ISTANBUL" ile listedeki
    /// "Ödeme  İstanbul" ayni satir.
    ///
    /// Kod karsilastirmasi bu toleransi ALMIYOR (bkz. <see cref="Normalize"/>):
    /// orada rakam farki gercek bir hata.
    /// </summary>
    public static string AciklamaAnahtari(string? aciklama)
    {
        if (string.IsNullOrWhiteSpace(aciklama)) return string.Empty;

        var sonuc = new System.Text.StringBuilder(aciklama.Length);

        foreach (var harf in aciklama.ToUpperInvariant())
        {
            var d = harf switch
            {
                'İ' or 'I' or 'Ì' or 'Í' => 'I',
                'Ş' => 'S',
                'Ğ' => 'G',
                'Ü' => 'U',
                'Ö' => 'O',
                'Ç' => 'C',
                _ => harf
            };

            if (char.IsLetterOrDigit(d)) sonuc.Append(d);
        }

        return sonuc.ToString();
    }

    /// <summary>
    /// Aciklamalar eslestirmede kullanilabilir mi? Iki tarafta da en az bir
    /// dolu aciklama olmali; yoksa <see cref="EslestirmeYolu.Sira"/> yedegine
    /// dusuluyor.
    /// </summary>
    public static bool AciklamayaGoreEslesebilir(
        IReadOnlyList<GridSatiri> beklenen, IReadOnlyList<OkunanGridSatiri> okunan)
        => beklenen.Any(b => AciklamaAnahtari(b.Aciklama).Length > 0) &&
           okunan.Any(o => AciklamaAnahtari(o.Aciklama).Length > 0);

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
        => AciklamayaGoreEslesebilir(beklenen, okunan)
            ? AciklamayaGore(beklenen, okunan, kesildi)
            : SiraylaKarsilastir(beklenen, okunan, kesildi);

    /// <summary>
    /// Her okunan satiri, aciklamasi tutan ilk KULLANILMAMIS beklenen satirla
    /// eslestirir.
    ///
    /// <b>Neden "ilk kullanilmamis" ve okunan sirayla geziliyor:</b> ekstrelerde
    /// ayni aciklama tekrar edebiliyor ("HAVALE", "EFT ODEME"). Tekrar eden
    /// aciklamalar ekrandaki siraya gore paylastiriliyor -- yani aciklamalar
    /// birbirinin AYNISI oldugunda davranis eski sira bazli eslestirmenin
    /// aynisina donuyor; kaydirma varsa yine kayar ama bu, aciklamalarin hicbir
    /// ayirt edici bilgi tasimadigi durum ve daha iyisi yok.
    /// </summary>
    private static GridDogrulamaSonucu AciklamayaGore(
        IReadOnlyList<GridSatiri> beklenen,
        IReadOnlyList<OkunanGridSatiri> okunan,
        bool kesildi)
    {
        var anahtarlar = beklenen.Select(b => AciklamaAnahtari(b.Aciklama)).ToList();
        var kullanildi = new bool[beklenen.Count];

        var farklar = new List<SatirFarki>();
        var eslesen = 0;
        var eslesmeyenOkunan = 0;

        foreach (var o in okunan)
        {
            var indeks = Eslesigi(AciklamaAnahtari(o.Aciklama), anahtarlar, kullanildi);

            if (indeks < 0)
            {
                eslesmeyenOkunan++;
                continue;
            }

            kullanildi[indeks] = true;

            if (KodTutuyor(beklenen[indeks].KarsiHesapKodu, o.KarsiHesapKodu))
            {
                eslesen++;
                continue;
            }

            farklar.Add(Fark(beklenen[indeks], o.KarsiHesapKodu));
        }

        return new GridDogrulamaSonucu(beklenen.Count, okunan.Count, eslesen, farklar, kesildi,
                                       EslestirmeYolu.Aciklama, eslesmeyenOkunan);
    }

    /// <summary>
    /// Aciklama anahtarina uyan ilk kullanilmamis beklenen satirin indeksi;
    /// yoksa -1.
    ///
    /// Once TAM esitlik araniyor. Bulunamazsa ON EK esitligi deneniyor: grid'in
    /// aciklama kolonu dar ve uzun aciklamalar ekranda kesilebiliyor. On ek
    /// eslesmesi yalniz TEK aday varsa kabul ediliyor -- iki aday varsa hangisi
    /// oldugu bilinmiyor demektir ve yanlis satiri "dogrulanmis" saymaktansa
    /// dogrulamamak yeglenir. En az uzunluk sarti kisa metinlerin
    /// ("EFT" gibi) her seye uymasini engelliyor.
    /// </summary>
    private static int Eslesigi(string anahtar, List<string> anahtarlar, bool[] kullanildi)
    {
        if (anahtar.Length == 0) return -1;

        for (var i = 0; i < anahtarlar.Count; i++)
        {
            if (kullanildi[i]) continue;
            if (string.Equals(anahtarlar[i], anahtar, StringComparison.Ordinal)) return i;
        }

        const int EnAzOnEk = 8;
        var adaylar = new List<int>();

        for (var i = 0; i < anahtarlar.Count; i++)
        {
            if (kullanildi[i]) continue;

            var diger = anahtarlar[i];
            if (diger.Length == 0) continue;

            var kisa = Math.Min(diger.Length, anahtar.Length);
            if (kisa < EnAzOnEk) continue;

            if (diger.AsSpan(0, kisa).SequenceEqual(anahtar.AsSpan(0, kisa)))
                adaylar.Add(i);
        }

        return adaylar.Count == 1 ? adaylar[0] : -1;
    }

    /// <summary>
    /// Eski davranis: okunan[i] ~ beklenen[i]. Yalniz aciklama iki tarafta da
    /// okunamadiginda kullaniliyor.
    /// </summary>
    private static GridDogrulamaSonucu SiraylaKarsilastir(
        IReadOnlyList<GridSatiri> beklenen,
        IReadOnlyList<OkunanGridSatiri> okunan,
        bool kesildi)
    {
        var farklar = new List<SatirFarki>();
        var eslesen = 0;

        var karsilastirilacak = Math.Min(beklenen.Count, okunan.Count);

        for (var i = 0; i < karsilastirilacak; i++)
        {
            if (KodTutuyor(beklenen[i].KarsiHesapKodu, okunan[i].KarsiHesapKodu))
            {
                eslesen++;
                continue;
            }

            farklar.Add(Fark(beklenen[i], okunan[i].KarsiHesapKodu));
        }

        var fazla = Math.Max(0, okunan.Count - beklenen.Count);

        return new GridDogrulamaSonucu(beklenen.Count, okunan.Count, eslesen, farklar, kesildi,
                                       EslestirmeYolu.Sira, fazla);
    }

    private static bool KodTutuyor(string? beklenen, string? okunan)
        => string.Equals(Normalize(beklenen), Normalize(okunan), StringComparison.OrdinalIgnoreCase);

    private static SatirFarki Fark(GridSatiri beklenen, string? okunanKod)
        => new(beklenen.SiraNo,
               beklenen.KarsiHesapKodu,
               // Bos okuma "okunamadi" demek; log'da tirnak icinde bos gorunmesin.
               string.IsNullOrWhiteSpace(okunanKod) ? "(okunamadi)" : okunanKod);

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

        // Tutmayan satir YOK, yalnizca ekrana sigmayanlar var: bu bir UYARI degil.
        // Kaydirilmis bir grid'de normal durum bu ve "DOGRULAMA UYARISI ... 0 satir
        // tutmuyor" satiri, gercek uyarilarla ayni gorunup onlari degersizlestirirdi.
        if (sonuc.Farklar.Count == 0 && sonuc.EslesmeyenOkunan == 0)
        {
            satirlar.Add($"Dogrulama: {sonuc.Eslesen}/{sonuc.Beklenen} satir eslesti, " +
                         "tutmayan satir yok.");
        }
        else
        {
            satirlar.Add($"DOGRULAMA UYARISI: {sonuc.Eslesen}/{sonuc.Beklenen} satir eslesti, " +
                         $"{sonuc.Farklar.Count} satir tutmuyor. KAYDET'e basmadan gozle kontrol edin.");
        }

        foreach (var fark in sonuc.Farklar)
            satirlar.Add($"  satir {fark.SiraNo}: beklenen '{fark.Beklenen}', okunan '{fark.Okunan}'");

        if (sonuc.EslesmeyenOkunan > 0)
        {
            satirlar.Add($"  {sonuc.EslesmeyenOkunan} okunan satir listede eslesmedi" +
                         (sonuc.Okunan > sonuc.Beklenen
                             ? "; goruntude beklenenden FAZLA satir var, grid'de onceki bir " +
                               "aktarimdan kalan satir olabilir."
                             : "; aciklamasi okunamamis olabilir."));
        }

        if (sonuc.Kesildi || sonuc.Karsilastirilmayan > 0)
        {
            satirlar.Add($"  {sonuc.Karsilastirilmayan} satir goruntude yoktu " +
                         "(tablo ekrana sigmamis olabilir); o satirlar DOGRULANMADI, " +
                         "asagi kaydirip elle bakin.");
        }

        if (sonuc.Yol == EslestirmeYolu.Sira)
        {
            // Sessizce siraya dusmek, dogrulamanin en pahali kirilma sekli olurdu:
            // grid kaydirilmissa bu olcut her satiri "tutmuyor" gosterir.
            satirlar.Add("  NOT: aciklama okunamadigi icin satirlar EKRANDAKI SIRAYA gore " +
                         "eslestirildi. Grid kaydirilmis durumdaysa bu liste yaniltir.");
        }

        return satirlar;
    }
}
