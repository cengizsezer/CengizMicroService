namespace WebApp.Pages.Hesaplamalar.OrtuluSermaye.Model
{
    /// <summary>Gider/gelir muavini satırının örtülü sermaye hesabındaki yeri.</summary>
    public enum MuavinSinifi
    {
        Yok,
        KurFarkiGideri,
        KurFarkiGeliri,
        FaizGideri,
        FaizKdv,
        Stopaj,
        DamgaVergisi
    }

    /// <summary>
    /// ORKA muavininden yapıştırılan bir satır. Kasıtlı olarak record DEĞİL: aynı değerlere
    /// sahip iki hareket (aynı gün, aynı tutar) gerçekte olabilir ve kümülatif bakiye satır
    /// kimliğine göre tutuluyor. <see cref="Sinif"/> ekranda değiştirilebildiği için yazılabilir.
    /// </summary>
    public class MuavinSatiri
    {
        public string HesapKodu { get; set; } = "";
        public string HesapAdi { get; set; } = "";
        public DateTime Tarih { get; set; }
        public string FisTuru { get; set; } = "";
        public string FisNo { get; set; } = "";
        public string Aciklama { get; set; } = "";
        public decimal Borc { get; set; }
        public decimal Alacak { get; set; }
        public decimal Bakiye { get; set; }
        public MuavinSinifi Sinif { get; set; }
    }
}
