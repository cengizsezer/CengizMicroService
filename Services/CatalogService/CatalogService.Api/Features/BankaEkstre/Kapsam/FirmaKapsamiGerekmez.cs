namespace CatalogService.Api.Features.BankaEkstre.Kapsam
{
    /// <summary>
    /// Bu uç noktada firma kapsamı aranmaz.
    ///
    /// Yazma isteklerinde <see cref="BankaFirmaFiltresi"/> <c>firmaId</c>'yi zorunlu tutar;
    /// kapsamsız yazmaya izin verilen <b>tek</b> durum, kaydın kapsamsız oluşunun işin
    /// kendisi olmasıdır: "sahipsiz kayıtları temizle" tam olarak <c>FirmaId &lt;= 0</c> olan
    /// satırları siler ve hiçbir firmaya ait değildir (KARARLAR §71).
    ///
    /// İkinci kullanım: <b>global yapılandırma tabloları</b>. Açıklama şablonları, unvan
    /// desenleri, sabit kurallar, vergi kodu eşlemeleri ve işlem kategorileri bankanın
    /// yazım kalıbına ait, firmaya değil — entity'leri <c>FirmaKapsamliEntity</c>'den de
    /// türemiyor. Bu tablolara yazarken firma sormak, kullanıcıyı anlamsız bir seçime
    /// zorlardı. (İsteğe bağlı <c>?firmaId=</c> yine işe yarar: hesap kodu o firmanın
    /// planına karşı doğrulanır.)
    ///
    /// Nitelik bilerek dar tutuldu: <c>FirmaKapsamliEntity</c> yazan hiçbir uç noktaya
    /// konmaz, yoksa yazma tarafındaki "firma açıkça belirtilsin" güvencesi sessizce
    /// ortadan kalkar.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class FirmaKapsamiGerekmezAttribute : Attribute
    {
    }
}
