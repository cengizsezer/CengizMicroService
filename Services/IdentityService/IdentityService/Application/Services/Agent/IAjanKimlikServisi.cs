using IdentityService.Application.Models.Agent;

namespace IdentityService.Application.Services.Agent
{
    public interface IAjanKimlikServisi
    {
        /// <summary>Yeni ajan kaydı; ham anahtar yalnız bu yanıtta görünür.</summary>
        Task<YeniAjanYaniti> OlusturAsync(YeniAjanIstegi istek, int olusturanKullaniciId, CancellationToken ct = default);

        Task<List<AjanListeSatiri>> ListeleAsync(CancellationToken ct = default);

        /// <summary>Ajanı devre dışı bırakır. Kayıt bulunamazsa false.</summary>
        Task<bool> IptalEtAsync(int id, string neden, CancellationToken ct = default);

        /// <summary>
        /// Ham anahtarı doğrulayıp ajan token'ı üretir. Anahtar geçersiz, iptal
        /// edilmiş ya da süresi dolmuşsa null döner — çağıran taraf hangisi
        /// olduğunu dışarı söylemez.
        /// </summary>
        Task<AjanTokenYaniti?> TokenUretAsync(string hamAnahtar, CancellationToken ct = default);
    }
}
