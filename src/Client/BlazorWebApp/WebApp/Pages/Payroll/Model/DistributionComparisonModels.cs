using System.Text.Json.Serialization;

namespace WebApp.Pages.Payroll.Model
{
    public class CompareDistributionRequest
    {
        public int Year { get; set; }
        public decimal YillikBrut { get; set; }
        public decimal YillikVergiMaliyeti { get; set; }
        public decimal YillikNetEleGecen { get; set; }
        public decimal StopajOrani { get; set; } = 0.15m;
    }

    public class DistributionComparisonResultDto
    {
        public int Year { get; set; }
        public decimal StopajOrani { get; set; }
        public decimal BeyanSiniri { get; set; }

        public HuzurHakkiBreakdownDto HuzurHakki { get; set; } = new();
        public KarDagitimiBreakdownDto KarDagitimi { get; set; } = new();

        public AvantajliYontem AvantajliYontem { get; set; }
        public decimal NetFark { get; set; }
    }

    public class HuzurHakkiBreakdownDto
    {
        public decimal A_YillikBrut { get; set; }
        public decimal B_VergiMaliyeti { get; set; }
        public decimal C_OrtalamaVergiOrani { get; set; }
        public decimal D_NetEleGecen { get; set; }
        public decimal E_GeciciVergiOrani { get; set; }
        public decimal F_GeciciVergiAvantaji { get; set; }
        public decimal G_NetVergiMaliyeti { get; set; }
    }

    public class KarDagitimiBreakdownDto
    {
        public decimal A_BrutKarDagitimi { get; set; }
        public decimal StopajOrani { get; set; }
        public decimal B_Stopaj { get; set; }
        public decimal C_NetEleGecen { get; set; }

        public decimal D_YuzdeElliIstisna { get; set; }
        public decimal E_BeyanaTabiGelir { get; set; }
        public decimal F_GelirVergisi { get; set; }
        public decimal OrtalamaVergiOrani { get; set; }

        public decimal G_IlaveVergi { get; set; }
        public decimal NetVergiMaliyeti { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AvantajliYontem
    {
        HuzurHakki,
        KarDagitimi,
        Esit
    }
}
