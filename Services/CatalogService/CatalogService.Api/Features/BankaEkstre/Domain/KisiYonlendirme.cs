using CatalogService.Api.Infrastructure.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Domain
{
    /// <summary>
    /// Kişi yönlendirmesi: adı geçen kişinin ödemesi hangi hesaba gitsin.
    ///
    /// Sabit kural grubu kişinin <b>ne</b> olduğunu bilmiyor: "masraf ödemesi" geçen her
    /// satırı personel avansına (195) yolluyor. Ama ortaklar ve yöneticiler için aynı
    /// ifade 331'e (ortaklara borçlar) gitmeli — ölçülen dosyada
    /// <c>ABDULKADİR SAYICI Masraf Ödemesi Arta Tekmer</c> satırı bunun örneği: kişi
    /// planda gerçekten var, ama <c>331 02</c> altında.
    ///
    /// Bu bilgi koda gömülmez; kullanıcı kendi tanımlar. Katman <b>sabit kuraldan önce</b>
    /// çalışır: kullanıcı elle yazdığı için güven en yüksektir.
    ///
    /// <see cref="Yon"/> alanı önemli: aynı kişi için giden ödeme <c>331</c>, gelen
    /// tahsilat başka bir hesap olabilir. <see cref="YonlendirmeYonu.Farketmez"/>
    /// seçilirse iki yönde de aynı hesap kullanılır.
    /// </summary>
    public class KisiYonlendirme : FirmaKapsamliEntity
    {
        public int Id { get; set; }

        /// <summary>
        /// Normalize isim çekirdeği (<see cref="Services.Normalizasyon.Cekirdek"/>), ör.
        /// <c>ABDULKADIR SAYICI</c>. Kullanıcının yazdığı biçim
        /// <see cref="Isim"/> alanında durur; eşleştirme bu alandan yapılır.
        /// </summary>
        public string IsimCekirdegi { get; set; } = string.Empty;

        /// <summary>Kullanıcının girdiği yazım; listede bu gösterilir.</summary>
        public string Isim { get; set; } = string.Empty;

        public YonlendirmeYonu Yon { get; set; } = YonlendirmeYonu.Farketmez;

        /// <summary>Boşluklu ORKA kodu; hesap planında bulunmayan kod kaydedilmez.</summary>
        public string HesapKodu { get; set; } = string.Empty;

        public string? HesapAdi { get; set; }

        /// <summary>Serbest not ("ortak", "yönetici" gibi); eşleştirmede kullanılmaz.</summary>
        public string? Aciklama { get; set; }

        /// <summary>
        /// Muhasebe kategorisi (<see cref="IslemKategorisi"/>). Yalnız etiket ve görünüm:
        /// eşleştirme kararına girmez, kural kategorisiz de aynen çalışır. Kategori
        /// silinirse alan boşalır, kural kalır.
        /// </summary>
        public int? IslemKategorisiId { get; set; }


        public bool Aktif { get; set; } = true;
    }
}
