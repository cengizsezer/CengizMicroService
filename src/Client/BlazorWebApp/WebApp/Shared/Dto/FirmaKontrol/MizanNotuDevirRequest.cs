namespace WebApp.Shared.Dto.FirmaKontrol
{
    /// <summary>
    /// Seçilen dönem notlarını yeni hesap dönemine taşıma isteği. Kalıcı notlar
    /// (DonemYili = null) zaten her dönemde göründüğünden devre dahil DEĞİLDİR.
    /// </summary>
    public class MizanNotuDevirRequest
    {
        public int KaynakYil { get; set; }
        public int HedefYil { get; set; }

        /// <summary>Devredilecek kaynak notların Id'leri (kullanıcı seçimi).</summary>
        public List<long> NotIdleri { get; set; } = new();
    }
}
