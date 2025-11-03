using System.Net.Http.Json;
using WebApp.Application.Services.Interfaces;
using WebApp.Shared.Dto.Scheduling;

namespace WebApp.Application.Services
{
    public class JobsApi : IJobsApi
    {
        private readonly HttpClient _http;
        private const string BasePath = "/catalog/jobs";

        public JobsApi(HttpClient http) => _http = http;

        public async Task<JobDto> CreateAsync(CreateJobRequest req, CancellationToken ct = default)
        {
            using var res = await _http.PostAsJsonAsync(BasePath, req, ct);
            res.EnsureSuccessStatusCode();
            var dto = await res.Content.ReadFromJsonAsync<JobDto>(cancellationToken: ct);
            return dto!;
        }

        public async Task<List<JobDto>> GetRangeAsync(DateTime from, DateTime to, string? assigneeId = null, CancellationToken ct = default)
        {
            var qs = new List<string>
            {
                $"from={Uri.EscapeDataString(from.ToUniversalTime().ToString("o"))}",
                $"to={Uri.EscapeDataString(to.ToUniversalTime().ToString("o"))}"
            };
            if (!string.IsNullOrWhiteSpace(assigneeId))
                qs.Add($"assigneeId={Uri.EscapeDataString(assigneeId)}");

            var url = $"{BasePath}?{string.Join("&", qs)}";

            using var res = await _http.GetAsync(url, ct);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"GET {url} failed: {(int)res.StatusCode} {res.ReasonPhrase}. Body: {body}");
            }

            return await res.Content.ReadFromJsonAsync<List<JobDto>>(cancellationToken: ct) ?? new();
        }
        //public async Task<List<JobDto>> GetRangeAsync(DateTime from, DateTime to, string? assigneeId = null, CancellationToken ct = default)
        //{
        //    var qs = new List<string>
        //{
        //    $"from={Uri.EscapeDataString(from.ToUniversalTime().ToString("o"))}",
        //    $"to={Uri.EscapeDataString(to.ToUniversalTime().ToString("o"))}"
        //};
        //    if (!string.IsNullOrWhiteSpace(assigneeId))
        //        qs.Add($"assigneeId={Uri.EscapeDataString(assigneeId)}");

        //    var url = $"{BasePath}?{string.Join("&", qs)}";

        //    using var res = await _http.GetAsync(url, ct);

        //    // 404 → UI kırılmasın
        //    if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
        //        return new List<JobDto>();

        //    res.EnsureSuccessStatusCode();
        //    return await res.Content.ReadFromJsonAsync<List<JobDto>>(cancellationToken: ct) ?? new();
        //}

        //public async Task UpdateAssignmentStatusAsync(long assignmentId, JobStatus status, CancellationToken ct = default)
        //{
        //    var body = new UpdateAssignmentStatusRequest { Status = status };

        //    // .NET 8 uyumlu, güvenli PATCH:
        //    using var content = JsonContent.Create(body);
        //    using var req = new HttpRequestMessage(HttpMethod.Patch, $"{BasePath}/assignments/{assignmentId}/status")
        //    {
        //        Content = content
        //    };
        //    using var res = await _http.SendAsync(req, ct);
        //    res.EnsureSuccessStatusCode();
        //}

        public async Task UpdateAssignmentStatusAsync(long assignmentId, JobStatus status, CancellationToken ct = default)
        {
            var body = new UpdateAssignmentStatusRequest { Status = status };
            using var content = JsonContent.Create(body); // application/json; charset=utf-8
            using var req = new HttpRequestMessage(HttpMethod.Patch, $"{BasePath}/assignments/{assignmentId}/status")
            {
                Content = content
            };
            using var res = await _http.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();
        }

        //public async Task<JobDto> UpdateAsync(long id, CreateJobRequest req, CancellationToken ct = default)
        //{
        //    using var res = await _http.PutAsJsonAsync($"{BasePath}/{id}", req, ct);
        //    res.EnsureSuccessStatusCode();
        //    var dto = await res.Content.ReadFromJsonAsync<JobDto>(cancellationToken: ct);
        //    return dto!;
        //}
        public async Task<JobDto> UpdateAsync(long id, CreateJobRequest req, CancellationToken ct = default)
        {
            using var res = await _http.PutAsJsonAsync($"{BasePath}/{id}", req, ct);
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<JobDto>(cancellationToken: ct))!;
        }

        public async Task DeleteAsync(long id, CancellationToken ct = default)
        {
            using var res = await _http.DeleteAsync($"{BasePath}/{id}", ct);
            res.EnsureSuccessStatusCode();
        }

    }
}
