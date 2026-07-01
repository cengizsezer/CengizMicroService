namespace CatalogService.Api.Features.FirmaKontrol.Dtos
{
    /// <summary>
    /// Tek bir kontrol maddesinin durum/not güncellemesi (idempotent upsert).
    /// Şablon maddesi: <see cref="MaddeKey"/> ile bağlanır, satır yoksa oluşturulur.
    /// Özel madde: <see cref="Id"/> ile bulunur (IsCustom=true).
    /// </summary>
    public class FirmaKontrolMaddeUpsertDto
    {
        public string? MaddeKey { get; set; }
        public long? Id { get; set; }
        public bool IsCustom { get; set; }

        /// <summary>Şablon maddesi için satır ilk kez oluşturulurken saklanır.</summary>
        public string Category { get; set; } = string.Empty;

        public bool IsChecked { get; set; }
        public int Status { get; set; }
        public string? Not { get; set; }
    }
}
