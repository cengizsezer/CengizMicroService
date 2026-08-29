namespace CatalogService.Api.Features.Ajanlar.Domain
{
    /// <summary>
    /// Bağlı bir ajanın bellekteki kaydı. Veritabanına yazılmıyor: kayıt bir
    /// <b>bağlantının</b> ömrü kadar yaşıyor, container yeniden başlarsa zaten
    /// bütün bağlantılar kopuyor ve ajanlar saniyeler içinde yeniden kaydoluyor.
    /// Kalıcı bir tablo, gerçekte bağlı olmayan ajanları "bağlı" göstermekten
    /// başka bir şey yapmazdı.
    /// </summary>
    public class AjanKaydi
    {
        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>Makineye özgü, yeniden başlatmalar arasında değişmeyen kimlik.</summary>
        public string MakineId { get; set; } = string.Empty;

        public string MakineAdi { get; set; } = string.Empty;
        public string AjanSurumu { get; set; } = string.Empty;
        public string? IsletimSistemi { get; set; }

        /// <summary>
        /// Kaydın sahibi: <b>token'daki</b> kullanıcı. İstekle gelen
        /// <see cref="MakineId"/>'ye güvenilmiyor — "kim hangi makineye iş
        /// gönderebilir" kuralı ileride buna dayanacak.
        /// </summary>
        public string KullaniciId { get; set; } = string.Empty;

        public DateTimeOffset BaglantiZamani { get; set; }
        public DateTimeOffset SonKalpAtisi { get; set; }

        /// <summary>Ajanın bildirdiği ORKA durumu; bilmiyorsa null.</summary>
        public bool? OrkaCalisiyorMu { get; set; }

        /// <summary>
        /// Bu bağlantıyı düşürmek için çağrılacak eylem. Hub kaydederken
        /// <c>Context.Abort</c>'u geçiriyor; aynı makine ikinci kez bağlandığında
        /// depo eski kaydı çıkarır, hub da bu eylemle eski soketi kapatır.
        /// Depoyu SignalR tiplerine bağlamamak için delege olarak tutuluyor.
        /// </summary>
        public Action? BaglantiyiKes { get; set; }
    }
}
