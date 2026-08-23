namespace CatalogService.Api.Features.BankaEkstre.Kapsam
{
    /// <summary>
    /// Banka Otomasyon isteğinin hangi firmaya ait olduğu.
    ///
    /// Değer <b>token'dan değil</b>, isteğin <c>firmaId</c> parametresinden gelir
    /// (bkz. <see cref="BankaFirmaFiltresi"/>). Modülün tüm sorguları bu değere göre
    /// süzülür; ekranda seçili firma ile veriye giden firma tek kaynaktan okunduğu için
    /// "ekran Aday yazarken kayıt SMMM'ye gitti" durumu oluşamaz.
    ///
    /// Global query filter <b>kurulmadı</b>: kapsam her sorguda görünür biçimde yazılır.
    /// Görünmez bir filtre, firma seçim ekranının sayaçları gibi meşru çoklu-firma
    /// sorgularında <c>IgnoreQueryFilters()</c> baypasını zorunlu kılıyordu.
    /// </summary>
    public interface IBankaFirmaKapsami
    {
        /// <summary>Seçili firmanın <c>catalog.Firmalar.Id</c> değeri; ayarlanmadıysa 0.</summary>
        int FirmaId { get; }

        /// <summary>Kapsam ayarlandı mı? Ayarlanmadan yapılan sorgu hiçbir kaydı görmez.</summary>
        bool Secili { get; }

        void Ayarla(int firmaId);
    }

    /// <summary>İstek ömrü boyunca yaşayan basit tutucu; Scoped kaydedilir.</summary>
    public sealed class BankaFirmaKapsami : IBankaFirmaKapsami
    {
        public int FirmaId { get; private set; }

        public bool Secili => FirmaId > 0;

        public void Ayarla(int firmaId) => FirmaId = firmaId > 0 ? firmaId : 0;
    }

    /// <summary>Testlerde ve arka plan işlerinde sabit kapsam.</summary>
    public sealed class SabitBankaFirmaKapsami : IBankaFirmaKapsami
    {
        public SabitBankaFirmaKapsami(int firmaId) => FirmaId = firmaId;

        public int FirmaId { get; }

        public bool Secili => FirmaId > 0;

        public void Ayarla(int firmaId) { }
    }
}
