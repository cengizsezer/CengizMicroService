namespace WebApp.Pages.Hesaplamalar.OrtuluSermaye
{
    /// <summary>
    /// Maketteki (ortulu-sermaye.html) örnek muavinler. "Örnek veriyi yükle" düğmesi ve
    /// birim testleri aynı metni okur; tek kaynak.
    /// </summary>
    public static class OrnekVeri
    {
        public const decimal DonemBasiOzSermaye = 5_000_000m;
        public static readonly DateTime DonemSonu = new(2026, 9, 30);

        public const string BorcMuavini =
            "331.01.001\tPKF Holding A.Ş.\t01.01.2026\tAçılış\t1\tAçılış fişi\t\t9.000.000,00\t9.000.000,00\n" +
            "331.01.001\tPKF Holding A.Ş.\t15.02.2026\tMahsup\t42\tOrtak borç girişi EUR 250.000\t\t9.375.000,00\t18.375.000,00\n" +
            "331.01.001\tPKF Holding A.Ş.\t31.03.2026\tMahsup\t118\tDönem sonu kur değerlemesi\t\t620.000,00\t18.995.000,00\n" +
            "331.01.001\tPKF Holding A.Ş.\t20.05.2026\tTediye\t205\tOrtağa borç ödemesi\t4.000.000,00\t\t14.995.000,00\n" +
            "336.05.002\tAday Danışmanlık Ltd.\t01.01.2026\tAçılış\t1\tAçılış fişi\t\t1.200.000,00\t1.200.000,00\n" +
            "336.05.002\tAday Danışmanlık Ltd.\t10.03.2026\tMahsup\t96\tGrup şirketi finansman\t\t2.500.000,00\t3.700.000,00\n" +
            "336.05.002\tAday Danışmanlık Ltd.\t30.06.2026\tTediye\t311\tKısmi geri ödeme\t1.500.000,00\t\t2.200.000,00";

        public const string GiderMuavini =
            "656.01.001\tKambiyo Zararları\t31.03.2026\tMahsup\t118\tPKF Holding borç kur değerlemesi\t620.000,00\t\t620.000,00\n" +
            "646.01.001\tKambiyo Kârları\t30.06.2026\tMahsup\t298\tPKF Holding borç kur değerlemesi\t\t180.000,00\t180.000,00\n" +
            "780.01.001\tFinansman Giderleri\t31.03.2026\tMahsup\t119\tPKF Holding faiz tahakkuku Q1\t410.000,00\t\t410.000,00\n" +
            "780.01.001\tFinansman Giderleri\t30.06.2026\tMahsup\t299\tPKF Holding faiz tahakkuku Q2\t385.000,00\t\t795.000,00\n" +
            "780.01.002\tFinansman Giderleri\t15.02.2026\tMahsup\t43\tKredi sözleşmesi damga vergisi\t88.968,75\t\t88.968,75\n" +
            "191.01.001\tİndirilecek KDV\t31.03.2026\tMahsup\t119\tFaiz faturası KDV\t82.000,00\t\t82.000,00\n" +
            "191.01.001\tİndirilecek KDV\t30.06.2026\tMahsup\t299\tFaiz faturası KDV\t77.000,00\t\t159.000,00";
    }
}
