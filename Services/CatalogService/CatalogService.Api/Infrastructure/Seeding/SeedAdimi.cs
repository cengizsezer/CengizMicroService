using Microsoft.EntityFrameworkCore;

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
    ///
    /// <b>Paylaşılan context:</b> global adımların hepsi aynı <c>ctxOnce</c>'u kullanır.
    /// Düşen bir adımın eklediği ama kaydedemediği varlıklar ChangeTracker'da kalır ve
    /// sıradaki adımın <c>SaveChanges</c>'iyle (transaction'sız, yarım) yazılırdı —
    /// Mevzuat Grafiği seed'i retry stratejisine takılınca kartları böyle yazıldı,
    /// bağlantı/tuvaller yazılmadı. Bu yüzden adım düşünce paylaşılan context'in
    /// ChangeTracker'ı temizlenir. Başarılı adımda context'e dokunulmaz.
    /// </summary>
    public static class SeedAdimi
    {
        /// <summary>
        /// Adımı yalıtık çalıştırır; adım düşerse <paramref name="paylasilanContext"/>'in
        /// bekleyen değişiklikleri atılır ki sonraki adımın kaydına karışmasın.
        /// </summary>
        public static async Task CalistirAsync(ILogger logger, string ad, DbContext paylasilanContext, Func<Task> adim)
        {
            if (await CalistirAsync(logger, ad, adim)) return;

            try
            {
                paylasilanContext.ChangeTracker.Clear();
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "✖ {Ad}: başarısız adımın bekleyen kayıtları context'ten atılamadı; " +
                    "sonraki adımların kaydına karışabilir.", ad);
            }
        }

        /// <summary>
        /// Context'i kendi içinde açıp kapatan adımlar için (ör. tenant başına ayrı context).
        /// Paylaşılan bir context kullanan adım diğer overload'u çağırmalı.
        /// </summary>
        /// <returns>Adım başarılıysa true.</returns>
        public static async Task<bool> CalistirAsync(ILogger logger, string ad, Func<Task> adim)
        {
            try
            {
                logger.LogInformation("🌱 {Ad}: seed uygulanıyor...", ad);
                await adim();
                logger.LogInformation("✔ {Ad}: seed tamamlandı.", ad);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "✖ {Ad}: seed BAŞARISIZ. Diğer seed adımları çalışmaya devam ediyor; " +
                    "bu adımın tablosu eksik kalmış olabilir.", ad);
                return false;
            }
        }
    }
}
