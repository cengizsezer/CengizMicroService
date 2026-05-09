namespace Sovos.InvoiceWorker.Core.DTOs;

public class BatchRunResult
{
    public List<BatchRunItem> Success { get; set; } = new();
    public List<BatchRunItem> Failed { get; set; } = new();
}

public class BatchRunItem
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = "";
    public string? Error { get; set; }
}
