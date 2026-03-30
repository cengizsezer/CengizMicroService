

using Microsoft.Data.SqlClient;

namespace CatalogService.Api.Infrastructure.Seeding
{
    public static class RetryHelper
    {
        public static async Task ExecuteWithRetryAsync(
            Func<Task> action,
            ILogger logger,
            int maxRetryCount = 10,
            int delaySeconds = 5,
            CancellationToken cancellationToken = default)
        {
            for (int attempt = 1; attempt <= maxRetryCount; attempt++)
            {
                try
                {
                    logger.LogInformation(
                        "🔁 İşlem deneniyor... Attempt {Attempt}/{MaxRetryCount}",
                        attempt, maxRetryCount);

                    await action();

                    logger.LogInformation("✅ İşlem başarılı.");
                    return;
                }
                catch (Exception ex) when (attempt < maxRetryCount && IsTransient(ex))
                {
                    logger.LogWarning(
                        ex,
                        "⚠️ Geçici hata oluştu. {DelaySeconds} sn sonra tekrar denenecek. Attempt {Attempt}/{MaxRetryCount}",
                        delaySeconds, attempt, maxRetryCount);

                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
            }

            await action();
        }

        private static bool IsTransient(Exception ex)
        {
            if (ex is SqlException)
                return true;

            if (ex.InnerException is SqlException)
                return true;

            var message = ex.ToString();

            return message.Contains("pre-login handshake", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("connection reset by peer", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("transport connection", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("server was not found", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
        }
    }
}
