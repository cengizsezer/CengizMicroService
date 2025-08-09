using Microsoft.AspNetCore.Components.Forms;
using WebApp.Shared.Dto;

namespace WebApp.Application.Services.Interfaces
{
    public interface IOcrService
    {
        Task<AnalyzeResponseDto?> AnalyzeAsync(IBrowserFile file, CancellationToken ct = default);
    }
}
