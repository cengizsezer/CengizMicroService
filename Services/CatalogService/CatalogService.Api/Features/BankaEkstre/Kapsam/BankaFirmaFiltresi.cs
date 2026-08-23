using CatalogService.Api.Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Kapsam
{
    /// <summary>
    /// Banka Otomasyon uç noktalarının firma kapsamını isteğin <c>firmaId</c>
    /// parametresinden kurar.
    ///
    /// Parametre <b>sorgu dizesinde</b> aranır (rota değeri de kabul edilir); form gövdesi
    /// okunmaz — dosya yükleyen uç noktalarda 20 MB'lik gövdeyi model bağlamadan önce
    /// tamponlamak gerekirdi. İstemci bu yüzden <c>?firmaId=</c>'yi her istekte, çok
    /// parçalı yüklemelerde de sorgu dizesinde gönderir.
    ///
    /// Eksik, geçersiz ya da tanınmayan firma → <b>400</b>. Sessiz varsayılan yok:
    /// kapsamsız bir istek "hiç kayıt yok" gibi görünüp kullanıcıyı yanıltırdı, kapsamsız
    /// bir yazma ise verinin nereye gittiğini belirsiz bırakırdı.
    /// </summary>
    public sealed class BankaFirmaFiltresi : IAsyncActionFilter
    {
        public const string Parametre = "firmaId";

        private readonly CatalogContext _db;
        private readonly IBankaFirmaKapsami _kapsam;

        public BankaFirmaFiltresi(CatalogContext db, IBankaFirmaKapsami kapsam)
        {
            _db = db;
            _kapsam = kapsam;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var ham = context.HttpContext.Request.Query[Parametre].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(ham) && context.RouteData.Values.TryGetValue(Parametre, out var rota))
                ham = rota?.ToString();

            if (!int.TryParse(ham, out var firmaId) || firmaId <= 0)
            {
                context.Result = new BadRequestObjectResult(new
                {
                    field = Parametre,
                    message = "Firma seçilmeden banka otomasyon isteği yapılamaz. Firma listesine dönüp bir firmaya girin."
                });
                return;
            }

            var tanimli = await _db.Firmalar.AsNoTracking().AnyAsync(f => f.Id == firmaId);
            if (!tanimli)
            {
                context.Result = new BadRequestObjectResult(new
                {
                    field = Parametre,
                    message = $"Firma bulunamadı (Id={firmaId}). Firma listesini yenileyip tekrar deneyin."
                });
                return;
            }

            _kapsam.Ayarla(firmaId);
            await next();
        }
    }
}
