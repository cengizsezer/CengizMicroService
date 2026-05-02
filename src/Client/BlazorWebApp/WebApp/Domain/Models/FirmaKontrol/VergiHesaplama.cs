namespace WebApp.Domain.Models.FirmaKontrol
{
    public class VergiHesaplama
    {
        // İlaveler (+)
        public decimal Kkeg { get; set; }
        public decimal KkegIstisna { get; set; }

        // İndirimler (-)
        public decimal GecmisYil_2024 { get; set; }
        public decimal GecmisYil_2023 { get; set; }
        public decimal GecmisYil_2022 { get; set; }
        public decimal GecmisYil_2021 { get; set; }
        public decimal TemettuGeliri { get; set; }
        public decimal BagisYardim { get; set; }

        // %5 indirim (Kurumlar Vergisi sadakat indirimi)
        public decimal Kv5Indirim { get; set; }

        // Dönem içi yapılan tevkifat ve ödemeler (3 alt kalem)
        public decimal GeciciVergi { get; set; }
        public decimal BankaStopaji { get; set; }
        public decimal DigerTevkifat { get; set; }
    }
}
