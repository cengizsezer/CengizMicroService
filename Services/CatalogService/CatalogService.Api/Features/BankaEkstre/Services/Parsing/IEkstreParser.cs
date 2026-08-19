using CatalogService.Api.Features.BankaEkstre.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Services.Parsing
{
    /// <summary>Ham dosyadan çıkan tek satır. Henüz açıklama/hesap üretilmemiştir.</summary>
    public class AyrilanSatir
    {
        public int SiraNo { get; set; }
        public DateTime Tarih { get; set; }
        public Yon Yon { get; set; }

        /// <summary>Her zaman pozitif.</summary>
        public decimal Tutar { get; set; }

        public string IslemTipi { get; set; } = string.Empty;
        public string HamAciklama { get; set; } = string.Empty;
        public string? KarsiIban { get; set; }
        public string? KarsiVkn { get; set; }
        public string? Kanal { get; set; }
    }

    public class EkstreParseSonuc
    {
        public List<AyrilanSatir> Satirlar { get; } = new();

        /// <summary>Kolon adı bulunamayıp indekse düşülmesi gibi durumlar burada raporlanır.</summary>
        public List<string> Uyarilar { get; } = new();

        public int AtlananSatir { get; set; }

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
