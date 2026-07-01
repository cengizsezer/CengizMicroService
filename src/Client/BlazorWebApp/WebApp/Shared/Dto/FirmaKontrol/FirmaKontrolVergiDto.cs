namespace WebApp.Shared.Dto.FirmaKontrol
{
    /// <summary>Vergi paneli girdileri (okuma + kaydetme). Sadece girdiler; türetilenler yok.</summary>
    public class FirmaKontrolVergiDto
    {
        public int Donem { get; set; }
        public int Yil { get; set; }

        public decimal Kkeg { get; set; }
        public decimal KkegIstisna { get; set; }
        public decimal GecmisYil_2024 { get; set; }
        public decimal GecmisYil_2023 { get; set; }
        public decimal GecmisYil_2022 { get; set; }
        public decimal GecmisYil_2021 { get; set; }
        public decimal TemettuGeliri { get; set; }
        public decimal BagisYardim { get; set; }
        public decimal Kv5Indirim { get; set; }
        public decimal GeciciVergi { get; set; }
        public decimal BankaStopaji { get; set; }
        public decimal DigerTevkifat { get; set; }
    }
}
