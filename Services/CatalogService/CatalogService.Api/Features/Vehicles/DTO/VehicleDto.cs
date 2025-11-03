namespace CatalogService.Api.Features.Vehicles.DTO
{
    public class VehicleDto
    {
        public int Id { get; set; }              // PK (otomatik)

        public string Plate { get; set; } = string.Empty;       // PLAKA
        public string Driver { get; set; } = string.Empty;        // SÜRÜCÜ
        public string Unit { get; set; } = string.Empty;       // BİRİM
        public string Department { get; set; } = string.Empty;   // BÖLÜM
        public string Description1 { get; set; } = string.Empty;  // AÇIKLAMA-1
        public string Region { get; set; } = string.Empty;     // BÖLGE
        public string Description2 { get; set; } = string.Empty; // AÇIKLAMA-2

        public string Type { get; set; } = string.Empty;      // TİP (Binek Araç vb.)
        public string Brand { get; set; } = string.Empty;       // MARKA
        public string Model { get; set; } = string.Empty;       // MODEL
        public string Gear { get; set; } = string.Empty;      // VİTES
        public string Fuel { get; set; } = string.Empty;      // YAKIT
        public string Fleet { get; set; } = string.Empty;      // FİLO
    }
}
