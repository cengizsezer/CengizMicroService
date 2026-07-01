using CatalogService.Api.Features.Firmalar.Domain;

namespace CatalogService.Api.Features.FirmaKontrol.Domain
{
    /// <summary>
    /// Firma Kontrol / Raporlar → "Firma Kontrol" sekmesindeki kontrol maddelerinin
    /// firma-bazlı DURUMU. Şablon maddelerin (46 sabit madde) METNİ burada TUTULMAZ —
    /// metin frontend kodunda (MockFirmaKontrolService.BuildTemplate) kalır ve satır
    /// buraya sadece <see cref="MaddeKey"/> ile bağlanır. Özel maddeler ise firmaya
    /// özgü olduğundan metni (<see cref="SoruMetni"/>) burada saklanır.
    /// </summary>
    public class FirmaKontrolMadde
    {
        public long Id { get; set; }

        public int FirmaId { get; set; }
        public Firma? Firma { get; set; }

        /// <summary>
        /// Şablon maddesi için stabil anahtar (örn "DV-01"). Özel maddede null.
        /// Durum/not bu anahtara bağlanır — sıra numarasına DEĞİL.
        /// </summary>
        public string? MaddeKey { get; set; }

        /// <summary>Firmaya özel (kullanıcı eklemiş) madde mi?</summary>
        public bool IsCustom { get; set; }

        public string Category { get; set; } = string.Empty;

        /// <summary>Sadece özel maddede dolu. Şablon maddesinin metni kodda kalır.</summary>
        public string? SoruMetni { get; set; }

        public bool IsChecked { get; set; }

        /// <summary>ControlStatus enum: 0=Pending, 1=Verified, 2=HasIssue.</summary>
        public int Status { get; set; }

        public string? Not { get; set; }

        public int SiraNo { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
