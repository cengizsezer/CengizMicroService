namespace CatalogService.Api.Features.BankaEkstre.Kapsam
{
    /// <summary>
    /// Banka Otomasyon isteğinin hangi firmaya ait olduğu.
    ///
    /// Değer <b>token'dan değil, isteğin kendisinden</b> gelir: <c>?firmaId=</c> parametresi
    /// (bkz. <see cref="BankaFirmaFiltresi"/>). Oturumda tutulan bir "aktif firma" yoktur —
    /// firma bir oturum bağlamı değil, verinin bir boyutudur (KARARLAR §99).
    ///
    /// <b>İki hâli var:</b>
    /// <list type="bullet">
    /// <item><see cref="FirmaId"/> &gt; 0 — tek firma. Sorgular o firmaya süzülür, yazılan
    /// kayıtlar o firmaya damgalanır.</item>
    /// <item><see cref="TumFirmalar"/> — kapsam belirtilmemiş. <b>Yalnız okuma</b> isteklerinde
    /// oluşabilir ve "tüm firmalar" demektir: Aktar ekranındaki banka hesabı listesi,
    /// Tanımlar'daki listeler firma kolonu ile bütün firmaları gösterir. Yazma isteğinde
    /// bu hâl hiç oluşmaz; filtre 400 döner ve <c>SaveChangesAsync</c> de kapsamsız kaydı
    /// ayrıca reddeder.</item>
    /// </list>
    ///
    /// Global query filter <b>kurulmadı</b>: kapsam her sorguda görünür biçimde yazılır.
    /// Görünmez bir filtre, çoklu-firma listeleri gibi meşru sorgularda
    /// <c>IgnoreQueryFilters()</c> baypasını zorunlu kılardı.
    /// </summary>
    public interface IBankaFirmaKapsami
    {
        /// <summary>Seçili firmanın <c>catalog.Firmalar.Id</c> değeri; kapsam yoksa 0.</summary>
        int FirmaId { get; }

        /// <summary>Tek bir firmaya süzülmüş mü?</summary>
        bool Secili { get; }

        /// <summary>
        /// Kapsam belirtilmedi: okuma istekleri tüm firmaları görür. Yazmada oluşamaz.
        /// </summary>
        bool TumFirmalar { get; }

        void Ayarla(int firmaId);
    }

    /// <summary>İstek ömrü boyunca yaşayan basit tutucu; Scoped kaydedilir.</summary>
    public sealed class BankaFirmaKapsami : IBankaFirmaKapsami
    {
        public int FirmaId { get; private set; }

        public bool Secili => FirmaId > 0;

        public bool TumFirmalar => FirmaId <= 0;

        public void Ayarla(int firmaId) => FirmaId = firmaId > 0 ? firmaId : 0;
    }

    /// <summary>Testlerde ve arka plan işlerinde sabit kapsam.</summary>
    public sealed class SabitBankaFirmaKapsami : IBankaFirmaKapsami
    {
        public SabitBankaFirmaKapsami(int firmaId) => FirmaId = firmaId;

        public int FirmaId { get; }

        public bool Secili => FirmaId > 0;

        public bool TumFirmalar => FirmaId <= 0;

        public void Ayarla(int firmaId) { }
    }
}
