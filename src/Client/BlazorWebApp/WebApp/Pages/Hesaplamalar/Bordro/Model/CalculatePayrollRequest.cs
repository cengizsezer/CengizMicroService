namespace WebApp.Pages.Hesaplamalar.Bordro.Model
{
    public class CalculatePayrollRequest
    {
        public int Year { get; set; }
        public int CalculationType { get; set; }
        public int EmployeeType { get; set; }

        public bool HasMandatoryBes { get; set; }
        public int DisabilityType { get; set; }

        public bool IncludeMinimumWageExemption { get; set; } = true;
        public bool IncludeStampTax { get; set; } = true;
        public bool IncludeEmployerCost { get; set; } = false;

        public int StartMonth { get; set; } = 1;
        public decimal PreviousCumulativeTaxBase { get; set; }

        public List<PayrollMonthInputRequest> Months { get; set; } = new();

        public string LawCode { get; set; } = "00000";
        public bool IsManufacturingSector { get; set; } = false;

        // 0 = Diğer (%80), 1 = Yüksek lisans (%90), 2 = Doktora (%95)
        public int ArGeEgitimDurumu { get; set; }

        // Teşvik ek girdileri (sunucu: PayrollTesvikKurallari)
        public int? TesvikAltSecimi { get; set; }
        public bool TurizmKonaklamaOnayi { get; set; }
        public int TurizmPrimGunu { get; set; } = 30;
        public decimal IsgDestegiTutari { get; set; }
        // 1 = Oran (%), 2 = Aylık tutar
        public int? ManuelTesvikTuru { get; set; }
        public decimal ManuelTesvikDegeri { get; set; }

        // Yol / yemek (2026+); null: kapalı
        public PayrollYolYemekRequest? YolYemek { get; set; }
    }

    public class PayrollYolYemekRequest
    {
        public int GunSayisi { get; set; } = 22;
        public decimal YemekGunlukTutar { get; set; }
        // 1 = Nakit, 2 = Kart (yemek kartı-çeki / ulaşım kartı-bilet-akbil)
        public int YemekOdemeSekli { get; set; } = 2;
        public decimal YolGunlukTutar { get; set; }
        public int YolOdemeSekli { get; set; } = 2;
    }

    public class PayrollMonthInputRequest
    {
        public int Month { get; set; }
        public decimal Amount { get; set; }
    }
}
