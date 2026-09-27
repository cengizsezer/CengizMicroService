using CatalogService.Api.Features.FirmaKontrol.Dtos;
using CatalogService.Api.Features.FirmaKontrol.Services;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;

namespace CatalogService.Api.Features.FirmaKontrol.Controllers
{
    [ApiController]
    [Route("api/catalog/firma-kontrol")]
    public class FirmaKontrolController : ControllerBase
    {
        private readonly IFirmaKontrolMaddeService _service;
        private readonly IFirmaKontrolMizanService _mizan;
        private readonly IFirmaKontrolVergiService _vergi;
        private readonly IMizanNotuService _mizanNotu;
        private readonly IEtiketService _etiket;
        private readonly ILogger<FirmaKontrolController> _log;

        public FirmaKontrolController(
            IFirmaKontrolMaddeService service,
            IFirmaKontrolMizanService mizan,
            IFirmaKontrolVergiService vergi,
            IMizanNotuService mizanNotu,
            IEtiketService etiket,
            ILogger<FirmaKontrolController> log)
        {
            _service = service;
            _mizan = mizan;
            _vergi = vergi;
            _mizanNotu = mizanNotu;
            _etiket = etiket;
            _log = log;
        }

        // ── Durum satırları (şablon durumları + özel maddeler) ──────────────

