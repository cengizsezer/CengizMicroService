using CatalogService.Api.Features.BankaEkstre.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Services.Parsing
{
    /// <summary>
    /// İş Bankası vadesiz TL hesap ekstresi.
    ///
    /// <b>Dosya eski .xls</b> (OLE2 kabı), xlsx değil: ClosedXML bu biçimi hiç açamıyor,
    /// okuma NPOI/HSSF ile yapılıyor (bkz. <see cref="EkstreTabloOkuyucu"/>). Dosyanın
    /// biçimi uzantıdan değil <b>imzasından</b> anlaşılır — kullanıcı aynı hesabın xlsx
    /// dışa aktarımını yüklerse o da okunur, tanınmayan bir biçim ise anlaşılır hata verir.
    ///
    /// Ölçülmüş yapı (7 aylık gerçek dosya, 418 veri satırı): 1–15. satırlar hesap künyesi,
    /// <b>16. satır kolon başlıkları</b>, veri 17'den. Başlıklar sırayla: Tarih/Saat, Valör,
    /// Kanal/Şube, İşlem Tutarı, Bakiye, (boş), İşlem, İşlem Tipi, Açıklama, …, Referans.
    ///
    /// Vakıfbank'tan üç farkı var ve üçü de tuzak:
    /// <list type="bullet">
    /// <item>Tarih ayracı <b>eğik çizgi</b> ve hücrede saat de var: <c>26/08/2026-14:58:47</c>.
    /// Saat tireyle ayrılmış — boşlukla değil (bkz. <see cref="TabloDeger"/>).</item>
    /// <item>Borç/alacak kolonu <b>yok</b>; yön tutarın işaretinden okunur.</item>
    /// <item>Kısa kodu tutan "İşlem" kolonu ile şablonun baktığı "İşlem Tipi" kolonu ayrı.
    /// Başlık araması tam ad üzerinden yapıldığı için karışmazlar.</item>
    /// </list>
    /// </summary>
    public class IsBankasiVadesizParser : TabloParserTemeli
    {
        public const string Tip = "ISBANKASI_VADESIZ";

        public override string ParserTipi => Tip;
        public override string Ad => "İş Bankası — Vadesiz TL";

        protected override int VarsayilanIlkVeriSatiri => 17;

        /// <summary>
        /// Ölçülen 1 tabanlı kolon numaraları. Zorunlu üçlü (tarih + tutar + açıklama)
        /// bulunmadan bir satır başlık sayılmaz; künye satırlarında da tek tük metin
        /// olduğu için tek kolonluk eşleşme yetseydi 3. satır başlık sanılırdı.
        /// </summary>
        protected override IReadOnlyList<KolonTanimi> Kolonlar { get; } = new[]
        {
            new KolonTanimi(KolonTarih, 1, true, "Tarih/Saat", "İşlem Tarihi", "Tarih"),
            new KolonTanimi(KolonKanal, 3, false, "Kanal/Şube", "Kanal", "Şube"),
            new KolonTanimi(KolonTutar, 4, true, "İşlem Tutarı", "Tutar"),
            // "İşlem" (E9, EF, CL … kısa kodu) burada KASITLI aranmıyor: şablon ve kural
            // eşleşmesi okunabilir olan "İşlem Tipi" kolonundan yapılıyor.
            new KolonTanimi(KolonIslemTipi, 8, false, "İşlem Tipi", "İşlem Türü", "İşlem Adı"),
            new KolonTanimi(KolonAciklama, 9, true, "Açıklama", "İşlem Açıklaması"),
            new KolonTanimi(KolonReferans, 15, false, "Referans", "Referans No", "Dekont No")
        };

        protected override void Doldur(
            TabloSatiri satir, KolonHaritasi kolonlar, decimal imzaliTutar,
            AyrilanSatir ayrilan, AyristirmaBaglami baglam)
        {
            // Borç/alacak kolonu yok: yön yalnız işaretten. Ölçümde çıkan hareketler
            // (ücret, EFT gönderimi) eksi, girenler artı yazılmış.
            ayrilan.Yon = YonBul(imzaliTutar, null, baglam);
            ayrilan.IslemTipi = satir.Hucre(kolonlar[KolonIslemTipi]).Metin;

            // Açıklamadaki VKN alanı (son yıldızdan sonraki numara) karşı tarafın değil,
            // işlemin kendi referansı; Vakıfbank'taki hatanın tekrarlanmaması için
            // karşı VKN doldurulmaz.
            ayrilan.KarsiVkn = null;
        }
    }
}
