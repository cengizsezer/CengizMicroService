using System.Text;

namespace FileApiService.Api.Domain.Extensions
{
    public static class FileKeyFactory
    {
        public static string ForDeclaration(string companyId, string year, string month, string declType, string docType, string? fileName = null)
        {
            var safeName = string.IsNullOrWhiteSpace(fileName) ? Guid.NewGuid().ToString("N") : Sanitize(fileName);
            var mm = int.Parse(month).ToString("00");
            return $"{companyId}/{year}/{mm}/{declType}/{docType}/{DateTime.UtcNow:yyyyMMddHHmmss}-{safeName}";
        }

        private static string Sanitize(string name)
        {
            var invalids = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (var ch in name) sb.Append(invalids.Contains(ch) ? '_' : ch);
            return sb.ToString();
        }
    }
}
