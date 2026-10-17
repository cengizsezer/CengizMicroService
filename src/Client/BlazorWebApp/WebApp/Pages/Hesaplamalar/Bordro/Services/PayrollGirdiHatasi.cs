using System.Net;

namespace WebApp.Pages.Hesaplamalar.Bordro.Services
{
    /// <summary>
    /// Sunucu teşvik girdisi hatasını 400 + düz metin döner (PayrollGirdiHatasiFiltresi).
    /// "Response status code does not indicate success" yerine o metin kullanıcıya gitsin.
    /// </summary>
    internal static class PayrollGirdiHatasi
    {
        public static async Task VarsaFirlatAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.StatusCode != HttpStatusCode.BadRequest) return;
            if (response.Content.Headers.ContentType?.MediaType != "text/plain") return;

            var mesaj = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(mesaj))
                throw new HttpRequestException(mesaj, null, HttpStatusCode.BadRequest);
        }
    }
}
