namespace CatalogService.Api.Features.FirmaKontrol.Domain
{
    /// <summary>Bir boyutun alabileceği etiket değeri ("PKF Aday", "Kurumsal Strateji").</summary>
    public class EtiketDegeri
    {
        public int Id { get; set; }

        public int BoyutId { get; set; }
        public EtiketBoyutu? Boyut { get; set; }

        public string Ad { get; set; } = string.Empty;

        /// <summary>Ekranda kullanılacak renk, hex ("#2C5282").</summary>
        public string? Renk { get; set; }

        public int Sira { get; set; }

        /// <summary>
        /// Bu etiket hangi firmaya karşılık geliyor ("PKF Aday" etiketi → PKF Aday firması).
        /// Yönetsel görünüm ve grup matrisi, bir defterde bu etikete düşen tutarın hangi
        /// şirketin yönetsel gelirine ekleneceğini buradan bilir. null = sistemde firması
        /// olmayan etiket (örn. "Grup dışı").
        ///
        /// Bilerek FK/navigation YOK: değer zaten boyuta (EtiketBoyutu) cascade ile bağlı ve
        /// boyut da Firma'ya cascade ile bağlı; ikinci bir Firma FK'sı SQL Server'da iki
        /// cascade yolu (1785) doğurur — EtiketKurali.FirmaId ile aynı gerekçe. Alan yalnızca
        /// eşleme anahtarıdır; üzerinde indeks var. Silinmiş bir firmaya işaret ederse
        /// istemci onu "firma bulunamadı" olarak gösterir.
        /// </summary>
        public int? FirmaId { get; set; }
    }
}
