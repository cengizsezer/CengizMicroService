using CatalogService.Api.Infrastructure.Domain;

namespace CatalogService.Api.Features.Ajanlar.Domain
{
    /// <summary>
    /// Ajana gönderilen bir iş.
    ///
    /// <b>Ajan listesinin aksine bu kayıt veritabanında:</b> bağlı ajanlar listesi
    /// bir bağlantının ömrü kadar yaşıyor ve kaybolması zararsız (bkz. KARARLAR
    /// §102), oysa "bu ekstre ORKA'ya aktarıldı mı" sorusunun yanıtı sunucu
    /// yeniden başlayınca da durmalı. Geçmiş de buradan okunuyor.
    /// </summary>
    public class AjanIsi : FirmaKapsamliEntity
    {
        /// <summary>
        /// Guid: kimliği <b>sunucu</b> üretiyor ve ajan onu geri bildiriyor.
        /// Artan bir sayı olsaydı ajan komşu işlerin kimliğini tahmin edebilirdi;
        /// sahiplik kontrolü var ama tahmin edilemez kimlik bedava bir katman.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>Hedef ajan (IdentityService'teki <c>Ajanlar.Id</c>, metin olarak).</summary>
        public string AjanId { get; set; } = string.Empty;

        /// <summary>Bkz. <see cref="AjanIsTipleri"/>.</summary>
        public string IsTipi { get; set; } = string.Empty;

        /// <summary>İş parametreleri (JSON). Tipe göre değişen alanlar burada.</summary>
        public string Yuk { get; set; } = "{}";

        public AjanIsDurumu Durum { get; set; } = AjanIsDurumu.Bekliyor;

        public int IlerlemeYuzde { get; set; }
        public string? IlerlemeMesaji { get; set; }

        public int ToplamAdim { get; set; }
        public int TamamlananAdim { get; set; }

        public string OlusturanKullaniciId { get; set; } = string.Empty;

        public DateTime OlusturmaZamani { get; set; }
        public DateTime? GonderimZamani { get; set; }
        public DateTime? BaslamaZamani { get; set; }
        public DateTime? BitisZamani { get; set; }

        /// <summary>
        /// Son ilerleme bildiriminin anı. Zaman aşımı bu alana bakıyor:
        /// <see cref="BaslamaZamani"/>'na bakmak, uzun ama düzenli ilerleyen bir işi
        /// de zaman aşımına uğratırdı.
        /// </summary>
        public DateTime? SonIlerlemeZamani { get; set; }

        public string? HataMesaji { get; set; }

        /// <summary>Başarılı işin özeti (JSON): kaç satır yazıldı, ne kadar sürdü.</summary>
        public string? SonucOzeti { get; set; }

        /// <summary>Hata ekranının görüntüsü; FileApiService'teki dosyanın kimliği.</summary>
        public string? HataEkraniDosyaId { get; set; }

        /// <summary>İş bitmiş mi? Bitmiş işin durumu bir daha değişmez.</summary>
        public bool Bitti =>
            Durum is AjanIsDurumu.Tamamlandi or AjanIsDurumu.Basarisiz
                  or AjanIsDurumu.IptalEdildi or AjanIsDurumu.ZamanAsimi;

        /// <summary>Ajanı meşgul eden durumlar; "aynı ajana tek iş" kuralı buna bakıyor.</summary>
        public bool Acik => !Bitti;
    }

    public enum AjanIsDurumu
    {
        /// <summary>Oluşturuldu ama ajan bağlı değildi; bağlanınca gönderilecek.</summary>
        Bekliyor = 0,

        /// <summary>Ajana iletildi, ajan henüz başladığını bildirmedi.</summary>
        Gonderildi = 1,

        Calisiyor = 2,
        Tamamlandi = 3,
        Basarisiz = 4,
        IptalEdildi = 5,

        /// <summary>Belirli süre ilerleme bildirilmedi; ajan ya da ORKA takılmış olabilir.</summary>
        ZamanAsimi = 6
    }

    public static class AjanIsTipleri
    {
        /// <summary>ORKA'ya dokunmadan iş akışını sınayan sahte iş (C adımı).</summary>
        public const string SahteAktarim = "SahteAktarim";

        /// <summary>Gerçek ORKA aktarımı (D adımı).</summary>
        public const string OrkayaAktar = "OrkayaAktar";
    }
}
