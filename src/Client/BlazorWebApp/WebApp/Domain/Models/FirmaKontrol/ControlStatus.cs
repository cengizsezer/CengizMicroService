namespace WebApp.Domain.Models.FirmaKontrol
{
    public enum ControlStatus
    {
        Pending = 0,
        Verified = 1,
        HasIssue = 2
    }

    public enum SatirTipi
    {
        MainGroup = 0,
        SubGroup = 1,
        Account = 2,
        Total = 3,
        Other = 4
    }

    public enum Donem
    {
        Onceki = 0,
        Cari = 1
    }

    public static class TaxConstants
    {
        public const decimal KurumlarVergisiOrani = 0.25m;
    }
}
