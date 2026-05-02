namespace WebApp.Application.Services.Interfaces
{
    public class MizanExcelRow
    {
        public string Kod { get; set; } = string.Empty;
        public decimal? OncekiDonem { get; set; }
        public decimal? CariDonem { get; set; }
    }

    public class MizanParseResult
    {
        public List<MizanExcelRow> Rows { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public interface IExcelMizanParser
    {
        Task<MizanParseResult> ParseAsync(Stream excelStream);
    }
}
