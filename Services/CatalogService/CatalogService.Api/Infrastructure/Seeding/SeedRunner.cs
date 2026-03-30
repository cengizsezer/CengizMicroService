using CatalogService.Api.Features.AccountPlan;
using CatalogService.Api.Features.Payroll.Persistence.Seeds;
using CatalogService.Api.Infrastructure.Accessor;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Infrastructure.Seeding
{
    public sealed class SeedRunner
    {
        private readonly IServiceProvider _serviceProvider;

        public SeedRunner(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task RunAsync(
            SeedMode mode,
            CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();

            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<CatalogContextSeed>>();
            var envHost = services.GetRequiredService<IWebHostEnvironment>();
            var options = services.GetRequiredService<DbContextOptions<CatalogContext>>();

            var seedSucceeded = false;
            string? seedErrorMessage = null;
            Exception? seedException = null;

            try
            {
                logger.LogInformation("========================================");
                logger.LogInformation("🚀 SEED BAŞLADI");
                logger.LogInformation("Mode: {Mode}", mode);
                logger.LogInformation("Environment: {Environment}", envHost.EnvironmentName);
                logger.LogInformation("========================================");

                switch (mode)
                {
                    case SeedMode.PayrollOnly:
                        await RunPayrollOnlyAsync(options, logger, cancellationToken);
                        break;

                    case SeedMode.Full:
                        await RunFullAsync(options, envHost, logger, cancellationToken);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(mode), mode, "Desteklenmeyen seed mode.");
                }

                seedSucceeded = true;
            }
            catch (Exception ex)
            {
                seedSucceeded = false;
                seedException = ex;
                seedErrorMessage = ex.Message;

                logger.LogError(ex, "❌ Seed sırasında hata oluştu.");
            }
            finally
            {
                logger.LogInformation("========================================");

                if (seedSucceeded)
                {
                    logger.LogInformation("✅ SEED BAŞARILI TAMAMLANDI");
                }
                else
                {
                    logger.LogWarning("⚠️ SEED HATA İLE TAMAMLANDI");
                    logger.LogWarning("Hata Özeti: {ErrorMessage}", seedErrorMessage);

                    if (seedException is SqlException sqlEx)
                    {
                        logger.LogWarning("SQL Error Number: {ErrorNumber}", sqlEx.Number);
                        logger.LogWarning("SQL Error State : {ErrorState}", sqlEx.State);
                        logger.LogWarning("SQL Error Class : {ErrorClass}", sqlEx.Class);
                    }

                    logger.LogWarning("Not: Uygulama Exit 0 ile kapanacaktır.");
                }

                logger.LogInformation("========================================");
            }
        }

        private static async Task RunPayrollOnlyAsync(
            DbContextOptions<CatalogContext> options,
            ILogger<CatalogContextSeed> logger,
            CancellationToken cancellationToken)
        {
            await RetryHelper.ExecuteWithRetryAsync(async () =>
            {
                using var ctx = new CatalogContext(options, new FixedTenantAccessor("schema"));

                logger.LogInformation("🧱 Migration uygulanıyor...");
                await ctx.Database.MigrateAsync(cancellationToken);

                logger.LogInformation("💼 SADECE Payroll seed uygulanıyor...");
                await PayrollSeedData.SeedAsync(ctx);

            }, logger, cancellationToken: cancellationToken);
        }

        private static async Task RunFullAsync(
            DbContextOptions<CatalogContext> options,
            IWebHostEnvironment envHost,
            ILogger<CatalogContextSeed> logger,
            CancellationToken cancellationToken)
        {
            await RetryHelper.ExecuteWithRetryAsync(async () =>
            {
                using var ctxOnce = new CatalogContext(options, new FixedTenantAccessor("schema"));

                logger.LogInformation("🧹 Catalog verileri temizleniyor ve şema kontrol ediliyor...");

                ctxOnce.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[catalog].[ProductDetails]', 'U') IS NOT NULL
    DELETE FROM [catalog].[ProductDetails];

IF OBJECT_ID(N'[catalog].[ReceiptItems]', 'U') IS NOT NULL
    DELETE FROM [catalog].[ReceiptItems];

IF OBJECT_ID(N'[catalog].[Expenses]', 'U') IS NOT NULL
    DELETE FROM [catalog].[Expenses];

IF OBJECT_ID(N'[catalog].[AccountingCodes]', 'U') IS NOT NULL
    DELETE FROM [catalog].[AccountingCodes];

IF OBJECT_ID(N'[catalog].[Personnels]', 'U') IS NOT NULL
    DELETE FROM [catalog].[Personnels];

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'pkf')
    EXEC('CREATE SCHEMA pkf');
");

                logger.LogInformation("🧱 Migration uygulanıyor...");
                await ctxOnce.Database.MigrateAsync(cancellationToken);

                logger.LogInformation("💼 Payroll seed uygulanıyor...");
                await PayrollSeedData.SeedAsync(ctxOnce);

            }, logger, cancellationToken: cancellationToken);

            var seeder = new CatalogContextSeed();
            var tenants = new[] { "201", "106", "108", "105", "107", "500" };

            foreach (var tenant in tenants)
            {
                await RetryHelper.ExecuteWithRetryAsync(async () =>
                {
                    using var ctx = new CatalogContext(options, new FixedTenantAccessor(tenant));

                    logger.LogInformation("🏢 Tenant seed başlıyor: {Tenant}", tenant);

                    await seeder.SeedAsync(ctx, envHost, logger, new[] { tenant }, force: true);
                    await AccountPlanSeed.SeedAsync(ctx, logger);

                    logger.LogInformation("✅ Tenant seed tamamlandı: {Tenant}", tenant);

                }, logger, cancellationToken: cancellationToken);
            }
        }
    }
}