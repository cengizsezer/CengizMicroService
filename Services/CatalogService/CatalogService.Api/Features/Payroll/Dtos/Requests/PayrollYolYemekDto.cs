using CatalogService.Api.Features.Payroll.Enums;

namespace CatalogService.Api.Features.Payroll.Dtos.Requests
{
    /// <summary>
    /// Yol / yemek yan hakkı girdisi (2026+). İstekte null: kapalı — hesap yan haksız.
    /// Tüm aylara aynen uygulanır; günlük tutar 0 olan kalem hesaba girmez.
    /// </summary>
    public class PayrollYolYemekDto
    {
        public int GunSayisi { get; set; } = 22;

        public decimal YemekGunlukTutar { get; set; }
        public PayrollYanHakOdemeSekli YemekOdemeSekli { get; set; } = PayrollYanHakOdemeSekli.Kart;

        public decimal YolGunlukTutar { get; set; }
        public PayrollYanHakOdemeSekli YolOdemeSekli { get; set; } = PayrollYanHakOdemeSekli.Kart;
    }
}
