namespace FileApiService.Api.Core.Extensions
{
    public static class Normalizer
    {
        public static string Canon(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var up = s.Trim().ToUpperInvariant();
            // Sadece harf/rakam kalsın (unicode dash, NBSP vs. hepsi uçar)
            var arr = up.Where(char.IsLetterOrDigit).ToArray();
            return new string(arr);
        }

        public static string Month2(int month) =>
            Math.Clamp(month, 1, 12).ToString("00");
    }
}
