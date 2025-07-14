namespace WebApp.Extensions
{
    public static class DateTimeExtensions
    {
        public static DateTime EndOfWeek(this DateTime dt)
        {
            int diff = DayOfWeek.Saturday - dt.DayOfWeek;
            return dt.AddDays(diff).Date;
        }
    }
}
