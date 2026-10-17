using CatalogService.Api.Features.Payroll.Enums;

namespace CatalogService.Api.Features.Payroll.Configuration
{
    public enum YanHakKalemi
    {
        Yemek,
        Yol
    }

    /// <summary>
    /// Bir kalem + ödeme şekli için <see cref="Baslangic"/> ayından itibaren geçerli günlük istisnalar.
    /// Dönemin sonu, aynı kalem/ödeme şeklinin bir sonraki satırının başlangıcıdır (yoksa açık uçlu).
    /// </summary>
    public sealed record YanHakIstisnaDonemi(
        YanHakKalemi Kalem,
        PayrollYanHakOdemeSekli Odeme,
        (int Yil, int Ay) Baslangic,
        decimal GvDvGunluk,
        decimal SgkGunluk);

    /// <summary>
    /// Yol / yemek günlük istisna parametreleri — yıl/ay aralıklı TEK tablo. Şimdilik config;
    /// ileride yönetim ekranından düzenlenecek. GV ve DV aynı istisnayı kullanır.
    /// Yol/yemek yalnız 2026+ hesaplanır (<see cref="IlkYil"/>); 2024/2025 dondurulmuş.
    /// </summary>
    public static class PayrollYanHakIstisnalari
    {
        public const int IlkYil = 2026;

        private const PayrollYanHakOdemeSekli Nakit = PayrollYanHakOdemeSekli.Nakit;
        private const PayrollYanHakOdemeSekli Kart = PayrollYanHakOdemeSekli.Kart;

        public static readonly IReadOnlyList<YanHakIstisnaDonemi> Donemler = new YanHakIstisnaDonemi[]
        {
            // Yemek 2026 tüm yıl — GV/DV: kart/çek 330 (KDV dahil), nakit 300.
            // SGK: kart/çek ve nakit 300 (7577 s. Kanun, SGK Genelgesi 2026/12).
            new(YanHakKalemi.Yemek, Kart,  (2026, 1), GvDvGunluk: 330m, SgkGunluk: 300m),
            new(YanHakKalemi.Yemek, Nakit, (2026, 1), GvDvGunluk: 300m, SgkGunluk: 300m),

            // Yol 2026 — kart/bilet/akbil: GV, DV ve SGK 158; nakit: istisna yok.
            new(YanHakKalemi.Yol, Kart,  (2026, 1), GvDvGunluk: 158m, SgkGunluk: 158m),
            new(YanHakKalemi.Yol, Nakit, (2026, 1), GvDvGunluk: 0m,   SgkGunluk: 0m),
        };

        /// <summary>Dönemde geçerli satır; o tarihte tanımlı değilse null.</summary>
        public static YanHakIstisnaDonemi? Bul(YanHakKalemi kalem, PayrollYanHakOdemeSekli odeme, int yil, int ay)
        {
            var donem = yil * 100 + ay;
            return Donemler
                .Where(x => x.Kalem == kalem && x.Odeme == odeme && x.Baslangic.Yil * 100 + x.Baslangic.Ay <= donem)
                .MaxBy(x => x.Baslangic.Yil * 100 + x.Baslangic.Ay);
        }
    }
}
