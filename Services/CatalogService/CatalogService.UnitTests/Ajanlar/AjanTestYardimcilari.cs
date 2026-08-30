using CatalogService.Api.Features.Ajanlar;
using CatalogService.Api.Features.Ajanlar.Domain;
using CatalogService.Api.Features.Ajanlar.Dtos;
using CatalogService.Api.Features.Ajanlar.Services;
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

        public SahteHubBaglami(string connectionId, string? ajanId = "7")
        {
            ConnectionId = connectionId;
            _kullanici = ajanId is null
                ? null
                : new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(AjanKimligi.TipClaim, AjanKimligi.AjanTipi),
                    new Claim(AjanKimligi.AjanIdClaim, ajanId),
                    new Claim("sub", $"ajan-{ajanId}")
                }, "Test"));
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

    /// <summary>
    /// Hub'ın kayıt kurallarını sınayan testler için iş servisi yerine geçen boş
    /// uygulama. İş akışının kendisi <c>AjanIsServisiTests</c>'te sınanıyor;
    /// buradaki testlerin konusu kayıt kabul/ret kararı.
    /// </summary>
    public class IssizIsServisi : IAjanIsServisi
    {
        public List<string> BekleyenSorulanAjanlar { get; } = new();
        public List<string> KopanAjanlar { get; } = new();

        public Task<AjanIsiOlusturSonucuDto> OlusturAsync(YeniAjanIsiDto istek, string kullaniciId, CancellationToken ct = default)
            => Task.FromResult(new AjanIsiOlusturSonucuDto());

        public Task<AjanIsDto?> GetirAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<AjanIsDto?>(null);

        public Task<List<AjanIsDto>> ListeleAsync(int? firmaId, AjanIsDurumu? durum, string? ajanId,
                                                  int enFazla = 50, CancellationToken ct = default)
            => Task.FromResult(new List<AjanIsDto>());

        public Task<AjanIsDto?> IptalAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<AjanIsDto?>(null);

        public Task<bool> BasladiAsync(string ajanId, Guid isId, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> IlerlemeAsync(string ajanId, Guid isId, int yuzde, string? mesaj,
                                        int? tamamlananAdim, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> BittiAsync(string ajanId, Guid isId, bool basarili, string? hataMesaji,
                                     string? sonucOzetiJson, string? hataEkraniDosyaId = null,
                                     CancellationToken ct = default)
            => Task.FromResult(true);

        public Task BekleyenleriGonderAsync(string ajanId, CancellationToken ct = default)
        {
            BekleyenSorulanAjanlar.Add(ajanId);
            return Task.CompletedTask;
        }

        public Task BaglantiKoptuAsync(string ajanId, CancellationToken ct = default)
        {
            KopanAjanlar.Add(ajanId);
            return Task.CompletedTask;
        }
    }

    public static class AjanTestVerisi
    {
        public static AgentHubAyarlari Ayarlar(string asgari = "1.0.0", string sunucu = "1.2.0", int zamanAsimi = 90)
            => new() { AsgariAjanSurumu = asgari, SunucuSurumu = sunucu, KalpAtisiZamanAsimiSaniye = zamanAsimi };
    }
}
