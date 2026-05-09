namespace WebApp.Shared.Dto.Sovos;

public class SovosBatchRunRequestDto
{
    public List<int> CompanyIds { get; set; } = new();
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

public class SovosBatchRunResultDto
{
    public List<SovosBatchRunItemDto> Success { get; set; } = new();
    public List<SovosBatchRunItemDto> Failed { get; set; } = new();
}

public class SovosBatchRunItemDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = "";
    public string? Error { get; set; }
}
