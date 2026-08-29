namespace CatalogService.Api.Features.Declarations.Entities
{
    /// <summary>Bir beyanname kaydına bağlanabilen belge türleri.</summary>
    public enum BeyannameEkTuru : byte
    {
        /// <summary>Vergi dairesinden alınan tahakkuk fişi.</summary>
        Tahakkuk = 1,

        /// <summary>Beyannamenin kendisi.</summary>
        Beyanname = 2,

        /// <summary>Ödeme dekontu; yalnız ödendi işaretli kayıtlarda istenir.</summary>
        Dekont = 3
    }

    /// <summary>
    /// Beyanname kaydına bağlı PDF belgesi.
    ///
    /// Asıl dosya <b>FileApiService</b>'te saklanır; burada yalnız onun döndürdüğü
    /// <see cref="FileId"/> ve görüntüleme için gereken metadata durur. Bu, repodaki
    /// mevcut kalıp: <c>JobAttachment</c> ve <c>TicaretSicilEk</c> aynı şekilde çalışıyor
    /// (bkz. KARARLAR §91) — yeni bir saklama mekanizması kurulmadı.
    ///
    /// Her (beyanname, tür) çifti için <b>tek</b> belge tutulur: aynı türden ikinci bir
    /// dosya yüklendiğinde eski kayıt değiştirilir. Kullanıcı "tahakkuk" ikonuna
    /// baktığında hangi dosyanın açılacağı belirsiz kalmasın.
    /// </summary>
    public class BeyannameEk
    {
        public int Id { get; set; }

        public int DeclarationId { get; set; }

        public Declaration? Declaration { get; set; }

        public BeyannameEkTuru Tur { get; set; }

        /// <summary>FileApiService'teki dosya kaydının kimliği (<c>/download?id=</c>, <c>/delete?id=</c>).</summary>
        public int FileId { get; set; }

        public string FileName { get; set; } = string.Empty;

        /// <summary>Yalnız <c>application/pdf</c> kabul edilir; alan doğrulamanın kaydedilmiş hâli.</summary>
        public string ContentType { get; set; } = "application/pdf";

        public long Length { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? YukleyenKullanici { get; set; }
    }
}
