using CatalogService.Api.Infrastructure.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Domain
{
    /// <summary>
    /// Kullanıcı onaylarından öğrenilen anahtar → hesap kodu eşlemesi.
    /// Modülün asıl değeri burada birikir: başarı ölçüsü ilk günkü isabet değil,
    /// üçüncü aydaki onay kuyruğunun uzunluğudur.
    /// </summary>
    public class OgrenmeKaydi : TenantEntity
    {
        public int Id { get; set; }

        /// <summary>Normalize açıklamanın hash'i, IBAN (sadece rakam) veya VKN.</summary>
        public string Anahtar { get; set; } = string.Empty;

        public AnahtarTipi AnahtarTipi { get; set; }

        /// <summary>Boşluklu ORKA kodu, ör. "120 D22".</summary>
        public string HesapKodu { get; set; } = string.Empty;

        public string? HesapAdi { get; set; }

        /// <summary>Kaydın öğrenildiği yön; aynı anahtar iki yönde farklı hesaba gidebilir.</summary>
        public Yon Yon { get; set; }

        public int KullanimSayisi { get; set; } = 1;

        public DateTime SonKullanim { get; set; }
    }
}
