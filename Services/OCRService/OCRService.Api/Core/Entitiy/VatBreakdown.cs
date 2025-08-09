namespace OCRService.Api.Core.Entitiy
{
    public class VatBreakdown
    {
        public int Id { get; set; }
        public int ReceiptAnalysisId { get; set; }
        public ReceiptAnalysis ReceiptAnalysis { get; set; } = default!;

        // 0.01m, 0.10m, 0.20m vb. oran (yüzde değil, oran)
        public decimal Rate { get; set; }

        public decimal BaseAmount { get; set; }  // Matrah
        public decimal Vat { get; set; }  // KDV tutarı
    }
}
