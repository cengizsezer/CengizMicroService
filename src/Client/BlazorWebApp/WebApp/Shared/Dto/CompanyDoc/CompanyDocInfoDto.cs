namespace WebApp.Shared.Dto.CompanyDoc
{
    public class CompanyDocInfoDto
    {
        public int Id { get; set; }
        public string DocCategory { get; set; } = default!;
        public string Year { get; set; } = default!;
        public string? Description { get; set; }
        public int? SequenceNo { get; set; }
        public string FileName { get; set; } = default!;
        public DateTime CreatedAtUtc { get; set; }
    }
}
