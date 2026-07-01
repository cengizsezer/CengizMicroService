namespace WebApp.Shared.Dto.FirmaKontrol
{
    /// <summary>Tek bir kontrol maddesinin durum/not güncellemesi (idempotent upsert).</summary>
    public class FirmaKontrolMaddeUpsertDto
    {
        public string? MaddeKey { get; set; }
        public long? Id { get; set; }
        public bool IsCustom { get; set; }
        public string Category { get; set; } = string.Empty;
        public bool IsChecked { get; set; }
        public int Status { get; set; }
        public string? Not { get; set; }
    }
}
