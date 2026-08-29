using CatalogService.Api.Features.BankaEkstre.Dtos;
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
    /// <b>Okuma ile yazma farklı davranır (KARARLAR §99):</b>
    /// <list type="bullet">
    /// <item><b>GET / HEAD</b> — <c>firmaId</c> yoksa istek reddedilmez, kapsam "tüm firmalar"
    /// olur. Firma artık bir oturum bağlamı değil verinin bir boyutu: Aktar ekranı bütün
    /// firmaların banka hesaplarını, Tanımlar bütün firmaların kayıtlarını firma kolonuyla
    /// listeler. Kullanıcı istediğinde <c>?firmaId=</c> ile daraltır.</item>
    /// <item><b>Diğer yöntemler (yazma)</b> — <c>firmaId</c> <b>zorunludur</b>. Hiçbir kayıt
    /// "aktif firma"dan türetilmez; firma ya seçilen kayıttan (banka hesabı → firma) ya da
    /// formdan gelir. Eksik kapsam 400 döner, ayrıca <c>SaveChangesAsync</c> kapsamsız
    /// yazmayı ikinci bir kez reddeder.</item>
    /// </list>
    ///
    /// Geçersiz ya da tanınmayan firma her iki durumda da <b>400</b>: yanlış bir Id'yi sessizce
    /// "tüm firmalar"a çevirmek, kullanıcıya başka bir firmanın verisini gösterirdi.
    /// </summary>
    public sealed class BankaFirmaFiltresi : IAsyncActionFilter
    {
        public const string Parametre = "firmaId";

        private readonly CatalogContext _db;
        private readonly IBankaFirmaKapsami _kapsam;
        private readonly IFirmaAdlari _firmaAdlari;

        public BankaFirmaFiltresi(CatalogContext db, IBankaFirmaKapsami kapsam, IFirmaAdlari firmaAdlari)
        {
            _db = db;
            _kapsam = kapsam;
            _firmaAdlari = firmaAdlari;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var ham = context.HttpContext.Request.Query[Parametre].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(ham) && context.RouteData.Values.TryGetValue(Parametre, out var rota))
                ham = rota?.ToString();

            var okuma = OkumaMi(context.HttpContext.Request.Method)
                        || context.ActionDescriptor.EndpointMetadata.OfType<FirmaKapsamiGerekmezAttribute>().Any();

            if (string.IsNullOrWhiteSpace(ham))
            {
                // Okumada kapsamsız istek meşru: "tüm firmalar". Yazmada değil.
                if (!okuma)
                {
                    context.Result = new BadRequestObjectResult(new
                    {
                        field = Parametre,
                        message = "Firma belirtilmeden kayıt yazılamaz. Kaydın firmasını formda seçin " +
                                  "ya da işlemi bir banka hesabı üzerinden yapın."
                    });
                    return;
                }

                _kapsam.Ayarla(0);
                await FirmaAdlariniDoldurAsync(next);
                return;
            }

            if (!int.TryParse(ham, out var firmaId) || firmaId <= 0)
            {
                context.Result = new BadRequestObjectResult(new
                {
                    field = Parametre,
                    message = $"Geçersiz firma değeri (\"{ham}\")."
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
            await FirmaAdlariniDoldurAsync(next);
        }

        /// <summary>
        /// Yanıttaki liste satırlarına firma adını yazar.
        ///
        /// Tek yerde durmasının sebebi: firma adı bir <b>görüntü alanı</b>, kapsam kararı
        /// değil. Beş servisin bir düzine dönüş noktasına dağıtılsaydı biri unutulur ve o
        /// listede firma kolonu boş çıkardı — üstelik sessizce. Kapsamın kendisi (hangi
        /// kayıtların geldiği) buraya taşınmadı; o hâlâ sorgularda görünür biçimde yazılı.
        /// </summary>
        private async Task FirmaAdlariniDoldurAsync(ActionExecutionDelegate next)
        {
            var sonuc = await next();

            if (sonuc.Result is not ObjectResult { Value: { } govde }) return;

            var satirlar = govde switch
            {
                IFirmaliSatir tek => new List<IFirmaliSatir> { tek },
                System.Collections.IEnumerable dizi => dizi.OfType<IFirmaliSatir>().ToList(),
                _ => new List<IFirmaliSatir>()
            };

            if (satirlar.Count > 0) await _firmaAdlari.DoldurAsync(satirlar);
        }

        /// <summary>Gövdesi olmayan, yan etkisiz yöntemler.</summary>
        private static bool OkumaMi(string yontem)
            => HttpMethods.IsGet(yontem) || HttpMethods.IsHead(yontem);
    }
}
