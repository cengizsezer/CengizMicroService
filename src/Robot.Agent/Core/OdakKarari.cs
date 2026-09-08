using PkfRobot.Arayuz;

namespace PkfRobot.Core;

/// <summary>Odak duzeltmenin verebilecegi kararlar.</summary>
public enum OdakEylemi
{
    /// <summary>
    /// Hedef pencere zaten on planda; yapilacak bir sey yok.
    ///
    /// <b>IsWindowEnabled'a bakilmadan verilir.</b> Delphi modallarinda "devre disi"
    /// raporu yaniltici olabiliyor (bkz. <see cref="OdakKarari.Karar"/>); tuslar her
    /// halukarda gonderilir.
    /// </summary>
    HedefZatenOnde,

    /// <summary>
    /// Odak ORKA'da ve hedef pencere DEVRE DISI: uzerinde modal bir diyalog acik
    /// (F7 firma listesi, firma sifresi popup'i, hesap plani). Dokunulmuyor.
    /// </summary>
    OrkaModaliAcik,

    /// <summary>
    /// Odak ORKA'nin bir penceresinde ama hedef o degil. Yine dokunulmuyor:
    /// ORKA'nin modelsiz (modal olmayan) kendi diyaloglari da buraya duser ve
    /// odagi calmak yaziyi yanlis pencereye gonderirdi.
    /// </summary>
    OrkaninBaskaPenceresi,

    /// <summary>Odak ORKA DISINDA bir uygulamada: duzeltilmeli.</summary>
    OneGetir,

    /// <summary>Hedef pencere secilemedi; yalnizca uyari yazilir.</summary>
    HedefYok
}

/// <summary>
/// "Klavyeye dokunmadan once odak duzeltilmeli mi, hangi pencereye?" karari.
///
/// Ekrana dokunmuyor: girdi pencereler ve on plandaki tutamac, cikti karar.
/// Kural burada durdugu icin "F7 diyalogu acikken odak calinir mi", "dosya secim
/// diyalogu acikken ana pencere one cekilir mi" gibi sorular ORKA olmadan test
/// edilebiliyor -- projedeki <see cref="PencereBekleyici.Esles"/> kalibinin aynisi.
/// </summary>
public static class OdakKarari
{
    /// <summary>
    /// Odagin ait olmasi gereken pencere.
    ///
    /// <b>Oncelik son ONE GETIRILEN penceredir.</b> Bir adim bilerek "Transfer
    /// Edilecek Excel" diyalogunu one getirdiyse ondan sonraki Yaz adimi oraya
    /// yazmali; "her zaman ana ekran" deseydik dosya adi ORKA ana penceresine
    /// giderdi. Hedef pencere kapanmissa (diyalog kapandi, sekme degisti) config'deki
    /// ana ekran/giris/sube sirasina donulur.
    ///
    /// <b>Bu deger tek basina yeterli DEGIL.</b> ORKA'nin kendi actigi modallar
    /// (F7 firma listesi) hicbir adim tarafindan beklenmedigi icin burada hic
    /// gorunmez; onlari <see cref="Karar"/> koruyor.
    /// </summary>
    /// <param name="sonOneGetirilen">BeklePencere/Tikla/OrkaBaslat'in en son one getirdigi pencere.</param>
    /// <param name="sonPencereYasiyor">O tutamac hala gecerli bir pencere mi.</param>
    /// <param name="orkaOnPenceresi">Yedek: config'deki bilinen ORKA pencereleri.</param>
    public static UstSeviyePencere? Hedef(
        UstSeviyePencere? sonOneGetirilen,
        bool sonPencereYasiyor,
        UstSeviyePencere? orkaOnPenceresi)
        => sonOneGetirilen is not null && sonPencereYasiyor
            ? sonOneGetirilen
            : orkaOnPenceresi;

    /// <summary>
    /// Odakla ne yapilacagi.
    ///
    /// <b>Neden "odak ORKA'da ise dokunma":</b> gorev dosyalari ORKA'nin actigi her
    /// modali beklemiyor. F7'den sonra <c>BeklePencere</c> yok, dogrudan
    /// <c>TemizleYaz</c> geliyor; o an <see cref="Hedef"/> hala giris ekranini (ya
    /// da ana pencereyi) gosteriyor ve pencere kimligine bakan bir kural odagi
    /// diyalogdan CALIYOR -- firma kodu hicbir yere yazilmiyordu.
    ///
    /// <b>Neden yine de bir kontrol var:</b> odak ORKA DISINDA ise duzeltmek sart.
    /// Ofis testinde robot ORKA yerine cmd penceresine yazdi ve SIFRE cmd'ye gitti;
    /// bu metodun varlik sebebi o.
    ///
    /// Modal olup olmadigi hedefin ETKIN olmasindan okunuyor: modal acikken sahibi
    /// devre disi kalir. Iki dal da "dokunma" diyor, ayrim log icin -- "modal var"
    /// ile "ORKA'nin baska penceresi onde" ayni sey degil ve ofiste bakan kisi
    /// hangisi oldugunu gormeli.
    /// </summary>
    /// <param name="hedef">Odagin ait olmasi gereken pencere.</param>
    /// <param name="onPlandaki">GetForegroundWindow ciktisi.</param>
    /// <param name="onPlanOrkada">On plandaki pencere ORKA surecine mi ait.</param>
    /// <param name="hedefEtkin">
    /// IsWindowEnabled(hedef). <b>YALNIZCA "odagi calalim mi" sorusunda kullanilir</b>
    /// -- hedef baska bir ORKA penceresinin ardindayken "ustunde modal var mi" ayrimini
    /// yapar. Tus gonderimini ASLA belirlemez: bkz. <see cref="OdakEylemi.HedefZatenOnde"/>.
    /// </param>
    public static OdakEylemi Karar(
        UstSeviyePencere? hedef,
        IntPtr onPlandaki,
        bool onPlanOrkada,
        bool hedefEtkin)
    {
        if (hedef is null || hedef.Tutamac == IntPtr.Zero) return OdakEylemi.HedefYok;

        // ON PLAN TESTI hedefEtkin'DEN ONCE gelir ve bu SIRA onemli.
        //
        // 06.09.2026 22:42 kosusu: on plandaki pencere 'Firma Sifresini Giriniz.'
        // popup'iydi, hedef de AYNI pencereydi, ama IsWindowEnabled false donuyordu.
        // Kullanici o sirada kutuya elle yazabiliyordu, yani Windows'un "devre disi"
        // raporu bu Delphi modalinde yaniltici. Etkinlige once bakan bir sira burayi
        // OrkaModaliAcik'a dusururdu: hedefin KENDISI "ustunde modal var" sayilir,
        // gonderim dogru pencerede oldugu halde supheli gorunurdu.
        //
        // Hedef on plandaysa tuslar oraya gider; etkinlik raporunun soyleyecegi
        // bir sey yok.
        if (hedef.Tutamac == onPlandaki) return OdakEylemi.HedefZatenOnde;

        if (onPlanOrkada)
            return hedefEtkin ? OdakEylemi.OrkaninBaskaPenceresi : OdakEylemi.OrkaModaliAcik;

        return OdakEylemi.OneGetir;
    }
}
