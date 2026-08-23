using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Application.Services.Interfaces
{
    /// <summary>
    /// Banka Otomasyon modülünün firma bağlamı.
    ///
    /// <b>Firma, token'daki tenant DEĞİLDİR.</b> Modül bir dönem <c>SelectFirmAsync</c> ile
    /// tenant'ı çevirerek çalıştı; pkfadmin tek tenant'a (500 / "PKF Istanbul SMMM") bağlı
    /// olduğu için firma listesi tek satıra düşüyor ve sekiz firmanın verisi aynı kovaya
    /// yazılıyordu (bkz. KARARLAR §68). Artık firma, Raporlar (<c>/firmakontrol</c>) ile
    /// aynı kaynaktan gelir: <c>catalog.Firmalar</c> tablosu, anahtar <c>Firma.Id</c>.
    ///
    /// Bu yüzden modül <b>tenant bağlamına hiç dokunmaz</b>: seçim yalnız burada tutulur ve
    /// istemci her isteğe <c>?firmaId=</c> ekler. Üstteki genel FİRMA DEĞİŞTİR ile çelişki
    /// de kalmadı — ikisi farklı şeylerdir ve birbirini ezmez.
    /// </summary>
    public interface IBankaOtomasyonOturumu
    {
        /// <summary>Modülde girilmiş firma; hiç girilmediyse null.</summary>
        FirmaDto? SeciliFirma { get; }

        /// <summary>
        /// Seçili firmanın <c>catalog.Firmalar.Id</c> değeri; seçim yoksa 0.
        /// İstek kapsamı bundan üretilir — <c>BankaEkstreApi</c> her adrese ekler.
        /// </summary>
        int FirmaId { get; }

        /// <summary>Ekranda gösterilecek firma adı; seçim yoksa boş.</summary>
        string FirmaAdi { get; }

        /// <summary>Seçim değişti; ekran başlığını ve verisini tazelemek için.</summary>
        event Action? Degisti;

        /// <summary>Firmaya gir: seçim hatırlanır. Tenant bağlamına dokunulmaz.</summary>
        Task GirAsync(FirmaDto firma);

        /// <summary>
        /// Firma içi ekranların açılışta çağırdığı hazırlık. Seçim yoksa (ya da firma artık
        /// tanımlı değilse) null döner — ekran firma listesine yönlendirir.
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
        Task<int?> FirmaIdAsync();
        Task FirmaIdYazAsync(int? firmaId);
    }
}
