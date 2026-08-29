namespace CatalogService.Api.Infrastructure.Seeding
{
    /// <summary>
    /// Açılıştaki seed'leri tek tek yalıtır.
    ///
    /// <b>Neden gerekti?</b> Bütün global seed'ler <c>Program.cs</c>'te tek bir
    /// <c>try/catch</c> içindeydi. Sıradaki herhangi bir seed patlayınca ondan
    /// sonrakilerin hiçbiri çalışmıyor, geriye yalnız tek bir genel hata satırı
    /// kalıyordu. <c>catalog.BeyannameTurleri</c>'nin yayında boş kalmasının sebebi
    /// buydu: kendi seed'inde bir sorun yoktu, sırası kendinden önceki bir seed'in
    /// hatasına takılmıştı.
    ///
    /// Bu yardımcı her adımı kendi <c>try/catch</c>'ine alır: bir adım düşse bile
    /// sonrakiler çalışır ve hangi adımın düştüğü <b>adıyla</b> loglanır. Sessiz
    /// geçiş yok — başarı da başarısızlık da loga yazılır.
    /// </summary>
    public static class SeedAdimi
    {
        public static async Task CalistirAsync(ILogger logger, string ad, Func<Task> adim)
        {
            try
            {
                logger.LogInformation("🌱 {Ad}: seed uygulanıyor...", ad);
                await adim();
                logger.LogInformation("✔ {Ad}: seed tamamlandı.", ad);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "✖ {Ad}: seed BAŞARISIZ. Diğer seed adımları çalışmaya devam ediyor; " +
                    "bu adımın tablosu eksik kalmış olabilir.", ad);
            }
        }
    }
}
