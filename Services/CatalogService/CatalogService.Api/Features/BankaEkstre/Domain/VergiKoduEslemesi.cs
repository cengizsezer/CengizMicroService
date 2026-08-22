namespace CatalogService.Api.Features.BankaEkstre.Domain
{
    /// <summary>
    /// Vergi tahsilatı satırlarında karşı hesabı belirleyen eşleme.
    ///
    /// Tek kural yetmiyor: gerçek dosyadaki 5 vergi satırı <b>dört farklı hesaba</b> gitmiş.
    /// <code>
    /// "9085/TRAFİK CEZ. Tahsilatı … Plaka:34MRP081"  → 689 9 1 (KKEG)
    /// "0040/S.DAMGA V. …"                            → 360 01 004
    /// "0033/… beyanname"                             → 770 04 001
    /// </code>
    /// Karşı hesap metnin içeriğine göre değişiyor; bu yüzden eşleme koda gömülmez,
    /// Tanımlar ekranından düzenlenebilen bir tabloda durur.
    ///
    /// Tablo <b>global</b>: vergi kodları (0040 = damga, 0033 = kurum geçici) firmadan
    /// firmaya değişmez. Hesap kodu da aynı mantıkla — <see cref="SabitKural"/> ile aynı
    /// yaklaşım; firmaya özel kırılım gerekiyorsa satır arayüzden düzenlenir.
    /// </summary>
    public class VergiKoduEslemesi
    {
        public int Id { get; set; }

        /// <summary>
        /// Metindeki dört haneli vergi kodu ("9085", "0040", "0033"). Boş bırakılırsa
        /// eşleme yalnız <see cref="AnahtarKelime"/> ile tutar.
        /// </summary>
        public string? VergiKodu { get; set; }

        /// <summary>
        /// Metinde aranacak anahtar kelime ("TRAFİK CEZ", "DAMGA", "BEYANNAME"). Kod
        /// değişse de kelime tuttuğu için ikisi birlikte kullanılır; ikisi de doluysa
        /// <b>herhangi biri</b> tutması yeter.
        /// </summary>
        public string? AnahtarKelime { get; set; }

        /// <summary>Boşluklu ORKA kodu.</summary>
        public string HesapKodu { get; set; } = string.Empty;

        public string? HesapAdi { get; set; }

        public int Sira { get; set; }

        public bool Aktif { get; set; } = true;
    }
}
