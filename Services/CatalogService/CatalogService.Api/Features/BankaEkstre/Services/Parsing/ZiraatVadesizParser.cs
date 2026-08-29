using CatalogService.Api.Features.BankaEkstre.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Services.Parsing
{
    /// <summary>
    /// Ziraat Bankası vadesiz TL hesap ekstresi (xlsx).
    ///
    /// Ölçülmüş yapı (7 aylık gerçek dosya, 356 veri satırı): <b>12. satır kolon başlıkları</b>,
    /// veri 13'ten. Başlıklar sırayla: Tarih, Fiş No, Açıklama, İşlem Tutarı, Bakiye.
    ///
    /// <b>Dosyanın stil tablosu bozuk.</b> openpyxl
    /// <c>expected &lt;class 'openpyxl.styles.fills.Fill'&gt;</c> hatasıyla açamıyor;
    /// biçim tablosunu okuyan her kütüphane aynı yerde patlıyor. Değerler ise sağlam:
    /// <see cref="EkstreTabloOkuyucu"/> ClosedXML başarısız olunca sırayla NPOI'yi, o da
    /// olmazsa <see cref="HamXlsxOkuyucu"/>'yu (zip içindeki XML'i doğrudan okuyan yol)
    /// dener ve hangi okuyucunun neden başarısız olduğunu uyarıya yazar.
    ///
    /// Ham XML yolunda hücrenin tarih biçimli olup olmadığı bilinemez; tarih kolonundaki
    /// sayısal değerler Excel seri numarası olarak yorumlanır (bkz. <see cref="TabloDeger"/>).
    ///
    /// Üç bankanın en az bilgi vereni: <b>işlem tipi kolonu ve borç/alacak kolonu yok</b>.
    /// Yön tutarın işaretinden, satırın niteliği yalnız açıklamadan okunur — Akbank'ta
    /// olduğu gibi uydurma bir işlem tipi türetilmez.
    /// </summary>
    public class ZiraatVadesizParser : TabloParserTemeli
    {
        public const string Tip = "ZIRAAT_VADESIZ";

        public override string ParserTipi => Tip;
        public override string Ad => "Ziraat Bankası — Vadesiz TL";

        protected override int VarsayilanIlkVeriSatiri => 13;

        protected override IReadOnlyList<KolonTanimi> Kolonlar { get; } = new[]
        {
            new KolonTanimi(KolonTarih, 1, true, "Tarih", "İşlem Tarihi"),
            new KolonTanimi(KolonReferans, 2, false, "Fiş No", "Dekont No", "Fiş/Dekont No"),
            new KolonTanimi(KolonAciklama, 3, true, "Açıklama", "İşlem Açıklaması"),
            new KolonTanimi(KolonTutar, 4, true, "İşlem Tutarı", "Tutar")
        };

        protected override void Doldur(
            TabloSatiri satir, KolonHaritasi kolonlar, decimal imzaliTutar,
            AyrilanSatir ayrilan, AyristirmaBaglami baglam)
        {
            ayrilan.Yon = YonBul(imzaliTutar, null, baglam);
            ayrilan.IslemTipi = string.Empty;
            ayrilan.KarsiVkn = null;
        }
    }
}
