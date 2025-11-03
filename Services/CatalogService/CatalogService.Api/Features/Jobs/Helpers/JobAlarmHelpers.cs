namespace CatalogService.Api.Features.Jobs.Helpers
{
    public static class JobAlarmHelpers
    {
        public static TimeZoneInfo SafeTz(string tz)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(tz); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }
        }

        // (JobId, UserId) → aynı kombinasyon her zaman aynı GUID
        public static Guid DeterministicGuid(long jobId, string userId)
        {
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes($"{jobId}:{userId}");
            var hash = sha1.ComputeHash(bytes);
            var g = new byte[16];
            Array.Copy(hash, g, 16);
            // version 5 benzeri
            g[6] = (byte)((g[6] & 0x0F) | 0x50);
            g[8] = (byte)((g[8] & 0x3F) | 0x80);
            return new Guid(g);
        }
    }
}
