using CatalogService.Api.Features.Firmalar.Domain;

namespace CatalogService.Api.Features.FirmaKontrol.Domain
{
    /// <summary>
    /// Firma Kontrol / Raporlar → Vergi paneli GİRDİLERİ (VergiHesaplama). Firma +
    /// dönem + yıl bazında tek satır. SADECE kullanıcı girdileri saklanır; türetilen
    /// değerler (Toplam İlaveler, Mali Kar, Hesaplanan KV %25, 691, Ödenecek Vergi)
    /// runtime'da VergiHesabiHelper ile hesaplanır — DB'ye YAZILMAZ.
    /// </summary>
    public class FirmaKontrolVergi
    {
        public long Id { get; set; }

        public int FirmaId { get; set; }
        public Firma? Firma { get; set; }

        /// <summary>Donem enum: 0=Onceki, 1=Cari. Vergi paneli şimdilik Cari.</summary>
        public int Donem { get; set; }

        /// <summary>Hesap dönemi yılı. Şimdilik cari yıl; yıl bazlı geçmiş için ucu açık.</summary>
        public int Yil { get; set; }

        // ── İlaveler (+) ──
        public decimal Kkeg { get; set; }
        public decimal KkegIstisna { get; set; }

        // ── İndirimler (-) ──
        public decimal GecmisYil_2024 { get; set; }
        public decimal GecmisYil_2023 { get; set; }
        public decimal GecmisYil_2022 { get; set; }
        public decimal GecmisYil_2021 { get; set; }
        public decimal TemettuGeliri { get; set; }
        public decimal BagisYardim { get; set; }

        // ── %5 indirim ──
        public decimal Kv5Indirim { get; set; }

        // ── Dönem içi tevkifat / ödemeler (-) ──
        public decimal GeciciVergi { get; set; }
        public decimal BankaStopaji { get; set; }
        public decimal DigerTevkifat { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
