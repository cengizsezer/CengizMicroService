using WebApp.Domain.Models.User;

namespace WebApp.Application.Services.Interfaces
{
    /// <summary>
    /// Banka Otomasyon modülünün firma bağlamı.
    ///
    /// Modül kendi firma seçim ekranıyla açılır ve seçilen firma <b>gerçekten</b> tenant
    /// bağlamını değiştirir: <see cref="IAppSessionManager.SelectFirmAsync"/> çağrılır,
    /// yeni token üretilir, sonraki tüm istekler o firmaya gider. Ekran "PKF Aday"
    /// gösterirken isteğin SMMM tenant'ıyla gitmesi böylece imkânsızlaşır.
    ///
    /// Üstteki genel FİRMA DEĞİŞTİR ile çelişki çıkarsa <b>sayfadaki seçim kazanır</b>:
    /// modül kendi firmasını geri uygular ve <see cref="Uyari"/> ile kullanıcıyı bilgilendirir.
    /// Bu üstünlük yalnız <see cref="Aktif"/> iken geçerlidir — modül ekranı kapalıyken
    /// kullanıcının genel firma seçimine karışılmaz.
    /// </summary>
    public interface IBankaOtomasyonOturumu
    {
        /// <summary>Modülde girilmiş firma; hiç girilmediyse null.</summary>
        FirmaDto? SeciliFirma { get; }

        /// <summary>
        /// Modülün firma içi ekranlarından biri açık mı. Çakışma çözümü yalnız açıkken
        /// devreye girer; modül dışında genel firma seçimi serbesttir.
        /// </summary>
        bool Aktif { get; set; }

        /// <summary>Seçim değişti; ekran başlığını ve verisini tazelemek için.</summary>
        event Action? Degisti;

        /// <summary>Çakışma uyarısı; ekran bunu bildirim olarak gösterir.</summary>
        event Action<string>? Uyari;

        /// <summary>Firmaya gir: tenant bağlamı bu firmaya geçer ve seçim hatırlanır.</summary>
        Task GirAsync(FirmaDto firma);

        /// <summary>
        /// Firma içi ekranların açılışta çağırdığı hazırlık. Seçim yoksa (ya da artık
        /// erişilebilir değilse) null döner — ekran firma listesine yönlendirir. Seçim
        /// varsa tenant bağlamının ona uyduğu garanti edilir.
        /// </summary>
        Task<FirmaDto?> BaglamiHazirlaAsync();

        /// <summary>Firma listesine dönüldü; modül seçimi bırakır.</summary>
        Task CikAsync();
    }

    /// <summary>
    /// Seçimin sayfa yenilemesinden sonra da durması için ince depo soyutlaması.
    /// Ayrı arayüz olmasının sebebi test edilebilirlik: oturum mantığı tarayıcı
    /// deposuna bağlı kalmadan sınanabiliyor.
    /// </summary>
    public interface IBankaOtomasyonDeposu
    {
        Task<string?> FirmaNoAsync();
        Task FirmaNoYazAsync(string? firmaNo);
    }
}
