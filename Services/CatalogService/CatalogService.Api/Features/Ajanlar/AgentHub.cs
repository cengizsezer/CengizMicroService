using CatalogService.Api.Features.Ajanlar.Domain;
using CatalogService.Api.Features.Ajanlar.Dtos;
using CatalogService.Api.Features.Ajanlar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

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
    [Authorize(Policy = AjanPolitikalari.YalnizAjan)]
    public class AgentHub : Hub
    {
        public const string Yol = "/agenthub";

        private readonly IAjanDeposu _depo;
        private readonly IOptionsMonitor<AgentHubAyarlari> _ayarlar;
        private readonly IAjanIsServisi _isler;
        private readonly ILogger<AgentHub> _log;

        public AgentHub(IAjanDeposu depo, IOptionsMonitor<AgentHubAyarlari> ayarlar,
                        IAjanIsServisi isler, ILogger<AgentHub> log)
        {
            _depo = depo;
            _ayarlar = ayarlar;
            _isler = isler;
            _log = log;
        }

        public override Task OnConnectedAsync()
        {
            // Bağlantı kurulmuş ama ajan henüz kendini tanıtmamış oluyor: listeye
            // Kaydol ile giriliyor. Tanıtmayan bir bağlantı listede görünmez.
            _log.LogInformation("Ajan bağlantısı açıldı: {ConnectionId} (ajan {AjanId})",
                Context.ConnectionId, AjanId());
            return base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var cikan = _depo.Cikar(Context.ConnectionId);
            if (cikan is not null)
            {
                _log.LogInformation("Ajan ayrıldı: {MakineAdi} ({MakineId})", cikan.MakineAdi, cikan.MakineId);

                // Yarım kalan iş "çalışıyor" bırakılmıyor: ajan onu bildiremeden
                // gitti ve kimse beklemesin.
                await _isler.BaglantiKoptuAsync(cikan.AjanId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>Ajan bağlandıktan sonra kendini tanıtır.</summary>
        public async Task<KayitSonucu> Kaydol(AjanKaydiIstegi istek)
        {
            var ayar = _ayarlar.CurrentValue;

            if (istek is null || string.IsNullOrWhiteSpace(istek.MakineId))
                return Sonuc(false, "MakineId zorunlu.", ayar);

            if (!SurumKontrolu.Uygun(istek.AjanSurumu, ayar.AsgariAjanSurumu, out var surumMesaji))
            {
                _log.LogWarning("Ajan kaydı reddedildi (sürüm): {MakineId} {Surum} — {Mesaj}",
                    istek.MakineId, istek.AjanSurumu, surumMesaji);
                return Sonuc(false, surumMesaji, ayar);
            }

            var kayit = new AjanKaydi
            {
                ConnectionId = Context.ConnectionId,
                MakineId = istek.MakineId.Trim(),
                MakineAdi = string.IsNullOrWhiteSpace(istek.MakineAdi) ? istek.MakineId.Trim() : istek.MakineAdi.Trim(),
                AjanSurumu = (istek.AjanSurumu ?? string.Empty).Trim(),
                IsletimSistemi = istek.IsletimSistemi,
                OrkaCalisiyorMu = istek.OrkaCalisiyorMu,
                // Sahip token'dan; istekle gelen bir kimlik alanı yok, olsaydı da
                // ona güvenilmezdi.
                AjanId = AjanId(),
                BaglantiyiKes = Context.Abort
            };

            var sonuc = _depo.Kaydet(kayit);
            if (sonuc.Dusurulen is not null)
            {
                _log.LogInformation("Aynı makine yeniden bağlandı, eski bağlantı düşürülüyor: {MakineId} ({Eski})",
                    kayit.MakineId, sonuc.Dusurulen.ConnectionId);
                sonuc.Dusurulen.BaglantiyiKes?.Invoke();
            }

            _log.LogInformation("Ajan kaydoldu: {MakineAdi} ({MakineId}) sürüm {Surum}, ajan {AjanId}",
                kayit.MakineAdi, kayit.MakineId, kayit.AjanSurumu, kayit.AjanId);

            // Ajan yokken açılmış işler burada sıradan alınıyor. Kayıt kabul
            // edilmeden gönderilmiyor: sürümü tutmayan ajana iş vermenin anlamı yok.
            await _isler.BekleyenleriGonderAsync(kayit.AjanId);

            return Sonuc(true, "Kayıt kabul edildi.", ayar);
        }

        // ---- ajanın iş bildirimleri ---------------------------------------
        //
        // Üçünde de <b>sahiplik sunucuda</b> doğrulanıyor: isId ile birlikte
        // token'daki ajan kimliği aranıyor, başka ajanın işi hiç yüklenmiyor.
        // Tekrarlanan bildirim zararsız — ağ kopup yeniden bağlanan ajan son
        // durumu tekrar gönderebiliyor.

        /// <summary>Ajan işe başladığını bildirir.</summary>
        public async Task IsBasladi(Guid isId)
        {
            if (!await _isler.BasladiAsync(AjanId(), isId))
                _log.LogWarning("Tanınmayan iş bildirimi (başladı): {IsId} / ajan {AjanId}", isId, AjanId());
        }

        /// <summary>Ajan ilerleme bildirir.</summary>
        public async Task IsIlerleme(Guid isId, int yuzde, string? mesaj, int? tamamlananAdim)
        {
            if (!await _isler.IlerlemeAsync(AjanId(), isId, yuzde, mesaj, tamamlananAdim))
                _log.LogWarning("Tanınmayan iş bildirimi (ilerleme): {IsId} / ajan {AjanId}", isId, AjanId());
        }

        /// <summary>Ajan işin bittiğini bildirir.</summary>
        public async Task IsBitti(Guid isId, bool basarili, string? hataMesaji,
                                  string? sonucOzetiJson, string? hataEkraniDosyaId)
        {
            if (!await _isler.BittiAsync(AjanId(), isId, basarili, hataMesaji, sonucOzetiJson, hataEkraniDosyaId))
                _log.LogWarning("Tanınmayan iş bildirimi (bitti): {IsId} / ajan {AjanId}", isId, AjanId());
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

        /// <summary>
        /// Bağlantının ajan kimliği. Politika bu claim olmadan bağlantıyı zaten
        /// içeri almıyor; burada boş dönmesi beklenmiyor.
        /// </summary>
        private string AjanId() => AjanKimligi.AjanId(Context.User);
    }
}
