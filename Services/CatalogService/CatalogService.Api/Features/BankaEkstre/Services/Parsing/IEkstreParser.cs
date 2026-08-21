using CatalogService.Api.Features.BankaEkstre.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Services.Parsing
{
    /// <summary>Ham dosyadan çıkan tek satır. Henüz açıklama/hesap üretilmemiştir.</summary>
    public class AyrilanSatir
    {
        public int SiraNo { get; set; }

        /// <summary>Kaynak dosyadaki Excel satır numarası; düzeltilmiş ekstre dışa aktarımında kullanılır.</summary>
        public int KaynakSatirNo { get; set; }

        public DateTime Tarih { get; set; }
        public Yon Yon { get; set; }

        /// <summary>Her zaman pozitif.</summary>
        public decimal Tutar { get; set; }

        public string IslemTipi { get; set; } = string.Empty;
        public string HamAciklama { get; set; } = string.Empty;
        public string? KarsiIban { get; set; }

        /// <summary>
        /// Karşı tarafın VKN'si. Vakıfbank ayrıştırıcısı bu alanı **doldurmaz** — oradaki VKN
        /// kolonu hesap sahibinin VKN'si. Karşı tarafın VKN'sini gerçekten veren bir banka
        /// eklenirse alan hazır.
        /// </summary>
        public string? KarsiVkn { get; set; }

        public string? Kanal { get; set; }
    }

    public class EkstreParseSonuc
    {
        public List<AyrilanSatir> Satirlar { get; } = new();

        /// <summary>Kolon adı bulunamayıp indekse düşülmesi gibi durumlar burada raporlanır.</summary>
        public List<string> Uyarilar { get; } = new();

        public int AtlananSatir { get; set; }

        /// <summary>
        /// Açıklama kolonunun 1 tabanlı numarası. Dışa aktarımın birinci parçası orijinal
        /// dosyanın açıklama hücrelerini değiştirdiği için saklanır.
        /// </summary>
        public int AciklamaKolonu { get; set; }

        public DateTime? DonemBaslangic => Satirlar.Count == 0 ? null : Satirlar.Min(s => s.Tarih);
        public DateTime? DonemBitis => Satirlar.Count == 0 ? null : Satirlar.Max(s => s.Tarih);
    }

    /// <summary>
    /// Banka ekstresi ayrıştırıcısı. Her banka bu arayüzü uygular; yeni banka eklemek
    /// yeni bir implementasyon + DI kaydı + yapılandırma satırlarından ibarettir.
    /// </summary>
    public interface IEkstreParser
    {
        /// <summary>Banka hesabındaki <c>ParserTipi</c> ile eşleşen anahtar, ör. "VAKIFBANK_VADESIZ".</summary>
        string ParserTipi { get; }

        /// <summary>İnsan okuru için ad, ör. "Vakıfbank — Vadesiz TL".</summary>
        string Ad { get; }

        EkstreParseSonuc Ayristir(Stream dosya);
    }
}
