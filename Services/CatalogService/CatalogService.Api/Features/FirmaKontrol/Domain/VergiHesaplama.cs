using CatalogService.Api.Features.Firmalar.Domain;

namespace CatalogService.Api.Features.FirmaKontrol.Domain
{
    /// <summary>
    /// Bir firmanın bir hesap dönemine ait kurumlar vergisi beyanname başlığı.
    /// SADECE kullanıcı girdileri saklanır; matrah, hesaplanan vergi, asgari vergi ve
    /// ödenecek vergi runtime'da <c>VergiHesaplamaMotoru</c> ile üretilir, DB'ye yazılmaz.
    /// (Mevcut <see cref="FirmaKontrolVergi"/> tablosundaki basit panelin yerini alır;
    /// eski tablo geriye dönük veriler için olduğu gibi bırakıldı.)
    /// </summary>
    public class VergiHesaplama
    {
        public int Id { get; set; }

        public int FirmaId { get; set; }
        public Firma? Firma { get; set; }

        public short DonemYil { get; set; }

        /// <summary>Gelir tablosundan (690) gelen ticari bilanço kârı/zararı; ekranda düzenlenemez.</summary>
        public decimal TicariKar { get; set; }

        /// <summary>Genel kurumlar vergisi oranı (yüzde). Varsayılan %25.</summary>
        public decimal KvOrani { get; set; } = 25.00m;

        /// <summary>İndirimli oran uygulanacaksa yüzde değeri; yoksa null.</summary>
        public decimal? IndirimliOran { get; set; }

        /// <summary>İndirimli oranın uygulanacağı matrah kısmı; kalan matraha genel oran uygulanır.</summary>
        public decimal? IndirimliOranMatrahi { get; set; }

        /// <summary>
        /// Yurt içi asgari kurumlar vergisi (KVK 32/C) paralel hesabı yapılsın mı.
        /// İlk defa faaliyete başlayan kurumlar 3 hesap dönemi muaf olduğu için kapatılabilir.
        /// </summary>
        public bool AsgariKvHesapla { get; set; } = true;

        public string? Notlar { get; set; }

        public DateTime GuncellemeT { get; set; } = DateTime.UtcNow;

        public List<VergiHesaplamaSatir> Satirlar { get; set; } = new();
        public List<GecmisYilZarari> GecmisYilZararlari { get; set; } = new();
    }
}
