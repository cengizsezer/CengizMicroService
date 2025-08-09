namespace OCRService.Api.Core.Entitiy
{
    public class ReceiptAnalysis
    {
        public int Id { get; set; }
        public string Source { get; set; } = "ocr+openai"; // kaynağı
        public string CompanyName { get; set; } =string.Empty;
        public string ReceiptNumber { get; set; } = string.Empty;
        public decimal NetTotal { get; set; }             // KDV hariç toplam
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Raw OCR
        public string ExtractedText { get; set; } = string.Empty ;    

        // KDV kırılımları
        public List<VatBreakdown> VatBreakdowns { get; set; } = new();
    }
}
