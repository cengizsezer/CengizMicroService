using CatalogService.Api.Features.BankaEkstre.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Services.Parsing
{
    /// <summary>
    /// Akbank vadesiz TL hesap ekstresi (xlsx).
    ///
    /// Ölçülmüş yapı (7 aylık gerçek dosya, 186 veri satırı): <b>10. satır kolon başlıkları</b>,
    /// veri 11'den. Başlıklar sırayla: Tarih, Saat, Tutar, Bakiye, Borç/Alacak, Açıklama,
    /// Fiş/Dekont No.
    ///
    /// <b>İşlem tipi kolonu yok.</b> Bu yüzden <see cref="AyrilanSatir.IslemTipi"/> boş
    /// bırakılır ve satırın niteliği yalnız açıklamadan okunur:
    /// <see cref="AciklamaUretici.SablonBul"/> zaten önce ham açıklamayı tarıyor, sabit
    /// kurallar da <see cref="Domain.KuralKapsami.Aciklama"/> kapsamıyla tanımlandı.
    ///
    /// Açıklamadan uydurma bir işlem tipi <b>türetilmedi</b>: türetilseydi unvan
    /// çıkarılamayan satırlarda öğrenme anahtarı "ISLEM:&lt;uydurulmuş tip&gt;" olur ve
    /// ilk onaydan sonra aynı kanaldan geçen ilgisiz satırları da çözerdi.
    ///
    /// Yön için iki sinyal var — Borç/Alacak kolonu (<c>B</c>/<c>A</c>) ve tutarın işareti —
    /// ve ikisi çapraz doğrulanır: kolon kazanır, çelişen satır sayısı uyarıya yazılır.
    /// </summary>
    public class AkbankVadesizParser : TabloParserTemeli
    {
        public const string Tip = "AKBANK_VADESIZ";

        public override string ParserTipi => Tip;
        public override string Ad => "Akbank — Vadesiz TL";

        protected override int VarsayilanIlkVeriSatiri => 11;

        protected override IReadOnlyList<KolonTanimi> Kolonlar { get; } = new[]
        {
            new KolonTanimi(KolonTarih, 1, true, "Tarih", "İşlem Tarihi"),
            new KolonTanimi(KolonTutar, 3, true, "Tutar", "İşlem Tutarı"),
            new KolonTanimi(KolonBorcAlacak, 5, false, "Borç/Alacak", "B/A", "BA"),
            new KolonTanimi(KolonAciklama, 6, true, "Açıklama", "İşlem Açıklaması"),
            new KolonTanimi(KolonReferans, 7, false, "Fiş/Dekont No", "Dekont No", "Fiş No")
        };

        protected override void Doldur(
            TabloSatiri satir, KolonHaritasi kolonlar, decimal imzaliTutar,
            AyrilanSatir ayrilan, AyristirmaBaglami baglam)
        {
            ayrilan.Yon = YonBul(imzaliTutar, satir.Hucre(kolonlar[KolonBorcAlacak]).Metin, baglam);

            // İşlem tipi kolonu yok; boş kalır (yukarıdaki açıklamaya bakınız).
            ayrilan.IslemTipi = string.Empty;
            ayrilan.KarsiVkn = null;
        }

        protected override void Tamamla(AyristirmaBaglami baglam)
        {
            if (baglam.YonCelismesi == 0) return;

            baglam.Sonuc.Uyarilar.Add(
                $"{baglam.YonCelismesi} satırda Borç/Alacak kolonu ile tutarın işareti çelişti; " +
                "yön kolondan alındı. Dosya beklenen biçimde değilse kolon eşlemesi kontrol edilmeli.");
        }
    }
}
