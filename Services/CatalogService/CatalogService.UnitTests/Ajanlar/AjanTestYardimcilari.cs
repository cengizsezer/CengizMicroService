using CatalogService.Api.Features.Ajanlar;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace CatalogService.UnitTests.Ajanlar
{
    /// <summary>
    /// İleri geri sürülebilen saat. Zaman aşımı testleri gerçek saati beklemesin:
    /// 90 saniyelik eşiği sınamak için 90 saniye uyuyan bir test, süre uzadıkça
    /// ilk atılacak testtir.
    /// </summary>
    public class SahteSaat : TimeProvider
    {
        private DateTimeOffset _simdi;

        public SahteSaat(DateTimeOffset? baslangic = null)
            => _simdi = baslangic ?? new DateTimeOffset(2026, 8, 29, 10, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _simdi;

        public void Ilerle(TimeSpan sure) => _simdi = _simdi.Add(sure);
    }

    /// <summary>Tek değerli, değişmeyen <see cref="IOptionsMonitor{T}"/>.</summary>
    public class SabitAyar<T> : IOptionsMonitor<T>
    {
        public SabitAyar(T deger) => CurrentValue = deger;

        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    /// <summary>
    /// SignalR'ın hub bağlamı yerine geçen sahte. <c>Hub.Context</c> yazılabilir
    /// olduğu için hub'ı gerçek bir bağlantı kurmadan, doğrudan sınayabiliyoruz.
    /// </summary>
    public class SahteHubBaglami : HubCallerContext
    {
        private readonly ClaimsPrincipal? _kullanici;

        public SahteHubBaglami(string connectionId, string? kullaniciId = "kullanici-1")
        {
            ConnectionId = connectionId;
            _kullanici = kullaniciId is null
                ? null
                : new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", kullaniciId) }, "Test"));
        }

        public bool Kesildi { get; private set; }

        public override string ConnectionId { get; }
        public override string? UserIdentifier => _kullanici?.FindFirst("sub")?.Value;
        public override ClaimsPrincipal? User => _kullanici;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort() => Kesildi = true;
    }

    public static class AjanTestVerisi
    {
        public static AgentHubAyarlari Ayarlar(string asgari = "1.0.0", string sunucu = "1.2.0", int zamanAsimi = 90)
            => new() { AsgariAjanSurumu = asgari, SunucuSurumu = sunucu, KalpAtisiZamanAsimiSaniye = zamanAsimi };
    }
}
