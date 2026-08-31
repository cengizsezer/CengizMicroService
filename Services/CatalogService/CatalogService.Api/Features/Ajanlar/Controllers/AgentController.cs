using CatalogService.Api.Features.Ajanlar.Dtos;
using CatalogService.Api.Features.Ajanlar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Features.Ajanlar.Controllers
{
    /// <summary>
    /// Ajanların durumu. Sıradan bir HTTP ucu olduğu için Ocelot'tan geçiyor
    /// (<c>/catalog/agent/baglilar</c>); yalnız hub'ın WebSocket yolu gateway'i
    /// baypas ediyor.
    ///
    /// Burası <b>insan tarafı</b>: kullanıcı token'ıyla çalışıyor, ajan token'ıyla
    /// çalışmıyor. Ajanın kendisinin diğer ajanların listesini okuması için bir
    /// nedeni yok.
    /// </summary>
    [ApiController]
    [Route("api/catalog/agent")]
    [Authorize(Policy = AjanPolitikalari.YalnizInsan)]
    public class AgentController : ControllerBase
    {
        private readonly IAjanDeposu _depo;
        private readonly ILogger<AgentController> _log;

        public AgentController(IAjanDeposu depo, ILogger<AgentController> log)
        {
            _depo = depo;
            _log = log;
        }

        [HttpGet("baglilar")]
        public ActionResult<List<BagliAjanDto>> Baglilar()
        {
            var liste = _depo.Baglilar().Select(a => new BagliAjanDto
            {
                MakineId = a.MakineId,
                MakineAdi = a.MakineAdi,
                AjanSurumu = a.AjanSurumu,
                IsletimSistemi = a.IsletimSistemi,
                AjanId = a.AjanId,
                BaglantiZamani = a.BaglantiZamani,
                SonKalpAtisi = a.SonKalpAtisi,
                OrkaCalisiyorMu = a.OrkaCalisiyorMu
            }).ToList();

            return Ok(liste);
        }

        /// <summary>
        /// Bir ajanın açık bağlantılarını düşürür.
        ///
        /// Anahtar iptali IdentityService'te oluyor ama soket burada duruyor;
        /// iptal edilen ajan, elindeki token'ın ömrü (8 saat) boyunca bağlı
        /// kalmasın diye yönetim ekranı iptalin hemen ardından burayı çağırıyor.
        /// Kayıt yoksa da başarılı: "bu ajan bağlı değil" istenen sonucun ta kendisi.
        /// </summary>
        [HttpPost("{ajanId}/dusur")]
        [Authorize(Policy = AjanPolitikalari.YonetimiDuzenle)]
        public ActionResult<int> Dusur(string ajanId)
        {
            var dusenler = _depo.AjanaGoreCikar(ajanId);

            foreach (var kayit in dusenler)
            {
                _log.LogInformation("Ajan bağlantısı iptal nedeniyle düşürülüyor: {MakineAdi} ({MakineId}), ajan {AjanId}",
                    kayit.MakineAdi, kayit.MakineId, kayit.AjanId);

                kayit.BaglantiyiKes?.Invoke();
            }

            return Ok(dusenler.Count);
        }
    }
}
