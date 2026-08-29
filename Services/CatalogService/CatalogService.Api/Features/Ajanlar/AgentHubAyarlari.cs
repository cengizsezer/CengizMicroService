namespace CatalogService.Api.Features.Ajanlar
{
    /// <summary>
    /// Hub'ın yapılandırması (<c>AgentHub</c> bölümü).
    ///
    /// Asgari ajan sürümü bilerek koda gömülmedi: ajan Google Drive üzerinden elle
    /// dağıtılıyor, yani sunucu yeni bir sözleşmeye geçtiğinde eski kurulumlar bir
    /// süre daha ayakta kalıyor. Eşiği yapılandırmadan okumak, sunucuyu yeniden
    /// derlemeden "şu sürümün altındakiler bağlanmasın" demeyi mümkün kılıyor.
    /// </summary>
    public class AgentHubAyarlari
    {
        public const string Bolum = "AgentHub";

        /// <summary>Ajana bildirilen sunucu sözleşme sürümü.</summary>
        public string SunucuSurumu { get; set; } = "1.0.0";

        /// <summary>Bu sürümün altındaki ajanların kaydı reddedilir.</summary>
        public string AsgariAjanSurumu { get; set; } = "1.0.0";

        /// <summary>
        /// Bu süre boyunca kalp atışı gelmeyen kayıt "bağlı" sayılmaz. Ajan
        /// varsayılan olarak bunun üçte biri kadar aralıkla atış gönderir; eşik
        /// tek bir kaçan atışta ajanı listeden düşürmeyecek kadar geniş tutuldu.
        /// </summary>
        public int KalpAtisiZamanAsimiSaniye { get; set; } = 90;
    }
}
