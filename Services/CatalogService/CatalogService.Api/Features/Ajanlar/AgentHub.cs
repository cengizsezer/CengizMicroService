using CatalogService.Api.Features.Ajanlar.Domain;
using CatalogService.Api.Features.Ajanlar.Dtos;
using CatalogService.Api.Features.Ajanlar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace CatalogService.Api.Features.Ajanlar
{
    /// <summary>
    /// Ofisteki banka bilgisayarında çalışan PkfRobot ajanının bağlandığı hub.
    ///
    /// <b>Yön ters:</b> sunucu ofis makinesine uzanamıyor (NAT arkasında, sabit
    /// adresi yok), o yüzden bağlantıyı ajan kuruyor ve açık tutuyor. İş emri
    /// bu açık kanaldan geriye doğru gidecek (B adımı).
    ///
    /// Hub <c>CatalogService.Api</c> içinde yaşıyor: ajanın işleyeceği banka
    /// aktarım paketini üreten uçlar (<c>api/catalog/banka-ekstre/*</c>) zaten
    /// burada. Ayrı bir servis, yeni bir container ve deploy adımı demek olurdu.
    /// </summary>
    [Authorize]
    public class AgentHub : Hub
    {
        public const string Yol = "/agenthub";

        private readonly IAjanDeposu _depo;
        private readonly IOptionsMonitor<AgentHubAyarlari> _ayarlar;
        private readonly ILogger<AgentHub> _log;

        public AgentHub(IAjanDeposu depo, IOptionsMonitor<AgentHubAyarlari> ayarlar, ILogger<AgentHub> log)
        {
            _depo = depo;
            _ayarlar = ayarlar;
            _log = log;
        }

        public override Task OnConnectedAsync()
        {
            // Bağlantı kurulmuş ama ajan henüz kendini tanıtmamış oluyor: listeye
            // Kaydol ile giriliyor. Tanıtmayan bir bağlantı listede görünmez.
            _log.LogInformation("Ajan bağlantısı açıldı: {ConnectionId} (kullanıcı {KullaniciId})",
                Context.ConnectionId, KullaniciId());
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            var cikan = _depo.Cikar(Context.ConnectionId);
            if (cikan is not null)
                _log.LogInformation("Ajan ayrıldı: {MakineAdi} ({MakineId})", cikan.MakineAdi, cikan.MakineId);

            return base.OnDisconnectedAsync(exception);
        }

        /// <summary>Ajan bağlandıktan sonra kendini tanıtır.</summary>
        public Task<KayitSonucu> Kaydol(AjanKaydiIstegi istek)
        {
            var ayar = _ayarlar.CurrentValue;

            if (istek is null || string.IsNullOrWhiteSpace(istek.MakineId))
                return Task.FromResult(Sonuc(false, "MakineId zorunlu.", ayar));

            if (!SurumKontrolu.Uygun(istek.AjanSurumu, ayar.AsgariAjanSurumu, out var surumMesaji))
            {
                _log.LogWarning("Ajan kaydı reddedildi (sürüm): {MakineId} {Surum} — {Mesaj}",
                    istek.MakineId, istek.AjanSurumu, surumMesaji);
                return Task.FromResult(Sonuc(false, surumMesaji, ayar));
            }

            var kayit = new AjanKaydi
            {
                ConnectionId = Context.ConnectionId,
                MakineId = istek.MakineId.Trim(),
                MakineAdi = string.IsNullOrWhiteSpace(istek.MakineAdi) ? istek.MakineId.Trim() : istek.MakineAdi.Trim(),
                AjanSurumu = (istek.AjanSurumu ?? string.Empty).Trim(),
                IsletimSistemi = istek.IsletimSistemi,
                OrkaCalisiyorMu = istek.OrkaCalisiyorMu,
                // Sahip token'dan; istekle gelen bir kullanıcı alanı yok, olsaydı da
                // ona güvenilmezdi.
                KullaniciId = KullaniciId(),
                BaglantiyiKes = Context.Abort
            };

            var sonuc = _depo.Kaydet(kayit);
            if (sonuc.Dusurulen is not null)
            {
                _log.LogInformation("Aynı makine yeniden bağlandı, eski bağlantı düşürülüyor: {MakineId} ({Eski})",
                    kayit.MakineId, sonuc.Dusurulen.ConnectionId);
                sonuc.Dusurulen.BaglantiyiKes?.Invoke();
            }

            _log.LogInformation("Ajan kaydoldu: {MakineAdi} ({MakineId}) sürüm {Surum}, kullanıcı {KullaniciId}",
                kayit.MakineAdi, kayit.MakineId, kayit.AjanSurumu, kayit.KullaniciId);

            return Task.FromResult(Sonuc(true, "Kayıt kabul edildi.", ayar));
        }

        /// <summary>Bağlantının canlı olduğunu bildirir.</summary>
        public Task KalpAtisi()
        {
            if (!_depo.KalpAtisi(Context.ConnectionId))
                _log.LogDebug("Kaydolmamış bağlantıdan kalp atışı: {ConnectionId}", Context.ConnectionId);

            return Task.CompletedTask;
        }

        private KayitSonucu Sonuc(bool kabul, string mesaj, AgentHubAyarlari ayar) => new()
        {
            Kabul = kabul,
            Mesaj = mesaj,
            SunucuSurumu = ayar.SunucuSurumu,
            AsgariAjanSurumu = ayar.AsgariAjanSurumu
        };

        private string KullaniciId() =>
            Context.User?.FindFirst("sub")?.Value ??
            Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            Context.UserIdentifier ??
            string.Empty;
    }
}
