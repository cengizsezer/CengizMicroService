namespace WebApp.Application.Services.Interfaces
{
    public class MizanExcelRow
    {
        public string Kod { get; set; } = string.Empty;

        /// <summary>
        /// Excel'den okunan hesap adı (PROGROUP formatında B sütunu).
        /// Boş olabilir; o zaman HesapPlani'ndeki ad kullanılır.
        /// </summary>
        public string? Ad { get; set; }

        public decimal? OncekiDonem { get; set; }
        public decimal? CariDonem { get; set; }
    }

    /// <summary>
    /// Mizan yüklemesinde değerlendirilemeyen / işleme alınmayan bir satırın detayı.
    /// Hem parser'ın eli​diği hiyerarşik/geçersiz satırlar hem de service'in
    /// hesap planında bulamadığı satırlar bu modele yazılır.
    /// </summary>
    public enum AtlamaSebebi
    {
        /// <summary>
        /// Hiyerarşik alt kod ("102 1", "120 1 1 02" gibi). Beklenen davranış —
        /// bilgilendirme amaçlı listelenir, kullanıcı eylemi gerekmez.
        /// </summary>
        HiyerarsikAltKod = 0,

        /// <summary>
        /// 3 haneli numerik formata uymayan kod ("ABC", "1020" gibi).
        /// </summary>
        GecersizFormat = 1,

        /// <summary>
        /// 3 haneli kod doğru ama bakiye sütunlarından sayısal değer okunamadı.
        /// </summary>
        BakiyeOkunamadi = 2,

        /// <summary>
        /// 3 haneli geçerli kod, bakiye var, ancak hesap planında bu kod yok.
        /// Kullanıcının dikkatine sunulur (yeni hesap eklenmesi gerekebilir).
        /// </summary>
        PlandaBulunamadi = 3,

        /// <summary>
        /// Mizan dosyasının son satırındaki genel toplam (Borç Toplam = Alacak Toplam,
        /// Borç Bakiye = Alacak Bakiye). Hesap değil, denklik kontrolü; atlanır.
        /// </summary>
        MizanToplamSatiri = 4
    }

    public class AtlananSatir
    {
        public string Kod { get; set; } = string.Empty;
        public string? Ad { get; set; }
        public decimal? Bakiye { get; set; }
        public AtlamaSebebi Sebep { get; set; }

        /// <summary>
        /// İnsan-okur sebep metni (UI'da gruplama başlığı için).
        /// </summary>
        public string SebepMetni { get; set; } = string.Empty;
    }

    public class MizanParseResult
    {
        public List<MizanExcelRow> Rows { get; set; } = new();
        public List<AtlananSatir> AtlananSatirlar { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public interface IExcelMizanParser
    {
        Task<MizanParseResult> ParseAsync(Stream excelStream);
    }
}