        [HttpGet("{firmaId:int}/maddeler")]
        [ProducesResponseType(typeof(List<FirmaKontrolMaddeDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<FirmaKontrolMaddeDto>>> GetMaddeler(int firmaId, CancellationToken ct)
        {
            var list = await _service.GetDurumlarAsync(firmaId, ct);
            return Ok(list);
        }

        // ── Tek madde durum/not upsert (şablon: MaddeKey, özel: Id) ─────────

        [HttpPut("{firmaId:int}/maddeler")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpsertMadde(
            int firmaId,
            [FromBody] FirmaKontrolMaddeUpsertDto dto,
            CancellationToken ct)
        {
            if (dto is null)
                return BadRequest(new { mesaj = "İstek gövdesi boş olamaz." });

            try
            {
                await _service.UpsertDurumAsync(firmaId, dto, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
        }

        // ── Özel madde ekle ─────────────────────────────────────────────────

        [HttpPost("{firmaId:int}/maddeler/ozel")]
        [ProducesResponseType(typeof(FirmaKontrolMaddeDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<FirmaKontrolMaddeDto>> AddOzel(
            int firmaId,
            [FromBody] OzelMaddeCreateDto dto,
            CancellationToken ct)
        {
            if (dto is null)
                return BadRequest(new { mesaj = "İstek gövdesi boş olamaz." });

            try
            {
                var created = await _service.AddOzelAsync(firmaId, dto, ct);
                return CreatedAtAction(nameof(GetMaddeler), new { firmaId }, created);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
        }

        // ── Özel madde metnini düzenle ──────────────────────────────────────

        [HttpPut("{firmaId:int}/maddeler/ozel/{id:long}")]
        [ProducesResponseType(typeof(FirmaKontrolMaddeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<FirmaKontrolMaddeDto>> UpdateOzel(
            int firmaId,
            long id,
            [FromBody] OzelMaddeUpdateDto dto,
            CancellationToken ct)
        {
            if (dto is null)
                return BadRequest(new { mesaj = "İstek gövdesi boş olamaz." });

            try
            {
                var updated = await _service.UpdateOzelAsync(firmaId, id, dto, ct);
                return updated is null
                    ? NotFound(new { mesaj = $"Özel madde bulunamadı: Id={id}" })
                    : Ok(updated);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
        }

        // ── Özel madde sil ──────────────────────────────────────────────────

        [HttpDelete("{firmaId:int}/maddeler/ozel/{id:long}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteOzel(int firmaId, long id, CancellationToken ct)
        {
            var ok = await _service.DeleteOzelAsync(firmaId, id, ct);
            return ok ? NoContent() : NotFound(new { mesaj = $"Özel madde bulunamadı: Id={id}" });
        }

        // ── Ham mizan: kaydet / getir / sıfırla ─────────────────────────────

        [HttpGet("{firmaId:int}/mizan")]
        [ProducesResponseType(typeof(List<FirmaKontrolMizanSatirDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<FirmaKontrolMizanSatirDto>>> GetMizan(
            int firmaId,
            [FromQuery] int yil,
            CancellationToken ct)
        {
            var list = await _mizan.GetSatirlarAsync(firmaId, yil, ct);
            return Ok(list);
        }

        // Kırılım ağacı ham satırların yanında saklanır; ayrı uç, çünkü /mizan yanıtının
        // şekli (List<FirmaKontrolMizanSatirDto>) değişmemeli. Ağacı olmayan dönemler
        // yanıtta yer almaz — eski yüklemelerde ağaç bulunmaması hata değildir.
        [HttpGet("{firmaId:int}/mizan/agac")]
        [ProducesResponseType(typeof(List<FirmaKontrolMizanAgacDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<FirmaKontrolMizanAgacDto>>> GetMizanAgac(
            int firmaId,
            [FromQuery] int yil,
            CancellationToken ct)
        {
            var list = await _mizan.GetAgaclarAsync(firmaId, yil, ct);
            return Ok(list);
        }

        [HttpPost("{firmaId:int}/mizan")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> KaydetMizan(
            int firmaId,
            [FromBody] MizanKaydetRequest req,
            CancellationToken ct)
        {
            if (req is null)
                return BadRequest(new { mesaj = "İstek gövdesi boş olamaz." });

            try
            {
                await _mizan.KaydetAsync(firmaId, req, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
        }

        [HttpDelete("{firmaId:int}/mizan")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> SifirlaMizan(
            int firmaId,
            [FromQuery] int yil,
            CancellationToken ct)
        {
            await _mizan.SifirlaAsync(firmaId, yil, ct);
            return NoContent();
        }

        // ── Mizan hesap notları: getir / yaz / sil ──────────────────────────

        [HttpGet("{firmaId:int}/mizan-notlari")]
        [ProducesResponseType(typeof(List<MizanNotuDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<MizanNotuDto>>> GetMizanNotlari(
            int firmaId,
            [FromQuery] int? yil,
            CancellationToken ct)
        {
            var list = await _mizanNotu.GetNotlarAsync(firmaId, yil, ct);
            return Ok(list);
        }

        [HttpPut("{firmaId:int}/mizan-notlari")]
        [ProducesResponseType(typeof(MizanNotuDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MizanNotuDto>> UpsertMizanNotu(
            int firmaId,
            [FromBody] MizanNotuUpsertDto dto,
            CancellationToken ct)
        {
            if (dto is null)
                return BadRequest(new { mesaj = "İstek gövdesi boş olamaz." });

            try
            {
                var kaydedilen = await _mizanNotu.UpsertAsync(firmaId, dto, ct);
                return Ok(kaydedilen);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
        }

        // Mevcut notu Id ile günceller — tip (kalıcı ↔ dönem) burada değişebilir.
        [HttpPut("{firmaId:int}/mizan-notlari/{id:long}")]
        [ProducesResponseType(typeof(MizanNotuDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<MizanNotuDto>> GuncelleMizanNotu(
            int firmaId,
            long id,
            [FromBody] MizanNotuGuncelleDto dto,
            CancellationToken ct)
        {
            if (dto is null)
                return BadRequest(new { mesaj = "İstek gövdesi boş olamaz." });

            try
            {
                var guncellenen = await _mizanNotu.GuncelleAsync(firmaId, id, dto, ct);
                return Ok(guncellenen);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { mesaj = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
        }

        // "Güncel say": not metnine dokunmadan snapshot'ı güncel bakiyeyle tazeler.
        [HttpPost("{firmaId:int}/mizan-notlari/{id:long}/snapshot-yenile")]
        [ProducesResponseType(typeof(MizanNotuDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<MizanNotuDto>> SnapshotYenile(int firmaId, long id, CancellationToken ct)
        {
            try
            {
                var yenilenen = await _mizanNotu.SnapshotYenileAsync(firmaId, id, ct);
                return Ok(yenilenen);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { mesaj = ex.Message });
            }
        }

        [HttpDelete("{firmaId:int}/mizan-notlari/{id:long}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteMizanNotu(int firmaId, long id, CancellationToken ct)
        {
            var ok = await _mizanNotu.SilAsync(firmaId, id, ct);
            return ok ? NoContent() : NotFound(new { mesaj = $"Mizan notu bulunamadı: Id={id}" });
        }

        // ── Mizan hesap notları: dönem devri ────────────────────────────────

        [HttpGet("{firmaId:int}/mizan-notlari/devir-adaylari")]
        [ProducesResponseType(typeof(List<MizanNotuDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<MizanNotuDto>>> GetDevirAdaylari(
            int firmaId,
            [FromQuery] int kaynakYil,
            [FromQuery] int hedefYil,
            CancellationToken ct)
        {
            var list = await _mizanNotu.DevirAdaylariAsync(firmaId, kaynakYil, hedefYil, ct);
            return Ok(list);
        }

        [HttpPost("{firmaId:int}/mizan-notlari/devir")]
        [ProducesResponseType(typeof(List<MizanNotuDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<List<MizanNotuDto>>> DevretMizanNotlari(
            int firmaId,
            [FromBody] MizanNotuDevirRequest req,
            CancellationToken ct)
        {
            if (req is null)
                return BadRequest(new { mesaj = "İstek gövdesi boş olamaz." });

            try
            {
                var devredilenler = await _mizanNotu.DevretAsync(firmaId, req, ct);
                return Ok(devredilenler);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mesaj = ex.Message });
            }
        }

        // ── Vergi paneli girdileri: getir / kaydet ──────────────────────────

        [HttpGet("{firmaId:int}/vergi")]
        [ProducesResponseType(typeof(FirmaKontrolVergiDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult<FirmaKontrolVergiDto>> GetVergi(
            int firmaId,
            [FromQuery] int donem,
            [FromQuery] int yil,
            CancellationToken ct)
        {
            var dto = await _vergi.GetAsync(firmaId, donem, yil, ct);
            return dto is null ? NoContent() : Ok(dto);
        }

        [HttpPost("{firmaId:int}/vergi")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> KaydetVergi(
            int firmaId,
            [FromBody] FirmaKontrolVergiDto dto,
            CancellationToken ct)
        {
            if (dto is null)
                return BadRequest(new { mesaj = "İstek gövdesi boş olamaz." });

            try
            {
                await _vergi.UpsertAsync(firmaId, dto, ct);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mesaj = ex.Message });
            }
        }

        // ── Etiket tanımları (boyut / değer / kural) ────────────────────────
        // Hangi hesaba hangi etiketin düştüğü BURADA hesaplanmaz ve saklanmaz;
        // kural motoru istemcide, çalışma zamanında çalışır.

        [HttpGet("{firmaId:int}/etiket/boyutlar")]
        [ProducesResponseType(typeof(List<EtiketBoyutuDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<EtiketBoyutuDto>>> GetEtiketBoyutlar(int firmaId, CancellationToken ct)
        {
            try
            {
                return Ok(await _etiket.GetBoyutlarAsync(firmaId, ct));
            }
            catch (Exception ex)
            {
                EtiketHatasiYaz(nameof(GetEtiketBoyutlar), firmaId, ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { mesaj = $"Etiket tanımları okunamadı: {ex.GetType().Name} — {ex.Message}" });
            }
        }

        [HttpPost("{firmaId:int}/etiket/boyutlar")]
        [ProducesResponseType(typeof(EtiketBoyutuDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<EtiketBoyutuDto>> EtiketBoyutEkle(
            int firmaId, [FromBody] EtiketBoyutuYazDto dto, CancellationToken ct)
            => await Calistir(() => _etiket.BoyutEkleAsync(firmaId, dto, ct));

        [HttpPut("{firmaId:int}/etiket/boyutlar/{boyutId:int}")]
        public async Task<IActionResult> EtiketBoyutGuncelle(
            int firmaId, int boyutId, [FromBody] EtiketBoyutuYazDto dto, CancellationToken ct)
            => await Calistir(() => _etiket.BoyutGuncelleAsync(firmaId, boyutId, dto, ct));

        [HttpDelete("{firmaId:int}/etiket/boyutlar/{boyutId:int}")]
        public async Task<IActionResult> EtiketBoyutSil(int firmaId, int boyutId, CancellationToken ct)
            => await Calistir(() => _etiket.BoyutSilAsync(boyutId, ct));

        [HttpPost("{firmaId:int}/etiket/boyutlar/{boyutId:int}/degerler")]
        [ProducesResponseType(typeof(EtiketDegeriDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<EtiketDegeriDto>> EtiketDegerEkle(
            int firmaId, int boyutId, [FromBody] EtiketDegeriYazDto dto, CancellationToken ct)
            => await Calistir(() => _etiket.DegerEkleAsync(boyutId, dto, ct));

        [HttpPut("{firmaId:int}/etiket/degerler/{degerId:int}")]
        public async Task<IActionResult> EtiketDegerGuncelle(
            int firmaId, int degerId, [FromBody] EtiketDegeriYazDto dto, CancellationToken ct)
            => await Calistir(() => _etiket.DegerGuncelleAsync(degerId, dto, ct));

        [HttpDelete("{firmaId:int}/etiket/degerler/{degerId:int}")]
        public async Task<IActionResult> EtiketDegerSil(int firmaId, int degerId, CancellationToken ct)
            => await Calistir(() => _etiket.DegerSilAsync(degerId, ct));

        [HttpPost("{firmaId:int}/etiket/boyutlar/{boyutId:int}/kurallar")]
        [ProducesResponseType(typeof(EtiketKuraliDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<EtiketKuraliDto>> EtiketKuralEkle(
            int firmaId, int boyutId, [FromBody] EtiketKuraliYazDto dto, CancellationToken ct)
            => await Calistir(() => _etiket.KuralEkleAsync(boyutId, dto, ct));

        [HttpPut("{firmaId:int}/etiket/kurallar/{kuralId:long}")]
        public async Task<IActionResult> EtiketKuralGuncelle(
            int firmaId, long kuralId, [FromBody] EtiketKuraliYazDto dto, CancellationToken ct)
            => await Calistir(() => _etiket.KuralGuncelleAsync(kuralId, dto, ct));

        [HttpDelete("{firmaId:int}/etiket/kurallar/{kuralId:long}")]
        public async Task<IActionResult> EtiketKuralSil(int firmaId, long kuralId, CancellationToken ct)
            => await Calistir(() => _etiket.KuralSilAsync(kuralId, ct));

        [HttpPut("{firmaId:int}/etiket/boyutlar/{boyutId:int}/kural-sira")]
        public async Task<IActionResult> EtiketKuralSira(
            int firmaId, int boyutId, [FromBody] List<EtiketKuralSiraDto> siralar, CancellationToken ct)
            => await Calistir(() => _etiket.KuralSiraGuncelleAsync(boyutId, siralar, ct));

        // Etiket uçlarının ortak hata çevirisi. Beklenmeyen hatalar YUTULMAZ: tipi ve
        // mesajı hem ILogger'a hem konsola yazılır, istemciye okunur bir mesaj döner.
        // (Daha önce bu uçlar sessizce 500 dönüyordu ve sebebi hiçbir yerde görünmüyordu.)
        private async Task<IActionResult> Calistir(Func<Task> is_, [CallerMemberName] string uc = "")
        {
            try
            {
                await is_();
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new { mesaj = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { mesaj = ex.Message }); }
            catch (Exception ex)
            {
                EtiketHatasiYaz(uc, null, ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { mesaj = $"{ex.GetType().Name} — {ex.Message}" });
            }
        }

        private async Task<ActionResult<T>> Calistir<T>(Func<Task<T>> is_, [CallerMemberName] string uc = "")
        {
            try
            {
                return Ok(await is_());
            }
            catch (KeyNotFoundException ex) { return NotFound(new { mesaj = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { mesaj = ex.Message }); }
            catch (Exception ex)
            {
                EtiketHatasiYaz(uc, null, ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { mesaj = $"{ex.GetType().Name} — {ex.Message}" });
            }
        }

        /// <summary>Etiket ucundaki beklenmeyen hatayı tipiyle birlikte görünür kılar.</summary>
        private void EtiketHatasiYaz(string uc, int? firmaId, Exception ex)
        {
            var ic = ex.InnerException is null ? string.Empty : $" | iç: {ex.InnerException.GetType().Name} — {ex.InnerException.Message}";
            var mesaj = $"[EtiketUc HATA] {uc} firmaId={firmaId?.ToString() ?? "-"} : {ex.GetType().Name} — {ex.Message}{ic}";

            _log.LogError(ex, "{Mesaj}", mesaj);
            Console.WriteLine(mesaj);
            Console.WriteLine(ex.ToString());
        }

    }
}
