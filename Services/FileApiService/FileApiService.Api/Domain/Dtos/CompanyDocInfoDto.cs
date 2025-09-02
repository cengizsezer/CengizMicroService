namespace FileApiService.Api.Domain.Dtos
{
    public sealed class CompanyDocInfoDto
    {
        public int Id { get; set; }
        public string DocCategory { get; set; } = default!;   // ENVANTERDEFTERI / VERGILEVHASI / ...
        public string Year { get; set; } = default!;          // "2025"
        public string? Description { get; set; }
        public int? SequenceNo { get; set; }                  // sıralama opsiyonel
        public string FileName { get; set; } = default!;
        public DateTime CreatedAtUtc { get; set; }
    }
}
