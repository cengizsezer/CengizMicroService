namespace CatalogService.Api.Infrastructure.Domain
{
    /// <summary>
    /// Verisi <b>firmaya</b> ait olan kayıtların ortak tabanı.
    ///
    /// <see cref="TenantEntity"/>'den farkı kapsam kaynağıdır: tenant, token'daki
    /// <c>tn</c> claim'inden gelir ve oturum açan kullanıcının kendi firmasını anlatır.
    /// <see cref="FirmaId"/> ise <c>catalog.Firmalar</c> tablosundaki firmanın kimliğidir
    /// ve isteğin <c>firmaId</c> parametresinden gelir — Raporlar (<c>/firmakontrol</c>)
    /// ekranı ile aynı mekanizma.
    ///
    /// Tek oturumla birçok firmanın işini yapan kullanıcıda (pkfadmin) doğru olan budur:
    /// token tek tenant taşır, oysa yönetilen firma sayısı sekizdir.
    /// </summary>
    public abstract class FirmaKapsamliEntity
    {
        /// <summary>catalog.Firmalar.Id. Sıfır "kapsamsız" demektir ve kaydedilmesi engellenir.</summary>
        public int FirmaId { get; set; }
    }
}
