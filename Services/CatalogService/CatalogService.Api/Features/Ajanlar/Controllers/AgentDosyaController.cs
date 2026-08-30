using CatalogService.Api.Features.Ajanlar.Domain;
using CatalogService.Api.Features.Ajanlar.Services;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Features.BankaEkstre.Services;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CatalogService.Api.Features.Ajanlar.Controllers
{
    /// <summary>
    /// Ajanın <b>kendi işinin</b> dosyalarını indirdiği uçlar: düzeltilmiş ekstre
    /// (xlsx) ve karşı hesap kodu listesi (JSON).
    ///
    /// <b>Neden Banka Otomasyon uçları ajana açılmadı:</b> orada dosya
    /// <c>?firmaId=</c> ile isteniyor ve isteyen her firmanın her ekstresini
    /// alabilirdi. Buradaki iki uç ajanın <b>o an atanmış</b> işine bağlı: iş
    /// kimliği token'daki ajana ait değilse ya da iş bitmişse dosya verilmiyor.
    /// Ofisteki makinede duran bir anahtarın erişebileceği alan, o anda
    /// yapmakta olduğu işten ibaret.
    ///
    /// Firma kapsamı isteğin parametresinden değil <b>işin kendisinden</b>
    /// kuruluyor; ajan hangi firmanın dosyasını alacağını seçemiyor.
    /// </summary>
    [ApiController]
    [Route("api/catalog/agent/is")]
    [Authorize(Policy = AjanPolitikalari.YalnizAjan)]
    public class AgentDosyaController : ControllerBase
    {
        private readonly CatalogContext _db;
        private readonly IEkstreService _ekstreler;
        private readonly IBankaFirmaKapsami _kapsam;
        private readonly ILogger<AgentDosyaController> _log;

        public AgentDosyaController(CatalogContext db, IEkstreService ekstreler,
                                    IBankaFirmaKapsami kapsam, ILogger<AgentDosyaController> log)
        {
            _db = db;
            _ekstreler = ekstreler;
            _kapsam = kapsam;
            _log = log;
        }

        /// <summary>ORKA'nın Veri Transferi ekranına yüklenecek düzeltilmiş ekstre.</summary>
        [HttpGet("{isId:guid}/ekstre")]
        public async Task<IActionResult> Ekstre(Guid isId, CancellationToken ct)
        {
            var (yuk, hata) = await IsinYukunuCozAsync(isId, ct);
            if (hata is not null) return hata;

            var dosya = await _ekstreler.DuzeltilmisEkstreAsync(yuk!.EkstreYuklemeId, ct);
            if (dosya is null) return NotFound(new { message = "Ekstre yüklemesi bulunamadı." });

            return File(dosya.Icerik,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        dosya.DosyaAdi);
        }

        /// <summary>Grid'e yazılacak karşı hesap kodları; <c>GridDoldur</c> bunu tüketiyor.</summary>
        [HttpGet("{isId:guid}/kod-listesi")]
        public async Task<IActionResult> KodListesi(Guid isId, CancellationToken ct)
        {
            var (yuk, hata) = await IsinYukunuCozAsync(isId, ct);
            if (hata is not null) return hata;

            var aktarim = await _ekstreler.DisaAktarAsync(yuk!.EkstreYuklemeId, ct);
            return aktarim is null
                ? NotFound(new { message = "Ekstre yüklemesi bulunamadı." })
                : Ok(aktarim);
        }

        /// <summary>
        /// İşi bulur, sahipliğini doğrular ve firma kapsamını işin firmasına kurar.
        /// </summary>
        private async Task<(OrkayaAktarYuku? Yuk, IActionResult? Hata)> IsinYukunuCozAsync(
            Guid isId, CancellationToken ct)
        {
            var ajanId = AjanKimligi.AjanId(User);

            var kayit = await _db.AjanIsleri.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == isId && x.AjanId == ajanId, ct);

            if (kayit is null)
            {
                _log.LogWarning("Ajan tanınmayan işin dosyasını istedi: {IsId} (ajan {AjanId})", isId, ajanId);
                return (null, NotFound(new { message = "İş bulunamadı." }));
            }

            if (kayit.Bitti)
                return (null, Conflict(new { message = "İş bitmiş; dosyaları artık verilmiyor." }));

            if (kayit.IsTipi != AjanIsTipleri.OrkayaAktar)
                return (null, BadRequest(new { message = "Bu iş tipinin dosyası yok." }));

            OrkayaAktarYuku? yuk;
            try
            {
                yuk = JsonSerializer.Deserialize<OrkayaAktarYuku>(kayit.Yuk);
            }
            catch (JsonException)
            {
                yuk = null;
            }

            if (yuk is null || yuk.EkstreYuklemeId <= 0)
                return (null, BadRequest(new { message = "İş paketi okunamadı." }));

            // Kapsam istekten değil işten: ajan başka firmanın dosyasını isteyemiyor.
            _kapsam.Ayarla(kayit.FirmaId);
            return (yuk, null);
        }
    }
}
