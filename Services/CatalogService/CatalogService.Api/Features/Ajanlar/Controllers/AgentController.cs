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
    /// </summary>
    [ApiController]
    [Route("api/catalog/agent")]
    [Authorize]
    public class AgentController : ControllerBase
    {
        private readonly IAjanDeposu _depo;

        public AgentController(IAjanDeposu depo) => _depo = depo;

        [HttpGet("baglilar")]
        public ActionResult<List<BagliAjanDto>> Baglilar()
        {
            var liste = _depo.Baglilar().Select(a => new BagliAjanDto
            {
                MakineId = a.MakineId,
                MakineAdi = a.MakineAdi,
                AjanSurumu = a.AjanSurumu,
                IsletimSistemi = a.IsletimSistemi,
                KullaniciId = a.KullaniciId,
                BaglantiZamani = a.BaglantiZamani,
                SonKalpAtisi = a.SonKalpAtisi,
                OrkaCalisiyorMu = a.OrkaCalisiyorMu
            }).ToList();

            return Ok(liste);
        }
    }
}
